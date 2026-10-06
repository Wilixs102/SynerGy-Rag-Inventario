using SynerGy.RAG.Application.Interfaces;
using SynerGy.RAG.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// OpenAPI / Swagger
builder.Services.AddOpenApi();

// Cadena de conexión
var connectionString =
    builder.Configuration
        .GetConnectionString("ActivosInformaticos")
    ?? throw new InvalidOperationException(
        "No se encontró la cadena de conexión ActivosInformaticos."
    );

// Dependency Injection
builder.Services.AddScoped<ISqlQueryService>(
    _ => new SqlQueryService(connectionString)
);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// IMPORTANTE
app.Run();
