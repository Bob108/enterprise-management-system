using EMS.Domain.Entities;
using EMS.Shared.Enums;

namespace EMS.Domain.Repositories;

public sealed record PurchaseRequestSearchCriteria(
    string? Search,
    PurchaseRequestStatus? Status,
    int? RequesterUserId,
    int Page,
    int PageSize);

public sealed record PurchaseOrderSearchCriteria(
    string? Search,
    PurchaseOrderStatus? Status,
    int Page,
    int PageSize);

public interface IProcurementRepository
{
    // Purchase requests
    Task<(IReadOnlyList<PurchaseRequest> Items, int TotalCount)> SearchRequestsAsync(
        PurchaseRequestSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>Tracked, with lines (incl. category/item refs) and department loaded.</summary>
    Task<PurchaseRequest?> GetRequestByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<string> GetNextRequestNumberAsync(CancellationToken cancellationToken = default);

    void AddRequest(PurchaseRequest request);

    void SetOriginalRowVersion(PurchaseRequest request, byte[] rowVersion);

    // Purchase orders
    Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> SearchOrdersAsync(
        PurchaseOrderSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>Tracked, with lines, GRNs (incl. warehouses), supplier and the source request loaded.</summary>
    Task<PurchaseOrder?> GetOrderByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<string> GetNextOrderNumberAsync(CancellationToken cancellationToken = default);

    Task<string> GetNextGrnNumberAsync(CancellationToken cancellationToken = default);

    void AddOrder(PurchaseOrder order);

    void SetOriginalRowVersion(PurchaseOrder order, byte[] rowVersion);
}
