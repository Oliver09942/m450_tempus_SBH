using System.Text.Json;
using Dapper;
using Tempus.Core;

namespace Tempus.Web;

public record Lohnblatt(string Person, string Login, string Monat, string ErstelltVon, string Stand, LohnEingabe Eingabe, LohnBetrag Betrag);
public partial class Journal
{
    void Hr()
    {
        if (actor.Role is not ("HR" or "Administration")) throw new Zugriff();
    }
    public object Stundenbericht(string month)
    {
        Hr(); var last = Month(month).AddMonths(1).AddDays(-1).Iso();
        var people = db.Query<Person>("select id,name,login,role,start,opening from people where start<=@last order by name", new { last });
        return people.Select(p =>
        {
            var (plans, entries, absences, days, sum) = Monat(p, month);
            var unpaid = days.Where(d => d.Delta.HasValue && d.Kind is "Sabbatical" or "Unbezahlter Urlaub").Sum(d => d.Plan);
            return new { p.Id, p.Name, sum, unpaid, closed = db.ExecuteScalar<int>("select count(*) from closings where owner=@id and month=@month", new { id = p.Id, month }) > 0 };
        }).ToArray();
    }
    public Lohnblatt? ReadSlip(int id, string month)
    {
        Hr(); Owner(id); Month(month);
        var json = db.ExecuteScalar<string>("select payload from payroll where owner=@id and month=@month", new { id, month });
        return json == null ? null : JsonSerializer.Deserialize<Lohnblatt>(json);
    }
    public void SaveSlip(int id, string month, LohnEingabe x)
    {
        Hr(); var person = Owner(id); var report = Monat(person, month);
        if (Month(month).AddMonths(1).AddDays(-1) < Arbeitszeit.Datum(person.Start)) throw new Fachfehler("Monat liegt vor dem Eintritt.");
        if (db.ExecuteScalar<int>("select count(*) from payroll where owner=@id and month=@month and final=1", new { id, month }) > 0)
            throw new Fachfehler("Das definitive Lohnblatt zuerst wieder öffnen.");
        if (x.Definitiv && db.ExecuteScalar<int>("select count(*) from closings where owner=@id and month=@month", new { id, month }) == 0)
            throw new Fachfehler("Für ein definitives Lohnblatt muss der Zeitmonat abgeschlossen sein.");
        x = x with { Arbeitgeber = Text(x.Arbeitgeber, 100, true), Bemerkung = Text(x.Bemerkung, 300) };
        var betrag = Lohn.Berechne(x, report.Summary.Actual + report.Summary.Credit);
        var slip = new Lohnblatt(person.Name, person.Login, month, actor.Name, DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm zzz"), x, betrag);
        db.Execute("insert into payroll values(@id,@month,@final,@json) on conflict(owner,month) do update set final=excluded.final,payload=excluded.payload",
            new { id, month, final = x.Definitiv ? 1 : 0, json = JsonSerializer.Serialize(slip) });
        Log(id, "Lohnblatt " + month + (x.Definitiv ? " definitiv gespeichert" : " als Entwurf gespeichert"));
    }
    public void ReopenSlip(int id, string month)
    {
        var slip = ReadSlip(id, month);
        if (slip == null || !slip.Eingabe.Definitiv) throw new Fachfehler("Kein definitives Lohnblatt vorhanden.");
        var changed = slip with { Eingabe = slip.Eingabe with { Definitiv = false }, ErstelltVon = actor.Name, Stand = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm zzz") };
        db.Execute("update payroll set final=0,payload=@json where owner=@id and month=@month", new { id, month, json = JsonSerializer.Serialize(changed) });
        Log(id, "Definitives Lohnblatt " + month + " wieder geöffnet");
    }
}
