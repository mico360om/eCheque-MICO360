namespace eCheque.MICO360.Models
{
    /// <summary>Result of an update check against the GitHub releases API.</summary>
    public class UpdateInfo
    {
        public string CurrentVersion { get; set; } = "";
        public string LatestVersion  { get; set; } = "";
        public string Changelog      { get; set; } = "";
        public string DownloadUrl    { get; set; } = "";
        public string AssetName      { get; set; } = "";
        public long   SizeBytes      { get; set; }
        public string Sha256         { get; set; } = "";   // expected hash, empty if none published
        public bool   Mandatory      { get; set; }

        /// <summary>True when the latest release is newer than the installed version and has a downloadable package.</summary>
        public bool UpdateAvailable { get; set; }

        public string SizeDisplay =>
            SizeBytes <= 0 ? "—" :
            SizeBytes >= 1024 * 1024 ? $"{SizeBytes / 1024.0 / 1024.0:N1} MB" :
            $"{SizeBytes / 1024.0:N0} KB";
    }
}
