using System.Collections.Generic;
using OrderManagementAPI.Features.Orders;

namespace OrderManagementAPI.Common.Data;

public class OrderStore
{
    // „DB” in-memory – sursa unică de adevăr pentru Orders
    public List<Order> Orders { get; } = new();
}
