namespace Tempus.Core;

public record LohnEingabe(string Arbeitgeber, string Modell, decimal Ansatz, decimal Korrektur, decimal Zulagen, decimal Sozialbeitraege,
    decimal Pensionskasse, decimal Versicherung, decimal Steuer, decimal Weitere, string Bemerkung, bool Definitiv);
public record LohnBetrag(int Minuten, decimal Basis, decimal Brutto, decimal Abzuege, decimal Netto);

public static class Lohn
{
    public static LohnBetrag Berechne(LohnEingabe x, int minuten)
    {
        if (x.Modell is not ("Stundenlohn" or "Monatslohn")) throw new Fachfehler("Lohnmodell unbekannt.");
        decimal[] values = [x.Ansatz, x.Korrektur, x.Zulagen, x.Sozialbeitraege, x.Pensionskasse, x.Versicherung, x.Steuer, x.Weitere];
        if (minuten < 0 || values.Any(v => v is < -1000000 or > 1000000 || decimal.Round(v, 2) != v) ||
            values.Where((v, i) => i != 1).Any(v => v < 0)) throw new Fachfehler("Ungültiger Geldbetrag. Maximal zwei Dezimalstellen verwenden.");
        var basis = x.Modell == "Monatslohn" ? x.Ansatz : decimal.Round(x.Ansatz * minuten / 60m, 2, MidpointRounding.AwayFromZero);
        var brutto = basis + x.Korrektur + x.Zulagen;
        var abzuege = x.Sozialbeitraege + x.Pensionskasse + x.Versicherung + x.Steuer + x.Weitere;
        if (brutto < 0 || brutto < abzuege) throw new Fachfehler("Abzüge oder Korrekturen übersteigen den Bruttolohn.");
        return new(minuten, basis, brutto, abzuege, brutto - abzuege);
    }
}
