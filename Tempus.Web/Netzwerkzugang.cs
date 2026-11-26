using System.Net;
using System.Net.Sockets;

namespace Tempus.Web;

internal record Netzwerkzugang(Uri Url, IPAddress Adresse, string Zertifikat, string? Passwort)
{
    public static Netzwerkzugang? Lesen(IConfiguration config)
    {
        string? url = config["FreigabeUrl"];
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.AbsolutePath != "/" || uri.Query.Length + uri.Fragment.Length + uri.UserInfo.Length != 0)
            throw new ArgumentException("FreigabeUrl muss eine HTTPS-Adresse ohne Pfad, Benutzerangabe oder Parameter sein.");
        if (!IPAddress.TryParse(config["FreigabeIP"], out var ip) || ip.AddressFamily != AddressFamily.InterNetwork
            || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.Broadcast))
            throw new ArgumentException("FreigabeIP muss die konkrete lokale IPv4-Adresse des Rechners sein.");
        string? cert = config["FreigabePfx"];
        if (string.IsNullOrWhiteSpace(cert) || !File.Exists(cert))
            throw new ArgumentException("FreigabePfx muss auf das HTTPS-Zertifikat mit privatem Schlüssel zeigen.");
        return new(uri, ip, Path.GetFullPath(cert), Environment.GetEnvironmentVariable("TEMPUS_PFX_PASSWORT"));
    }
}
