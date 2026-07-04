using eCheque.MICO360.Sync.Contracts;

namespace eCheque.MICO360.Sync.Client
{
    /// <summary>Describes how one table maps onto the sync wire.</summary>
    public sealed class SyncEntityDef
    {
        public required string Name { get; init; }        // wire entity name (see SyncEntities)
        public required string Table { get; init; }       // local table name
        public bool Guid { get; init; } = true;           // GUID identity (SyncId) vs a natural key
        public string? NaturalKey { get; init; }          // e.g. "Name" for Payees, "Key" for settings
        public bool HasProfileFk { get; init; }           // carries ProfileSyncId (portable FK to ChequeProfiles)
        public HashSet<string>? Exclude { get; init; }    // columns that must NOT sync (kept per-PC)
    }

    /// <summary>The tables that sync, split by database tier. Master-tier tables live in companies.db
    /// (CompanyId 0); company-tier tables live in each company_N.db (CompanyId = N).</summary>
    public static class SyncRegistry
    {
        public static readonly SyncEntityDef[] Master =
        {
            new() { Name = SyncEntities.Company,       Table = "Companies" },
            // Users sync (so any PC can log in), but the volatile per-PC login state stays local.
            new() { Name = SyncEntities.User,          Table = "Users",
                    Exclude = new(System.StringComparer.OrdinalIgnoreCase) { "LastLogin", "FailedLoginAttempts", "LockoutUntil" } },
            new() { Name = SyncEntities.MasterSetting, Table = "MasterSettings", Guid = false, NaturalKey = "Key" },
        };

        public static readonly SyncEntityDef[] Company =
        {
            new() { Name = SyncEntities.ChequeProfile, Table = "ChequeProfiles" },
            new() { Name = SyncEntities.ChequeRecord,  Table = "ChequeRecords", HasProfileFk = true },
            new() { Name = SyncEntities.Bank,          Table = "Banks" },
            new() { Name = SyncEntities.Payee,         Table = "Payees",      Guid = false, NaturalKey = "Name" },
            new() { Name = SyncEntities.AppSetting,    Table = "AppSettings", Guid = false, NaturalKey = "Key" },
        };
    }

    /// <summary>Cloud-sync connection state shown in the client UI.</summary>
    public enum SyncConnState
    {
        LocalOnly,     // cloud sync is turned off — data stays on this PC
        NotConnected,  // sync on, but this PC hasn't been registered with a server yet
        Connected,     // registered and the server was reachable on the last check
        Disconnected   // registered but the server could not be reached
    }

    /// <summary>What one SyncScopeAsync pass did — surfaced to the UI and asserted in tests.</summary>
    public sealed class SyncReport
    {
        public int Pulled { get; set; }
        public int Applied { get; set; }
        public int Pushed { get; set; }
        public int Conflicts { get; set; }
        public bool Ok { get; set; } = true;
        public string? Error { get; set; }
        public override string ToString() =>
            Ok ? $"pulled {Pulled} (applied {Applied}), pushed {Pushed}, conflicts {Conflicts}"
               : $"failed: {Error}";
    }
}
