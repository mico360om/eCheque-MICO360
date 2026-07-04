using System.Net;
using eCheque.MICO360.Core.Models;

namespace eCheque.MICO360.Core.Services
{
    /// <summary>
    /// Emails a reminder of post-dated cheques coming due. Desktop app, so reminders are evaluated when the
    /// app runs (on startup): if reminders are enabled, the chosen frequency interval has elapsed, and cheques
    /// are due within the look-ahead window, a summary email is sent to the configured recipient.
    /// </summary>
    public static class PdcReminderService
    {
        const string KeyEnabled   = "PdcReminderEnabled";
        const string KeyEmail     = "PdcReminderEmail";
        const string KeyFreq      = "PdcReminderFrequencyDays";
        const string KeyLookAhead = "PdcReminderLookAheadDays";
        const string KeyLastSent  = "PdcReminderLastSent";
        const string KeyWhatsApp  = "PdcReminderWhatsApp";

        public static bool  Enabled       { get => DatabaseService.GetSetting(KeyEnabled, "0") == "1"; set => DatabaseService.SaveSetting(KeyEnabled, value ? "1" : "0"); }
        public static string Recipient    { get => DatabaseService.GetSetting(KeyEmail, ""); set => DatabaseService.SaveSetting(KeyEmail, (value ?? "").Trim()); }
        public static string WhatsAppNumber { get => DatabaseService.GetSetting(KeyWhatsApp, ""); set => DatabaseService.SaveSetting(KeyWhatsApp, (value ?? "").Trim()); }
        public static int   FrequencyDays { get => Clamp(DatabaseService.GetSetting(KeyFreq, "7"), 7, 1, 90); set => DatabaseService.SaveSetting(KeyFreq, value.ToString()); }
        public static int   LookAheadDays { get => Clamp(DatabaseService.GetSetting(KeyLookAhead, "7"), 7, 1, 365); set => DatabaseService.SaveSetting(KeyLookAhead, value.ToString()); }
        public static DateTime? LastSent  => DateTime.TryParse(DatabaseService.GetSetting(KeyLastSent, ""), out var d) ? d : null;

        static int Clamp(string s, int def, int min, int max) => int.TryParse(s, out var v) ? Math.Min(max, Math.Max(min, v)) : def;

        /// <summary>
        /// Builds a WhatsApp "click-to-send" URL (https://wa.me/&lt;number&gt;?text=…) carrying the same due-cheque
        /// summary the email reminder sends. Returns (url, "") on success, or (null, reason) if the number is
        /// invalid or nothing is due. The caller opens the URL so the user can review and tap Send in WhatsApp.
        /// </summary>
        public static (string? url, string message) BuildWhatsAppReminder()
        {
            var digits = DigitsOnly(WhatsAppNumber);
            if (digits.Length < 8) return (null, "Enter a valid WhatsApp number (with country code, e.g. 96891234567) in Settings.");
            var due = ChequeService.GetDuePdcCheques(LookAheadDays);
            if (due.Count == 0) return (null, $"No cheques are due within {LookAheadDays} day(s) — nothing to remind about.");
            return ("https://wa.me/" + digits + "?text=" + Uri.EscapeDataString(BuildPlainSummary(due)), "");
        }

        static string DigitsOnly(string? s) => new(string.Concat((s ?? "").Where(char.IsDigit)));

        static string BuildPlainSummary(List<ChequeRecord> due)
        {
            var company = CompanyService.CurrentCompanyName;
            decimal total = due.Sum(c => c.Amount);
            var currency = due.FirstOrDefault()?.Currency ?? "OMR";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"*PDC reminder — {company}*");
            sb.AppendLine($"{due.Count} post-dated cheque(s) due within {LookAheadDays} day(s). Total {currency} {total:N3}.");
            sb.AppendLine();
            foreach (var c in due)
                sb.AppendLine($"• #{c.ChequeNumber}  {c.PayeeName}  {currency} {c.Amount:N3}  {c.ChequeDate:dd/MM/yyyy}  ({c.DueLabel})");
            return sb.ToString().TrimEnd();
        }

        /// <summary>Called at startup. Sends a reminder only if enabled, configured, the frequency interval has
        /// elapsed since the last send, and cheques are actually due. Never throws.</summary>
        public static async Task MaybeSendAsync()
        {
            try
            {
                if (!Enabled) return;
                var last = LastSent;
                if (last != null && (DateTime.Now - last.Value).TotalDays < FrequencyDays) return; // not time yet
                await SendAsync(force: false);
            }
            catch (Exception ex) { BugReportService.Report(ex, "PdcReminder.MaybeSend"); }
        }

        /// <summary>Sends the reminder now (used by the "Send test reminder" button). Returns a user-facing result.</summary>
        public static async Task<string> SendNowAsync() => await SendAsync(force: true);

        static async Task<string> SendAsync(bool force)
        {
            if (!EmailService.IsConfigured) return "Email isn't configured. Add your Mailjet keys in Settings first.";
            var to = Recipient;
            if (string.IsNullOrWhiteSpace(to) || !to.Contains('@')) return "Enter a valid reminder recipient email.";

            var due = ChequeService.GetDuePdcCheques(LookAheadDays);
            if (due.Count == 0)
                return force ? $"No cheques are due within {LookAheadDays} day(s) — nothing to remind about." : "";

            var company = CompanyService.CurrentCompanyName;
            decimal total = due.Sum(c => c.Amount);
            var currency = due.FirstOrDefault()?.Currency ?? "OMR";

            string Row(ChequeRecord c) =>
                $"<tr><td style='padding:6px 10px;border-bottom:1px solid #eee'>{WebUtility.HtmlEncode(c.ChequeNumber)}</td>" +
                $"<td style='padding:6px 10px;border-bottom:1px solid #eee'>{WebUtility.HtmlEncode(c.PayeeName)}</td>" +
                $"<td style='padding:6px 10px;border-bottom:1px solid #eee'>{WebUtility.HtmlEncode(c.BankName)}</td>" +
                $"<td style='padding:6px 10px;border-bottom:1px solid #eee;text-align:right'>{c.Currency} {c.Amount:N3}</td>" +
                $"<td style='padding:6px 10px;border-bottom:1px solid #eee'>{c.ChequeDate:dd/MM/yyyy}</td>" +
                $"<td style='padding:6px 10px;border-bottom:1px solid #eee;color:{(c.DaysUntilDue < 0 ? "#C62828" : c.DaysUntilDue == 0 ? "#E65100" : "#555")}'>{WebUtility.HtmlEncode(c.DueLabel)}</td></tr>";

            var html = $@"<div style='font-family:Segoe UI,Arial,sans-serif;color:#1a1a1a'>
  <p>Hello,</p>
  <p><b>{due.Count}</b> post-dated cheque(s) for <b>{WebUtility.HtmlEncode(company)}</b> are due within the next {LookAheadDays} day(s):</p>
  <table style='border-collapse:collapse;font-size:13px;min-width:520px'>
    <thead><tr style='background:#8B1818;color:#fff'>
      <th style='padding:7px 10px;text-align:left'>Cheque #</th><th style='padding:7px 10px;text-align:left'>Payee</th>
      <th style='padding:7px 10px;text-align:left'>Bank</th><th style='padding:7px 10px;text-align:right'>Amount</th>
      <th style='padding:7px 10px;text-align:left'>Cheque Date</th><th style='padding:7px 10px;text-align:left'>Due</th>
    </tr></thead>
    <tbody>{string.Concat(due.Select(Row))}</tbody>
    <tfoot><tr><td colspan='3' style='padding:7px 10px;font-weight:bold'>Total</td>
      <td style='padding:7px 10px;text-align:right;font-weight:bold'>{currency} {total:N3}</td><td colspan='2'></td></tr></tfoot>
  </table>
  <p style='color:#888;font-size:12px;margin-top:16px'>Sent automatically by eCheque MICO360 (PDC reminders, every {FrequencyDays} day(s)). Manage this in Settings.</p>
</div>";

            var text = $"{due.Count} post-dated cheque(s) for {company} due within {LookAheadDays} day(s). Total {currency} {total:N3}.\n" +
                       string.Join("\n", due.Select(c => $"  #{c.ChequeNumber}  {c.PayeeName}  {c.Currency} {c.Amount:N3}  {c.ChequeDate:dd/MM/yyyy}  ({c.DueLabel})"));

            var (ok, err) = await EmailService.SendAsync(to, "PDC Reminder",
                $"⏰ {due.Count} post-dated cheque(s) due soon — {company}", html, text);
            if (ok)
            {
                DatabaseService.SaveSetting(KeyLastSent, DateTime.Now.ToString("o"));
                try { DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "SYSTEM", "PDC Reminder Sent", to, $"{due.Count} cheque(s)"); } catch { }
                return $"Reminder sent to {to} — {due.Count} cheque(s) due.";
            }
            return $"Could not send the reminder: {err}";
        }
    }
}
