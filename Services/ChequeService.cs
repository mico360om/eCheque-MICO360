using Microsoft.Data.Sqlite;
using eCheque.MICO360.Models;

namespace eCheque.MICO360.Services
{
    public static class ChequeService
    {
        // Tolerant of a column that doesn't exist yet (e.g. a schema migration that hasn't run / partially failed).
        static int Ord(SqliteDataReader r,string col){try{return r.GetOrdinal(col);}catch{return -1;}}
        static string S(SqliteDataReader r,string col){var o=Ord(r,col);return o<0||r.IsDBNull(o)?"":r.GetString(o);}
        static double D(SqliteDataReader r,string col){var o=Ord(r,col);return o<0||r.IsDBNull(o)?0:r.GetDouble(o);}
        static int I(SqliteDataReader r,string col){var o=Ord(r,col);return o<0||r.IsDBNull(o)?0:r.GetInt32(o);}

        public static List<ChequeRecord> GetCheques(string? search=null,string? status=null,DateTime? from=null,DateTime? to=null,int limit=0)
        {
            var list=new List<ChequeRecord>();
            using var conn=DatabaseService.GetConnection();
            var sql="SELECT * FROM ChequeRecords WHERE 1=1";
            var cmd=new SqliteCommand{Connection=conn};
            if(!string.IsNullOrWhiteSpace(search)){sql+=" AND (ChequeNumber LIKE @s OR PayeeName LIKE @s OR BankName LIKE @s)";cmd.Parameters.AddWithValue("@s",$"%{search}%");}
            if(!string.IsNullOrWhiteSpace(status)){sql+=" AND Status=@st";cmd.Parameters.AddWithValue("@st",status);}
            if(from.HasValue){sql+=" AND ChequeDate>=@fd";cmd.Parameters.AddWithValue("@fd",from.Value.ToString("yyyy-MM-dd"));}
            if(to.HasValue){sql+=" AND ChequeDate<=@td";cmd.Parameters.AddWithValue("@td",to.Value.ToString("yyyy-MM-dd"));}
            sql+=" ORDER BY CreatedDate DESC";
            if(limit>0){sql+=" LIMIT @lim";cmd.Parameters.AddWithValue("@lim",limit);} // SQL-side cap — never load the whole table for "recent N"
            cmd.CommandText=sql;
            using var r=cmd.ExecuteReader();
            while(r.Read())list.Add(MapCheque(r));
            return list;
        }

        public static ChequeRecord? GetCheque(int id){using var conn=DatabaseService.GetConnection();using var cmd=new SqliteCommand("SELECT * FROM ChequeRecords WHERE Id=@id",conn);cmd.Parameters.AddWithValue("@id",id);using var r=cmd.ExecuteReader();return r.Read()?MapCheque(r):null;}
        public static bool ChequeNumberExists(string num,string bank,int excludeId=0){using var conn=DatabaseService.GetConnection();using var cmd=new SqliteCommand("SELECT COUNT(*) FROM ChequeRecords WHERE ChequeNumber=@n AND BankName=@b AND Id!=@id",conn);cmd.Parameters.AddWithValue("@n",num);cmd.Parameters.AddWithValue("@b",bank);cmd.Parameters.AddWithValue("@id",excludeId);return Convert.ToInt32(cmd.ExecuteScalar())>0;}

        /// <summary>Statuses that represent an issued/closed cheque which must not be edited or re-printed casually.</summary>
        public static bool IsLocked(string? status) => status is "Printed" or "Reprinted" or "Presented" or "Cleared" or "Bounced" or "Cancelled" or "Void";
        /// <summary>A cheque whose status forbids printing entirely (settled or closed).</summary>
        public static bool IsPrintBlocked(string? status) => status is "Cancelled" or "Void" or "Cleared" or "Bounced";
        /// <summary>Number of saved cheques referencing a profile (used to block deletion of in-use profiles).</summary>
        public static int CountChequesUsingProfile(int profileId){using var conn=DatabaseService.GetConnection();using var cmd=new SqliteCommand("SELECT COUNT(*) FROM ChequeRecords WHERE ProfileId=@id",conn);cmd.Parameters.AddWithValue("@id",profileId);return Convert.ToInt32(cmd.ExecuteScalar());}

        public static int SaveCheque(ChequeRecord c)
        {
            using var conn=DatabaseService.GetConnection();
            if(c.Id==0)
            {
                using var cmd=new SqliteCommand("INSERT INTO ChequeRecords(ChequeNumber,ChequeDate,PayeeName,Amount,AmountInWords,BankName,AccountName,AccountNumber,ProfileId,ProfileName,Currency,Remarks,ReferenceNumber,InvoiceNumber,VoucherNumber,PreparedBy,ApprovedBy,Department,PaymentCategory,Status,CreatedBy,CreatedDate,PrintCount,PdfFilePath,CancellationReason)VALUES(@cn,@cd,@pn,@amt,@aw,@bn,@an,@anum,@pid,@pnm,@cur,@rem,@ref,@inv,@vou,@prep,@appr,@dept,@cat,@stat,@cb,@cdate,0,'','')",conn);
                FillCheque(cmd,c);cmd.ExecuteNonQuery();
                using var id=new SqliteCommand("SELECT last_insert_rowid()",conn);c.Id=Convert.ToInt32(id.ExecuteScalar());
                DatabaseService.LogAudit(c.CreatedBy,"Cheque Created",c.ChequeNumber);
            }
            else
            {
                var old=GetCheque(c.Id);
                using var cmd=new SqliteCommand("UPDATE ChequeRecords SET ChequeNumber=@cn,ChequeDate=@cd,PayeeName=@pn,Amount=@amt,AmountInWords=@aw,BankName=@bn,AccountName=@an,AccountNumber=@anum,ProfileId=@pid,ProfileName=@pnm,Currency=@cur,Remarks=@rem,ReferenceNumber=@ref,InvoiceNumber=@inv,VoucherNumber=@vou,PreparedBy=@prep,ApprovedBy=@appr,Department=@dept,PaymentCategory=@cat,Status=@stat WHERE Id=@id",conn);
                FillCheque(cmd,c);cmd.Parameters.AddWithValue("@id",c.Id);cmd.ExecuteNonQuery();
                DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"","Cheque Updated",c.ChequeNumber,DescribeChequeChanges(old,c));
            }
            SavePayee(c.PayeeName);
            return c.Id;
        }

        public static void UpdateStatus(int id,string status,string? pdf=null)
        {
            using var conn=DatabaseService.GetConnection();
            if(status is "Printed" or "Reprinted"){using var cmd=new SqliteCommand("UPDATE ChequeRecords SET Status=@s,PrintedDate=@pd,PrintCount=PrintCount+1,PdfFilePath=COALESCE(@pdf,PdfFilePath) WHERE Id=@id",conn);cmd.Parameters.AddWithValue("@s",status);cmd.Parameters.AddWithValue("@pd",DateTime.Now.ToString("o"));cmd.Parameters.AddWithValue("@pdf",pdf??(object)DBNull.Value);cmd.Parameters.AddWithValue("@id",id);cmd.ExecuteNonQuery();}
            else{using var cmd=new SqliteCommand("UPDATE ChequeRecords SET Status=@s WHERE Id=@id",conn);cmd.Parameters.AddWithValue("@s",status);cmd.Parameters.AddWithValue("@id",id);cmd.ExecuteNonQuery();}
        }

        public static void RecordPrint(int chequeId,string chequeNum,bool isReprint,string reason="")
        {
            using var conn=DatabaseService.GetConnection();
            using var cmd=new SqliteCommand("INSERT INTO PrintHistory(ChequeId,ChequeNumber,PrintedBy,PrintedDate,Reason,IsReprint)VALUES(@cid,@cn,@pb,@pd,@r,@ir)",conn);
            cmd.Parameters.AddWithValue("@cid",chequeId);cmd.Parameters.AddWithValue("@cn",chequeNum);cmd.Parameters.AddWithValue("@pb",AuthService.CurrentUser?.Username??"");cmd.Parameters.AddWithValue("@pd",DateTime.Now.ToString("o"));cmd.Parameters.AddWithValue("@r",reason);cmd.Parameters.AddWithValue("@ir",isReprint?1:0);cmd.ExecuteNonQuery();
        }

        public static void CancelCheque(int id,string reason)
        {
            using var conn=DatabaseService.GetConnection();
            using var cmd=new SqliteCommand("UPDATE ChequeRecords SET Status='Cancelled',CancellationReason=@r WHERE Id=@id",conn);
            cmd.Parameters.AddWithValue("@r",reason);cmd.Parameters.AddWithValue("@id",id);cmd.ExecuteNonQuery();
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"","Cheque Cancelled",id.ToString(),reason);
        }

        // ───────────────────────── Cheque lifecycle / reconciliation ─────────────────────────

        // Returns true when the transition actually applied (a valid predecessor status existed).
        public static bool MarkPresented(int id,DateTime date)
        {
            using var conn=DatabaseService.GetConnection();
            using var cmd=new SqliteCommand("UPDATE ChequeRecords SET Status='Presented',PresentedDate=@d WHERE Id=@id AND Status IN('Printed','Reprinted')",conn);
            cmd.Parameters.AddWithValue("@d",date.ToString("o"));cmd.Parameters.AddWithValue("@id",id);
            if(cmd.ExecuteNonQuery()==0) return false;
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"","Cheque Presented",id.ToString(),date.ToString("yyyy-MM-dd"));
            return true;
        }

        public static bool MarkCleared(int id,DateTime date)
        {
            using var conn=DatabaseService.GetConnection();
            using var cmd=new SqliteCommand("UPDATE ChequeRecords SET Status='Cleared',ClearedDate=@d WHERE Id=@id AND Status IN('Printed','Reprinted','Presented')",conn);
            cmd.Parameters.AddWithValue("@d",date.ToString("o"));cmd.Parameters.AddWithValue("@id",id);
            if(cmd.ExecuteNonQuery()==0) return false;
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"","Cheque Cleared",id.ToString(),date.ToString("yyyy-MM-dd"));
            return true;
        }

        public static bool MarkBounced(int id,string reason)
        {
            using var conn=DatabaseService.GetConnection();
            using var cmd=new SqliteCommand("UPDATE ChequeRecords SET Status='Bounced',BounceReason=@r WHERE Id=@id AND Status IN('Printed','Reprinted','Presented')",conn);
            cmd.Parameters.AddWithValue("@r",reason);cmd.Parameters.AddWithValue("@id",id);
            if(cmd.ExecuteNonQuery()==0) return false;
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"","Cheque Bounced",id.ToString(),reason);
            return true;
        }

        // All PDC/reconciliation queries filter to issued cheques IN SQL (small subset once most cheques are
        // Cleared) instead of materializing the whole table and filtering in memory. ChequeDate is stored as
        // yyyy-MM-dd; date() normalizes defensively without changing the semantics.
        static List<ChequeRecord> QueryIssued(int? dueWithinDays)
        {
            var list=new List<ChequeRecord>();
            using var conn=DatabaseService.GetConnection();
            var cmd=new SqliteCommand{Connection=conn};
            var sql="SELECT * FROM ChequeRecords WHERE Status IN('Printed','Reprinted','Presented')";
            if(dueWithinDays.HasValue){sql+=" AND date(ChequeDate)<=@d";cmd.Parameters.AddWithValue("@d",DateTime.Today.AddDays(dueWithinDays.Value).ToString("yyyy-MM-dd"));}
            sql+=" ORDER BY ChequeDate";
            cmd.CommandText=sql;
            using var r=cmd.ExecuteReader();
            while(r.Read())list.Add(MapCheque(r));
            return list;
        }

        /// <summary>Outstanding issued cheques (post-dated and overdue), ordered by due date — for the PDC register.</summary>
        public static List<ChequeRecord> GetPdcCheques() => QueryIssued(null);

        /// <summary>Issued cheques awaiting settlement (for the reconciliation screen).</summary>
        public static List<ChequeRecord> GetOutstandingCheques() => QueryIssued(null);

        /// <summary>Count of issued cheques due within the next <paramref name="days"/> days, INCLUDING overdue ones.</summary>
        public static int GetDuePdcCount(int days)
        {
            using var conn=DatabaseService.GetConnection();
            using var cmd=new SqliteCommand("SELECT COUNT(*) FROM ChequeRecords WHERE Status IN('Printed','Reprinted','Presented') AND date(ChequeDate)<=@d",conn);
            cmd.Parameters.AddWithValue("@d",DateTime.Today.AddDays(days).ToString("yyyy-MM-dd"));
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>Issued cheques due within the next <paramref name="days"/> days (incl. overdue), soonest first — for reminders.</summary>
        public static List<ChequeRecord> GetDuePdcCheques(int days) => QueryIssued(days);

        public static void VoidCheque(int id,string reason)
        {
            using var conn=DatabaseService.GetConnection();
            using var cmd=new SqliteCommand("UPDATE ChequeRecords SET Status='Void',CancellationReason=@r WHERE Id=@id",conn);
            cmd.Parameters.AddWithValue("@r",reason);cmd.Parameters.AddWithValue("@id",id);cmd.ExecuteNonQuery();
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"","Cheque Voided",id.ToString(),reason);
        }

        // Builds a human-readable "field: old → new" list for the audit trail.
        static string DescribeChequeChanges(ChequeRecord? o,ChequeRecord n)
        {
            if(o==null) return "";
            var ch=new List<string>();
            void C(string label,string ov,string nv){if(ov!=nv)ch.Add($"{label}: '{ov}' → '{nv}'");}
            C("Cheque#",o.ChequeNumber,n.ChequeNumber);
            C("Date",o.ChequeDate.ToString("yyyy-MM-dd"),n.ChequeDate.ToString("yyyy-MM-dd"));
            C("Payee",o.PayeeName,n.PayeeName);
            C("Amount",o.Amount.ToString("N3"),n.Amount.ToString("N3"));
            C("Bank",o.BankName,n.BankName);
            C("Status",o.Status,n.Status);
            C("Memo",o.Remarks,n.Remarks);
            return ch.Count==0?"(no field changes)":string.Join("; ",ch);
        }

        public static (int total,int printed,int draft,int cancelled,decimal totalAmount) GetStats()
        {
            using var conn=DatabaseService.GetConnection();
            using var cmd=new SqliteCommand("SELECT COUNT(*),SUM(CASE WHEN Status IN('Printed','Reprinted')THEN 1 ELSE 0 END),SUM(CASE WHEN Status='Draft'THEN 1 ELSE 0 END),SUM(CASE WHEN Status='Cancelled'THEN 1 ELSE 0 END),COALESCE(SUM(CASE WHEN Status IN('Printed','Reprinted')THEN Amount ELSE 0 END),0) FROM ChequeRecords",conn);
            using var r=cmd.ExecuteReader();
            if(r.Read())return(r.IsDBNull(0)?0:r.GetInt32(0),r.IsDBNull(1)?0:r.GetInt32(1),r.IsDBNull(2)?0:r.GetInt32(2),r.IsDBNull(3)?0:r.GetInt32(3),r.IsDBNull(4)?0m:(decimal)r.GetDouble(4));
            return(0,0,0,0,0);
        }

        public static DashboardStats GetDashboardStats()
        {
            using var conn = DatabaseService.GetConnection();
            var sql = @"SELECT
                COUNT(*),
                SUM(CASE WHEN Status IN('Printed','Reprinted') THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status IN('Draft','ReadyToPrint') THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status='Cancelled' THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status='Void' THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status IN('Printed','Reprinted') AND DATE(PrintedDate)=DATE('now','localtime') THEN 1 ELSE 0 END),
                SUM(CASE WHEN Status IN('Printed','Reprinted') AND strftime('%Y-%m',PrintedDate)=strftime('%Y-%m',DATE('now','localtime')) THEN 1 ELSE 0 END),
                COALESCE(SUM(CASE WHEN Status IN('Printed','Reprinted') THEN Amount ELSE 0 END),0),
                COALESCE(SUM(CASE WHEN Status IN('Printed','Reprinted') AND strftime('%Y-%m',PrintedDate)=strftime('%Y-%m',DATE('now','localtime')) THEN Amount ELSE 0 END),0),
                COALESCE(SUM(CASE WHEN Status IN('Printed','Reprinted') AND strftime('%Y',PrintedDate)=strftime('%Y',DATE('now','localtime')) THEN Amount ELSE 0 END),0)
                FROM ChequeRecords";
            using var cmd = new SqliteCommand(sql, conn);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return new DashboardStats();
            return new DashboardStats
            {
                Total          = r.IsDBNull(0)?0:r.GetInt32(0),
                Printed        = r.IsDBNull(1)?0:r.GetInt32(1),
                Draft          = r.IsDBNull(2)?0:r.GetInt32(2),
                Cancelled      = r.IsDBNull(3)?0:r.GetInt32(3),
                Voided         = r.IsDBNull(4)?0:r.GetInt32(4),
                TodayPrinted   = r.IsDBNull(5)?0:r.GetInt32(5),
                MonthPrinted   = r.IsDBNull(6)?0:r.GetInt32(6),
                TotalAmount    = r.IsDBNull(7)?0m:(decimal)r.GetDouble(7),
                MonthAmount    = r.IsDBNull(8)?0m:(decimal)r.GetDouble(8),
                YearAmount     = r.IsDBNull(9)?0m:(decimal)r.GetDouble(9)
            };
        }

        public static List<ChequeProfile> GetProfiles(bool activeOnly=true)
        {
            var list=new List<ChequeProfile>();
            using var conn=DatabaseService.GetConnection();
            using var cmd=new SqliteCommand("SELECT * FROM ChequeProfiles"+(activeOnly?" WHERE IsActive=1":"")+" ORDER BY Name",conn);
            using var r=cmd.ExecuteReader();
            while(r.Read())list.Add(MapProfile(r));
            return list;
        }

        public static ChequeProfile? GetProfile(int id){using var conn=DatabaseService.GetConnection();using var cmd=new SqliteCommand("SELECT * FROM ChequeProfiles WHERE Id=@id",conn);cmd.Parameters.AddWithValue("@id",id);using var r=cmd.ExecuteReader();return r.Read()?MapProfile(r):null;}

        public static int SaveProfile(ChequeProfile p)
        {
            using var conn=DatabaseService.GetConnection();
            if(p.Id==0)
            {
                using var cmd=new SqliteCommand("INSERT INTO ChequeProfiles(Name,BankName,AccountName,AccountNumber,ChequeWidth,ChequeHeight,DateX,DateY,PayeeX,PayeeY,AmountNumX,AmountNumY,AmountWordsX,AmountWordsY,ChequeNumX,ChequeNumY,FontFamily,FontSize,IsBold,PrintOffsetX,PrintOffsetY,PaperSize,IsActive,CreatedDate,CreatedBy,BackgroundImage,FieldsJson)VALUES(@n,@bn,@an,@anum,@cw,@ch,@dx,@dy,@px,@py,@ax,@ay,@wx,@wy,@nx,@ny,@ff,@fs,@ib,@ox,@oy,@ps,@ia,@cd,@cb,@bg,@fj)",conn);
                FillProfile(cmd,p);cmd.ExecuteNonQuery();
                using var id=new SqliteCommand("SELECT last_insert_rowid()",conn);p.Id=Convert.ToInt32(id.ExecuteScalar());
                DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"","Profile Created",p.Name);
            }
            else
            {
                using var cmd=new SqliteCommand("UPDATE ChequeProfiles SET Name=@n,BankName=@bn,AccountName=@an,AccountNumber=@anum,ChequeWidth=@cw,ChequeHeight=@ch,DateX=@dx,DateY=@dy,PayeeX=@px,PayeeY=@py,AmountNumX=@ax,AmountNumY=@ay,AmountWordsX=@wx,AmountWordsY=@wy,ChequeNumX=@nx,ChequeNumY=@ny,FontFamily=@ff,FontSize=@fs,IsBold=@ib,PrintOffsetX=@ox,PrintOffsetY=@oy,PaperSize=@ps,IsActive=@ia,BackgroundImage=@bg,FieldsJson=@fj WHERE Id=@id",conn);
                FillProfile(cmd,p);cmd.Parameters.AddWithValue("@id",p.Id);cmd.ExecuteNonQuery();
                DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"","Profile Updated",p.Name);
            }
            return p.Id;
        }

        /// <summary>Soft-deletes a profile. Returns false (and does nothing) if the profile is referenced by existing cheques.</summary>
        public static bool DeleteProfile(int id)
        {
            if(CountChequesUsingProfile(id)>0) return false;
            using var conn=DatabaseService.GetConnection();
            using var cmd=new SqliteCommand("UPDATE ChequeProfiles SET IsActive=0 WHERE Id=@id",conn);
            cmd.Parameters.AddWithValue("@id",id);cmd.ExecuteNonQuery();
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"","Profile Deleted",id.ToString());
            return true;
        }
        public static List<string> GetBanks(){var list=new List<string>();using var conn=DatabaseService.GetConnection();using var cmd=new SqliteCommand("SELECT Name FROM Banks WHERE IsActive=1 ORDER BY Name",conn);using var r=cmd.ExecuteReader();while(r.Read())list.Add(r.GetString(0));return list;}

        /// <summary>The profile id marked as default for New Cheque (0 = none). Stored per-company.</summary>
        public static int GetDefaultProfileId() => int.TryParse(DatabaseService.GetSetting("DefaultProfileId",""), out var id) ? id : 0;
        public static void SetDefaultProfile(int id) => DatabaseService.SaveSetting("DefaultProfileId", id.ToString());
        public static List<string> GetPayees()
        {
            var list=new List<string>();
            using var conn=DatabaseService.GetConnection();
            // Saved payees, most-recently-used first.
            using(var cmd=new SqliteCommand("SELECT Name FROM Payees ORDER BY LastUsed DESC, Name LIMIT 300",conn))
            using(var r=cmd.ExecuteReader()) while(r.Read()) list.Add(r.GetString(0));
            if(list.Count==0) // fall back to distinct historical cheque payees
            {
                using var c2=new SqliteCommand("SELECT DISTINCT PayeeName FROM ChequeRecords WHERE PayeeName!='' ORDER BY PayeeName LIMIT 300",conn);
                using var r2=c2.ExecuteReader(); while(r2.Read()) list.Add(r2.GetString(0));
            }
            return list;
        }

        /// <summary>History summary for one payee (excludes cancelled/void): how many cheques, and the most
        /// recent one — shown inline on the entry screen so the accountant can spot repeats at a glance.</summary>
        public static (int Count, string LastNumber, DateTime LastDate, decimal LastAmount)? GetPayeeSummary(string payee)
        {
            if (string.IsNullOrWhiteSpace(payee)) return null;
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand(
                @"SELECT COUNT(*),
                         (SELECT ChequeNumber FROM ChequeRecords WHERE PayeeName=@p AND Status NOT IN('Cancelled','Void') ORDER BY CreatedDate DESC LIMIT 1),
                         (SELECT ChequeDate   FROM ChequeRecords WHERE PayeeName=@p AND Status NOT IN('Cancelled','Void') ORDER BY CreatedDate DESC LIMIT 1),
                         (SELECT Amount       FROM ChequeRecords WHERE PayeeName=@p AND Status NOT IN('Cancelled','Void') ORDER BY CreatedDate DESC LIMIT 1)
                  FROM ChequeRecords WHERE PayeeName=@p AND Status NOT IN('Cancelled','Void')", conn);
            cmd.Parameters.AddWithValue("@p", payee.Trim());
            using var r = cmd.ExecuteReader();
            if (!r.Read() || r.GetInt32(0) == 0) return null;
            return (r.GetInt32(0),
                    r.IsDBNull(1) ? "" : r.GetString(1),
                    DateTime.TryParse(r.IsDBNull(2) ? "" : r.GetString(2), out var d) ? d : DateTime.Today,
                    r.IsDBNull(3) ? 0m : (decimal)r.GetDouble(3));
        }

        /// <summary>Possible double payment: a non-cancelled cheque to the same payee for the same amount created
        /// within the last <paramref name="days"/> days. Returns its cheque number, or null. Warning only — never blocks.</summary>
        public static string? FindRecentDuplicate(string payee, decimal amount, int days = 30, int excludeId = 0)
        {
            if (string.IsNullOrWhiteSpace(payee) || amount <= 0) return null;
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand(
                @"SELECT ChequeNumber FROM ChequeRecords
                  WHERE PayeeName=@p AND Amount=@a AND Id!=@id AND Status NOT IN('Cancelled','Void')
                    AND CreatedDate >= @since ORDER BY CreatedDate DESC LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@p", payee.Trim());
            cmd.Parameters.AddWithValue("@a", (double)amount);
            cmd.Parameters.AddWithValue("@id", excludeId);
            cmd.Parameters.AddWithValue("@since", DateTime.Now.AddDays(-days).ToString("o"));
            return cmd.ExecuteScalar() as string;
        }

        /// <summary>
        /// Cheque numbers used by more than one live (non-cancelled/void) cheque on the same bank. Because sync
        /// identity is a per-row GUID and two offline PCs can each issue the same leaf, a collision can only be
        /// DETECTED after sync, not prevented. Surfaced as a warning so a duplicate leaf is never printed twice
        /// unnoticed. Returns (number, bank, count) per collision, worst first.
        /// </summary>
        public static List<(string Number, string Bank, int Count)> FindDuplicateChequeNumbers()
        {
            var list = new List<(string, string, int)>();
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand(
                @"SELECT ChequeNumber, BankName, COUNT(*) AS c
                  FROM ChequeRecords
                  WHERE Status NOT IN('Cancelled','Void') AND ChequeNumber <> ''
                  GROUP BY ChequeNumber, BankName HAVING c > 1
                  ORDER BY c DESC, ChequeNumber", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.IsDBNull(0) ? "" : r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1), r.GetInt32(2)));
            return list;
        }

        /// <summary>How many cheque numbers are duplicated across live cheques (0 = none). Cheap COUNT for badges.</summary>
        public static int DuplicateChequeCount()
        {
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand(
                @"SELECT COUNT(*) FROM (SELECT 1 FROM ChequeRecords
                  WHERE Status NOT IN('Cancelled','Void') AND ChequeNumber <> ''
                  GROUP BY ChequeNumber, BankName HAVING COUNT(*) > 1)", conn);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>Remembers a payee for reuse in the autocomplete (upsert with last-used timestamp).</summary>
        public static void SavePayee(string name)
        {
            if(string.IsNullOrWhiteSpace(name)) return;
            using var conn=DatabaseService.GetConnection();
            using var cmd=new SqliteCommand("INSERT INTO Payees(Name,LastUsed)VALUES(@n,@d) ON CONFLICT(Name) DO UPDATE SET LastUsed=@d",conn);
            cmd.Parameters.AddWithValue("@n",name.Trim()); cmd.Parameters.AddWithValue("@d",DateTime.Now.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public static List<AuditLog> GetAuditLogs(int limit=500)
        {
            var list=new List<AuditLog>();
            using var conn=DatabaseService.GetConnection();
            using var cmd=new SqliteCommand($"SELECT * FROM AuditLogs ORDER BY ActionDate DESC LIMIT {limit}",conn);
            using var r=cmd.ExecuteReader();
            while(r.Read())list.Add(new AuditLog{Id=r.GetInt32(0),UserName=r.IsDBNull(1)?"":r.GetString(1),Action=r.IsDBNull(2)?"":r.GetString(2),RecordReference=r.IsDBNull(3)?"":r.GetString(3),Remarks=r.IsDBNull(4)?"":r.GetString(4),ActionDate=DateTime.TryParse(r.IsDBNull(5)?"":r.GetString(5),out var d)?d:DateTime.Now});
            return list;
        }

        static void FillCheque(SqliteCommand cmd,ChequeRecord c)
        {
            cmd.Parameters.AddWithValue("@cn",c.ChequeNumber);cmd.Parameters.AddWithValue("@cd",c.ChequeDate.ToString("yyyy-MM-dd"));cmd.Parameters.AddWithValue("@pn",c.PayeeName);cmd.Parameters.AddWithValue("@amt",(double)c.Amount);cmd.Parameters.AddWithValue("@aw",c.AmountInWords);cmd.Parameters.AddWithValue("@bn",c.BankName);cmd.Parameters.AddWithValue("@an",c.AccountName);cmd.Parameters.AddWithValue("@anum",c.AccountNumber);cmd.Parameters.AddWithValue("@pid",c.ProfileId);cmd.Parameters.AddWithValue("@pnm",c.ProfileName);cmd.Parameters.AddWithValue("@cur",c.Currency);cmd.Parameters.AddWithValue("@rem",c.Remarks);cmd.Parameters.AddWithValue("@ref",c.ReferenceNumber);cmd.Parameters.AddWithValue("@inv",c.InvoiceNumber);cmd.Parameters.AddWithValue("@vou",c.VoucherNumber);cmd.Parameters.AddWithValue("@prep",c.PreparedBy);cmd.Parameters.AddWithValue("@appr",c.ApprovedBy);cmd.Parameters.AddWithValue("@dept",c.Department);cmd.Parameters.AddWithValue("@cat",c.PaymentCategory);cmd.Parameters.AddWithValue("@stat",c.Status);cmd.Parameters.AddWithValue("@cb",c.CreatedBy);cmd.Parameters.AddWithValue("@cdate",c.CreatedDate.ToString("o"));
        }

        static void FillProfile(SqliteCommand cmd,ChequeProfile p)
        {
            cmd.Parameters.AddWithValue("@n",p.Name);cmd.Parameters.AddWithValue("@bn",p.BankName);cmd.Parameters.AddWithValue("@an",p.AccountName);cmd.Parameters.AddWithValue("@anum",p.AccountNumber);cmd.Parameters.AddWithValue("@cw",p.ChequeWidth);cmd.Parameters.AddWithValue("@ch",p.ChequeHeight);cmd.Parameters.AddWithValue("@dx",p.DateX);cmd.Parameters.AddWithValue("@dy",p.DateY);cmd.Parameters.AddWithValue("@px",p.PayeeX);cmd.Parameters.AddWithValue("@py",p.PayeeY);cmd.Parameters.AddWithValue("@ax",p.AmountNumX);cmd.Parameters.AddWithValue("@ay",p.AmountNumY);cmd.Parameters.AddWithValue("@wx",p.AmountWordsX);cmd.Parameters.AddWithValue("@wy",p.AmountWordsY);cmd.Parameters.AddWithValue("@nx",p.ChequeNumX);cmd.Parameters.AddWithValue("@ny",p.ChequeNumY);cmd.Parameters.AddWithValue("@ff",p.FontFamily);cmd.Parameters.AddWithValue("@fs",p.FontSize);cmd.Parameters.AddWithValue("@ib",p.IsBold?1:0);cmd.Parameters.AddWithValue("@ox",p.PrintOffsetX);cmd.Parameters.AddWithValue("@oy",p.PrintOffsetY);cmd.Parameters.AddWithValue("@ps",p.PaperSize);cmd.Parameters.AddWithValue("@ia",p.IsActive?1:0);cmd.Parameters.AddWithValue("@cd",p.CreatedDate.ToString("o"));cmd.Parameters.AddWithValue("@cb",p.CreatedBy);cmd.Parameters.AddWithValue("@bg",p.BackgroundImage??"");cmd.Parameters.AddWithValue("@fj",p.FieldsJson??"");
        }

        static ChequeRecord MapCheque(SqliteDataReader r)=>new(){Id=I(r,"Id"),ChequeNumber=S(r,"ChequeNumber"),ChequeDate=DateTime.TryParse(S(r,"ChequeDate"),out var cd)?cd:DateTime.Today,PayeeName=S(r,"PayeeName"),Amount=(decimal)D(r,"Amount"),AmountInWords=S(r,"AmountInWords"),BankName=S(r,"BankName"),AccountName=S(r,"AccountName"),AccountNumber=S(r,"AccountNumber"),ProfileId=I(r,"ProfileId"),ProfileName=S(r,"ProfileName"),Currency=S(r,"Currency"),Remarks=S(r,"Remarks"),ReferenceNumber=S(r,"ReferenceNumber"),InvoiceNumber=S(r,"InvoiceNumber"),VoucherNumber=S(r,"VoucherNumber"),PreparedBy=S(r,"PreparedBy"),ApprovedBy=S(r,"ApprovedBy"),Department=S(r,"Department"),PaymentCategory=S(r,"PaymentCategory"),Status=S(r,"Status"),CreatedBy=S(r,"CreatedBy"),CreatedDate=DateTime.TryParse(S(r,"CreatedDate"),out var crd)?crd:DateTime.Now,PrintCount=I(r,"PrintCount"),PdfFilePath=S(r,"PdfFilePath"),CancellationReason=S(r,"CancellationReason"),PrintedDate=DateTime.TryParse(S(r,"PrintedDate"),out var prd)?prd:null,PresentedDate=DateTime.TryParse(S(r,"PresentedDate"),out var psd)?psd:null,ClearedDate=DateTime.TryParse(S(r,"ClearedDate"),out var cld)?cld:null,BounceReason=S(r,"BounceReason")};

        static ChequeProfile MapProfile(SqliteDataReader r)=>new(){Id=I(r,"Id"),Name=S(r,"Name"),BankName=S(r,"BankName"),AccountName=S(r,"AccountName"),AccountNumber=S(r,"AccountNumber"),ChequeWidth=D(r,"ChequeWidth"),ChequeHeight=D(r,"ChequeHeight"),DateX=D(r,"DateX"),DateY=D(r,"DateY"),PayeeX=D(r,"PayeeX"),PayeeY=D(r,"PayeeY"),AmountNumX=D(r,"AmountNumX"),AmountNumY=D(r,"AmountNumY"),AmountWordsX=D(r,"AmountWordsX"),AmountWordsY=D(r,"AmountWordsY"),ChequeNumX=D(r,"ChequeNumX"),ChequeNumY=D(r,"ChequeNumY"),FontFamily=S(r,"FontFamily"),FontSize=D(r,"FontSize"),IsBold=I(r,"IsBold")==1,PrintOffsetX=D(r,"PrintOffsetX"),PrintOffsetY=D(r,"PrintOffsetY"),PaperSize=S(r,"PaperSize"),IsActive=I(r,"IsActive")==1,LastChequeNumber=I(r,"LastChequeNumber"),BackgroundImage=S(r,"BackgroundImage"),FieldsJson=S(r,"FieldsJson")};
        public static List<Models.PrintHistory> GetPrintHistory(int? chequeId = null, int limit = 300)
        {
            var list = new List<Models.PrintHistory>();
            using var conn = DatabaseService.GetConnection();
            var sql = "SELECT ph.Id,ph.ChequeId,ph.ChequeNumber,ph.PrintedBy,ph.PrintedDate,ph.Reason,ph.IsReprint,COALESCE(cr.PayeeName,''),COALESCE(cr.BankName,''),COALESCE(cr.Amount,0) FROM PrintHistory ph LEFT JOIN ChequeRecords cr ON ph.ChequeId=cr.Id";
            if (chequeId.HasValue) sql += " WHERE ph.ChequeId=@id";
            sql += $" ORDER BY ph.PrintedDate DESC LIMIT {limit}";
            using var cmd = new SqliteCommand(sql, conn);
            if (chequeId.HasValue) cmd.Parameters.AddWithValue("@id", chequeId.Value);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new Models.PrintHistory
                {
                    Id = r.GetInt32(0), ChequeId = r.IsDBNull(1) ? 0 : r.GetInt32(1),
                    ChequeNumber = r.IsDBNull(2) ? "" : r.GetString(2),
                    PrintedBy = r.IsDBNull(3) ? "" : r.GetString(3),
                    PrintedDate = DateTime.TryParse(r.IsDBNull(4) ? "" : r.GetString(4), out var pd) ? pd : DateTime.Now,
                    Reason = r.IsDBNull(5) ? "" : r.GetString(5),
                    IsReprint = r.IsDBNull(6) ? false : r.GetInt32(6) == 1,
                    PayeeName = r.IsDBNull(7) ? "" : r.GetString(7),
                    BankName = r.IsDBNull(8) ? "" : r.GetString(8),
                    Amount = r.IsDBNull(9) ? 0 : (decimal)r.GetDouble(9)
                });
            return list;
        }

        public static string GetNextChequeNumber(int profileId)
        {
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand("SELECT LastChequeNumber FROM ChequeProfiles WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", profileId);
            var current = Convert.ToInt32(cmd.ExecuteScalar());
            return (current + 1).ToString("D6");
        }

        public static void IncrementChequeNumber(int profileId)
        {
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand("UPDATE ChequeProfiles SET LastChequeNumber=LastChequeNumber+1 WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", profileId);
            cmd.ExecuteNonQuery();
        }

        public static void SaveProfilePositions(ChequeProfile p)
        {
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand(
                "UPDATE ChequeProfiles SET DateX=@dx,DateY=@dy,PayeeX=@px,PayeeY=@py,AmountNumX=@ax,AmountNumY=@ay,AmountWordsX=@wx,AmountWordsY=@wy,ChequeNumX=@nx,ChequeNumY=@ny WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@dx",p.DateX); cmd.Parameters.AddWithValue("@dy",p.DateY);
            cmd.Parameters.AddWithValue("@px",p.PayeeX); cmd.Parameters.AddWithValue("@py",p.PayeeY);
            cmd.Parameters.AddWithValue("@ax",p.AmountNumX); cmd.Parameters.AddWithValue("@ay",p.AmountNumY);
            cmd.Parameters.AddWithValue("@wx",p.AmountWordsX); cmd.Parameters.AddWithValue("@wy",p.AmountWordsY);
            cmd.Parameters.AddWithValue("@nx",p.ChequeNumX); cmd.Parameters.AddWithValue("@ny",p.ChequeNumY);
            cmd.Parameters.AddWithValue("@id",p.Id);
            cmd.ExecuteNonQuery();
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"","Profile Layout Changed",p.Name);
        }

        /// <summary>Persists the visual designer output: background image, the full field layout (JSON), and the legacy X/Y for the print fallback.</summary>
        public static void SaveLayout(ChequeProfile p)
        {
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand(
                "UPDATE ChequeProfiles SET BackgroundImage=@bg,FieldsJson=@fj,DateX=@dx,DateY=@dy,PayeeX=@px,PayeeY=@py,AmountNumX=@ax,AmountNumY=@ay,AmountWordsX=@wx,AmountWordsY=@wy,ChequeNumX=@nx,ChequeNumY=@ny WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@bg", p.BackgroundImage ?? ""); cmd.Parameters.AddWithValue("@fj", p.FieldsJson ?? "");
            cmd.Parameters.AddWithValue("@dx",p.DateX); cmd.Parameters.AddWithValue("@dy",p.DateY);
            cmd.Parameters.AddWithValue("@px",p.PayeeX); cmd.Parameters.AddWithValue("@py",p.PayeeY);
            cmd.Parameters.AddWithValue("@ax",p.AmountNumX); cmd.Parameters.AddWithValue("@ay",p.AmountNumY);
            cmd.Parameters.AddWithValue("@wx",p.AmountWordsX); cmd.Parameters.AddWithValue("@wy",p.AmountWordsY);
            cmd.Parameters.AddWithValue("@nx",p.ChequeNumX); cmd.Parameters.AddWithValue("@ny",p.ChequeNumY);
            cmd.Parameters.AddWithValue("@id",p.Id);
            cmd.ExecuteNonQuery();
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"","Cheque Layout Saved",p.Name);
        }
}
}
