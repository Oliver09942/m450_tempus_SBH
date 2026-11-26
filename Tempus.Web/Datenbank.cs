using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using Tempus.Core;

namespace Tempus.Web;

public class Datenbank(string directory)
{
    public SqliteConnection Open()
    {
        var path = Path.Combine(directory, "tempus.sqlite3");
        var legacyPath = Path.Combine(directory, "saldo.sqlite3");
        if (!File.Exists(path) && File.Exists(legacyPath)) path = legacyPath;
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            ForeignKeys = true,
            DefaultTimeout = 10
        }.ToString());
        db.Open();
        return db;
    }

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 210000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(salt) + "." + Convert.ToBase64String(key);
    }

    public static bool Verify(string password, string hash)
    {
        var parts = hash.Split('.');
        var key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Convert.FromBase64String(parts[0]), 210000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(key, Convert.FromBase64String(parts[1]));
    }

    public void Init()
    {
        Directory.CreateDirectory(directory);
        using var db = Open();
        db.Execute("pragma journal_mode=WAL");
        using var tx = db.BeginTransaction();
        db.Execute("""
            CREATE TABLE IF NOT EXISTS people(id INTEGER PRIMARY KEY,name TEXT NOT NULL,login TEXT UNIQUE COLLATE NOCASE NOT NULL,
                role TEXT NOT NULL,start TEXT NOT NULL,opening INTEGER NOT NULL,hash TEXT NOT NULL,version INTEGER NOT NULL DEFAULT 1);
            CREATE TABLE IF NOT EXISTS plans(owner INTEGER REFERENCES people NOT NULL,since TEXT NOT NULL,minutes TEXT NOT NULL,PRIMARY KEY(owner,since));
            CREATE TABLE IF NOT EXISTS projects(id INTEGER PRIMARY KEY,name TEXT UNIQUE COLLATE NOCASE NOT NULL);
            CREATE TABLE IF NOT EXISTS entries(id INTEGER PRIMARY KEY,owner INTEGER REFERENCES people NOT NULL,day TEXT NOT NULL,
                fromMin INTEGER NOT NULL,toMin INTEGER NOT NULL,pause INTEGER NOT NULL,projectId INTEGER REFERENCES projects NOT NULL,note TEXT NOT NULL,
                CHECK(fromMin>=0 AND toMin<=1440 AND toMin>fromMin AND pause>=0 AND pause<toMin-fromMin));
            CREATE TABLE IF NOT EXISTS project_leads(project INTEGER PRIMARY KEY REFERENCES projects(id),person INTEGER NOT NULL REFERENCES people(id));
            CREATE INDEX IF NOT EXISTS entryProjectDay ON entries(projectId,day,owner);
            CREATE INDEX IF NOT EXISTS entryDays ON entries(owner,day);
            CREATE TABLE IF NOT EXISTS absences(id INTEGER PRIMARY KEY,owner INTEGER REFERENCES people NOT NULL,von TEXT NOT NULL,bis TEXT NOT NULL,
                kind TEXT NOT NULL,status TEXT NOT NULL,note TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS closings(owner INTEGER REFERENCES people NOT NULL,month TEXT NOT NULL,PRIMARY KEY(owner,month));
            CREATE TABLE IF NOT EXISTS timers(owner INTEGER PRIMARY KEY REFERENCES people, day TEXT NOT NULL,fromMin INTEGER NOT NULL,
                projectId INTEGER REFERENCES projects NOT NULL,note TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS payroll(owner INTEGER REFERENCES people NOT NULL,month TEXT NOT NULL,final INTEGER NOT NULL,payload TEXT NOT NULL,PRIMARY KEY(owner,month));
            CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY,at TEXT NOT NULL,actor INTEGER REFERENCES people NOT NULL,
                owner INTEGER REFERENCES people NOT NULL,action TEXT NOT NULL);
            """, transaction: tx);
        if (db.ExecuteScalar<int>("select count(*) from people", transaction: tx) == 0) Seed(db);
        db.Execute("update people set role='Projektleitung', version=version+1 where role='Teamleitung'", transaction: tx);
        tx.Commit();
    }

    static void Seed(SqliteConnection db)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        db.Execute("insert into projects(name) values ('Kundenportal'),('Betrieb & Support'),('Interne Weiterbildung')");
        var people = new List<(string Name, string Login, string Role, string Minutes)> {
            ("Nora Keller", "nora", "Mitarbeit", "480,480,480,480,480,0,0"),
            ("Luca Meier", "luca", "Mitarbeit", "480,480,0,480,0,0,0"),
            ("Samira Frei", "samira", "Projektleitung", "480,480,480,480,480,0,0"),
            ("Admin Tempus", "admin", "Administration", "480,480,480,480,480,0,0"),
            ("Elena Rossi", "hr", "HR", "480,480,480,480,480,0,0"),
            ("Mara Vogel", "mara", "Projektleitung", "480,480,480,480,480,0,0"),
            ("Jonas Baumann", "jonas", "Entwicklungsleitung", "480,480,480,480,480,0,0") };
        string[] vornamen = ["Anna", "Ben", "Chiara", "Dario", "Eva", "Finn", "Gina"];
        string[] nachnamen = ["Ammann", "Blum", "Costa", "Dürr", "Egger", "Frei", "Graf"];
        for (int i = 0; people.Count < 50; i++)
        {
            string name = vornamen[i % vornamen.Length] + " " + nachnamen[i / vornamen.Length];
            string pensum = i % 4 == 0 ? "360,360,0,360,360,0,0" : "480,480,480,480,480,0,0";
            people.Add((name, $"team{i + 1:00}", "Mitarbeit", pensum));
        }
        var index = 0;
        foreach (var (name, login, role, minutes) in people)
        {
            index++;
            db.Execute("insert into people(name,login,role,start,opening,hash) values (@name,@login,@role,@start,@opening,@hash)",
                new { name, login, role, start = first.Iso(), opening = index == 1 ? 90 : 0, hash = Hash("Tempus-Demo26!") });
            db.Execute("insert into plans values(@index,@since,@minutes)", new { index, since = first.Iso(), minutes });
            var week = minutes.Split(',').Select(int.Parse).ToArray();
            for (var day = first; day < today; day = day.AddDays(1))
            {
                if (week[Arbeitszeit.Wochentag(day)] == 0) continue;
                int offset = (day.DayNumber % 5 - 2) * 10;
                db.Execute("insert into entries(owner,day,fromMin,toMin,pause,projectId,note) values (@index,@day,480,@end,45,@project,@note)",
                    new { index, day = day.Iso(), end = 480 + week[Arbeitszeit.Wochentag(day)] + 45 + offset, project = 1 + (day.DayNumber + index) % 3, note = "Demobuchung" });
            }
        }
        db.Execute("insert into project_leads(project,person) values (1,3),(2,6),(3,6)");
    }
}
