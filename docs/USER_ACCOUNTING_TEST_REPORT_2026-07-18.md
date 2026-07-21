# BatoBuzz Accounting — Full User Accounting Test Report

**Test date:** 2026-07-18  
**Application version:** 1.0.8  
**Test type:** End-to-end desktop user acceptance, accounting integrity, recovery, and targeted source review  
**Test data:** Isolated with `BATOBUZZ_DATA_DIRECTORY=tmp/user-e2e`

## Executive summary

The core double-entry engine is working and remained balanced through a complete trading cycle, payments, VAT/TDS posting, reversals, a stock write-off, period locking, bank reconciliation, and backup/restore.

The test did not find database corruption or an unbalanced posted journal. However, the desktop product is not yet complete enough to describe as a full accounting workflow. The most serious gaps are:

1. Saved sales and purchase drafts cannot be reopened after the current form is reset, closed, or lost.
2. Credit and debit notes are only manual journals. They cannot use customer, supplier, or inventory control ledgers and cannot process a return against an invoice or bill.
3. Users cannot create or maintain their own ledger accounts or account groups.
4. Direct inventory damage/write-off entries have no correction or reversal workflow.

**Release recommendation:** Do not market the current desktop build as supporting complete accounting until issues BB-001 through BB-004 are addressed.

## What passed

- Release solution build: 0 warnings, 0 errors.
- Existing smoke suite passed.
- First-run owner creation and company setup.
- Customer, supplier, item, opening stock, and weighted-average inventory.
- Sales invoice with discount and 13% VAT.
- Customer receipt, invoice allocation, and customer balance.
- Purchase bill with discount and 13% VAT.
- Supplier payment with TDS and supplier balance.
- Balanced opening journal; an unbalanced journal was correctly rejected.
- Sales invoice, receipt, purchase bill, payment, and manual-journal reversals with audit reasons.
- Period lock blocked an inventory posting on the lock date without mutating stock.
- Bank receipt creation, reconciliation, cleared-state persistence, reversal, and reconciliation of the reversal.
- Password change; old password rejected and new password accepted; failed-attempt count reset after successful login.
- Manual backup, restore, sentinel-data removal, and automatic safety backup.
- In-app update check completed and reported v1.0.8 current.
- All 12 report types generated without error.
- Chart of Accounts Excel export created successfully.
- Final database `PRAGMA quick_check`: `ok`.
- Final database foreign-key check: no violations.
- All 14 journal entries balanced individually and in aggregate.

## Accounting scenario and final reconciliation

The test company recorded:

- Opening stock: 20 × Rs. 200 = Rs. 4,000.
- Sales: 3 × Rs. 300, 10% discount, VAT Rs. 105.30, invoice total Rs. 915.30.
- Receipt: Rs. 500.
- Purchase: 10 × Rs. 180, 5% discount, VAT Rs. 222.30, bill total Rs. 1,932.30.
- Payment: cash Rs. 985 plus manually corrected TDS Rs. 15.
- The receipt, invoice, payment, bill, and opening-capital journal were then reversed/cancelled.
- A direct damage movement wrote off one item at its actual average cost of Rs. 200.

Final results:

| Check | Result |
|---|---:|
| Trial Balance debit | Rs. 4,000.00 |
| Trial Balance credit | Rs. 4,000.00 |
| Stock-in-Hand | Rs. 3,800.00 |
| Inventory Write-off | Rs. 200.00 |
| Net profit/loss | Rs. -200.00 |
| Total assets | Rs. 3,800.00 |
| Total liabilities | Rs. 0.00 |
| Total equity | Rs. 3,800.00 |
| Physical stock | 19 units × Rs. 200 |

## Issue register

### BB-001 — Saved drafts cannot be reopened, posted, or removed

**Severity:** High  
**Applies to:** Sales invoices and purchase bills

**Reproduction**

1. Create a sales invoice and select **Save Draft**.
2. The application reports `Saved draft INV-000002`.
3. Select **New Invoice**, close the tab, log out, or restart.
4. Reopen **Sales Invoice**.

**Actual**

- The form opens as a new blank invoice.
- `INV-000002` remains in SQLite with Draft status and total Rs. 339.
- There is no invoice browser or “Open Draft” command.
- Corrections displays the draft, but attempting to cancel it fails with: `The invoice is not linked to its posted journal and cannot be cancelled safely.`
- The only form reference to the draft is the in-memory `_savedInvoiceId`; **New Invoice** clears it.
- `PurchaseBillViewModel` has the same `_savedBillId` design and no bill-loading workflow.

**Expected**

Users must be able to list, reopen, edit, post, and discard saved drafts.

**Impact**

A normal user can permanently strand transactions simply by clicking **New**, closing a tab, logging out, or restarting the application. Stranded drafts also remain in registers and consume document numbers.

**Suggested direction**

Add invoice/bill list screens with status filters and an explicit load-by-ID command. Allow draft deletion or voiding with audit history.

**Evidence**

- `tmp/user-e2e/07-saved-draft.png`
- `SalesInvoiceViewModel.NewInvoice()` clears `_savedInvoiceId`.
- `PurchaseBillViewModel.NewBill()` clears `_savedBillId`.
- The Sales and Purchase menus expose only new-entry screens, not document browsers.

### BB-002 — Credit and debit notes are not operational credit/debit notes

**Severity:** High

**Actual**

- **Sales > Credit Notes** opens the generic Journal Entry screen with voucher type `Credit Note`.
- **Purchase > Debit Notes** uses the same generic journal screen.
- There is no customer/supplier, source invoice/bill, item, quantity, tax, or stock-return workflow.
- The accounting service explicitly rejects customer, supplier, and inventory control ledgers in manual journals.

**Expected**

A sales credit note should be linked to a customer and source invoice, update the receivable and VAT, optionally return stock, and support partial quantities. A purchase debit note should provide the corresponding supplier and stock behavior.

**Impact**

Partial sales returns, purchase returns, price corrections, and VAT adjustments cannot be completed safely. Full invoice/bill cancellation is not a substitute for a partial note.

**Evidence**

- `tmp/user-e2e/09-credit-note-is-manual-journal.png`
- `MainWindow.xaml` routes Credit Notes and Debit Notes to `ShowJournalEntryWithTypeCommand`.
- `AccountingService` rejects manual journals using Customer, Supplier, or Inventory ledger types.

### BB-003 — No ledger or account-group maintenance

**Severity:** High

**Actual**

- **Chart of Accounts** is a read-only report.
- There is no desktop view or view model for ledgers or account groups.
- Manual journal ledger selection is limited to existing active ledgers.
- A bank ledger can be auto-created indirectly by entering a new bank name on a receipt/payment, but users cannot intentionally configure bank, expense, income, loan, fixed-asset, or other accounts.

**Expected**

An accounting user needs create/edit/deactivate controls for account groups and ledgers, with validation around protected control accounts.

**Impact**

Users cannot represent their real chart of accounts or record ordinary expenses and assets that were not seeded by the application.

**Evidence**

- `tmp/user-e2e/05-chart-of-accounts-readonly.png`
- Desktop views/view models contain no ledger or account-group management screen.

### BB-004 — Direct inventory movements cannot be corrected or reversed

**Severity:** High

**Reproduction**

1. Post **Inventory > Stock Movement > Damage** for one item.
2. The application posts `SJ-000002`: Dr Inventory Write-off Rs. 200 / Cr Stock-in-Hand Rs. 200.
3. Open **Accounting > Corrections & Reversals**.

**Actual**

The correction center supports invoices, receipts, bills, payments, and manual journals only. It has no inventory-movement tab or reversal command. The successful damage movement therefore cannot be corrected through the UI.

**Expected**

Posted opening stock, damage, and write-off movements should have a dated, reasoned reversal that restores both stock quantity/value and the general ledger.

**Impact**

A single entry mistake permanently changes inventory and financial statements unless the user has an unsupported workaround.

### BB-005 — Inventory maintenance menus do not provide the advertised masters

**Severity:** Medium

**Actual**

- **Items**, **Stock Movement**, **Stock Adjustment**, and **Warehouses** all open the same Inventory screen.
- The screen can create an item and post only Opening Stock, Damage, or Write Off.
- Unit and warehouse records are silently created as defaults.
- There is no category, unit, or warehouse maintenance workflow and no warehouse selection for a movement.

**Expected**

Each advertised master/workflow should either be implemented or removed from the navigation until available.

### BB-006 — Receipts and payments force oldest-first allocation

**Severity:** Medium

**Actual**

- Receipt entry has no invoice-allocation grid.
- Payment entry has no bill-allocation grid.
- The view models silently allocate the total to the oldest outstanding documents by date, then treat any remainder as an advance.

**Expected**

Show eligible invoices/bills and let the user choose allocations, with oldest-first offered as an explicit shortcut.

**Impact**

Users cannot honor remittance advice or allocate a payment to a specific newer document. The posting may be balanced but operationally wrong.

### BB-007 — TDS automatic calculation has inconsistent amount semantics

**Severity:** Medium; requires accountant/product confirmation

**Reproduction**

1. Enter payment Amount Rs. 985.
2. Select a 1.5% TDS rate.

**Actual**

- The application calculates TDS as `985 × 1.5% = 14.78`.
- It then displays `Total Settled (cash + TDS) = 999.78`.

If Rs. 985 is the net cash paid and 1.5% is withheld from the gross settled amount, TDS is Rs. 15 and total settled is Rs. 1,000. If Rs. 985 is meant to be the gross base, the UI should not also describe Rs. 985 + TDS as the settled total.

**Expected**

Define the field as either gross amount or net cash and calculate consistently. For net cash, the gross-up formula is `cash × rate / (100 - rate)`.

**Evidence**

- `tmp/user-e2e/03-tds-calculation.png`
- `PaymentViewModel.RecalculateTds()` calculates `amount × rate / 100`.

### BB-008 — Successful restore leaves SQLite staging sidecars

**Severity:** Low

**Actual**

After a successful restore, the test data folder retained:

- `.restore-staging-<guid>.db-shm`
- `.restore-staging-<guid>.db-wal`
- safety-backup `.db-shm` and `.db-wal` files

The staging `.db` is moved into place, but its sidecars are not removed.

**Expected**

Clear SQLite pools and remove the staging and safety-backup sidecars after successful validation.

### BB-009 — Correction errors are sometimes non-actionable

**Severity:** Low

When cancelling a partially paid invoice, the application first reports `Only an issued or overdue invoice can be cancelled.` The more useful existing message—`Cannot cancel an invoice with receipts. Reverse receipts first.`—is unreachable because the status check runs first. Purchase bills have the same validation order.

### BB-010 — Tester documentation does not match the desktop UI

**Severity:** Low

Examples:

- The tester guide asks for owner email and name, but first-run setup asks only for username and password.
- The guide asks the tester to create a unit and warehouse; those controls do not exist.
- The guide instructs users to allocate receipts/payments to documents; the UI performs hidden oldest-first allocation.

## Evidence files

All evidence is in `tmp/user-e2e`:

1. `01-company-setup.png`
2. `02-posted-sales-invoice.png`
3. `03-tds-calculation.png`
4. `04-trial-balance.png`
5. `05-chart-of-accounts-readonly.png`
6. `06-bank-reconciliation.png`
7. `07-saved-draft.png`
8. `08-final-balance-sheet.png`
9. `09-credit-note-is-manual-journal.png`

## Test artifacts outside the repository

The UI tests created these files in the configured Documents backup folder:

- `D:\User Files\Documents\BatoBuzz Backups\Chart-of-Accounts-20260718-110411.xlsx`
- `D:\User Files\Documents\BatoBuzz Backups\BatoBuzz-20260718-110643.db`

They were retained as export/restore evidence.
