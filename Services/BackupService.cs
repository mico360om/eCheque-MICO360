using System.IO;

namespace eCheque.MICO360.Services
{
    public static class BackupService
    {
        public static string CreateBackup()
        {
            var dest=DatabaseService.GetSetting("BackupPath",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"eCheque MICO360","Backups"));
            Directory.CreateDirectory(dest);
            var src=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"eCheque_MICO360","echeque.db");
            var file=Path.Combine(dest,$"eCheque_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            File.Copy(src,file,true);
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"system","Backup Created",file);
            return file;
        }
        public static void RestoreBackup(string backupFile)
        {
            var dest=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"eCheque_MICO360","echeque.db");
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
