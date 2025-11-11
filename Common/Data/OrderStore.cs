using System.Collections.Generic;
using OrderManagementAPI.Features.Orders;

namespace OrderManagementAPI.Common.Data;

public class OrderStore
{
    public List<Order> Orders { get; } = new();
}
