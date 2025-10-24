using AutoMapper;
using OrderManagementAPI.Common.Mapping;
using OrderManagementAPI.Features.Orders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();
builder.Services.AddAutoMapper(typeof(AdvancedOrderMappingProfile).Assembly);

builder.Services.AddScoped<CreateOrderHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/orders", async (CreateOrderProfileRequest req, CreateOrderHandler handler, CancellationToken ct) =>
{
    var dto = await handler.HandleAsync(req, ct);
    return Results.Created($"/orders/{dto.Id}", dto);
})
.WithName("CreateOrder")
.WithOpenApi();

app.Run();
