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

    public void SetTimeouts(int pingTimeoutMs, int shareTimeoutMs)
    {
        _pingTimeoutMs = pingTimeoutMs;
        _shareTimeoutMs = shareTimeoutMs;
    }

    public List<NetworkTarget> ScanNetwork(List<string> ips, CancellationToken ct, Action<ScanLogEntry>? onLog = null, Action<string>? onProgress = null)
    {
        var targets = new List<NetworkTarget>();
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < ips.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var ip = ips[i];
            onProgress?.Invoke($"[{i + 1}/{ips.Count}] {ip}");

            NetworkTarget target;
            if (_cache.TryGetValue(ip, out var cached) && (DateTime.Now - cached.Scanned) < CacheDuration)
            {
                target = cached.Target;
                onLog?.Invoke(new ScanLogEntry
                {
                    IpAddress = ip,
                    Status = target.IsOnline ? ScanLogEntryStatus.Online : ScanLogEntryStatus.Unreachable,
                    Message = $"(из кэша) {sw.ElapsedMilliseconds}мс"
                });
            }
            else
            {
                target = ScanHost(ip, onLog);
                _cache[ip] = (target, DateTime.Now);
            }

            targets.Add(target);
        }

        return targets;
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

        if (!target.IsOnline)
        {
            onLog?.Invoke(new ScanLogEntry
            {
                IpAddress = ip,
                Status = ScanLogEntryStatus.Unreachable,
                Message = $"Ping: {sw.ElapsedMilliseconds}мс — timeout"
            });
            return target;
        }

        // TCP port 445
        sw.Restart();
        var portOpen = IsPortOpen(ip, 445, 1000);
        sw.Stop();

        if (!portOpen)
        {
            onLog?.Invoke(new ScanLogEntry
            {
                IpAddress = ip,
                Status = ScanLogEntryStatus.Online,
                Message = $"Ping: {sw.ElapsedMilliseconds}мс — port 445 закрыт"
            });
            return target;
        }

        // Shares
        sw.Restart();
        target.AvailableShares = DiscoverShares(ip);
        sw.Stop();

        var shareMsg = target.AvailableShares.Count > 0
            ? $"shares: {string.Join(", ", target.AvailableShares)}"
            : "no accessible shares";

        onLog?.Invoke(new ScanLogEntry
        {
            IpAddress = ip,
            Status = ScanLogEntryStatus.Online,
            Message = $"Ping: {sw.ElapsedMilliseconds}мс — {shareMsg}"
        });

        return target;
    }

    private List<string> DiscoverShares(string ip)
    {
        var shares = new List<string>();
        string[] commonShares = ["C$", "D$", "ADMIN$", "Users", "IPC$"];
        var sw = Stopwatch.StartNew();

        foreach (var share in commonShares)
        {
            if (sw.ElapsedMilliseconds >= _shareTimeoutMs) break;

            try
            {
                var path = $"\\\\{ip}\\{share}";
                var task = Task.Run(() => Directory.Exists(path));
                var remaining = _shareTimeoutMs - (int)sw.ElapsedMilliseconds;
                if (remaining <= 0) break;
                if (task.Wait(Math.Min(remaining, 1000)) && task.Result)
                    shares.Add(share);
            }
            catch { }
        }

        return shares;
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
