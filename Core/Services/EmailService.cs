using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace eCheque.MICO360.Core.Services
{
    /// <summary>Sends transactional email via the Mailjet Send API v3.1 (config in the master DB, shared by macOS + Windows).</summary>
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
                return (false, "Email (Mailjet) is not configured.");

            var payload = new
            {
                Messages = new[]
                {
                    new
                    {
                        From = new { Email = fromEmail, Name = fromName },
                        To = new[] { new { Email = toEmail, Name = string.IsNullOrWhiteSpace(toName) ? toEmail : toName } },
                        Subject = subject, TextPart = textBody, HTMLPart = htmlBody
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
                if (resp.IsSuccessStatusCode) return (true, null);
                var body = await resp.Content.ReadAsStringAsync();
                return (false, $"Mailjet error {(int)resp.StatusCode}: {body}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }
    }
}
