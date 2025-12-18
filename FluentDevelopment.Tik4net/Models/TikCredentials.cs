

namespace FluentDevelopment.Tik4net.Models;

public class TikCredentials
{
    public string Host { get; set; } = "192.168.88.1";
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "";
    public int Port { get; set; } = 8728;
    public bool IsValid { get; set; }
}
