using AutoMapper;
using FluentValidation;
using OrderManagementAPI.Common.Data;
using OrderManagementAPI.Common.Mapping;
using OrderManagementAPI.Common.Middleware;
using OrderManagementAPI.Features.Orders;
using OrderManagementAPI.Validators;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAutoMapper(typeof(AdvancedOrderMappingProfile).Assembly);

builder.Services.AddSingleton<OrderStore>();
builder.Services.AddScoped<IOrderReadRepository, InMemoryOrderReadRepository>();

builder.Services.AddScoped<CreateOrderHandler>();

builder.Services.AddScoped<CreateOrderProfileValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderProfileValidator>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCorrelation();

app.UseGlobalExceptionHandling();

app.MapPost("/orders", async (CreateOrderProfileRequest req,
                               CreateOrderHandler handler,
                               CreateOrderProfileValidator validator,
                               CancellationToken ct) =>
{
    var result = await validator.ValidateAsync(req, ct);
    if (!result.IsValid)
    {
        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return Results.ValidationProblem(errors);
    }

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
