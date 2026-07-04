using eCheque.MICO360.Server;
using eCheque.MICO360.Sync.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Run as a Windows Service when installed as one (auto-detected; no effect when run from a console).
builder.Host.UseWindowsService();

// --- configuration ---------------------------------------------------------
// Read config from echeque.server.json NEXT TO THE EXE (written by the installer), plus env vars / args.
// A file is used because a freshly-created Windows Service does not reliably see newly-set machine env vars.
builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "echeque.server.json"), optional: true, reloadOnChange: false);

// Org key + DB path come from config (file > env > default). "Urls" in the file / ASPNETCORE_URLS is auto-bound.
string? orgKey = builder.Configuration["ECHEQUE_ORG_KEY"];
string dbPath = builder.Configuration["ECHEQUE_SERVER_DB"]
                ?? Path.Combine(AppContext.BaseDirectory, "data", "server.db");

// Bind URLs from config ("Urls") / ASPNETCORE_URLS if set; otherwise a sensible LAN default. Put TLS in front
// for production — the client speaks plain HTTP or HTTPS depending on the URL it is given.
if (string.IsNullOrEmpty(builder.Configuration["urls"]) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    builder.WebHost.UseUrls("http://0.0.0.0:5210");

// Cap request bodies so a malformed/hostile client cannot exhaust memory. The client chunks pushes to 500
// rows, which is far under this limit.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 32 * 1024 * 1024); // 32 MB

var store = new SqliteServerStore(dbPath);
store.Initialize(orgKey);
builder.Services.AddSingleton<IServerStore>(store);

string appVersion = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

var app = builder.Build();

// --- helpers ---------------------------------------------------------------
static string? BearerToken(HttpRequest req)
{
    var h = req.Headers.Authorization.ToString();
    return h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? h["Bearer ".Length..].Trim() : null;
}

// --- endpoints -------------------------------------------------------------

// Human-readable status page (no secrets).
app.MapGet("/", (IServerStore s) => Results.Content(
    $"<html><body style='font-family:Segoe UI,Arial;margin:40px'>" +
    $"<h2>eCheque MICO360 Sync Server</h2>" +
    $"<p>Status: <b style='color:#2E7D32'>running</b> &nbsp; Version {appVersion}</p>" +
    $"<p>Registered devices: {s.DeviceCount()} &nbsp; Synced rows: {s.RowCount()}</p>" +
    $"<p>Server time (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}</p>" +
    $"<p style='color:#888'>API base: <code>/api</code></p></body></html>", "text/html"));

app.MapGet("/api/health", () => Results.Ok(new HealthResponse
{
    Status = "ok",
    Version = appVersion,
    ServerTimeUtc = DateTime.UtcNow.ToString("o")
}));

// Device registration: exchange the shared org key for a per-device bearer token.
app.MapPost("/api/register", (RegisterRequest req, IServerStore s) =>
{
    if (string.IsNullOrWhiteSpace(req.OrgKey) || !FixedEquals(req.OrgKey.Trim(), s.OrgKey))
        return Results.Json(new { error = "Invalid organisation key." }, statusCode: StatusCodes.Status401Unauthorized);
    var (deviceId, token) = s.RegisterDevice(req.DeviceName, req.MachineId);
    return Results.Ok(new RegisterResponse { DeviceId = deviceId, Token = token });
});

// Pull: give me every change newer than the versions I hold.
app.MapPost("/api/sync/pull", (PullRequest req, HttpRequest http, IServerStore s) =>
{
    if (!s.ValidateToken(BearerToken(http), out var deviceId))
        return Results.Unauthorized();
    s.TouchDevice(deviceId);
    return Results.Ok(s.Pull(req));
});

// Push: apply my local changes, resolving conflicts last-write-wins.
app.MapPost("/api/sync/push", (PushRequest req, HttpRequest http, IServerStore s) =>
{
    if (!s.ValidateToken(BearerToken(http), out var deviceId))
        return Results.Unauthorized();
    s.TouchDevice(deviceId);
    return Results.Ok(s.Push(req));
});

// Startup banner so the admin can see the URL + org key to register clients with.
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine();
    Console.WriteLine("  eCheque MICO360 Sync Server " + appVersion);
    Console.WriteLine("  DB   : " + dbPath);
    Console.WriteLine("  Org key (register clients with this): " + store.OrgKey);
    Console.WriteLine("  Set ECHEQUE_ORG_KEY to pin your own key. Put TLS in front for production.");
    Console.WriteLine();
});

app.Run();

// Constant-time string compare so the org-key check does not leak length/timing.
static bool FixedEquals(string a, string b)
{
    var ba = System.Text.Encoding.UTF8.GetBytes(a);
    var bb = System.Text.Encoding.UTF8.GetBytes(b);
    return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(ba, bb);
}

public partial class Program { } // exposed for the integration test host
