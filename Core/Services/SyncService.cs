using System.Net.Http;
using eCheque.MICO360.Sync.Client;
using eCheque.MICO360.Sync.Contracts;

namespace eCheque.MICO360.Core.Services
{
    /// <summary>
    /// App-level cloud sync (macOS/Core copy — mirrors the Windows Services.SyncService). Registers this Mac
    /// with the server, then syncs the master tier and active company via the shared <see cref="SyncClient"/>.
    /// Config lives in master settings; offline-tolerant; a failed cycle retries on the next tick.
    /// </summary>
    public static class SyncService
    {
        static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
        static CancellationTokenSource? _loop;
        static volatile bool _reachable;      // was the server reachable on the last check/sync?
        static DateTime? _lastSyncUtc;        // last SUCCESSFUL full sync (UTC)

        /// <summary>Current connection state for the UI indicator.</summary>
        public static SyncConnState ConnectionStatus =>
            !Enabled        ? SyncConnState.LocalOnly :
            !IsRegistered   ? SyncConnState.NotConnected :
            _reachable      ? SyncConnState.Connected :
                              SyncConnState.Disconnected;

        /// <summary>Local time of the last successful sync, or null if none this session.</summary>
        public static DateTime? LastSyncLocal => _lastSyncUtc?.ToLocalTime();

        public static bool   Enabled   { get => CompanyService.GetMasterSetting("Sync_Enabled", "0") == "1"; set => CompanyService.SetMasterSetting("Sync_Enabled", value ? "1" : "0"); }
        public static string ServerUrl { get => CompanyService.GetMasterSetting("Sync_ServerUrl", "");       set => CompanyService.SetMasterSetting("Sync_ServerUrl", (value ?? "").Trim()); }
        public static string LastResult{ get => CompanyService.GetMasterSetting("Sync_LastResult", "");      private set => CompanyService.SetMasterSetting("Sync_LastResult", value); }
        static string Token     { get => CompanyService.GetMasterSetting("Sync_Token", "");    set => CompanyService.SetMasterSetting("Sync_Token", value); }
        static string DeviceId  { get => CompanyService.GetMasterSetting("Sync_DeviceId", "");  set => CompanyService.SetMasterSetting("Sync_DeviceId", value); }
        public static bool IsRegistered => !string.IsNullOrEmpty(Token);

        public static async Task<string> RegisterAsync(string url, string orgKey)
        {
            url = (url ?? "").Trim();
            if (string.IsNullOrWhiteSpace(url)) return "Enter the server URL first.";
            var resp = await SyncClient.RegisterAsync(Http, url,
                new RegisterRequest { OrgKey = orgKey?.Trim() ?? "", DeviceName = Environment.MachineName, MachineId = MachineId() });
            if (resp == null) return "Could not connect / register — check the server URL and organisation key.";
            ServerUrl = url; Token = resp.Token; DeviceId = resp.DeviceId;
            return "Connected. This Mac is registered with the sync server.";
        }

        public static async Task<SyncReport> SyncOnceAsync()
        {
            var agg = new SyncReport();
            if (!Enabled || !IsRegistered) { agg.Ok = false; agg.Error = "Sync is off or this device isn't registered."; return agg; }
            try
            {
                var client = new SyncClient(Http, ServerUrl, Token);
                using (var master = CompanyService.GetMasterConnection())
                    Merge(agg, await client.SyncScopeAsync(master, SyncEntities.MasterCompanyId, SyncRegistry.Master));
                if (CompanyService.CurrentCompanyId > 0)
                    using (var company = DatabaseService.GetConnection())
                        Merge(agg, await client.SyncScopeAsync(company, CompanyService.CurrentCompanyId, SyncRegistry.Company));
                LastResult = $"{DateTime.Now:HH:mm} — {agg}";
                CompanyService.SetMasterSetting("Sync_LastRunUtc", DateTime.UtcNow.ToString("o"));
                _reachable = agg.Ok;
                if (agg.Ok) _lastSyncUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                agg.Ok = false; agg.Error = ex.Message; _reachable = false;
                LastResult = $"{DateTime.Now:HH:mm} — error: {ex.Message}";
                BugReportService.Report(ex, "Sync");
            }
            return agg;
        }

        /// <summary>Lightweight reachability probe (GET /api/health) used between full syncs to keep the
        /// connection indicator fresh without moving data. Updates <see cref="ConnectionStatus"/>.</summary>
        public static async Task<bool> CheckConnectionAsync()
        {
            if (!Enabled || !IsRegistered || string.IsNullOrWhiteSpace(ServerUrl)) { _reachable = false; return false; }
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                using var resp = await Http.GetAsync(ServerUrl.TrimEnd('/') + "/api/health", HttpCompletionOption.ResponseHeadersRead, cts.Token);
                _reachable = resp.IsSuccessStatusCode;
            }
            catch { _reachable = false; }
            return _reachable;
        }

        static void Merge(SyncReport a, SyncReport b)
        {
            a.Pulled += b.Pulled; a.Applied += b.Applied; a.Pushed += b.Pushed; a.Conflicts += b.Conflicts;
            if (!b.Ok) { a.Ok = false; a.Error = b.Error; }
        }

        public static void StartBackground(TimeSpan? interval = null)
        {
            _loop?.Cancel();
            _loop = new CancellationTokenSource();
            var ct = _loop.Token;
            _ = Task.Run(async () =>
            {
                int tick = 0;
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        if (Enabled && IsRegistered)
                        {
                            if (tick % 3 == 0) await SyncOnceAsync();      // full sync every ~60s
                            else await CheckConnectionAsync();             // reachability probe on the other ticks
                        }
                    }
                    catch { }
                    tick++;
                    try { await Task.Delay(TimeSpan.FromSeconds(20), ct); } catch { break; }
                }
            }, ct);
        }

        public static void StopBackground() => _loop?.Cancel();

        static string MachineId()
        {
            var id = CompanyService.GetMasterSetting("Sync_MachineId", "");
            if (string.IsNullOrEmpty(id)) { id = Guid.NewGuid().ToString("N"); CompanyService.SetMasterSetting("Sync_MachineId", id); }
            return id;
        }
    }
}
