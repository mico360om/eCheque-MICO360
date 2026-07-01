using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace eCheque.MICO360.Services
{
    /// <summary>
    /// Sends transactional email via the Mailjet Send API v3.1.
    /// Configuration (API key, secret key, from address/name) is stored in the master DB and
    /// set by an admin in Settings. See https://dev.mailjet.com/email/guides/send-api-v31/
    /// </summary>
    public static class EmailService
    {
        static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
        const string SendUrl = "https://api.mailjet.com/v3.1/send";

        public static bool IsConfigured =>
            CompanyService.GetMasterSetting("Mailjet_ApiKey", "").Length > 0 &&
            CompanyService.GetMasterSetting("Mailjet_SecretKey", "").Length > 0 &&
            CompanyService.GetMasterSetting("Mailjet_FromEmail", "").Length > 0;

        public static async Task<(bool ok, string? error)> SendAsync(string toEmail, string toName, string subject, string htmlBody, string textBody)
        {
            var apiKey    = CompanyService.GetMasterSetting("Mailjet_ApiKey", "");
            var secretKey = CompanyService.GetMasterSetting("Mailjet_SecretKey", "");
            var fromEmail = CompanyService.GetMasterSetting("Mailjet_FromEmail", "");
            var fromName  = CompanyService.GetMasterSetting("Mailjet_FromName", "eCheque MICO360");
            if (apiKey.Length == 0 || secretKey.Length == 0 || fromEmail.Length == 0)
                return (false, "Email (Mailjet) is not configured. Ask an administrator to set it in Settings.");

            var payload = new
            {
                Messages = new[]
                {
                    new
                    {
                        From = new { Email = fromEmail, Name = fromName },
                        To = new[] { new { Email = toEmail, Name = string.IsNullOrWhiteSpace(toName) ? toEmail : toName } },
                        Subject = subject,
                        TextPart = textBody,
                        HTMLPart = htmlBody
                    }
                }
            };

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, SendUrl);
                req.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{apiKey}:{secretKey}")));
                req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                using var resp = await Http.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    return (false, $"Mailjet error {(int)resp.StatusCode}: {body}");

                // Mailjet v3.1 returns HTTP 200 even when a message fails — check per-message Status.
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var status = doc.RootElement.GetProperty("Messages")[0].GetProperty("Status").GetString();
                    if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
                        return (false, $"Mailjet did not send the message (status: {status}). Check the From address is a verified sender.");
                }
                catch { /* unparseable but 2xx — assume accepted */ }
                return (true, null);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }
    }
}
