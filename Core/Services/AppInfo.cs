using System.Reflection;

namespace eCheque.MICO360.Core.Services
{
    public static class AppInfo
    {
        public const string AppName      = "eCheque MICO360";
        public const string CompanyName  = "MICO360 Softwares";
        public const string ContactEmail = "info@mico360om";
        public const string Website      = "www.mico360.com";
        public const string Platform     = "macOS";

        public const string GitHubOwner  = "mico360om";
        public const string GitHubRepo   = "eCheque-MICO360";
        public static string ReleasesApiUrl => $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
        public static string RepoUrl        => $"https://github.com/{GitHubOwner}/{GitHubRepo}";

        public static string Version
        {
            get { var v = Assembly.GetExecutingAssembly().GetName().Version; return v == null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}"; }
        }

        public const string CompanyIntro =
            "MICO360 Softwares develops practical business tools for companies in Oman and the GCC. " +
            "eCheque MICO360 is a secure desktop solution for preparing, printing, and tracking " +
            "cheques against configurable bank layouts, with multi-company support, audit logging, " +
            "and role-based access control.";

        public const string DefaultTerms =
@"PLACEHOLDER — Terms & Conditions. Replace this text with your finalized legal wording.

1. ACCEPTANCE OF TERMS
By installing or using eCheque MICO360 (the Software), you agree to these Terms & Conditions.

2. APP USAGE RULES
The Software is licensed for use by authorized personnel of the licensed company only.

3. USER RESPONSIBILITY
You are responsible for safeguarding your login credentials and actions performed under your account.

4. DOCUMENT ACCURACY RESPONSIBILITY
You are solely responsible for verifying the accuracy of all cheque details before printing or issuing any cheque.

5. LIMITATION OF LIABILITY
MICO360 Softwares shall not be liable for losses arising from use of, or inability to use, the Software.

6. SOFTWARE UPDATES
The Software may check for and install updates. Some updates may be mandatory for continued use.

7. SUPPORT TERMS
Support is provided on a commercially reasonable-effort basis.

8. ACCEPTANCE
Continued use after changes constitutes acceptance of the revised Terms.";

        public const string DefaultPrivacy =
@"PLACEHOLDER — Privacy Policy. Replace this text with your finalized legal wording.

1. WHAT DATA IS COLLECTED
Company details, user accounts (hashed passwords), cheque records, payees, layouts, audit logs, settings.

2. HOW THE DATA IS USED
Only to provide the Software's functionality. We do not sell your data.

3. HOW DOCUMENTS / FILES ARE STORED
Locally on your Mac in an encrypted SQLite database and generated PDF/report files.

4. DATA SECURITY
Passwords are hashed with BCrypt; the database is encrypted at rest with SQLCipher.

5. USER RIGHTS
You may view, edit, export, and delete records subject to your role.

6. CONTACT INFORMATION
Contact MICO360 Softwares using the details on the About Us page.";
    }
}
