using CardExchange.core.Domain;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.Run();
