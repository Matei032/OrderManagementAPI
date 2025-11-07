using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OrderManagementAPI.Common.Data;

namespace OrderManagementAPI.Features.Orders;

public class InMemoryOrderReadRepository : IOrderReadRepository
{
    private readonly OrderStore _store;
    public InMemoryOrderReadRepository(OrderStore store) => _store = store;

    public Task<bool> IsIsbnUniqueAsync(string normalizedIsbn, CancellationToken ct) =>
        Task.FromResult(!_store.Orders.Any(o => Normalize(o.ISBN) == normalizedIsbn));

    public Task<bool> IsTitleUniqueForAuthorAsync(string title, string author, CancellationToken ct) =>
        Task.FromResult(!_store.Orders.Any(o =>
            string.Equals(o.Title, title, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(o.Author, author, StringComparison.OrdinalIgnoreCase)));

    public Task<int> CountOrdersAddedOnDateAsync(DateTime utcDate, CancellationToken ct)
    {
        var day = utcDate.Date;
        return Task.FromResult(_store.Orders.Count(o => o.CreatedAt.Date == day));
    }

    private static string Normalize(string s) => s.Replace("-", "").Replace(" ", "");
}
