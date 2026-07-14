namespace BatoBuzz.Domain.Enums;

/// <summary>
/// Types of accounting vouchers supported by the system.
/// </summary>
public enum VoucherType
{
    Sales = 1,
    Purchase = 2,
    Receipt = 3,
    Payment = 4,
    Contra = 5,
    Journal = 6,
    DebitNote = 7,
    CreditNote = 8,
    SalesReturn = 9,
    PurchaseReturn = 10,
    OpeningBalance = 11,
    StockJournal = 12,
    Reversal = 99
}
