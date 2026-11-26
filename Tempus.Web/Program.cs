using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Tempus.Core;
using Tempus.Web;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot")) ? AppContext.BaseDirectory : Directory.GetCurrentDirectory()
});
var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var defaultData = Path.Combine(local, "TempusM450");
var legacyData = Path.Combine(local, "SaldoM450");
if (!File.Exists(Path.Combine(defaultData, "tempus.sqlite3")) && File.Exists(Path.Combine(legacyData, "saldo.sqlite3")))
    defaultData = legacyData;
var data = Path.GetFullPath(builder.Configuration["DataDir"] ?? defaultData);
int port = builder.Configuration.GetValue<int?>("Port") ?? 0;
var share = Netzwerkzugang.Lesen(builder.Configuration);
builder.WebHost.ConfigureKestrel(k =>
{
    k.Listen(IPAddress.Loopback, port);
    if (share != null)
        k.Listen(share.Adresse, share.Url.Port, endpoint => endpoint.UseHttps(share.Zertifikat, share.Passwort));
    k.Limits.MaxRequestBodySize = 64 * 1024;
});
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var database = new Datenbank(data);
database.Init();
var app = builder.Build();
var sessions = new ConcurrentDictionary<string, Session>();
var attempts = new ConcurrentDictionary<string, (DateTime Since, int Count)>();
using var schreibzugriff = new SemaphoreSlim(1);
string cookie = "TempusSession";
app.Use(async (ctx, next) =>
{
    bool hostErlaubt = ctx.Request.IsHttps
        ? share != null && string.Equals(ctx.Request.Host.Host, share.Url.Host, StringComparison.OrdinalIgnoreCase)
        : ctx.Request.Host.Host == "127.0.0.1";
    if (!hostErlaubt) { ctx.Response.StatusCode = 400; return; }
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    if (!ctx.Request.Path.StartsWithSegments("/api")) { await next(); return; }
    ctx.Response.Headers.CacheControl = "no-store";
    bool schreibt = false;
    try
    {
        bool write = !HttpMethods.IsGet(ctx.Request.Method);
        if (write && (ctx.Request.Headers["X-Tempus"] != "1" || ctx.Request.Headers.TryGetValue("Origin", out var origin) && origin != $"{ctx.Request.Scheme}://{ctx.Request.Host}"))
            throw new Zugriff();
        if (ctx.Request.Path == "/api/login") { await next(); return; }
        if (!ctx.Request.Cookies.TryGetValue(cookie, out var token) || !sessions.TryGetValue(token, out var session) || session.Expires <= DateTime.UtcNow)
        { ctx.Response.StatusCode = 401; return; }
        if (write)
        {
            await schreibzugriff.WaitAsync(ctx.RequestAborted);
            schreibt = true;
        }
        using var db = database.Open(); using var tx = db.BeginTransaction(deferred: !write);
        var actor = db.QuerySingleOrDefault<Person>("select id,name,role,login,start,opening from people where id=@Id and version=@Version", session);
        if (actor == null) { sessions.TryRemove(token, out _); ctx.Response.StatusCode = 401; return; }
        ctx.Items["actor"] = actor; ctx.Items["journal"] = new Journal(db, actor);
        // Commit before the response leaves the server, even if the client disconnects early.
        ctx.Items["commit"] = (Action)(() => tx.Commit());
        await next();
    }
    catch (Exception ex) when (ex is Fachfehler or Zugriff or BadHttpRequestException or SqliteException)
    {
        if (ctx.Response.HasStarted) throw;
        if (ex is SqliteException) app.Logger.LogError(ex, "SQLite-Operation fehlgeschlagen");
        ctx.Response.StatusCode = ex switch { Zugriff => 403, SqliteException => 503, _ => 400 };
        await ctx.Response.WriteAsJsonAsync(new { error = ex switch { Fachfehler => ex.Message, Zugriff => "Für diese Aktion fehlt die Berechtigung.", SqliteException => "Datenbank momentan nicht verfügbar. Erneut versuchen.", _ => "Eingabe konnte nicht gelesen werden." } });
    }
    finally { if (schreibt) schreibzugriff.Release(); }
});
app.UseDefaultFiles(); app.UseStaticFiles();
Journal J(HttpContext c) => (Journal)c.Items["journal"]!;
IResult Done(HttpContext c) { ((Action)c.Items["commit"]!)(); return Results.NoContent(); }
app.MapPost("/api/login", (Anmeldung x, HttpContext c) =>
{
    var login = Journal.Text(x.Login, 40, true).ToLowerInvariant();
    if (x.Password == null || x.Password.Length > 128) return Results.Unauthorized();
    if (c.Request.IsHttps && x.Password is ("Tempus-Demo26!" or "Saldo-Demo26!"))
        return Results.Json(new { error = "Für den Gerätezugriff das Demopasswort zuerst lokal durch ein persönliches Passwort ersetzen." }, statusCode: 403);
    var now = DateTime.UtcNow;
    if (attempts.TryGetValue(login, out var f) && f.Count >= 10 && now - f.Since < TimeSpan.FromMinutes(2))
        return Results.Json(new { error = "Zu viele Anmeldeversuche. In zwei Minuten erneut versuchen." }, statusCode: 429);
    using var db = database.Open();
    var row = db.QuerySingleOrDefault<LoginRecord>("select id,hash,version from people where login=@login", new { login });
    if (row == null || !Datenbank.Verify(x.Password, row.Hash))
    {
        attempts.AddOrUpdate(login, (now, 1), (_, old) => now - old.Since > TimeSpan.FromMinutes(2) ? (now, 1) : (old.Since, old.Count + 1));
        return Results.Json(new { error = "Login oder Passwort stimmt nicht." }, statusCode: 401);
    }
    attempts.TryRemove(login, out _);
    foreach (var item in sessions.Where(p => p.Value.Expires <= now)) sessions.TryRemove(item.Key, out _);
    string key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    sessions[key] = new(row.Id, row.Version, now.AddHours(12));
    c.Response.Cookies.Append(cookie, key, new CookieOptions { HttpOnly = true, Secure = c.Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/", MaxAge = TimeSpan.FromHours(12) });
    return Results.NoContent();
});
app.MapPost("/api/logout", (HttpContext c) => { sessions.TryRemove(c.Request.Cookies[cookie]!, out _); c.Response.Cookies.Delete(cookie); return Done(c); });
app.MapGet("/api/state", (HttpContext c, int? person, string? month) =>
{
    var actor = (Person)c.Items["actor"]!;
    return J(c).State(person ?? actor.Id, month ?? DateTime.Today.ToString("yyyy-MM"));
});
app.MapPost("/api/entries", (HttpContext c, Buchung x) => { J(c).Save(0, x); return Done(c); });
app.MapPut("/api/entries/{id:int:min(1)}", (HttpContext c, int id, Buchung x) => { J(c).Save(id, x); return Done(c); });
app.MapDelete("/api/entries/{id:int:min(1)}", (HttpContext c, int id) => { J(c).Delete(id); return Done(c); });
app.MapPost("/api/timer/start", (HttpContext c, UhrStart x) => { J(c).Start(x); return Done(c); });
app.MapPost("/api/timer/stop", (HttpContext c, UhrStopp x) => { J(c).Stop(x.Pause); return Done(c); });
app.MapDelete("/api/timer", (HttpContext c) => { J(c).CancelTimer(); return Done(c); });
app.MapPost("/api/absences", (HttpContext c, Antrag x) => { J(c).Request(x); return Done(c); });
app.MapPut("/api/absences/{id:int}", (HttpContext c, int id, Entscheid x) => { J(c).Decide(id, x.Status); return Done(c); });
app.MapPost("/api/people", (HttpContext c, Konto x) => { J(c).CreatePerson(x); return Done(c); });
app.MapPost("/api/people/{id:int}/plan", (HttpContext c, int id, Pensum x) => { J(c).Schedule(id, x); return Done(c); });
app.MapPost("/api/people/{id:int}/close", (HttpContext c, int id, Abschluss x) => { J(c).Close(id, x); return Done(c); });
app.MapPost("/api/projects", (HttpContext c, Projekt x) => { J(c).AddProject(x); return Done(c); });
app.MapGet("/api/project-report", (HttpContext c, string month) => J(c).Projektbericht(month));
app.MapPut("/api/projects/{id:int}/lead", (HttpContext c, int id, Leitungswechsel x) => { J(c).SetLead(id, x.PersonId); return Done(c); });
app.MapPost("/api/password", (HttpContext c, Passwort x) => { J(c).ChangePassword(x); return Done(c); });
app.MapGet("/api/hr/hours", (HttpContext c, string month) => J(c).Stundenbericht(month));
app.MapGet("/api/hr/slip/{id:int}", (HttpContext c, int id, string month) => Results.Json(new { slip = J(c).ReadSlip(id, month) }));
app.MapPut("/api/hr/slip/{id:int}", (HttpContext c, int id, string month, LohnEingabe x) => { J(c).SaveSlip(id, month, x); return Done(c); });
app.MapPost("/api/hr/slip/{id:int}/reopen", (HttpContext c, int id, string month) => { J(c).ReopenSlip(id, month); return Done(c); });
app.Lifetime.ApplicationStarted.Register(() =>
{
    var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single(u => u.StartsWith("http://", StringComparison.Ordinal));
    Console.WriteLine($"Tempus · Lokale Zeiterfassung\n{url}\nDatenordner: {data}\nZum Beenden dieses Fenster schliessen oder Strg+C drücken.");
    if (share != null) Console.WriteLine($"Gemeinsamer Gerätezugriff: {share.Url.GetLeftPart(UriPartial.Authority)}");
    if (!builder.Configuration.GetValue<bool>("NoBrowser"))
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { Console.WriteLine("Bitte die Adresse oben im Browser öffnen."); }
});
app.Run();
record Session(int Id, int Version, DateTime Expires);
