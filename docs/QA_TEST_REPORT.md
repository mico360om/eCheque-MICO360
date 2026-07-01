# eCheque MICO360 — QA / QC Test Report

**Product:** eCheque MICO360
**Vendor:** MICO360 Softwares
**Edition under test:** macOS (Avalonia UI · .NET 9), sharing the cross-platform Core with the Windows (WPF) edition
**Report date:** 2026-07-01
**Prepared by:** QA / QC
**Test type:** Functional, validation, security, and platform-parity testing

> **Execution note:** The test cases below are written to be **executed on a Mac** (macOS 12+). "Actual" results marked *Pending (on-device)* require a physical macOS run before final sign-off. Windows behavior is treated as the parity baseline.

---

## 1. Purpose & Scope

This report provides a reusable **test template** plus a **representative executed test set** covering all modules: Login, Roles, Dashboard, New Cheque (validation, amount-in-words, duplicates), Cheque History, Print History, Cheque Profiles, Settings (persistence, backup/restore), Users, Audit Log, Companies, Help & Legal (updates), and cross-cutting security (BCrypt, SQLCipher encryption).

**Legend**
- **Status:** Pass / Fail / Pending
- **Severity:** Critical / High / Medium / Low
- **Parity:** cases marked **[Parity]** verify identical behavior to the Windows edition.

---

## 2. Test Environment

| Item | Value |
|------|-------|
| OS | macOS 12+ (target device) |
| Runtime | .NET 9 |
| Build | Avalonia UI macOS build (source or packaged) |
| Data path | `~/Library/Application Support/eCheque_MICO360/` |
| DB | SQLite per company, SQLCipher-encrypted |
| Baseline | Windows (WPF) edition, same Core version |

---

## 3. Test Case Template

| Field | Description |
|-------|-------------|
| Test Case ID | Unique identifier (TC-XXX) |
| Module | Feature area under test |
| Scenario | What is being verified |
| Steps | Actions to perform |
| Expected | Expected result |
| Actual | Observed result |
| Status | Pass / Fail / Pending |
| Severity | Critical / High / Medium / Low |

---

## 4. Executed Test Cases

| ID | Module | Scenario | Steps | Expected | Actual | Status | Severity |
|----|--------|----------|-------|----------|--------|--------|----------|
| TC-001 | Login | Valid login | Select company, enter valid admin creds, click Login | Dashboard opens for selected company | Pending (on-device) | Pending | Critical |
| TC-002 | Login | Invalid password | Enter valid user, wrong password | Login rejected with error; no access | Pending (on-device) | Pending | Critical |
| TC-003 | Login | Company selector [Parity] | Create 2 companies, switch selector, log in | Logs into the chosen company's data only | Pending (on-device) | Pending | High |
| TC-004 | Security | BCrypt hashing | Create user; inspect stored password field | Password stored as BCrypt hash, not plaintext | Pending (on-device) | Pending | Critical |
| TC-005 | Security | DB encryption (SQLCipher) [Parity] | Open company DB file with a plain SQLite reader | File is encrypted; not readable without key | Pending (on-device) | Pending | Critical |
| TC-006 | Roles | Viewer read-only | Log in as Viewer; attempt New Cheque/Print | Create/Print blocked; read-only access | Pending (on-device) | Pending | High |
| TC-007 | Roles | Accountant permissions | Log in as Accountant; create & print cheque | Allowed; Users/Settings/Companies hidden or blocked | Pending (on-device) | Pending | High |
| TC-008 | Roles | Edit printed cheque = Admin only [Parity] | As Accountant, edit a Printed cheque | Edit blocked; permitted only for Admin | Pending (on-device) | Pending | High |
| TC-009 | Users | Last-admin protection | Attempt to delete/demote the only Admin | Action prevented with message | Pending (on-device) | Pending | Critical |
| TC-010 | Users | User validation | Create user with missing/invalid fields | Validation errors shown; not saved | Pending (on-device) | Pending | Medium |
| TC-011 | Users | User CRUD | Create, edit, delete an Accountant | Changes persist correctly | Pending (on-device) | Pending | Medium |
| TC-012 | New Cheque | Required-field validation | Save with empty payee/amount | Blocked with field errors | Pending (on-device) | Pending | High |
| TC-013 | New Cheque | Profile auto-fill [Parity] | Select a profile | Layout/positioning fields auto-fill | Pending (on-device) | Pending | Medium |
| TC-014 | New Cheque | Payee autocomplete | Type first letters of an existing payee | Suggestion(s) appear and can be selected | Pending (on-device) | Pending | Low |
| TC-015 | New Cheque | Amount 3-decimal handling | Enter `4956.250` | Accepted as OMR with 3 decimals | Pending (on-device) | Pending | High |
| TC-016 | Amount-in-Words | 4956.250 conversion [Parity] | Enter `4956.250`; read words field | "Omani Rials Four Thousand Nine Hundred Fifty Six and Baisa Two Hundred Fifty Only" (per settings) | Pending (on-device) | Pending | High |
| TC-017 | Amount-in-Words | Whole amount, zero Baisa | Enter `100.000` | Words show 100 Rials, no/zero Baisa per settings | Pending (on-device) | Pending | Medium |
| TC-018 | Amount-in-Words | Settings variations | Toggle case/"Only"/Baisa wording; re-enter amount | Words reflect each setting | Pending (on-device) | Pending | Medium |
| TC-019 | New Cheque | Duplicate cheque number | Enter a cheque number already used in company | Duplicate rejected with message | Pending (on-device) | Pending | High |
| TC-020 | New Cheque | Save Draft | Fill valid cheque, Save Draft | Saved as Pending/Draft; appears in History | Pending (on-device) | Pending | Medium |
| TC-021 | New Cheque | Preview | Click Preview on valid cheque | Preview shows fields on layout | Pending (on-device) | Pending | Medium |
| TC-022 | Print | Print cheque [Parity] | Click Print on valid cheque | Prints via native macOS print; status = Printed | Pending (on-device) | Pending | Critical |
| TC-023 | Print | Mandatory reprint reason | Reprint an already-Printed cheque | Reprint blocked until reason entered | Pending (on-device) | Pending | High |
| TC-024 | Print | Cancelled cannot print [Parity] | Cancel a cheque, attempt Print | Print blocked | Pending (on-device) | Pending | High |
| TC-025 | Print | Voided cannot print [Parity] | Void a cheque, attempt Print | Print blocked | Pending (on-device) | Pending | High |
| TC-026 | History | Filter by status | Filter History by Printed | Only Printed cheques listed | Pending (on-device) | Pending | Medium |
| TC-027 | History | Filter by date range | Apply date range filter | Only cheques in range listed | Pending (on-device) | Pending | Medium |
| TC-028 | History | Export | Export filtered history | File exported with correct rows | Pending (on-device) | Pending | Medium |
| TC-029 | History | Cancel & Void actions | Cancel then Void cheques from rows | Statuses update; audit recorded | Pending (on-device) | Pending | Medium |
| TC-030 | Print History | Excel export & empty state | Apply filter with no matches, then export a match | Empty state shown; Excel export succeeds | Pending (on-device) | Pending | Medium |
| TC-031 | Profiles | Layout designer coordinates [Parity] | Set X/Y, width/height (mm), font; save; print | Fields print at configured positions | Pending (on-device) | Pending | High |
| TC-032 | Settings | Persistence | Change date format & amount-in-words options; restart app | Settings retained after restart | Pending (on-device) | Pending | Medium |
| TC-033 | Settings | PDF path | Set PDF path; generate PDF | PDF written to configured path | Pending (on-device) | Pending | Medium |
| TC-034 | Settings | Backup | Set backup path; Backup Now | Backup file created at path | Pending (on-device) | Pending | High |
| TC-035 | Settings | Restore | Restore from a backup | Company data restored from backup | Pending (on-device) | Pending | High |
| TC-036 | Audit Log | Logging & filters | Perform actions; open Audit Log; filter | Actions recorded and filterable | Pending (on-device) | Pending | Medium |
| TC-037 | Companies | Multi-company CRUD | Create/edit/delete a company | Company DB created/updated; isolated data | Pending (on-device) | Pending | High |
| TC-038 | Help & Legal | Check for Updates [Parity] | Click Check for Updates | Queries GitHub Releases; reports latest version | Pending (on-device) | Pending | Medium |
| TC-039 | Help & Legal | Terms/Privacy/About | Open each | Content displays correctly | Pending (on-device) | Pending | Low |
| TC-040 | Install | Gatekeeper quarantine | Launch unsigned build; run `xattr -dr com.apple.quarantine` | App launches after clearing quarantine | Pending (on-device) | Pending | Medium |

---

## 5. Summary

| Metric | Count |
|--------|------:|
| Total test cases | 40 |
| Passed | 0 |
| Failed | 0 |
| Pending (on-device macOS execution) | 40 |
| Critical-severity cases | 6 |
| High-severity cases | 15 |
| Platform-parity cases | 11 |

### Coverage
All listed modules and cross-cutting concerns (role enforcement, BCrypt, SQLCipher encryption, amount-in-words including the `4956.250` example, duplicate detection, mandatory reprint reason, cancelled/void print blocking, history/print filters and export, backup/restore, settings persistence, updates) are represented.

### Notes & Risks
- Test design is complete; **on-device macOS execution is pending**. All "Actual" values must be captured on a physical Mac before release.
- Parity cases assume the Windows (WPF) edition as the verified baseline; any deviation must be logged as a defect.
- Printing and PDF generation use native macOS mechanisms — verify alignment against physical cheques during TC-022 and TC-031.

### Release Decision: **Approved with Conditions**
The macOS build is **approved with conditions**, contingent on:
1. Successful on-device execution of all 40 cases on macOS 12+, with **all Critical and High cases Passing**.
2. Confirmation of print/layout alignment on physical cheque stock.
3. Validation of backup/restore round-trip (TC-034/TC-035).

Final production sign-off is withheld until the above on-device validation is completed and this report is updated with actual results.

---

*© MICO360 Softwares — eCheque MICO360. QA/QC Test Report (template + representative cases).*
