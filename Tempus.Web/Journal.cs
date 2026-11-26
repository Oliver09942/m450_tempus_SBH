using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;
using Tempus.Core;

namespace Tempus.Web;

public partial class Journal(SqliteConnection db, Person actor)
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
    public static DateOnly Month(string? text) => Arbeitszeit.Datum(text + "-01");
    static string Key(DateOnly day) => day.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    public static string Text(string? value, int max, bool required = false)
    {
        var clean = (value ?? "").Trim();
        if (clean.Length > max || required && clean.Length == 0) throw new Fachfehler($"Text muss zwischen {(required ? 1 : 0)} und {max} Zeichen lang sein.");
        return clean;
    }
    public void Admin() { if (actor.Role != "Administration") throw new Zugriff(); }
    bool Personalverwaltung => actor.Role is "Administration" or "HR";
    public Person Owner(int id)
    {
        if (id != actor.Id && !Personalverwaltung) throw new Zugriff();
        return db.QuerySingleOrDefault<Person>("select id,name,login,role,start,opening from people where id=@id", new { id }) ?? throw new Fachfehler("Person unbekannt.");
    }
    void Manages(int id)
    {
        if (!Personalverwaltung || actor.Role == "HR" && id == actor.Id) throw new Zugriff();
    }
    void Log(int owner, string action) => db.Execute("insert into audit(at,actor,owner,action) values(@at,@by,@owner,@action)",
        new { at = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"), by = actor.Id, owner, action });
    void ProjectExists(int id)
    {
        if (db.ExecuteScalar<int>("select count(*) from projects where id=@id", new { id }) == 0) throw new Fachfehler("Projekt unbekannt.");
    }
    void OpenPeriod(int owner, DateOnly from, DateOnly to)
    {
        if (db.ExecuteScalar<int>("select count(*) from closings where owner=@owner and month between @a and @b",
            new { owner, a = Key(from), b = Key(to) }) > 0) throw new Fachfehler("Der Zeitraum enthält einen abgeschlossenen Monat.");
    }
    public object State(int id, string month)
    {
        var person = Owner(id);
        var report = Monat(person, month);
        var (plans, entries, absences, days, summary) = report;
        var available = Personalverwaltung ? db.Query<Person>("select id,name,login,role,start,opening from people order by name").ToArray() : [actor];
        var projects = actor.Role == "Administration"
            ? db.Query("select p.id,p.name,l.person leadId from projects p left join project_leads l on p.id=l.project order by p.name").AsList()
            : db.Query("select id,name from projects order by name").AsList();
        var stats = entries.GroupBy(x => x.Project).Select(x => new { name = x.Key, minutes = x.Sum(e => e.ToMin - e.FromMin - e.Pause) }).OrderByDescending(x => x.minutes);
        return new
        {
            actor,
            person,
            people = available,
            projects,
            plans,
            entries,
            absences,
            days,
            stats,
            today = Today.Iso(),
            month,
            summary,
            closed = db.ExecuteScalar<int>("select count(*) from closings where owner=@id and month=@month", new { id, month }) > 0,
            timer = db.QuerySingleOrDefault("select day,fromMin,projectId,note from timers where owner=@id", new { id }),
            audit = db.Query("select a.at,p.name actor,a.action from audit a join people p on p.id=a.actor where a.owner=@id order by a.id desc limit 50", new { id }).AsList()
        };
    }

    Monatsdaten Monat(Person person, string month)
    {
        var id = person.Id;
        var first = Month(month);
        var last = first.AddMonths(1).AddDays(-1);
        var start = Arbeitszeit.Datum(person.Start);
        var plans = db.Query<Plan>("select since,minutes from plans where owner=@id order by since", new { id }).ToArray();
        var schedules = plans.Select(x => (Since: Arbeitszeit.Datum(x.Since), Days: x.Tage)).ToArray();
        var history = db.Query<Zeit>("""
            select e.id,day,fromMin,toMin,pause,projectId,p.name project,note from entries e join projects p on p.id=e.projectId
            where owner=@id and day<=@last order by day,fromMin
            """, new { id, last = last.Iso() }).ToArray();
        var entries = history.Where(x => String.CompareOrdinal(x.Day, first.Iso()) >= 0).ToArray();
        var absences = db.Query<Absenz>("select * from absences where owner=@id order by von desc,id desc", new { id }).ToArray();
        var approved = absences.Where(x => x.Status == "Bewilligt").Select(x => (From: Arbeitszeit.Datum(x.Von), To: Arbeitszeit.Datum(x.Bis), x.Kind)).OrderBy(x => x.From).ToArray();
        var worked = history.GroupBy(x => x.Day).ToDictionary(x => x.Key, x => x.Sum(e => Arbeitszeit.Netto(e.FromMin, e.ToMin, e.Pause)));
        var days = new List<Tageszeile>();
        int carry = first > start ? person.Opening : first.AddMonths(1) > start ? person.Opening : 0;
        int actual = 0, target = 0, credit = 0, planned = 0, pi = 0, ai = 0;
        for (var day = start < first ? start : first; day <= last; day = day.AddDays(1))
        {
            while (pi + 1 < schedules.Length && schedules[pi + 1].Since <= day) pi++;
            while (ai < approved.Length && approved[ai].To < day) ai++;
            var kind = ai < approved.Length && approved[ai].From <= day ? approved[ai].Kind : null;
            int regular = day >= start ? schedules[pi].Days[Arbeitszeit.Wochentag(day)] : 0;
            var value = Arbeitszeit.Berechne(regular, worked.GetValueOrDefault(day.Iso()), kind);
            if (day < first) { if (day <= Today) carry += value.Differenz; continue; }
            planned += value.Soll;
            if (day <= Today) { actual += value.Arbeit; target += value.Soll; credit += value.Gutschrift; }
            days.Add(new Tageszeile(day.Iso(), kind, value.Plan, value.Soll, value.Arbeit, value.Gutschrift, day <= Today ? value.Differenz : null));
        }
        var summary = new Summe(carry, actual, target, credit, planned, actual + credit - target, carry + actual + credit - target);
        return new(plans, entries, absences, days, summary);
    }
    void CheckEntry(DateOnly day, int from, int to, int pause, int project, int ignore, bool stopping)
    {
        Arbeitszeit.Netto(from, to, pause);
        if (day < Arbeitszeit.Datum(actor.Start) || day > Today) throw new Fachfehler("Arbeitstag liegt ausserhalb des zulässigen Zeitraums.");
        var now = DateTime.Now;
        if (day == DateOnly.FromDateTime(now) && to > now.Hour * 60 + now.Minute) throw new Fachfehler("Das Ende liegt in der Zukunft.");
        ProjectExists(project);
        OpenPeriod(actor.Id, day, day);
        var args = new { owner = actor.Id, d = day.Iso(), from, to, ignore };
        if (db.ExecuteScalar<int>("select count(*) from entries where owner=@owner and day=@d and id<>@ignore and fromMin<@to and toMin>@from", args) > 0)
            throw new Fachfehler("Dieser Zeitraum überschneidet sich mit einer Buchung.");
        if (db.ExecuteScalar<int>("select count(*) from absences where owner=@owner and status='Bewilligt' and @d between von and bis", args) > 0)
            throw new Fachfehler("Für diesen Tag ist eine ganztägige Abwesenheit bewilligt.");
        if (!stopping && db.ExecuteScalar<int>("select count(*) from timers where owner=@owner and (day<@d or (day=@d and fromMin<@to))", args) > 0)
            throw new Fachfehler("Dieser Zeitraum überschneidet sich mit der laufenden Stempeluhr.");
    }
    Zeit Existing(int id) => db.QuerySingleOrDefault<Zeit>("select * from entries where id=@id and owner=@owner", new { id, owner = actor.Id }) ?? throw new Fachfehler("Buchung nicht gefunden.");
    public void Save(int id, Buchung item)
    {
        var day = Arbeitszeit.Datum(item.Day);
        int from = Arbeitszeit.Minute(item.From), to = Arbeitszeit.Minute(item.To, true);
        if (id != 0) { var old = Arbeitszeit.Datum(Existing(id).Day); OpenPeriod(actor.Id, old, old); }
        CheckEntry(day, from, to, item.Pause, item.ProjectId, id, false);
        var args = new { id, owner = actor.Id, day = day.Iso(), from, to, pause = item.Pause, projectId = item.ProjectId, note = Text(item.Note, 300) };
        db.Execute(id == 0 ? "insert into entries(owner,day,fromMin,toMin,pause,projectId,note) values(@owner,@day,@from,@to,@pause,@projectId,@note)"
            : "update entries set day=@day,fromMin=@from,toMin=@to,pause=@pause,projectId=@projectId,note=@note where id=@id", args);
        Log(actor.Id, $"Arbeitszeit {(id == 0 ? "erfasst" : "korrigiert")} · {item.Day} {item.From}–{item.To}, {item.Pause} min Pause");
    }
    public void Delete(int id)
    {
        var day = Arbeitszeit.Datum(Existing(id).Day);
        OpenPeriod(actor.Id, day, day);
        db.Execute("delete from entries where id=@id", new { id });
        Log(actor.Id, $"Arbeitszeit gelöscht · {day.Iso()}, Buchung {id}");
    }
    public void Start(UhrStart item)
    {
        var now = DateTime.Now;
        var day = DateOnly.FromDateTime(now);
        if (day < Arbeitszeit.Datum(actor.Start)) throw new Fachfehler("Der Eintritt liegt in der Zukunft.");
        OpenPeriod(actor.Id, day, day); ProjectExists(item.ProjectId);
        var args = new { owner = actor.Id, day = day.Iso(), minute = now.Hour * 60 + now.Minute, item.ProjectId, note = Text(item.Note, 300) };
        if (db.ExecuteScalar<int>("select count(*) from timers where owner=@owner", args) > 0) throw new Fachfehler("Stempeluhr läuft bereits.");
        if (db.ExecuteScalar<int>("select count(*) from absences where owner=@owner and status='Bewilligt' and @day between von and bis", args) > 0)
            throw new Fachfehler("Heute ist eine Abwesenheit bewilligt.");
        db.Execute("insert into timers values(@owner,@day,@minute,@ProjectId,@note)", args);
        Log(actor.Id, "Stempeluhr gestartet");
    }
    public void Stop(int pause)
    {
        var owner = actor.Id;
        var timer = db.QuerySingleOrDefault<Zeit>("select day,fromMin,projectId,note from timers where owner=@owner", new { owner }) ?? throw new Fachfehler("Keine laufende Stempeluhr.");
        var now = DateTime.Now;
        var first = Arbeitszeit.Datum(timer.Day); var last = DateOnly.FromDateTime(now); int end = now.Hour * 60 + now.Minute;
        if (first > last || first == last && end <= timer.FromMin) throw new Fachfehler("Mindestens eine volle Zeitminute abwarten oder die Uhr verwerfen.");
        if (first != last && pause != 0) throw new Fachfehler("Über Mitternacht ohne Pause stoppen und die Pause anschliessend am richtigen Tag nachtragen.");
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            int from = day == first ? timer.FromMin : 0, to = day == last ? end : 1440;
            if (to == 0) continue;
            CheckEntry(day, from, to, pause, timer.ProjectId, 0, true);
            db.Execute("insert into entries(owner,day,fromMin,toMin,pause,projectId,note) values(@owner,@day,@from,@to,@pause,@projectId,@note)",
                new { owner, day = day.Iso(), from, to, pause, projectId = timer.ProjectId, note = timer.Note });
        }
        db.Execute("delete from timers where owner=@owner", new { owner }); Log(owner, "Stempeluhr gestoppt und verbucht");
    }
    public void CancelTimer()
    {
        if (db.Execute("delete from timers where owner=@id", new { id = actor.Id }) == 0) throw new Fachfehler("Keine laufende Stempeluhr.");
        Log(actor.Id, "Stempeluhr verworfen");
    }

    public void Request(Antrag item)
    {
        var a = Arbeitszeit.Datum(item.Von); var b = Arbeitszeit.Datum(item.Bis);
        if (a < Arbeitszeit.Datum(actor.Start) || b < a) throw new Fachfehler("Abwesenheitszeitraum ungültig.");
        if (!Arbeitszeit.Abwesenheitsarten.Contains(item.Kind)) throw new Fachfehler("Abwesenheitsart unbekannt.");
        OpenPeriod(actor.Id, a, b);
        var args = new { owner = actor.Id, von = item.Von, bis = item.Bis, kind = item.Kind, note = Text(item.Note, 300) };
        if (db.ExecuteScalar<int>("select count(*) from absences where owner=@owner and status in ('Beantragt','Bewilligt') and von<=@bis and bis>=@von", args) > 0)
            throw new Fachfehler("Der Zeitraum überschneidet sich mit einer bestehenden Abwesenheit.");
        db.Execute("insert into absences(owner,von,bis,kind,note,status) values(@owner,@von,@bis,@kind,@note,'Beantragt')", args);
        Log(actor.Id, $"{item.Kind} beantragt · {item.Von} bis {item.Bis}");
    }
    public void Decide(int id, string status)
    {
        var x = db.QuerySingleOrDefault<Absenz>("select * from absences where id=@id", new { id }) ?? throw new Fachfehler("Antrag unbekannt.");
        if (status == "Zurückgezogen" && x.Owner == actor.Id && x.Status == "Beantragt") { }
        else
        {
            Manages(x.Owner);
            bool allowed = (x.Status == "Beantragt" && status is "Bewilligt" or "Abgelehnt") || (x.Status == "Bewilligt" && status == "Widerrufen");
            if (!allowed) throw new Fachfehler("Dieser Statuswechsel ist nicht möglich.");
        }
        OpenPeriod(x.Owner, Arbeitszeit.Datum(x.Von), Arbeitszeit.Datum(x.Bis));
        if (status == "Bewilligt")
        {
            if (db.ExecuteScalar<int>("select count(*) from entries where owner=@Owner and day between @Von and @Bis", x) > 0)
                throw new Fachfehler("Im Zeitraum bestehen Arbeitsbuchungen. Zuerst durch die betreffende Person klären lassen.");
            if (db.ExecuteScalar<int>("select count(*) from timers where owner=@Owner and day<=@Bis and @today>=@Von", new { x.Owner, x.Von, x.Bis, today = Today.Iso() }) > 0)
                throw new Fachfehler("Im Zeitraum läuft eine Stempeluhr.");
        }
        db.Execute("update absences set status=@status where id=@id", new { id, status }); Log(x.Owner, $"{x.Kind} · {x.Von} bis {x.Bis} · {status}");
    }
    public void Close(int id, Abschluss item)
    {
        var person = Owner(id); Manages(id);
        var first = Month(item.Month); var last = first.AddMonths(1).AddDays(-1);
        if (last < Arbeitszeit.Datum(person.Start)) throw new Fachfehler("Monat liegt vor dem Eintritt.");
        var args = new { id, month = item.Month, first = first.Iso(), last = last.Iso() };
        if (item.Closed)
        {
            if (last >= Today) throw new Fachfehler("Nur vollständig vergangene Monate können abgeschlossen werden.");
            if (db.ExecuteScalar<int>("select count(*) from absences where owner=@id and status='Beantragt' and von<=@last and bis>=@first", args) > 0
                || db.ExecuteScalar<int>("select count(*) from timers where owner=@id and day<=@last", args) > 0)
                throw new Fachfehler("Zuerst offene Anträge oder die laufende Stempeluhr klären.");
            // Saldi werden vorgetragen. Spätere Abschlüsse dürfen nachträgliche Änderungen im Vormonat nicht zulassen.
            var begin = DateOnly.ParseExact(person.Start, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var earliest = new DateOnly(begin.Year, begin.Month, 1);
            var prior = (first.Year - earliest.Year) * 12 + first.Month - earliest.Month;
            if (db.ExecuteScalar<int>("select count(*) from closings where owner=@id and month<@month", args) != prior)
                throw new Fachfehler("Bitte zuerst die früheren Monate abschliessen.");
            if (db.Execute("insert or ignore into closings values(@id,@month)", args) == 0) throw new Fachfehler("Monat ist bereits abgeschlossen.");
        }
        else
        {
            if (db.ExecuteScalar<int>("select count(*) from payroll where owner=@id and month=@month and final=1", args) > 0)
                throw new Fachfehler("HR muss zuerst das definitive Lohnblatt wieder öffnen.");
            if (db.ExecuteScalar<int>("select count(*) from closings where owner=@id and month>@month", args) > 0) throw new Fachfehler("Bitte zuerst den jüngsten Abschluss öffnen.");
            if (db.Execute("delete from closings where owner=@id and month=@month", args) == 0) throw new Fachfehler("Monat ist bereits offen.");
        }
        Log(id, $"Monat {item.Month} {(item.Closed ? "abgeschlossen" : "wieder geöffnet")}");
    }
    static string Week(int[]? minutes)
    {
        if (minutes == null || minutes.Length != 7 || minutes.Any(m => m < 0 || m > 1440) || minutes.Sum() > Arbeitszeit.VollzeitWoche)
            throw new Fachfehler("Sieben Tageswerte in Minuten angeben, zusammen höchstens 40 Stunden.");
        return string.Join(',', minutes);
    }
    public void CreatePerson(Konto x)
    {
        Admin(); var start = Arbeitszeit.Datum(x.Start); var week = Week(x.Minutes);
        var name = Text(x.Name, 80, true); var login = Text(x.Login, 40, true).ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(login, "^[a-z0-9._-]+$")) throw new Fachfehler("Login nur mit a–z, Ziffern, Punkt, Bindestrich und Unterstrich.");
        if (x.Role is not ("Mitarbeit" or "Projektleitung" or "Entwicklungsleitung" or "Administration" or "HR")) throw new Fachfehler("Rolle unbekannt.");
        PasswordLength(x.Password);
        if (Math.Abs((long)x.Opening) > 600000) throw new Fachfehler("Anfangssaldo ausserhalb des Bereichs.");
        if (db.ExecuteScalar<int>("select count(*) from people where login=@login", new { login }) > 0) throw new Fachfehler("Login bereits vergeben.");
        db.Execute("insert into people(name,login,role,start,opening,hash) values(@name,@login,@role,@start,@opening,@hash)",
            new { name, login, role = x.Role, start = start.Iso(), opening = x.Opening, hash = Datenbank.Hash(x.Password) });
        var id = db.ExecuteScalar<int>("select last_insert_rowid()");
        db.Execute("insert into plans values(@id,@start,@week)", new { id, start = start.Iso(), week }); Log(id, "Konto und Wochenplan angelegt");
    }
    public void Schedule(int id, Pensum x)
    {
        Admin(); var person = Owner(id); var day = Arbeitszeit.Datum(x.Since);
        if (day < Arbeitszeit.Datum(person.Start)) throw new Fachfehler("Pensum beginnt vor dem Eintritt.");
        OpenPeriod(id, day, new DateOnly(2099, 12, 31)); var week = Week(x.Minutes);
        db.Execute("insert into plans values(@id,@since,@week) on conflict(owner,since) do update set minutes=excluded.minutes", new { id, since = x.Since, week });
        Log(id, $"Wochenplan ab {x.Since} geändert");
    }
    public static void PasswordLength(string? text) { if (text == null || text.Length is < 10 or > 128) throw new Fachfehler("Passwort benötigt 10 bis 128 Zeichen."); }
    public void ChangePassword(Passwort x)
    {
        var id = actor.Id; var hash = db.ExecuteScalar<string>("select hash from people where id=@id", new { id })!;
        if (!Datenbank.Verify(x.Old ?? "", hash)) throw new Fachfehler("Bisheriges Passwort stimmt nicht.");
        PasswordLength(x.NewPassword);
        db.Execute("update people set hash=@hash,version=version+1 where id=@id", new { id, hash = Datenbank.Hash(x.NewPassword) }); Log(id, "Passwort geändert");
    }
    public void AddProject(Projekt x)
    {
        Admin(); var name = Text(x.Name, 80, true);
        if (db.Execute("insert or ignore into projects(name) values(@name)", new { name }) == 0) throw new Fachfehler("Projektname bereits vorhanden.");
        Log(actor.Id, $"Projekt angelegt · {name}");
    }
}
public class Zugriff : Exception;
