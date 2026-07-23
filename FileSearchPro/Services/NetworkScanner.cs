using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FileSearchPro.Models;

namespace FileSearchPro.Services;

public class NetworkScanner
{
    private static readonly ConcurrentDictionary<string, (NetworkTarget Target, DateTime Scanned)> _cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);
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
        _cache.Clear();
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
                    Message = string.Format(LanguageManager.GetString("LogFromCache"), sw.ElapsedMilliseconds, shareMsg)
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

        // Ping (ICMP) — may be blocked by Windows Firewall
        var sw = Stopwatch.StartNew();
        string? pingError = null;
        try
        {
            using var ping = new Ping();
            var reply = ping.Send(ip, _pingTimeoutMs);
            target.IsOnline = reply.Status == IPStatus.Success;
            if (!target.IsOnline)
                pingError = reply.Status.ToString();
        }
        catch (Exception ex)
        {
            target.IsOnline = false;
            pingError = ex.Message;
        }
        sw.Stop();
        var pingMs = sw.ElapsedMilliseconds;

        // TCP port 445 — fallback if ping failed (ICMP may be blocked)
        sw.Restart();
        string? portError = null;
        var portOpen = IsPortOpenWithRetry(ip, 445, 1000, 2, out portError);
        sw.Stop();
        var portMs = sw.ElapsedMilliseconds;

        if (!target.IsOnline && !portOpen)
        {
            var diag = pingError != null ? $"ping: {pingError}" : "ping: blocked";
            if (portError != null) diag += $", port445: {portError}";
            onLog?.Invoke(new ScanLogEntry
            {
                IpAddress = ip,
                Status = ScanLogEntryStatus.Unreachable,
                Message = string.Format(LanguageManager.GetString("LogPingPort"), pingMs, portMs, diag)
            });
            return target;
        }

        // If ping failed but port 445 is open — host is online (ICMP blocked by firewall)
        if (!target.IsOnline && portOpen)
        {
            target.IsOnline = true;
            onLog?.Invoke(new ScanLogEntry
            {
                IpAddress = ip,
                Status = ScanLogEntryStatus.Online,
                Message = string.Format(LanguageManager.GetString("LogPingBlockedPort445"), pingMs, portMs)
            });
        }

        if (!portOpen)
        {
            onLog?.Invoke(new ScanLogEntry
            {
                IpAddress = ip,
                Status = ScanLogEntryStatus.Online,
                Message = string.Format(LanguageManager.GetString("LogPingPort"), pingMs, portMs, LanguageManager.GetString("LogPortClosed445"))
            });
            return target;
        }

        // Shares — с retry
        sw.Restart();
        target.AvailableShares = DiscoverSharesWithRetry(ip, 2);
        sw.Stop();
        var sharesMs = sw.ElapsedMilliseconds;

        var shareMsg = target.AvailableShares.Count > 0
            ? $"shares: {string.Join(", ", target.AvailableShares)}"
            : "no accessible shares";

        onLog?.Invoke(new ScanLogEntry
        {
            IpAddress = ip,
            Status = ScanLogEntryStatus.Online,
            Message = string.Format(LanguageManager.GetString("LogSharesSummary"), pingMs, portMs, sharesMs, shareMsg)
        });

        return target;
    }

    private bool IsPortOpenWithRetry(string ip, int port, int timeoutMs, int maxRetries, out string? error)
    {
        error = null;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync(ip, port);
                if (task.Wait(timeoutMs))
                {
                    if (task.IsCompletedSuccessfully)
                        return true;

                    error = task.Exception?.InnerException?.Message ?? "connection failed";
                }
                else
                {
                    error = "timeout";
                }
            }
            catch (SocketException ex)
            {
                error = $"SocketError({ex.SocketErrorCode}): {ex.Message}";
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            if (attempt < maxRetries - 1)
                Thread.Sleep(200);
        }
        return false;
    }

    private List<string> DiscoverSharesWithRetry(string ip, int maxRetries)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var result = ImpersonationHelper.RunAs(_credentials, () =>
                ShareEnumerator.EnumerateShares(ip));
            if (result.Count > 0 || attempt == maxRetries - 1)
                return result;

            Thread.Sleep(300 * (attempt + 1));
        }
        return new List<string>();
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
