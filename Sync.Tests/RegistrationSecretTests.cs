using eCheque.MICO360.Sync.Client;
using eCheque.MICO360.Sync.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace eCheque.MICO360.Sync.Tests
{
    /// <summary>
    /// When the server is configured with an enrollment secret (ECHEQUE_REGISTER_SECRET), /api/register must
    /// reject clients that don't present the exact secret — so reaching the endpoint is not enough to obtain a
    /// device token. Serial with the other suite: Program reads server config from process env vars.
    /// </summary>
    [Collection("sync-serial")]
    public class RegistrationSecretTests : IDisposable
    {
        const string Secret = "correct horse battery staple";
        readonly string _db;
        readonly WebApplicationFactory<Program> _factory;
        readonly HttpClient _http;

        public RegistrationSecretTests()
        {
            _db = Path.Combine(Path.GetTempPath(), "echeque_regsec_" + Guid.NewGuid().ToString("N") + ".db");
            Environment.SetEnvironmentVariable("ECHEQUE_SERVER_DB", _db);
            Environment.SetEnvironmentVariable("ECHEQUE_REGISTER_SECRET", Secret);
            _factory = new WebApplicationFactory<Program>();
            _http = _factory.CreateClient();
        }

        RegisterRequest Req(string? secret) => new() { DeviceName = "PC", MachineId = "MID", EnrollSecret = secret };

        [Fact]
        public async Task Registration_is_rejected_without_the_secret()
            => Assert.Null(await SyncClient.RegisterAsync(_http, "", Req(null)));

        [Fact]
        public async Task Registration_is_rejected_with_a_wrong_secret()
            => Assert.Null(await SyncClient.RegisterAsync(_http, "", Req("wrong secret")));

        [Fact]
        public async Task Registration_succeeds_with_the_correct_secret()
        {
            var resp = await SyncClient.RegisterAsync(_http, "", Req(Secret));
            Assert.NotNull(resp);
            Assert.False(string.IsNullOrEmpty(resp!.Token));
        }

        public void Dispose()
        {
            _http.Dispose();
            _factory.Dispose();
            // Critical: clear the secret so the other (open-registration) tests in this serial collection pass.
            Environment.SetEnvironmentVariable("ECHEQUE_REGISTER_SECRET", null);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { if (File.Exists(_db)) File.Delete(_db); } catch { }
        }
    }
}
