using SynerGy.RAG.Application.Interfaces;
using SynerGy.RAG.Application.UseCases;
using SynerGy.RAG.Infrastructure.Configuration;
using SynerGy.RAG.Infrastructure.Ollama;
using SynerGy.RAG.Infrastructure.Persistence;
using SynerGy.RAG.Infrastructure.Security;

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

// Ollama
builder.Services.Configure<OllamaOptions>(
    builder.Configuration.GetSection(OllamaOptions.Seccion));

var ollamaOptions =
    builder.Configuration.GetSection(OllamaOptions.Seccion).Get<OllamaOptions>()
    ?? new OllamaOptions();

builder.Services.AddHttpClient<IOllamaClient, OllamaClient>(client =>
{
    client.BaseAddress = new Uri(ollamaOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(ollamaOptions.TimeoutSegundos);
});

// Seguridad SQL: lista blanca de vistas consultables por el LLM
builder.Services.AddSingleton<ISqlValidator>(
    _ => new SqlValidator(["VistaEquipo"]));

builder.Services.AddScoped<IRagService, RagService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

// IMPORTANTE
app.Run();
