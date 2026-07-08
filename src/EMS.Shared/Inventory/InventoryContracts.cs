using EMS.Shared.Enums;

namespace EMS.Shared.Inventory;

public sealed record WarehouseDto(int Id, string Name, string Code, string? Location, int StockedItemCount);

public sealed record SaveWarehouseRequest(string Name, string Code, string? Location);

public sealed record InventoryItemListDto(
    int Id,
    string ItemCode,
    string Name,
    string? Category,
    string Unit,
    int TotalOnHand,
    int TotalMinimum,
    bool IsBelowMinimum,
    int WarehouseCount);

public sealed record StockLevelDto(int WarehouseId, string WarehouseName, int Quantity, int MinimumQuantity);

public sealed record InventoryItemDetailDto(
    int Id,
    string ItemCode,
    string Name,
    string? Category,
    string Unit,
    string? Description,
    IReadOnlyList<StockLevelDto> StockLevels,
    string RowVersion);

/// <summary>Minimum stock threshold per warehouse (design §6.4: min level per warehouse).</summary>
public sealed record WarehouseMinimum(int WarehouseId, int MinimumQuantity);

public sealed record CreateInventoryItemRequest(
    string Name,
    string? Category,
    string Unit,
    string? Description,
    List<WarehouseMinimum> Minimums);

public sealed record UpdateInventoryItemRequest(
    string Name,
    string? Category,
    string Unit,
    string? Description,
    List<WarehouseMinimum> Minimums,
    string RowVersion);

public sealed record StockMovementRequest(int WarehouseId, int Quantity, string? Reason, string? Reference);

/// <summary>Signed delta: positive adds stock, negative removes it (never below zero).</summary>
public sealed record StockAdjustmentRequest(int WarehouseId, int QuantityChange, string? Reason);

public sealed record InventoryTransactionDto(
    long Id,
    InventoryTransactionType Type,
    string WarehouseName,
    int QuantityChange,
    string? Reason,
    string? Reference,
    DateTime PerformedAtUtc,
    string? PerformedBy);
