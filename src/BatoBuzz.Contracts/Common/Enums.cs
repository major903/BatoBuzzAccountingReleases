namespace BatoBuzz.Contracts.Common;

public enum AccountTypeDto { Asset = 1, Liability = 2, Equity = 3, Income = 4, CostOfSales = 5, Expense = 6, OtherIncome = 7, OtherExpense = 8 }
public enum VoucherTypeDto { Sales = 1, Purchase = 2, Receipt = 3, Payment = 4, Contra = 5, Journal = 6, DebitNote = 7, CreditNote = 8, SalesReturn = 9, PurchaseReturn = 10, OpeningBalance = 11, StockJournal = 12, Reversal = 99 }
public enum LedgerTypeDto { General = 1, Bank = 2, Cash = 3, Tax = 4, Customer = 5, Supplier = 6, Inventory = 7, Employee = 8 }
public enum ItemTypeDto { Goods = 1, Service = 2, Asset = 3 }
public enum CostingMethodDto { FIFO = 1, WeightedAverage = 2, StandardCost = 3 }
public enum InvoiceStatusDto { Draft = 1, Issued = 2, PartiallyPaid = 3, Paid = 4, Overdue = 5, Cancelled = 6, CreditNoteIssued = 7 }
public enum BillStatusDto { Draft = 1, Received = 2, PartiallyPaid = 3, Paid = 4, Overdue = 5, Cancelled = 6, DebitNoteIssued = 7 }
public enum PaymentMethodDto { Cash = 1, Cheque = 2, BankTransfer = 3, MobileMoney = 4, Card = 5 }
public enum TransactionStatusDto { Draft = 1, Submitted = 2, Approved = 3, Posted = 4, Cancelled = 5, Voided = 6, Reversed = 7 }
public enum MovementTypeDto { OpeningStock = 1, PurchaseIn = 2, SaleOut = 3, PurchaseReturn = 4, SalesReturn = 5, StockAdjustment = 6, StockTransferIn = 7, StockTransferOut = 8, Damage = 9, WriteOff = 10 }
