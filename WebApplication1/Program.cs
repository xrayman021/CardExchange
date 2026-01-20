using CardExchange.Api.Exchange;
using CardExchange.core.Domain;
using CardExchange.Core.Exchange;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// Exchange services (go here, before builder.Build())
builder.Services.AddSingleton<ExchangeState>();
builder.Services.AddSingleton<ExchangeHost>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ExchangeHost>());
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

//Endpoints
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/skus/sample", () =>
{
    var sku = new Sku(
        new SkuId("PKM-SV151-BB-EN"),
        "Pokemon",
        "Scarlet & Violet 151",
        "BoosterBox",
        "EN",
        "US"
    );
    return Results.Ok(sku);
});


//Exchange endpoints (go here, before app.Run())
app.MapPost("/accounts/{userId:guid}/deposit-cash", async (ExchangeHost host, Guid userId, DepositCashRequest req) =>
{
    var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    await host.Enqueue(new DepositCashCmd(userId, req.Cents, tcs));
    return Results.Ok(await tcs.Task);
});

app.MapPost("/accounts/{userId:guid}/deposit-inv", async (ExchangeHost host, Guid userId, DepositInvRequest req) =>
{
    var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    await host.Enqueue(new DepositInvCmd(userId, new SkuId(req.Sku), req.Qty, tcs));
    return Results.Ok(await tcs.Task);
});

app.MapGet("/accounts/{userId:guid}/balance", async (ExchangeHost host, Guid userId) =>
{
    var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    await host.Enqueue(new GetBalanceCmd(userId, tcs));
    return Results.Ok(await tcs.Task);
});
app.MapPost("/orders/limit", async (ExchangeHost host, PlaceLimitOrderRequest req) =>
{
    var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    await host.Enqueue(new PlaceLimitOrderCmd(req.UserId, req.Sku, req.Side, req.LimitPriceCents, req.Qty, tcs));
    return Results.Ok(await tcs.Task);
});

app.MapPost("/orders/{orderId:guid}/cancel", async (ExchangeHost host, Guid orderId, Guid userId) =>
{
    var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    await host.Enqueue(new CancelOrderCmd(userId, orderId, tcs));
    return Results.Ok(await tcs.Task);
});

app.MapGet("/orders/open/{userId:guid}", async (ExchangeHost host, Guid userId) =>
{
    var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    await host.Enqueue(new ListOpenOrdersCmd(userId, tcs));
    return Results.Ok(await tcs.Task);
});

app.MapGet("/book/top/{sku}", async (ExchangeHost host, string sku) =>
{
    var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    await host.Enqueue(new GetBookTopCmd(sku, tcs));
    return Results.Ok(await tcs.Task);
});

app.MapGet("/trades/{sku}", async (ExchangeHost host, string sku, int limit = 50) =>
{
    var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    await host.Enqueue(new GetTradesCmd(sku, limit, tcs));
    return Results.Ok(await tcs.Task);
});

app.MapGet("/book/{sku}", async (ExchangeHost host, string sku, int depth = 20) =>
{
    var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
    await host.Enqueue(new GetBookSnapshotCmd(sku, depth, tcs));
    return Results.Ok(await tcs.Task);
});



app.Run();

public sealed record DepositCashRequest(long Cents);
public sealed record DepositInvRequest(string Sku, int Qty);
public sealed record PlaceLimitOrderRequest(
    Guid UserId,
    string Sku,
    Side Side,
    long LimitPriceCents,
    int Qty
);
