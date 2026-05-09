

namespace FluentDevelopment.Tik4net.Models;

/// <summary>
/// Represents credentials required to connect to a MikroTik device.
/// </summary>
public class TikCredentials
{
    /// <summary>
    /// Gets or sets the host address of the MikroTik device.
    /// </summary>
    public string Host { get; set; } = "192.168.88.1";

    /// <summary>
    /// Gets or sets the username for authentication.
    /// </summary>
    public string Username { get; set; } = "admin";

    /// <summary>
    /// Gets or sets the password for authentication.
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// Gets or sets the port used for the connection.
    /// </summary>
    public int Port { get; set; } = 8728;

    /// <summary>
    /// Gets or sets a value indicating whether the credentials are valid.
    /// </summary>
    public bool IsValid { get; set; }
}
