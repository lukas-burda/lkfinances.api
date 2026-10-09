using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace LkFinances.Api.Auth;

public static class ForwardedNetworks
{
    /// <summary>
    /// Trusts X-Forwarded-* only from CIDRs in ForwardedHeaders:KnownNetworks.
    /// Empty (development, API hit directly) means no proxy is trusted, so clients cannot spoof rate-limit IPs.
    /// </summary>
    public static void Configure(ForwardedHeadersOptions options, IConfiguration config)
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        var raw = config["ForwardedHeaders:KnownNetworks"];
        if (string.IsNullOrWhiteSpace(raw))
            return;

        foreach (var piece in raw.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var slash = piece.IndexOf('/');
            if (slash < 0)
                continue;
            if (IPAddress.TryParse(piece[..slash], out var ip) && int.TryParse(piece[(slash + 1)..], out var prefix))
                options.KnownIPNetworks.Add(new System.Net.IPNetwork(ip, prefix));
        }
    }
}
