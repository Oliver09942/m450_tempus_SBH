namespace Tempus.Web;

public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Login { get; set; } = "";
    public string Role { get; set; } = "";
    public string Start { get; set; } = "";
    public int Opening { get; set; }
}
public class Zeit
{
    public int Id { get; set; }
    public string Day { get; set; } = "";
    public int FromMin { get; set; }
    public int ToMin { get; set; }
    public int Pause { get; set; }
    public int ProjectId { get; set; }
    public string Project { get; set; } = "";
    public string Note { get; set; } = "";
}
public class Absenz
{
    public int Id { get; set; }
    public int Owner { get; set; }
    public string Von { get; set; } = "";
    public string Bis { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Status { get; set; } = "";
    public string Note { get; set; } = "";
}
public class Plan
{
    public string Since { get; set; } = "";
    public string Minutes { get; set; } = "";
    public int[] Tage => Minutes.Split(',').Select(int.Parse).ToArray();
}
public record Anmeldung(string Login, string Password);
public record Passwort(string Old, string NewPassword);
public record Buchung(string Day, string From, string To, int Pause, int ProjectId, string Note);
public record Antrag(string Von, string Bis, string Kind, string Note);
public record Entscheid(string Status);
public record Projekt(string Name);
public record Konto(string Name, string Login, string Password, string Role, string Start, int Opening, int[] Minutes);
public record Pensum(string Since, int[] Minutes);
public record UhrStart(int ProjectId, string Note);
public record UhrStopp(int Pause);
public record Abschluss(string Month, bool Closed);
public class LoginRecord
{
    public int Id { get; set; }
    public int Version { get; set; }
    public string Hash { get; set; } = "";
}
public record Tageszeile(string Day, string? Kind, int Plan, int Soll, int Arbeit, int Gutschrift, int? Delta);
public record Summe(int Carry, int Actual, int Target, int Credit, int Planned, int Delta, int Balance);
public record Monatsdaten(Plan[] Plans, Zeit[] Entries, Absenz[] Absences, List<Tageszeile> Days, Summe Summary);
