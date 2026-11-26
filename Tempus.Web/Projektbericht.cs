using Dapper;
using Tempus.Core;

namespace Tempus.Web;

public class Projektstunden
{
    public int ProjektId { get; set; }
    public string Projekt { get; set; } = "";
    public string? Leitung { get; set; }
    public int? PersonId { get; set; }
    public string? Person { get; set; }
    public int Minuten { get; set; }
}

public record Leitungswechsel(int? PersonId);

public partial class Journal
{
    public object Projektbericht(string month)
    {
        if (actor.Role is not ("Projektleitung" or "Entwicklungsleitung" or "Administration")) throw new Zugriff();
        var first = Month(month);
        // Die Projektliste bestimmt den Zugriff, auch wenn im Monat noch keine Buchungen vorliegen.
        var rows = db.Query<Projektstunden>("""
            select p.id ProjektId, p.name Projekt, lead.name Leitung,
                   w.id PersonId, w.name Person, coalesce(sum(e.toMin-e.fromMin-e.pause),0) Minuten
            from projects p
            left join project_leads l on l.project=p.id
            left join people lead on lead.id=l.person
            left join entries e on e.projectId=p.id and e.day>=@von and e.day<@bis
            left join people w on w.id=e.owner
            where @alle or l.person=@id
            group by p.id,w.id
            order by p.name collate nocase,w.name collate nocase,w.id
            """, new { alle = actor.Role != "Projektleitung", id = actor.Id, von = first.Iso(), bis = first.AddMonths(1).Iso() });
        var projekte = rows.GroupBy(r => r.ProjektId).Select(g => new
        {
            id = g.Key, name = g.First().Projekt, leitung = g.First().Leitung,
            minuten = g.Sum(r => (long)r.Minuten),
            beteiligte = g.Where(r => r.PersonId.HasValue).Select(r => new { id = r.PersonId, name = r.Person, minuten = r.Minuten })
        }).ToArray();
        return new { month, projekte, gesamt = projekte.Sum(p => p.minuten) };
    }

    public void SetLead(int project, int? personId)
    {
        Admin();
        ProjectExists(project);
        if (personId.HasValue)
        {
            if (db.ExecuteScalar<int>("select count(*) from people where id=@personId and role='Projektleitung'", new { personId }) == 0)
                throw new Fachfehler("Bitte eine Person mit der Rolle Projektleitung auswählen.");
            db.Execute("""
                insert into project_leads(project,person) values(@project,@personId)
                on conflict(project) do update set person=excluded.person
                """, new { project, personId });
        }
        else db.Execute("delete from project_leads where project=@project", new { project });
        Log(actor.Id, $"Projekt {project}: Leitung {(personId?.ToString() ?? "aufgehoben")}");
    }
}
