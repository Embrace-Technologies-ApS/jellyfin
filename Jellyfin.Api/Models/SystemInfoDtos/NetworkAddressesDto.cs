using System.Collections.Generic;

namespace Jellyfin.Api.Models.SystemInfoDtos;

/// <summary>
/// The server's network addresses that are reachable on the LAN or globally, grouped by IP version.
/// </summary>
public record NetworkAddressesDto
{
    /// <summary>
    /// Gets or sets the RFC 1918 private IPv4 addresses of the server.
    /// </summary>
    public required IReadOnlyCollection<NetworkAddressDto> LanIpv4Addresses { get; set; }

    /// <summary>
    /// Gets or sets the global-unicast IPv6 addresses of the server. Link-local addresses are excluded.
    /// </summary>
    public required IReadOnlyCollection<NetworkAddressDto> GlobalIpv6Addresses { get; set; }
}
