// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Services.Metadata.Catalog.Icons
{
    /// <summary>
    /// Default <see cref="IExternalIconUrlPolicy"/> that mitigates SSRF by only
    /// permitting http/https URLs whose host resolves exclusively to public,
    /// routable IP addresses. Any failure to resolve, or any address that falls
    /// inside loopback, private, link-local, multicast, broadcast,
    /// carrier-grade NAT, or other reserved ranges causes the URL to be
    /// rejected.
    /// </summary>
    public class PublicExternalIconUrlPolicy : IExternalIconUrlPolicy
    {
        private readonly ILogger<PublicExternalIconUrlPolicy> _logger;

        public PublicExternalIconUrlPolicy(ILogger<PublicExternalIconUrlPolicy> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> IsAllowedAsync(Uri iconUrl, CancellationToken cancellationToken)
        {
            if (iconUrl == null)
            {
                return false;
            }

            if (!iconUrl.IsAbsoluteUri)
            {
                _logger.LogInformation("Rejecting non-absolute icon URL");
                return false;
            }

            if (iconUrl.Scheme != Uri.UriSchemeHttp && iconUrl.Scheme != Uri.UriSchemeHttps)
            {
                _logger.LogInformation("Rejecting icon URL {IconUrl} due to disallowed scheme {Scheme}", iconUrl, iconUrl.Scheme);
                return false;
            }

            if (iconUrl.HostNameType == UriHostNameType.IPv4 || iconUrl.HostNameType == UriHostNameType.IPv6)
            {
                if (IPAddress.TryParse(iconUrl.DnsSafeHost, out var literalAddress))
                {
                    if (IsRestrictedAddress(literalAddress))
                    {
                        _logger.LogInformation("Rejecting icon URL {IconUrl} because host literal {Address} is not a public IP address", iconUrl, literalAddress);
                        return false;
                    }

                    return true;
                }

                _logger.LogInformation("Rejecting icon URL {IconUrl} because the host literal could not be parsed as an IP address", iconUrl);
                return false;
            }

            if (iconUrl.HostNameType != UriHostNameType.Dns)
            {
                _logger.LogInformation("Rejecting icon URL {IconUrl} due to unsupported host name type {HostType}", iconUrl, iconUrl.HostNameType);
                return false;
            }

            IPAddress[] addresses;
            try
            {
                addresses = await Dns.GetHostAddressesAsync(iconUrl.DnsSafeHost).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogInformation(0, e, "Rejecting icon URL {IconUrl} because DNS resolution failed", iconUrl);
                return false;
            }

            if (addresses == null || addresses.Length == 0)
            {
                _logger.LogInformation("Rejecting icon URL {IconUrl} because DNS resolution returned no addresses", iconUrl);
                return false;
            }

            // Reject if ANY resolved address is in a restricted range. Allowing a
            // mix of public and private addresses would still let an attacker
            // reach the private destination via DNS round-robin.
            foreach (var address in addresses)
            {
                if (IsRestrictedAddress(address))
                {
                    _logger.LogInformation("Rejecting icon URL {IconUrl} because host {Host} resolves to non-public address {Address}", iconUrl, iconUrl.DnsSafeHost, address);
                    return false;
                }
            }

            return true;
        }

        internal static bool IsRestrictedAddress(IPAddress address)
        {
            if (address == null)
            {
                return true;
            }

            if (IPAddress.IsLoopback(address)
                || IPAddress.Any.Equals(address)
                || IPAddress.Broadcast.Equals(address)
                || IPAddress.IPv6Any.Equals(address)
                || IPAddress.IPv6Loopback.Equals(address)
                || IPAddress.None.Equals(address))
            {
                return true;
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();
                // 0.0.0.0/8 - "this" network
                if (bytes[0] == 0) return true;
                // 10.0.0.0/8 - private
                if (bytes[0] == 10) return true;
                // 100.64.0.0/10 - carrier-grade NAT
                if (bytes[0] == 100 && (bytes[1] & 0xC0) == 64) return true;
                // 127.0.0.0/8 - loopback (also covered by IsLoopback)
                if (bytes[0] == 127) return true;
                // 169.254.0.0/16 - link-local
                if (bytes[0] == 169 && bytes[1] == 254) return true;
                // 172.16.0.0/12 - private
                if (bytes[0] == 172 && (bytes[1] & 0xF0) == 16) return true;
                // 192.0.0.0/24 - IETF protocol assignments
                if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0) return true;
                // 192.0.2.0/24 - TEST-NET-1
                if (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) return true;
                // 192.168.0.0/16 - private
                if (bytes[0] == 192 && bytes[1] == 168) return true;
                // 198.18.0.0/15 - benchmarking
                if (bytes[0] == 198 && (bytes[1] & 0xFE) == 18) return true;
                // 198.51.100.0/24 - TEST-NET-2
                if (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) return true;
                // 203.0.113.0/24 - TEST-NET-3
                if (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113) return true;
                // 224.0.0.0/4 - multicast
                if ((bytes[0] & 0xF0) == 0xE0) return true;
                // 240.0.0.0/4 - reserved (includes 255.255.255.255 broadcast)
                if ((bytes[0] & 0xF0) == 0xF0) return true;

                return false;
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
                {
                    return true;
                }

                // IPv4-mapped IPv6 addresses (::ffff:0:0/96) - check the embedded IPv4.
                if (address.IsIPv4MappedToIPv6)
                {
                    return IsRestrictedAddress(address.MapToIPv4());
                }

                var bytes = address.GetAddressBytes();
                // fc00::/7 - unique local addresses
                if ((bytes[0] & 0xFE) == 0xFC) return true;
                // ::/128 - unspecified, and ::1/128 - loopback handled above
                // 2001:db8::/32 - documentation
                if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8) return true;
                // 64:ff9b::/96 - IPv4/IPv6 translation - block to be safe
                if (bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xFF && bytes[3] == 0x9B) return true;

                return false;
            }

            // Unknown address family - reject by default.
            return true;
        }
    }
}
