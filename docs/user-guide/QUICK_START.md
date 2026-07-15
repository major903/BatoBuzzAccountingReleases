# BatoBuzz Accounting Quick Start

For a simple step-by-step test plan, see the [Tester Guide](TESTER_GUIDE.md).

## Before installation

The supported desktop target is 64-bit Windows 10 or Windows 11. A release made
with `scripts\build-release.ps1` is self-contained and does not require a
separate .NET installation. The Microsoft Edge WebView2 Runtime is required to
display and print reports; it is normally present on current Windows systems.

For production, use the signed setup program supplied by BatoBuzz Technologies Pvt Ltd
and verify its publisher in Windows before running it. The portable build is
intended for controlled testing or support scenarios.

## First start

1. Start **BatoBuzz Accounting** from the Start menu.
2. On a new database, enter a username of at least three characters and a
   password of at least eight characters.
3. Select **Create Owner Account** and store this credential securely.
4. Open **File > Company Setup** and confirm the legal/trading name, PAN/VAT
   details, address, financial year, and reporting dates before entering live
   vouchers.
5. Verify opening balances and the chart of accounts with the responsible
   accountant.

The local database is created at:

```text
%LOCALAPPDATA%\BatoBuzz\Accounting\BatoBuzz.db
```

## Recommended setup order

1. **Company Setup** — legal details and the active financial year.
2. **Customers and Suppliers** — names, contact details, PAN/VAT information,
   and opening balances.
3. **Inventory** — units, warehouses, item codes, prices, costs, reorder levels,
   and opening stock.
4. **Tax Settings** — add only rates confirmed by a qualified Nepal accountant.
   TDS rates are not preloaded.
5. **Opening accounting entries** — reconcile the resulting Trial Balance
   before normal operation.

## Daily workflow

- Record supplier bills before their related payments.
- Record sales invoices before customer receipts.
- Check the date, financial year, party, warehouse, tax, totals, and narration
  before posting a voucher.
- Use journal or contra vouchers only when the debit and credit treatment is
  understood.
- Review receivables/payables, stock, and the Trial Balance regularly.
- Use the period lock after a reporting period is reviewed. Back-dated
  corrections should follow the organization's approval process.

## Correcting posted documents

Never delete or directly edit a posted accounting document. Open
**Accounting > Corrections & Reversals**, select the source document, choose a
correction date, and enter a clear reason.

- Reverse receipts before cancelling their invoice.
- Reverse payments before cancelling their purchase bill.
- A sales cancellation restores the exact quantity and value recorded by the
  original sale. A purchase cancellation is refused when later stock use makes
  an exact reversal unsafe.
- Manual journals can be reversed only from the manual-journal tab. Operational
  vouchers must be corrected from their own document tab.
- A correction cannot be dated before its source and cannot cross a period lock.

After a material correction, verify the Trial Balance, party ageing, stock
summary, and affected ledger. The source, contra entry, reason, date, and acting
username remain in the audit trail.

## Reports, export, and printing

Open **Reports**, select the report and date range, then generate it before
printing or exporting. If WebView2 is reported missing, install the Microsoft
Edge WebView2 Evergreen Runtime and reopen BatoBuzz.

Exported PDF and Excel files can contain confidential data. Save them only to
an approved location and remove temporary copies when no longer needed.

## Backup

Use **File > Backup** regularly and before every update or large import. Keep
at least one encrypted copy outside the computer. Record the backup date and
periodically test a copy on a non-production machine.

BatoBuzz also creates one validated automatic backup each day while you are
signed in. These are kept separately in `Documents\BatoBuzz Backups\Automatic`;
the newest 14 daily backups are retained. The status bar warns you only when an
automatic backup needs attention. Manual backups remain recommended before an
update, restore, or major accounting change.

## Restore

1. Close other BatoBuzz windows and stop data entry.
2. Select **File > Restore** and choose a trusted BatoBuzz database backup.
3. Confirm the prompt. The restore is staged and the application closes.
4. Start BatoBuzz again to complete the restore.
5. Confirm the company, financial year, latest vouchers, Trial Balance,
   receivables/payables, and stock before resuming work.

The application makes a safety copy of the current database during restore.
Retain it until the restored data has been reconciled.

The desktop release supports one owner of each company. Do not share the owner
password or place the SQLite database on a network share for simultaneous use.

## Updating

1. Finish or discard in-progress work.
2. Create and verify a backup.
3. Confirm that the installer is signed by the expected publisher and matches
   the supplied SHA-256 manifest.
4. Install the update over the existing application.
5. Start the application and reconcile key reports before entering new data.

Do not delete the Local AppData database to solve an update problem. Preserve a
copy and contact support if startup reports a schema or database error.

## Troubleshooting

- **Application does not start:** read
  `%LOCALAPPDATA%\BatoBuzz\Accounting\logs\startup-error.log` and preserve the
  database before attempting repairs.
- **Reports do not display:** install or repair the WebView2 Runtime.
- **Login fails:** check the username, keyboard layout, and account lockout
  message. Do not create a replacement database.
- **Trial Balance does not reconcile:** stop posting and have the accountant
  review source vouchers and the date range before making corrections.
- **Backup fails:** verify free disk space and write permission at the selected
  destination.

For security-sensitive incidents, follow the private reporting guidance in the
[Security Guide](../security/SECURITY.md).
