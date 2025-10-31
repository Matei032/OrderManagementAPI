using AutoMapper;
using OrderManagementAPI.Common.Mapping;
using OrderManagementAPI.Common.Middleware;
using OrderManagementAPI.Features.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();
builder.Services.AddAutoMapper(typeof(AdvancedOrderMappingProfile).Assembly);

builder.Services.AddScoped<CreateOrderHandler>();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCorrelation();

app.MapPost("/orders", async (CreateOrderProfileRequest req, CreateOrderHandler handler, CancellationToken ct) =>
{
    var dto = await handler.HandleAsync(req, ct);
    return Results.Created($"/orders/{dto.Id}", dto);
})
.WithName("CreateOrder")
.WithOpenApi();

app.MapGet("/orders", (CreateOrderHandler handler) =>
{
    var list = handler.GetAll();
    return Results.Ok(list);
})
.WithName("GetAllOrders")
.WithOpenApi();

app.Run();
