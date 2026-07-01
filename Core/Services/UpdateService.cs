using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using eCheque.MICO360.Core.Models;

namespace eCheque.MICO360.Core.Services
{
    /// <summary>
    /// Cross-platform self-update against GitHub Releases (shared by Windows and macOS).
    /// Check → compare → download → verify SHA-256 → apply on restart. All steps logged.
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

        public static string LogPath => Path.Combine(AppPaths.DataFolder, "update.log");

        public static void Log(string message)
        {
            try { Directory.CreateDirectory(AppPaths.DataFolder); File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}{Environment.NewLine}"); }
            catch { }
        }

        /// <summary>Queries GitHub for the latest release. Throws HttpRequestException on network errors.</summary>
        public static async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            var info = new UpdateInfo { CurrentVersion = AppInfo.Version };
            Log($"Checking for updates. Current {info.CurrentVersion}. Source {AppInfo.ReleasesApiUrl}");

            using var resp = await Http.GetAsync(AppInfo.ReleasesApiUrl);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Log("No releases published yet (404).");
                info.LatestVersion = info.CurrentVersion; info.UpdateAvailable = false; return info;
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

            // Prefer a macOS asset (.dmg/.zip with 'mac'); fall back to the first .zip.
            string shaFromAsset = "";
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    var url  = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                    var size = a.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                    bool isMac = name.Contains("mac", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase);
                    if ((name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase))
                        && (string.IsNullOrEmpty(info.DownloadUrl) || isMac))
                    { info.DownloadUrl = url; info.AssetName = name; info.SizeBytes = size; }
                    else if (name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(url))
                    { try { shaFromAsset = (await Http.GetStringAsync(url)).Trim().Split(' ')[0]; } catch { } }
                }
            }
            if (string.IsNullOrEmpty(shaFromAsset))
                foreach (var line in info.Changelog.Split('\n'))
                { var l = line.Trim(); if (l.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase)) { shaFromAsset = l[7..].Trim(); break; } }
            info.Sha256 = shaFromAsset;

            info.UpdateAvailable = !string.IsNullOrEmpty(info.LatestVersion) && IsNewer(info.LatestVersion, info.CurrentVersion);
            Log($"Latest {info.LatestVersion}. Available: {info.UpdateAvailable}. Mandatory: {info.Mandatory}.");
            return info;
        }

        public static bool IsNewer(string latest, string current)
        {
            if (Version.TryParse(Normalize(latest), out var l) && Version.TryParse(Normalize(current), out var c)) return l > c;
            return !string.IsNullOrEmpty(latest) && !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
        }

        static string Normalize(string v)
        {
            v = (v ?? "").Trim().TrimStart('v', 'V');
            var parts = v.Split('.');
            return parts.Length switch { 1 => v + ".0.0", 2 => v + ".0", _ => v };
        }

        public static string ComputeSha256(string file)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(file);
            return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
        }

        public static bool VerifyChecksum(string file, string expectedSha)
        {
            if (string.IsNullOrWhiteSpace(expectedSha)) { Log("No checksum published — skipping verification."); return true; }
            var actual = ComputeSha256(file);
            var ok = string.Equals(actual, expectedSha.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
            Log($"Checksum verify ok={ok}");
            return ok;
        }
    }
}
