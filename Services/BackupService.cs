using System.IO;

namespace eCheque.MICO360.Services
{
    public static class BackupService
    {
        public static string CreateBackup()
        {
            var dest=DatabaseService.GetSetting("BackupPath",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"eCheque MICO360","Backups"));
            Directory.CreateDirectory(dest);
            // Back up the CURRENTLY OPEN company database, not a hard-coded file name.
            var src=DatabaseService.DbPath;
            if(string.IsNullOrEmpty(src)||!File.Exists(src))
                throw new FileNotFoundException("No active database to back up.");
            var companyTag = Path.GetFileNameWithoutExtension(src);
            var file=Path.Combine(dest,$"eCheque_Backup_{companyTag}_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            File.Copy(src,file,true);
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"system","Backup Created",file);
            return file;
        }
        public static void RestoreBackup(string backupFile)
        {
            if(!File.Exists(backupFile)) throw new FileNotFoundException("Backup file not found.", backupFile);
            // Restore into the currently active company database.
            var dest=DatabaseService.DbPath;
            if(string.IsNullOrEmpty(dest)) throw new InvalidOperationException("No active database to restore into.");
            // Safety copy of the current DB before overwriting, so a bad restore can be undone.
            try { File.Copy(dest, dest + ".prerestore", true); } catch { }
            File.Copy(backupFile,dest,true);
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"system","Backup Restored",backupFile);
        }
        public static List<string> GetBackups()
        {
            var path=DatabaseService.GetSetting("BackupPath",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"eCheque MICO360","Backups"));
            return Directory.Exists(path)?Directory.GetFiles(path,"*.db").OrderByDescending(f=>f).ToList():new();
        }
    }
}
