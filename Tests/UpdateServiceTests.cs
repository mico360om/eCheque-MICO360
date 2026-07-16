using eCheque.MICO360.Services;
using Xunit;

namespace eCheque.MICO360.Tests
{
    public class UpdateServiceTests
    {
        [Theory]
        [InlineData("1.0.1", "1.0.0", true)]
        [InlineData("1.1.0", "1.0.9", true)]
        [InlineData("2.0.0", "1.9.9", true)]
        [InlineData("v1.2.0", "1.1.0", true)]   // tolerates leading 'v'
        [InlineData("1.0", "1.0.0", false)]     // 1.0 normalizes to 1.0.0
        [InlineData("1.0.0", "1.0.0", false)]
        [InlineData("1.0.0", "1.0.1", false)]   // older is not newer
        public void IsNewer_compares_versions(string latest, string current, bool expected)
            => Assert.Equal(expected, UpdateService.IsNewer(latest, current));

        [Fact]
        public void IsNewer_handles_short_tags()
            => Assert.True(UpdateService.IsNewer("2", "1.9.9"));

        // ---- asset selection: the release carries TWO installers (client + server). The updater must
        // download the CLIENT one and verify it against ITS OWN .sha256 — regression for the
        // "update file is corrupted" bug where the server hash was paired with the client exe. ----

        static readonly (string Name, string Url, long Size) ClientExe = ("eCheque-MICO360-Setup-1.2.0.exe", "u/client.exe", 48_000_000);
        static readonly (string Name, string Url, long Size) ClientSha = ("eCheque-MICO360-Setup-1.2.0.exe.sha256", "u/client.sha", 97);
        static readonly (string Name, string Url, long Size) ServerExe = ("eCheque-MICO360-Server-Setup-1.2.0.exe", "u/server.exe", 33_000_000);
        static readonly (string Name, string Url, long Size) ServerSha = ("eCheque-MICO360-Server-Setup-1.2.0.exe.sha256", "u/server.sha", 104);

        [Fact]
        public void Selects_client_installer_and_its_own_sha_in_upload_order()
        {
            var info = new Models.UpdateInfo();
            var shaUrl = UpdateService.SelectClientPackage(info, new[] { ClientExe, ClientSha, ServerExe, ServerSha });
            Assert.Equal("u/client.exe", info.DownloadUrl);
            Assert.Equal(ClientExe.Name, info.AssetName);
            Assert.Equal("u/client.sha", shaUrl); // NOT the server's hash, regardless of position
        }

        [Fact]
        public void Selects_client_installer_even_when_server_sorts_first()
        {
            // Alphabetical order puts "…Server-Setup…" before "…Setup…".
            var info = new Models.UpdateInfo();
            var shaUrl = UpdateService.SelectClientPackage(info, new[] { ServerExe, ServerSha, ClientExe, ClientSha });
            Assert.Equal("u/client.exe", info.DownloadUrl);
            Assert.Equal("u/client.sha", shaUrl);
        }

        [Fact]
        public void Missing_sha_asset_returns_empty_but_still_picks_client()
        {
            var info = new Models.UpdateInfo();
            var shaUrl = UpdateService.SelectClientPackage(info, new[] { ServerExe, ClientExe });
            Assert.Equal("u/client.exe", info.DownloadUrl);
            Assert.Equal("", shaUrl);
        }

        [Fact]
        public void No_client_asset_leaves_info_empty()
        {
            var info = new Models.UpdateInfo();
            var shaUrl = UpdateService.SelectClientPackage(info, new[] { ServerExe, ServerSha });
            Assert.Equal("", info.DownloadUrl ?? "");
            Assert.Equal("", shaUrl);
        }
    }

    public class ChequeStatusGuardTests
    {
        [Theory]
        [InlineData("Printed", true)]
        [InlineData("Reprinted", true)]
        [InlineData("Presented", true)]
        [InlineData("Cleared", true)]
        [InlineData("Bounced", true)]
        [InlineData("Cancelled", true)]
        [InlineData("Void", true)]
        [InlineData("Draft", false)]
        [InlineData("ReadyToPrint", false)]
        public void IsLocked_flags_issued_or_closed_cheques(string status, bool expected)
            => Assert.Equal(expected, ChequeService.IsLocked(status));

        [Theory]
        [InlineData("Cancelled", true)]
        [InlineData("Void", true)]
        [InlineData("Cleared", true)]
        [InlineData("Bounced", true)]
        [InlineData("Printed", false)]
        [InlineData("Reprinted", false)]
        [InlineData("Presented", false)]
        [InlineData("Draft", false)]
        public void IsPrintBlocked_blocks_closed_and_settled(string status, bool expected)
            => Assert.Equal(expected, ChequeService.IsPrintBlocked(status));
    }
}
