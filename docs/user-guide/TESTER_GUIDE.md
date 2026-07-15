# BatoBuzz Accounting Tester Guide

Use this guide to test BatoBuzz without needing accounting experience. Use a
test company and sample names only; do not enter real customer, bank, PAN/VAT,
or password information.

## Before you start

1. Install the latest BatoBuzz setup file.
2. Start **BatoBuzz Accounting** from the Start menu.
3. If Windows asks for permission, choose **Yes** only when the installer came
   from the official BatoBuzz release page.
4. Keep a note of anything unexpected: what you clicked, what you expected,
   what happened, and a screenshot if possible.

## Quick test checklist

Complete the checks below in order. A ✅ means the expected result happened.

| Test | What to do | Expected result |
| --- | --- | --- |
| 1. Start app | Open BatoBuzz. | The login screen opens without an error. |
| 2. Create owner | Enter a username, email, name, and password, then choose **Create Owner Account**. | You are signed in. |
| 3. Create company | Open **File > Company Setup** and enter a test company name such as `Demo Trading`. Save it. | The company name appears at the top of the app. |
| 4. Add customer | Open **Sales > Customers** and create `Test Customer`. | The customer appears in the customer list. |
| 5. Add supplier | Open **Purchase > Suppliers** and create `Test Supplier`. | The supplier appears in the supplier list. |
| 6. Add inventory item | Open **Inventory > Items**. Create a unit, warehouse, and an item such as `Test Item`; give it a sale price and cost. | The item appears in the inventory list. |
| 7. Create sale | Open **Sales > Sales Invoice**. Select `Test Customer`, add `Test Item`, save, then post the invoice. | The invoice receives a number and shows as posted. |
| 8. Record receipt | Open **Sales > Receipts**. Select `Test Customer`, enter a small amount, and allocate it to the test invoice. | The receipt saves and reduces the invoice balance. |
| 9. Create purchase | Open **Purchase > Purchase Bill**. Select `Test Supplier`, add `Test Item`, save, then post. | The bill receives a number and shows as posted. |
| 10. Record payment | Open **Purchase > Payments**. Select `Test Supplier`, enter a small amount, and allocate it to the test bill. | The payment saves and reduces the bill balance. |
| 11. Check reports | Open **Reports** and generate **Trial Balance**, **Profit & Loss**, and **Stock Summary**. | Reports load without an error. Trial Balance debit and credit totals match. |
| 12. Backup | Open **File > Backup** and create a backup. | A `.db` backup file is created in the selected location. |
| 13. Check updates | Select **Help > Check for Updates** or use the status-bar button. | The app says it is up to date, or shows the new version and its change log. |

## Important things to test

- Try closing and reopening the app after creating data. The test company and
  records should still be there.
- Use the menus after login and confirm each screen opens.
- Try an incorrect password five times. The app should show an account-lockout
  message rather than signing in.
- Enter a clearly invalid amount, such as a negative sale quantity or an empty
  required field. The app should show a helpful validation message.
- Do not delete the database file to solve a problem. Use **File > Backup**
  first and report the issue.

## Testing an update when a newer version is available

1. Finish or discard open work.
2. Create a backup with **File > Backup**.
3. Select **Help > Check for Updates**.
4. Read the version number and change log.
5. Select **Yes** only if you want to install the update.
6. The app downloads and verifies the setup file, then opens the installer.
7. Complete the installer and reopen BatoBuzz.
8. Check that your company and test records are still present.

## How to report a problem

Copy and complete this template:

```text
App version:
Windows version:
What I was trying to do:
Steps I clicked:
Expected result:
Actual result / error message:
Can I repeat the problem? Yes / No
Screenshot attached? Yes / No
```

For a startup problem, also attach or copy the contents of:

```text
%LOCALAPPDATA%\BatoBuzz\Accounting\logs\startup-error.log
```

Never send a real database, password, PAN/VAT number, bank detail, or customer
data in a public message.
