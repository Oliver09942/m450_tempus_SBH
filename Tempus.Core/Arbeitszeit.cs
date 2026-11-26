using System.Globalization;

namespace Tempus.Core;

public sealed class Fachfehler(string message) : Exception(message);

public static class Arbeitszeit
{
    public const int VollzeitWoche = 40 * 60;
    public static readonly string[] Abwesenheitsarten = ["Ferien", "Krankheit", "Unbezahlter Urlaub", "Sabbatical", "Gleitzeitabbau"];

    public static DateOnly Datum(string? text)
    {
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tag)
            || tag.Year is < 2000 or > 2099)
            throw new Fachfehler("Bitte ein gültiges Datum zwischen 2000 und 2099 wählen.");
        return tag;
    }

    public static string Iso(this DateOnly tag) => tag.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static int Minute(string? text, bool tagesende = false)
    {
        if (tagesende && text == "24:00") return 1440;
        if (!TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var zeit))
            throw new Fachfehler("Zeit bitte als HH:mm eingeben.");
        return zeit.Hour * 60 + zeit.Minute;
    }

    public static int Netto(int von, int bis, int pause)
    {
        if (von < 0 || bis > 1440 || von >= bis) throw new Fachfehler("Das Ende muss am selben Tag nach dem Beginn liegen.");
        if (pause < 0 || pause >= bis - von) throw new Fachfehler("Die Pause muss kürzer als die erfasste Zeit sein.");
        return bis - von - pause;
    }

    public static int Wochentag(DateOnly tag) => ((int)tag.DayOfWeek + 6) % 7;

    public static Tageswert Berechne(int plan, int arbeit, string? bewilligteAbsenz)
    {
        int soll = bewilligteAbsenz is "Unbezahlter Urlaub" or "Sabbatical" ? 0 : plan;
        int gutschrift = bewilligteAbsenz is "Ferien" or "Krankheit" ? soll : 0;
        return new(plan, soll, arbeit, gutschrift, arbeit + gutschrift - soll);
    }
}
public record Tageswert(int Plan, int Soll, int Arbeit, int Gutschrift, int Differenz);
