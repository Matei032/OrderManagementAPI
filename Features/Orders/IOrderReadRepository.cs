using System;
using System.Threading;
using System.Threading.Tasks;

namespace OrderManagementAPI.Features.Orders;

public interface IOrderReadRepository
{
    Task<bool> IsIsbnUniqueAsync(string normalizedIsbn, CancellationToken ct);
    Task<bool> IsTitleUniqueForAuthorAsync(string title, string author, CancellationToken ct);
    Task<int> CountOrdersAddedOnDateAsync(DateTime utcDate, CancellationToken ct);
}
