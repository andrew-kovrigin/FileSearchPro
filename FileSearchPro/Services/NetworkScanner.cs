using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using FileSearchPro.Models;

namespace FileSearchPro.Services;

public class NetworkScanner
{
    private static readonly ConcurrentDictionary<string, (NetworkTarget Target, DateTime Scanned)> _cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private int _pingTimeoutMs = 300;
    private int _shareTimeoutMs = 3000;
    private NetworkCredential _credentials = CredentialCache.DefaultNetworkCredentials;

    public void SetTimeouts(int pingTimeoutMs, int shareTimeoutMs)
    {
        _pingTimeoutMs = pingTimeoutMs;
        _shareTimeoutMs = shareTimeoutMs;
    }

    public void SetCredentials(NetworkCredential credentials)
    {
        _credentials = credentials ?? CredentialCache.DefaultNetworkCredentials;
    }

    public List<NetworkTarget> ScanNetwork(List<string> ips, CancellationToken ct, Action<ScanLogEntry>? onLog = null, Action<string>? onProgress = null)
    {
        var targets = new ConcurrentBag<NetworkTarget>();
        var sw = Stopwatch.StartNew();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount * 2,
            CancellationToken = ct
        };

        int processed = 0;
        int total = ips.Count;

        Parallel.ForEach(ips, options, ip =>
        {
            ct.ThrowIfCancellationRequested();

            var current = Interlocked.Increment(ref processed);
            onProgress?.Invoke($"[{current}/{total}] {ip}");

            NetworkTarget target;
            if (_cache.TryGetValue(ip, out var cached) && (DateTime.Now - cached.Scanned) < CacheDuration)
            {
                target = cached.Target;
                var shareMsg = target.AvailableShares.Count > 0
                    ? $"shares: {string.Join(", ", target.AvailableShares)}"
                    : "no shares";
                onLog?.Invoke(new ScanLogEntry
                {
                    IpAddress = ip,
                    Status = target.IsOnline ? ScanLogEntryStatus.Online : ScanLogEntryStatus.Unreachable,
                    Message = $"(из кэша) {sw.ElapsedMilliseconds}мс — {shareMsg}"
                });
            }
            else
            {
                target = ScanHost(ip, onLog);
                _cache[ip] = (target, DateTime.Now);
            }

            targets.Add(target);
        });

        return targets.ToList();
    }

    public void ClearCache() => _cache.Clear();

    private NetworkTarget ScanHost(string ip, Action<ScanLogEntry>? onLog)
    {
        var target = new NetworkTarget { IpAddress = ip };

        // Ping
        var sw = Stopwatch.StartNew();
        try
        {
            using var ping = new Ping();
            var reply = ping.Send(ip, _pingTimeoutMs);
            target.IsOnline = reply.Status == IPStatus.Success;
        }
        catch
        {
            target.IsOnline = false;
        }
        sw.Stop();
        var pingMs = sw.ElapsedMilliseconds;

        if (!target.IsOnline)
        {
            onLog?.Invoke(new ScanLogEntry
            {
                IpAddress = ip,
                Status = ScanLogEntryStatus.Unreachable,
                Message = $"Ping: {pingMs}мс — timeout"
            });
            return target;
        }

        // TCP port 445
        sw.Restart();
        var portOpen = IsPortOpen(ip, 445, 1000);
        sw.Stop();
        var portMs = sw.ElapsedMilliseconds;

        if (!portOpen)
        {
            onLog?.Invoke(new ScanLogEntry
            {
                IpAddress = ip,
                Status = ScanLogEntryStatus.Online,
                Message = $"Ping: {pingMs}мс, порт 445: {portMs}мс — port 445 закрыт"
            });
            return target;
        }

        // Shares — параллельно
        sw.Restart();
        target.AvailableShares = DiscoverSharesParallel(ip);
        sw.Stop();
        var sharesMs = sw.ElapsedMilliseconds;

        var shareMsg = target.AvailableShares.Count > 0
            ? $"shares: {string.Join(", ", target.AvailableShares)}"
            : "no accessible shares";

        onLog?.Invoke(new ScanLogEntry
        {
            IpAddress = ip,
            Status = ScanLogEntryStatus.Online,
            Message = $"Ping: {pingMs}мс, шары: {sharesMs}мс — {shareMsg}"
        });

        return target;
    }

    private List<string> DiscoverSharesParallel(string ip)
    {
        var shares = new ConcurrentBag<string>();
        string[] commonShares = ["C$", "D$", "ADMIN$", "Users", "IPC$"];

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = commonShares.Length
        };

        Parallel.ForEach(commonShares, options, share =>
        {
            try
            {
                var path = $"\\\\{ip}\\{share}";
                var task = Task.Run(() =>
                    ImpersonationHelper.RunAs(_credentials, () => Directory.Exists(path)));
                if (task.Wait(_shareTimeoutMs) && task.Result)
                    shares.Add(share);
            }
            catch { }
        });

        return shares.ToList();
    }

    private bool IsPortOpen(string ip, int port, int timeoutMs)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var task = client.ConnectAsync(ip, port);
            if (task.Wait(timeoutMs))
                return task.IsCompletedSuccessfully;
            return false;
        }
        catch { return false; }
    }

    public static List<string> ParseIpRange(string input)
    {
        var ips = new List<string>();
        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();

            if (trimmed.Contains('-'))
            {
                var rangeParts = trimmed.Split('-', 2);
                if (rangeParts.Length == 2 &&
                    IPAddress.TryParse(rangeParts[0], out var baseIp))
                {
                    var baseBytes = baseIp.GetAddressBytes();

                    if (int.TryParse(rangeParts[1], out int endNum))
                    {
                        for (int i = baseBytes[3]; i <= Math.Min(endNum, 255); i++)
                        {
                            var bytes = new[] { baseBytes[0], baseBytes[1], baseBytes[2], (byte)i };
                            ips.Add(new IPAddress(bytes).ToString());
                        }
                    }
                    else if (IPAddress.TryParse(rangeParts[1], out var endIp))
                    {
                        var endBytes = endIp.GetAddressBytes();
                        for (int i = baseBytes[3]; i <= endBytes[3]; i++)
                        {
                            var bytes = new[] { baseBytes[0], baseBytes[1], baseBytes[2], (byte)i };
                            ips.Add(new IPAddress(bytes).ToString());
                        }
                    }
                }
            }
            else if (IPAddress.TryParse(trimmed, out var singleIp))
            {
                ips.Add(singleIp.ToString());
            }
        }

        return ips.Distinct().ToList();
    }
}
