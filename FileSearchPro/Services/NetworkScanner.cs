using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using FileSearchPro.Models;

namespace FileSearchPro.Services;

public class NetworkScanner
{
    private static readonly ConcurrentDictionary<string, (NetworkTarget Target, DateTime Scanned)> _cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<List<NetworkTarget>> ScanNetworkAsync(List<string> ips, int maxWorkers, CancellationToken ct)
    {
        var targets = new List<NetworkTarget>();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxWorkers,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(ips, parallelOptions, async (ip, token) =>
        {
            NetworkTarget target;
            if (_cache.TryGetValue(ip, out var cached) && (DateTime.Now - cached.Scanned) < CacheDuration)
            {
                target = cached.Target;
            }
            else
            {
                target = await ScanHostAsync(ip, token);
                _cache[ip] = (target, DateTime.Now);
            }
            lock (targets)
            {
                targets.Add(target);
            }
        });

        return targets;
    }

    public void ClearCache() => _cache.Clear();

    public async Task<NetworkTarget> ScanHostAsync(string ip, CancellationToken ct)
    {
        var target = new NetworkTarget { IpAddress = ip };

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, 1000);
            target.IsOnline = reply.Status == IPStatus.Success;
        }
        catch
        {
            target.IsOnline = false;
        }

        if (target.IsOnline)
        {
            target.AvailableShares = await DiscoverSharesAsync(ip, ct);
        }

        return target;
    }

    private async Task<List<string>> DiscoverSharesAsync(string ip, CancellationToken ct)
    {
        var shares = new List<string>();
        string[] commonShares = ["C$", "D$", "ADMIN$", "Users", "IPC$"];

        foreach (var share in commonShares)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var path = $"\\\\{ip}\\{share}";
                var task = Task.Run(() => Directory.Exists(path), ct);
                if (await Task.WhenAny(task, Task.Delay(2000, ct)) == task && task.Result)
                {
                    shares.Add(share);
                }
            }
            catch
            {
                // Share not accessible
            }
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
                        // Short range: 192.168.1.1-254
                        for (int i = baseBytes[3]; i <= Math.Min(endNum, 255); i++)
                        {
                            var bytes = new[] { baseBytes[0], baseBytes[1], baseBytes[2], (byte)i };
                            ips.Add(new IPAddress(bytes).ToString());
                        }
                    }
                    else if (IPAddress.TryParse(rangeParts[1], out var endIp))
                    {
                        // Full range: 192.168.1.1-192.168.1.254
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
