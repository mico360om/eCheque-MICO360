using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;

namespace eCheque.MICO360.Services
{
    /// <summary>
    /// Self-update against GitHub Releases. Flow:
    ///   Check → compare versions → download package → verify SHA-256 → apply on restart → rollback on failure.
    /// All steps are logged to update.log. User data (database, PDFs, settings) lives outside the app
    /// folder (LocalAppData / MyDocuments) and is never touched by the update.
    /// </summary>
    public static class UpdateService
    {
        static readonly HttpClient Http = CreateClient();

        static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd("eCheque-MICO360-Updater");
            c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return c;
        }

        public static string LogPath
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eCheque_MICO360");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "update.log");
            }
        }

        public static void Log(string message)
        {
            try { File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}{Environment.NewLine}"); }
            catch { }
        }

        /// <summary>Queries GitHub for the latest release. Throws HttpRequestException on no-internet/network errors.</summary>
        public static async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            var info = new UpdateInfo { CurrentVersion = AppInfo.Version };
            Log($"Checking for updates. Current version {info.CurrentVersion}. Source {AppInfo.ReleasesApiUrl}");

            using var resp = await Http.GetAsync(AppInfo.ReleasesApiUrl);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Log("No releases published yet (404).");
                info.LatestVersion = info.CurrentVersion;
                info.UpdateAvailable = false;
                return info;
            }
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag  = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            info.LatestVersion = tag.TrimStart('v', 'V');
            info.Changelog = string.IsNullOrWhiteSpace(body) ? "(No release notes provided.)" : body.Trim();
            info.Mandatory = body.Contains("[mandatory]", StringComparison.OrdinalIgnoreCase)
                          || body.Contains("mandatory: true", StringComparison.OrdinalIgnoreCase);

            // Pick the CLIENT installer and ITS OWN .sha256 companion. Releases carry two installers
            // (client + server); the selection must be order-independent and pair by exact name, or the
            // client download gets verified against the server's hash and reports "corrupted".
            var list = new List<(string Name, string Url, long Size)>();
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                foreach (var a in assets.EnumerateArray())
                    list.Add((a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                              a.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "",
                              a.TryGetProperty("size", out var s) ? s.GetInt64() : 0));

            var shaUrl = SelectClientPackage(info, list);
            string shaFromAsset = "";
            if (!string.IsNullOrEmpty(shaUrl))
                try { shaFromAsset = (await Http.GetStringAsync(shaUrl)).Trim().Split(' ')[0]; } catch { }

            // Fallback: a "SHA256: <hash>" line in the release notes.
            if (string.IsNullOrEmpty(shaFromAsset))
            {
                foreach (var line in info.Changelog.Split('\n'))
                {
                    var l = line.Trim();
                    if (l.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
                    { shaFromAsset = l.Substring(7).Trim(); break; }
                }
            }
            info.Sha256 = shaFromAsset;

            info.UpdateAvailable = !string.IsNullOrEmpty(info.DownloadUrl) && IsNewer(info.LatestVersion, info.CurrentVersion);
            Log($"Latest version {info.LatestVersion}. Update available: {info.UpdateAvailable}. Mandatory: {info.Mandatory}. Asset: {info.AssetName}");
            return info;
        }

        /// <summary>
        /// Chooses the desktop-app installer from a release's assets and returns the URL of ITS matching
        /// .sha256 companion ("" if none). The client asset is a Setup*.exe that is NOT the server installer;
        /// the checksum is paired by exact file name ("&lt;asset&gt;.sha256"), never by position, so asset order
        /// on the release can never mismatch the hash. Pure and testable.
        /// </summary>
        public static string SelectClientPackage(UpdateInfo info, IReadOnlyList<(string Name, string Url, long Size)> assets)
        {
            foreach (var a in assets)
            {
                if (!a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (!a.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase)) continue;
                if (a.Name.Contains("Server", StringComparison.OrdinalIgnoreCase)) continue; // server installer — not an app update
                info.DownloadUrl = a.Url; info.AssetName = a.Name; info.SizeBytes = a.Size;
                foreach (var s in assets)
                    if (string.Equals(s.Name, a.Name + ".sha256", StringComparison.OrdinalIgnoreCase))
                        return s.Url;
                return "";
            }
            return "";
        }

        /// <summary>True if <paramref name="latest"/> is a strictly higher version than <paramref name="current"/>.</summary>
        public static bool IsNewer(string latest, string current)
        {
            if (Version.TryParse(Normalize(latest), out var l) && Version.TryParse(Normalize(current), out var c))
                return l > c;
            // Fall back to string comparison if the tags aren't dotted versions.
            return !string.IsNullOrEmpty(latest) && !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
        }

        static string Normalize(string v)
        {
            v = (v ?? "").Trim().TrimStart('v', 'V');
            var parts = v.Split('.');
            return parts.Length switch { 1 => v + ".0.0", 2 => v + ".0", _ => v };
        }

        /// <summary>Downloads the package to a temp file, reporting 0..1 progress. Returns the file path.</summary>
        public static async Task<string> DownloadAsync(UpdateInfo info, IProgress<double> progress, CancellationToken ct)
        {
            CheckDiskSpace(info.SizeBytes);
            var dir = Path.Combine(Path.GetTempPath(), "eCheque_Update");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, string.IsNullOrEmpty(info.AssetName) ? "update.zip" : info.AssetName);

            Log($"Downloading {info.DownloadUrl} -> {file}");
            using var resp = await Http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? info.SizeBytes;

            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true);
            var buffer = new byte[1 << 20];   // 1 MB buffer — far fewer round-trips than 80 KB
            long read = 0; int n; double lastReported = -1;
            while ((n = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                read += n;
                if (total > 0)
                {
                    // Only push progress on ~1% change so the UI thread isn't flooded (which itself slows the download).
                    var pct = Math.Min(1.0, (double)read / total);
                    if (pct - lastReported >= 0.01 || pct >= 1.0) { lastReported = pct; progress.Report(pct); }
                }
            }
            Log($"Download complete: {read} bytes.");
            return file;
        }

        static void CheckDiskSpace(long needed)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetTempPath()) ?? "C:\\");
                // Require the package size plus headroom for backup + extraction.
                var required = Math.Max(needed, 1) * 3 + 50L * 1024 * 1024;
                if (drive.AvailableFreeSpace < required)
                    throw new IOException($"Not enough disk space. Need ~{required / 1024 / 1024} MB free.");
            }
            catch (IOException) { throw; }
            catch { /* if we can't determine space, proceed */ }
        }

        public static string ComputeSha256(string file)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(file);
            return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
        }

        /// <summary>Verifies the downloaded file against the expected hash. Returns true if no hash was published (cannot verify).</summary>
        public static bool VerifyChecksum(string file, string expectedSha)
        {
            if (string.IsNullOrWhiteSpace(expectedSha))
            {
                Log("No checksum published — skipping verification.");
                return true;
            }
            var actual = ComputeSha256(file);
            var ok = string.Equals(actual, expectedSha.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
            Log($"Checksum verify: expected {expectedSha}, actual {actual}, ok={ok}");
            return ok;
        }

        /// <summary>
        /// Launches the downloaded installer to apply the update, then signals the caller to shut down
        /// so the installer can replace files. The installer (Inno Setup) closes any running instance,
        /// installs over the existing version, and relaunches the app when finished.
        /// </summary>
        public static void ApplyAndRestart(string installerPath)
        {
            Log($"Launching installer {installerPath}");
            // UseShellExecute lets Windows elevate (the installer requests admin) via the normal UAC prompt.
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true
            });
        }
    }
}
