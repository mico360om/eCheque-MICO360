using eCheque.MICO360.Server;
using eCheque.MICO360.Sync.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Run as a Windows Service when installed as one (auto-detected; no effect when run from a console).
builder.Host.UseWindowsService();

// --- configuration ---------------------------------------------------------
// Read config from echeque.server.json NEXT TO THE EXE (written by the installer), plus env vars / args.
// A file is used because a freshly-created Windows Service does not reliably see newly-set machine env vars.
builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "echeque.server.json"), optional: true, reloadOnChange: false);

// DB path from config (file > env > default). "Urls" in the file / ASPNETCORE_URLS is auto-bound.
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
store.Initialize();
builder.Services.AddSingleton<IServerStore>(store);

string appVersion = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

// Optional enrollment secret: when set, /api/register requires a matching secret so that merely reaching the
// endpoint (e.g. through a firewall gap) is not enough to obtain a device token. Empty = open registration.
string registerSecret = (builder.Configuration["ECHEQUE_REGISTER_SECRET"] ?? "").Trim();

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

// Device registration: single-organisation server, so a PC just gets its own bearer token.
// Defence in depth: network controls (TLS + firewall allowlist) AND, when configured, an enrollment secret.
app.MapPost("/api/register", (RegisterRequest req, IServerStore s) =>
{
    if (registerSecret.Length > 0)
    {
        var provided = System.Text.Encoding.UTF8.GetBytes(req.EnrollSecret ?? "");
        var expected = System.Text.Encoding.UTF8.GetBytes(registerSecret);
        // Constant-time compare — never leak the secret's length/content through response timing.
        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(provided, expected))
            return Results.Unauthorized();
    }
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

// Startup banner.
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine();
    Console.WriteLine("  eCheque MICO360 Sync Server " + appVersion);
    Console.WriteLine("  DB   : " + dbPath);
    Console.WriteLine(registerSecret.Length > 0
        ? "  Enrollment: PROTECTED by a registration secret."
        : "  Enrollment: OPEN — anyone who can reach /api/register can enrol. Set ECHEQUE_REGISTER_SECRET to require a secret.");
    Console.WriteLine("  Single-organisation server — clients connect by URL only. Restrict access with TLS + firewall.");
    Console.WriteLine();
});

app.Run();

public partial class Program { } // exposed for the integration test host
