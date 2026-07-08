using EMS.Shared.Enums;

namespace EMS.Shared.Assets;

public sealed record AssetCategoryDto(
    int Id, string Name, string CodePrefix, DepreciationMethod Method,
    int UsefulLifeMonths, decimal ResidualRate, string? Description, int AssetCount);

public sealed record SaveAssetCategoryRequest(
    string Name, string CodePrefix, DepreciationMethod Method,
    int UsefulLifeMonths, decimal ResidualRate, string? Description);

public sealed record SupplierDto(
    int Id, string Name, string? ContactPerson, string? Email, string? Phone, string? Address, int AssetCount);

public sealed record SaveSupplierRequest(
    string Name, string? ContactPerson, string? Email, string? Phone, string? Address);

public sealed record AssetListItemDto(
    int Id,
    string AssetCode,
    string Name,
    string CategoryName,
    string DepartmentName,
    AssetStatus Status,
    string? AssigneeName,
    DateOnly PurchaseDate,
    decimal PurchaseCost,
    decimal BookValue);

public sealed record AssetAssignmentDto(
    int Id, string EmployeeName, DateOnly AssignedOn, DateOnly? ReturnedOn,
    string? ConditionOut, string? ConditionIn);

public sealed record AssetTransferDto(
    int Id, string FromDepartment, string ToDepartment, DateOnly TransferredOn, string? Reason);

public sealed record AssetDisposalDto(
    DateOnly DisposedOn, DisposalMethod Method, decimal? Proceeds, decimal GainLoss, string? Reason);

public sealed record AssetDetailDto(
    int Id,
    string AssetCode,
    string Name,
    int CategoryId,
    string CategoryName,
    int DepartmentId,
    string DepartmentName,
    int? SupplierId,
    string? SupplierName,
    string? SerialNumber,
    string? Model,
    DateOnly PurchaseDate,
    decimal PurchaseCost,
    decimal BookValue,
    DateOnly? WarrantyExpiryDate,
    AssetStatus Status,
    int? AssigneeEmployeeId,
    string? AssigneeName,
    string? Notes,
    IReadOnlyList<AssetAssignmentDto> Assignments,
    IReadOnlyList<AssetTransferDto> Transfers,
    AssetDisposalDto? Disposal,
    string RowVersion);

public sealed record RegisterAssetRequest(
    string Name,
    int CategoryId,
    int DepartmentId,
    int? SupplierId,
    string? SerialNumber,
    string? Model,
    DateOnly PurchaseDate,
    decimal PurchaseCost,
    DateOnly? WarrantyExpiryDate,
    string? Notes);

public sealed record UpdateAssetRequest(
    string Name,
    int DepartmentId,
    int? SupplierId,
    string? SerialNumber,
    string? Model,
    DateOnly PurchaseDate,
    decimal PurchaseCost,
    DateOnly? WarrantyExpiryDate,
    string? Notes,
    string RowVersion);

public sealed record AssignAssetRequest(int EmployeeId, string? ConditionNotes);

public sealed record ReturnAssetRequest(string? ConditionNotes);

public sealed record TransferAssetRequest(int ToDepartmentId, string? Reason);

public sealed record DisposeAssetRequest(DisposalMethod Method, decimal? Proceeds, string? Reason);
