# eCheque MICO360 — User Manual

**Product:** eCheque MICO360
**Vendor:** MICO360 Softwares
**Edition:** macOS (Avalonia UI · .NET 9) — feature-identical to the Windows edition
**Currency:** Omani Rial (OMR). 1000 Baisa = 1 Rial. Amounts use 3 decimals.

---

## Table of Contents

1. [Getting Started](#1-getting-started)
2. [Login](#2-login)
3. [Dashboard](#3-dashboard)
4. [New Cheque](#4-new-cheque)
5. [Full New-Cheque-to-Print Workflow](#5-full-new-cheque-to-print-workflow)
6. [Cheque History](#6-cheque-history)
7. [Print History](#7-print-history)
8. [Cheque Profiles & Layout Designer](#8-cheque-profiles--layout-designer)
9. [Settings](#9-settings)
10. [Users](#10-users)
11. [Audit Log](#11-audit-log)
12. [Companies](#12-companies)
13. [Help & Legal](#13-help--legal)
14. [Roles & Permissions](#14-roles--permissions)
15. [Amount in Words](#15-amount-in-words)
16. [Tips](#16-tips)

---

## 1. Getting Started

eCheque MICO360 helps you prepare, print, and track cheques against configurable bank layouts. Each **company** has its own encrypted database. You log in, select a company, and work within it. Access to features depends on your **role** (Admin, Accountant, or Viewer).

---

## 2. Login

The Login screen authenticates you into a selected company.

| Element | What it does |
|---------|--------------|
| **Company selector** | Choose which company to log into (multi-company support) |
| **Username** | Your account username |
| **Password** | Your password (verified against a BCrypt hash; never stored in plain text) |
| **Login button** | Authenticates and opens the Dashboard |

**Steps**
1. Select your company.
2. Enter username and password.
3. Click **Login**.

If it's the first run, create a company first (see [Companies](#12-companies)) and log in with the default admin, then change the password immediately.

---

## 3. Dashboard

The Dashboard gives an at-a-glance summary of cheque activity.

**Stats cards**
- Total cheques
- Printed
- Pending
- Cancelled
- Voided
- Today's count
- This month's count
- Monthly value
- Yearly value

**Other sections**
- **Recent cheques** — the latest cheque records for quick reference.
- **Quick actions** — shortcuts to common tasks (e.g., New Cheque, History).

---

## 4. New Cheque

Create and prepare a cheque for printing.

| Field / Button | What it does |
|----------------|--------------|
| **Profile** | Select a cheque profile (bank layout). Selecting a profile **auto-fills** layout-related settings. |
| **Payee** | Beneficiary name. **Autocomplete** suggests previously used payees as you type. |
| **Amount** | Numeric amount in OMR (3 decimals, e.g., `4956.250`). |
| **Amount in words** | **Auto-converts** the numeric amount to words (OMR/Baisa) per your Settings. |
| **Memo** | Optional note printed/recorded with the cheque. |
| **Reference details** | Optional reference/document information. |
| **Cheque number** | Cheque serial; checked for duplicates within the company. |
| **Date** | Cheque date (format per Settings). |
| **Save Draft** | Saves the cheque without printing (status: Pending/Draft). |
| **Preview** | Shows the cheque positioned on the selected layout before printing. |
| **Print** | Prints the cheque and records it (status: Printed). |
| **Cancel** | Cancels the current cheque entry. |

**Validation includes:** required fields, valid amount, and a **duplicate cheque-number check** within the company.

---

## 5. Full New-Cheque-to-Print Workflow

1. Open **New Cheque**.
2. **Select a profile** — the layout and print positioning auto-fill.
3. **Enter the payee** — accept an autocomplete suggestion or type a new name.
4. **Enter the amount** (e.g., `4956.250`). The **amount in words** updates automatically.
5. Add **memo** and **reference details** if needed.
6. Enter or confirm the **cheque number** and **date**. Duplicates are rejected.
7. Click **Save Draft** if you're not ready to print, or continue.
8. Click **Preview** to verify positioning against the bank layout.
9. Click **Print**. The cheque prints via the native macOS print system and its status becomes **Printed**.
10. The action is recorded in the **Audit Log** and appears in **Print History**.

**Reprinting:** If you print an already-printed cheque again, a **reprint reason is mandatory** before the reprint proceeds.

**Blocked actions:** Cheques that are **Cancelled** or **Voided cannot be printed**.

---

## 6. Cheque History

Search, review, and manage all cheques for the company.

**Search & filters**
- Free-text search
- Filter by **status** (Printed, Pending, Cancelled, Voided)
- Filter by **date** range

**Row actions**
| Action | Notes |
|--------|-------|
| **Edit** | Edit a cheque. **Printed cheques can be edited by Admins only.** |
| **Print** | Print/reprint (reprint requires a reason; cancelled/void are blocked). |
| **Cancel** | Cancel a cheque. |
| **Void** | Void a cheque. |
| **Export** | Export the filtered list. |

---

## 7. Print History

A dedicated view of print events.

- **Filters** to narrow the print records.
- **Excel export** of the results.
- **Empty state** message shown when no print records match.

---

## 8. Cheque Profiles & Layout Designer

Profiles define a bank's cheque layout so that fields print in the correct positions.

**Profile fields**
- Bank / profile name and identifying details.

**Visual layout designer**
| Setting | Meaning |
|---------|---------|
| **X coordinate** | Horizontal position of a field on the cheque |
| **Y coordinate** | Vertical position of a field on the cheque |
| **Width (mm)** | Field width in millimetres |
| **Height (mm)** | Field height in millimetres |
| **Font** | Font used for the printed field |

Use the designer to position each element (payee, amount, amount-in-words, date, memo, etc.) to match the physical cheque. Selecting the profile in **New Cheque** auto-fills these settings.

---

## 9. Settings

Company-wide configuration.

| Setting | Description |
|---------|-------------|
| **Company** | Company details used on cheques/records |
| **Currency** | Currency configuration (OMR) |
| **Date format** | How dates are displayed and printed |
| **Amount-in-words: wording** | Phrasing style for the words conversion |
| **Amount-in-words: case** | Letter case of the converted words |
| **Amount-in-words: Baisa** | How Baisa (sub-unit) is expressed |
| **Amount-in-words: "Only"** | Whether to append "Only" at the end |
| **PDF path** | Folder where generated PDFs are saved |
| **Backup path** | Folder where database backups are written |
| **Backup** | Create a backup now |
| **Restore** | Restore the database from a backup |

---

## 10. Users

Manage user accounts (subject to role permissions).

- **CRUD** — create, read, update, delete users.
- **Roles** — Admin, Accountant, Viewer.
- **Validation** — enforced on user data entry.
- **Last-admin protection** — the system prevents deleting or demoting the final Admin, so a company always retains at least one administrator.

Passwords are hashed with **BCrypt**.

---

## 11. Audit Log

A filterable record of significant actions (logins, cheque creation, printing, reprints with reasons, cancellations, voids, user and settings changes). Use the **filters** to narrow by criteria for review and compliance.

---

## 12. Companies

Multi-company management.

- **CRUD** — create, read, update, delete companies.
- Each company has its **own encrypted SQLite database**.
- Switch companies at the **Login** screen via the company selector.

---

## 13. Help & Legal

- **Terms** — terms of use.
- **Privacy** — privacy statement.
- **About** — version and product information.
- **Check for Updates** — checks GitHub Releases for a newer version.

---

## 14. Roles & Permissions

Role-based access is enforced throughout the app.

| Capability | Admin | Accountant | Viewer |
|------------|:-----:|:----------:|:------:|
| View Dashboard / History / Print History | ✅ | ✅ | ✅ |
| Create / Save Draft cheque | ✅ | ✅ | ❌ |
| Print cheque | ✅ | ✅ | ❌ |
| Reprint (with mandatory reason) | ✅ | ✅ | ❌ |
| Edit unprinted cheque | ✅ | ✅ | ❌ |
| **Edit printed cheque** | ✅ | ❌ | ❌ |
| Cancel / Void cheque | ✅ | ✅ | ❌ |
| Export history / print records | ✅ | ✅ | ✅ |
| Manage Cheque Profiles | ✅ | ✅ | ❌ |
| Manage Settings (incl. backup/restore) | ✅ | ❌ | ❌ |
| Manage Users | ✅ | ❌ | ❌ |
| Manage Companies | ✅ | ❌ | ❌ |
| View Audit Log | ✅ | ❌ | ❌ |

> Viewers have read-only access. Accountants handle day-to-day cheque operations. Admins additionally manage users, companies, settings, and can edit printed cheques.

---

## 15. Amount in Words

The app converts the numeric amount into words using OMR and Baisa, controlled by the **Settings → Amount-in-words** options (wording, case, Baisa expression, and whether "Only" is appended).

**Conversion basis:** 1 Rial = 1000 Baisa; amounts use 3 decimals. The whole-number part is Rials and the 3-decimal fractional part is Baisa.

**Worked example — `4956.250` OMR**
- Rials (whole part): **4956** → *Four Thousand Nine Hundred Fifty Six*
- Decimal `.250` = **250 Baisa** → *Two Hundred Fifty*
- With default wording and "Only":

  > **Omani Rials Four Thousand Nine Hundred Fifty Six and Baisa Two Hundred Fifty Only**

The exact phrasing (case, ordering of the Baisa portion, and the "Only" suffix) follows your Settings selections.

---

## 16. Tips

- **Preview before printing** to confirm the cheque aligns with your bank layout.
- **Set up profiles carefully** once per bank; positioning in millimetres saves reprints.
- **Change the default admin password** on first run.
- **Back up regularly** to your Backup path, ideally on a separate/cloud volume.
- **Use autocomplete** for payees to keep names consistent.
- **Remember:** Cancelled and Voided cheques cannot be printed, and reprints always require a reason.
- **Least privilege:** give staff the **Viewer** or **Accountant** role unless they need Admin.

---

*© MICO360 Softwares — eCheque MICO360.*
