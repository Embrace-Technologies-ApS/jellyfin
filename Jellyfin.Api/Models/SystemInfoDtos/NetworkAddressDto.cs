namespace Jellyfin.Api.Models.SystemInfoDtos;

/// <summary>
/// A network address of the server, paired with the name of the interface it belongs to.
/// </summary>
public record NetworkAddressDto
{
    /// <summary>
    /// Gets or sets the IP address. IPv4 addresses are private (RFC 1918); IPv6 addresses are global-unicast.
    /// </summary>
    public required string Address { get; set; }

    /// <summary>
    /// Gets or sets the name of the network interface that has this address.
    /// </summary>
    public required string InterfaceName { get; set; }
}
