namespace Jellyfin.Api.Models.SystemInfoDtos;

/// <summary>
/// A network address of the server and the network interface it is assigned to.
/// </summary>
public record NetworkAddressDto
{
    /// <summary>
    /// Gets or sets the IP address.
    /// </summary>
    public required string Address { get; set; }

    /// <summary>
    /// Gets or sets the name of the network interface the address is assigned to.
    /// </summary>
    public required string InterfaceName { get; set; }
}
