using System.Reflection;

namespace eCheque.MICO360.Helpers
{
    /// <summary>
    /// Central place for app identity, contact details, the update source repo,
    /// and the default (placeholder) legal text. Admins/developers can override the
    /// legal text at runtime via Settings → it is stored in the database.
    /// </summary>
    public static class AppInfo
    {
        public const string AppName      = "eCheque MICO360";
        public const string CompanyName  = "MICO360 Softwares";
        public const string ContactEmail = "mico360om@gmail.com";
        public const string Website      = "https://www.mico360.com";

        // ── Auto-update source (GitHub repository) ──
        public const string GitHubOwner  = "mico360om";
        public const string GitHubRepo   = "eCheque-MICO360";
        public static string ReleasesApiUrl => $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
        public static string RepoUrl        => $"https://github.com/{GitHubOwner}/{GitHubRepo}";

        /// <summary>Installed app version, e.g. "1.0.0".</summary>
        public static string Version
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v == null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
            }
        }

        public const string CompanyIntro =
            "MICO360 Softwares develops practical business tools for companies in Oman and the GCC. " +
            "eCheque MICO360 is a secure desktop solution for preparing, printing, and tracking " +
            "cheques against configurable bank layouts, with multi-company support, audit logging, " +
            "and role-based access control.";

        // ─────────────────────────────────────────────────────────────
        // Default placeholder legal content. Edit in-app (admin) or here.
        // ─────────────────────────────────────────────────────────────

        public const string DefaultTerms =
@"PLACEHOLDER — Terms & Conditions. Replace this text with your finalized legal wording.

1. ACCEPTANCE OF TERMS
By installing or using eCheque MICO360 (""the Software""), you agree to be bound by these Terms & Conditions. If you do not agree, do not use the Software.

2. APP USAGE RULES
The Software is licensed for use by authorized personnel of the licensed company only. You agree to use it solely for lawful cheque preparation, printing, and record-keeping purposes, and not to misuse, reverse engineer, or redistribute it.

3. USER RESPONSIBILITY
You are responsible for safeguarding your login credentials, for the actions performed under your account, and for ensuring only authorized users have access to the Software.

4. DOCUMENT ACCURACY RESPONSIBILITY
You are solely responsible for verifying the accuracy of all cheque details — payee, amount, amount in words, date, account, and bank layout alignment — before printing or issuing any cheque. The Software is a tool to assist data entry and printing; it does not validate the correctness of financial information.

5. LIMITATION OF LIABILITY
To the maximum extent permitted by law, MICO360 Softwares shall not be liable for any direct, indirect, incidental, or consequential losses arising from the use of, or inability to use, the Software — including printing errors, misaligned output, data loss, or financial loss resulting from incorrect cheque data.

6. SOFTWARE UPDATES
The Software may check for and install updates to improve functionality and security. Some updates may be mandatory for continued use. You agree to keep the Software reasonably up to date.

7. SUPPORT TERMS
Support is provided on a commercially reasonable-effort basis through the contact channels published by MICO360 Softwares. Support scope, response times, and availability may change.

8. CHANGES TO THESE TERMS
MICO360 Softwares may revise these Terms from time to time. Continued use of the Software after changes constitutes acceptance of the revised Terms.";

        public const string DefaultPrivacy =
@"PLACEHOLDER — Privacy Policy. Replace this text with your finalized legal wording.

1. WHAT DATA IS COLLECTED
The Software stores the data you enter to operate it, including company details, user accounts (with securely hashed passwords), cheque records, payees, bank/cheque-profile layouts, audit logs, and application settings.

2. HOW THE DATA IS USED
Your data is used only to provide the Software's functionality — preparing and printing cheques, maintaining history and reports, enforcing access control, and producing audit trails. We do not sell your data.

3. HOW DOCUMENTS / FILES ARE STORED
Application data is stored locally on your machine in a SQLite database and in generated PDF files saved to the folder you configure. Backups are written to the backup folder you specify. No cheque content is transmitted to MICO360 Softwares.

4. DATA SECURITY
Passwords are stored using one-way BCrypt hashing. The database file is protected on disk using Windows data-protection mechanisms. Access to modules is restricted by user role. You remain responsible for the physical and account security of the device on which the Software runs.

5. NETWORK / UPDATE CONNECTIONS
When checking for updates, the Software contacts the official update repository to read version information and download update packages. This connection does not transmit your cheque or company data.

6. USER RIGHTS
You may view, edit, export, and delete the records you manage within the Software, subject to your assigned role and applicable audit-retention requirements.

7. CONTACT INFORMATION
For privacy questions or requests, contact MICO360 Softwares using the details on the About Us page.";
    }
}
