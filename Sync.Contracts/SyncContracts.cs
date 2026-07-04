namespace eCheque.MICO360.Sync.Contracts
{
    /// <summary>Canonical entity names on the sync wire. Keep in lock-step with the client mappers and server store.</summary>
    public static class SyncEntities
    {
        public const string Company       = "Company";       // master tier (CompanyId = 0)
        public const string User          = "User";          // master tier
        public const string MasterSetting = "MasterSetting"; // master tier
        public const string ChequeProfile = "ChequeProfile"; // company tier
        public const string ChequeRecord  = "ChequeRecord";  // company tier
        public const string AppSetting    = "AppSetting";    // company tier
        public const string Bank          = "Bank";          // company tier
        public const string Payee         = "Payee";         // company tier

        /// <summary>Master-tier entities live under the reserved CompanyId 0; everything else is per-company.</summary>
        public const int MasterCompanyId = 0;

        public static readonly string[] Master  = { Company, User, MasterSetting };
        public static readonly string[] Company_ = { ChequeProfile, ChequeRecord, AppSetting, Bank, Payee };
    }

    /// <summary>Result of applying one pushed change.</summary>
    public enum PushStatus { Applied, Conflict, Rejected }

    /// <summary>A local change a client is pushing to the server. Identity is <see cref="SyncId"/> (a GUID), never an
    /// autoincrement id, so the same logical row is one row across every PC and replays never duplicate.</summary>
    public sealed class ChangeItem
    {
        public string Entity { get; set; } = "";
        public string SyncId { get; set; } = "";
        public string UpdatedAtUtc { get; set; } = "";   // ISO-8601 UTC, used for last-write-wins
        public bool   Deleted { get; set; }
        public long   BaseServerVersion { get; set; }     // server version the client last saw for this row (0 = never synced)
        public string PayloadJson { get; set; } = "";     // serialized row fields (portable FKs carried as *SyncId)
    }

    /// <summary>A server-side change streamed down to a client during pull.</summary>
    public sealed class ServerChange
    {
        public string Entity { get; set; } = "";
        public string SyncId { get; set; } = "";
        public long   ServerVersion { get; set; }
        public string UpdatedAtUtc { get; set; } = "";
        public bool   Deleted { get; set; }
        public string PayloadJson { get; set; } = "";
    }

    /// <summary>Delta-pull request: give me everything newer than the version I already have, per entity.</summary>
    public sealed class PullRequest
    {
        public int CompanyId { get; set; }                                  // scope (0 = master tier)
        public Dictionary<string, long> Cursors { get; set; } = new();      // entity -> last ServerVersion the client holds
        public int MaxBatch { get; set; } = 500;                            // bound the response so the server stays light
    }

    public sealed class PullResponse
    {
        public List<ServerChange> Changes { get; set; } = new();
        public Dictionary<string, long> NextCursors { get; set; } = new();  // advance the client's cursors to these
        public bool HasMore { get; set; }                                   // true => call pull again immediately
    }

    /// <summary>Batch push: the client's local changes since it last synced.</summary>
    public sealed class PushRequest
    {
        public int CompanyId { get; set; }
        public string DeviceId { get; set; } = "";
        public List<ChangeItem> Changes { get; set; } = new();
    }

    public sealed class PushResponse
    {
        public List<PushResult> Results { get; set; } = new();
    }

    /// <summary>Per-row outcome of a push. On <see cref="PushStatus.Conflict"/> the server already resolved it
    /// (last-write-wins) and returns the winning row so the client can reconcile its local copy.</summary>
    public sealed class PushResult
    {
        public string Entity { get; set; } = "";
        public string SyncId { get; set; } = "";
        public PushStatus Status { get; set; }
        public long   ServerVersion { get; set; }         // the row's authoritative version after this push
        public string? ServerPayloadJson { get; set; }    // populated when the server's copy won a conflict
        public string? Message { get; set; }
    }

    /// <summary>Device registration — a PC gets its own bearer token. Single-organisation server, so no key.</summary>
    public sealed class RegisterRequest
    {
        public string DeviceName { get; set; } = "";
        public string MachineId { get; set; } = "";       // stable per-PC id so re-registration is idempotent
    }

    public sealed class RegisterResponse
    {
        public string DeviceId { get; set; } = "";
        public string Token { get; set; } = "";
    }

    public sealed class HealthResponse
    {
        public string Status { get; set; } = "ok";
        public string Version { get; set; } = "";
        public string ServerTimeUtc { get; set; } = "";
    }
}
