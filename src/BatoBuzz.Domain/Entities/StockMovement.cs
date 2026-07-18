using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Domain.Entities;

/// <summary>
/// Records an individual stock movement (in, out, adjustment, transfer).
/// These records are immutable and form the audit trail for inventory.
/// </summary>
public class StockMovement : AuditableEntity
{
    public Guid ItemId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public MovementType MovementType { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal TotalCost { get; private set; }
    public decimal BalanceQuantity { get; private set; }
    public decimal BalanceValue { get; private set; }
    public DateTime MovementDate { get; private set; }
    public Guid? SourceDocumentId { get; private set; }
    public string? SourceDocumentType { get; private set; }
    public string? BatchNumber { get; private set; }
    public DateTime? ExpiryDate { get; private set; }
    public string? Narration { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public Guid? ReversedByStockMovementId { get; private set; }

    // Navigation
    public Item Item { get; private set; } = null!;
    public Warehouse Warehouse { get; private set; } = null!;

    private StockMovement() { }

    public static StockMovement Create(
        Guid companyId,
        Guid itemId,
        Guid warehouseId,
        MovementType movementType,
        decimal quantity,
        decimal unitCost,
        decimal balanceQuantity,
        decimal balanceValue,
        DateTime movementDate,
        Guid? sourceDocumentId = null,
        string? sourceDocumentType = null,
        string? batchNumber = null,
        DateTime? expiryDate = null,
        string? narration = null,
        decimal? totalCostOverride = null)
    {
        if (companyId == Guid.Empty || itemId == Guid.Empty || warehouseId == Guid.Empty)
            throw new ArgumentException("Company, item, and warehouse are required.");
        if (!Enum.IsDefined(movementType))
            throw new ArgumentOutOfRangeException(nameof(movementType), "Movement type is invalid.");
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Movement quantity must be greater than zero.");
        if (unitCost < 0)
            throw new ArgumentOutOfRangeException(nameof(unitCost), "Movement unit cost cannot be negative.");

        return new StockMovement
        {
            CompanyId = companyId,
            ItemId = itemId,
            WarehouseId = warehouseId,
            MovementType = movementType,
            Quantity = quantity,
            UnitCost = unitCost,
            TotalCost = Money.Round(totalCostOverride ?? quantity * unitCost),
            BalanceQuantity = balanceQuantity,
            BalanceValue = Money.Round(balanceValue),
            MovementDate = movementDate.Date,
            SourceDocumentId = sourceDocumentId,
            SourceDocumentType = sourceDocumentType,
            BatchNumber = batchNumber,
            ExpiryDate = expiryDate,
            Narration = narration
        };
    }

    public void AttachPostedJournal(Guid journalEntryId)
    {
        if (journalEntryId == Guid.Empty)
            throw new ArgumentException("Posted journal entry ID is required.", nameof(journalEntryId));
        if (JournalEntryId.HasValue && JournalEntryId != journalEntryId)
            throw new InvalidOperationException("The stock movement is already linked to another posted journal.");
        JournalEntryId = journalEntryId;
    }

    public void MarkReversed(Guid reversalStockMovementId, Guid modifiedByUserId)
    {
        if (reversalStockMovementId == Guid.Empty)
            throw new ArgumentException("Reversal stock movement ID is required.", nameof(reversalStockMovementId));
        if (ReversedByStockMovementId.HasValue)
            throw new InvalidOperationException("This stock movement has already been reversed.");
        ReversedByStockMovementId = reversalStockMovementId;
        SetModifiedBy(modifiedByUserId);
    }
}
