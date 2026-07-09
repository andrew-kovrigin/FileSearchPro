using System.Collections.Concurrent;
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

    public Task<List<NetworkTarget>> ScanNetworkAsync(List<string> ips, CancellationToken ct, Action<ScanLogEntry>? onLog = null, Action<string>? onProgress = null)
    {
        return Task.Run(() =>
        {
            var targets = new List<NetworkTarget>();

            foreach (var ip in ips)
            {
                ct.ThrowIfCancellationRequested();
                onProgress?.Invoke(ip);

                NetworkTarget target;
                if (_cache.TryGetValue(ip, out var cached) && (DateTime.Now - cached.Scanned) < CacheDuration)
                {
                    target = cached.Target;
                }
                else
                {
                    target = ScanHost(ip);
                    _cache[ip] = (target, DateTime.Now);
                }

                targets.Add(target);

                onLog?.Invoke(new ScanLogEntry
                {
                    IpAddress = ip,
                    Status = target.IsOnline ? ScanLogEntryStatus.Online : ScanLogEntryStatus.Unreachable,
                    Message = target.IsOnline
                        ? (target.AvailableShares.Count > 0
                            ? $"Online — shares: {string.Join(", ", target.AvailableShares)}"
                            : "Online — no accessible shares")
                        : "Unreachable (ping timeout)",
                    SharesFound = target.AvailableShares
                });
            }

            return targets;
        }, ct);
    }

    public void ClearCache() => _cache.Clear();

    private NetworkTarget ScanHost(string ip)
    {
        var target = new NetworkTarget { IpAddress = ip };

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

        if (target.IsOnline)
        {
            target.AvailableShares = DiscoverShares(ip);
        }

        return target;
    }

    private List<string> DiscoverShares(string ip)
    {
        var shares = new List<string>();
        string[] commonShares = ["C$", "D$", "ADMIN$", "Users", "IPC$"];

        foreach (var share in commonShares)
        {
            try
            {
                var path = $"\\\\{ip}\\{share}";
                var task = Task.Run(() => Directory.Exists(path));
                if (task.Wait(_shareTimeoutMs) && task.Result)
                    shares.Add(share);
            }
            catch { }
        }

        return shares;
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
