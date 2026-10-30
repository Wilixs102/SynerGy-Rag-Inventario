using SynerGy.RAG.Application.Interfaces;
using SynerGy.RAG.Application.UseCases;
using SynerGy.RAG.Infrastructure.Configuration;
using SynerGy.RAG.Infrastructure.Ollama;
using SynerGy.RAG.Infrastructure.Persistence;
using SynerGy.RAG.Infrastructure.Security;
using SynerGy.RAG.Infrastructure.Rag;
using SynerGy.RAG.Application.DTOs;
using Microsoft.Extensions.Options;
using System.Text.Json;

var indexarCatalogo = args.Contains("--indexar-catalogo");
var indexarActas = args.Contains("--indexar-actas");
var builder = WebApplication.CreateBuilder(args.Where(a => a is not "--indexar-catalogo" and not "--indexar-actas").ToArray());

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
builder.Services.AddScoped<IDirectorioCustodios>(_ => new SqlDirectorioCustodios(connectionString));

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

var vectorOptions = builder.Configuration.GetSection(VectorOptions.Seccion).Get<VectorOptions>()
    ?? new VectorOptions();
vectorOptions.Validar();
builder.Services.AddSingleton<IOptions<VectorOptions>>(Options.Create(vectorOptions));
builder.Services.AddHttpClient<IEmbeddingClient, OllamaEmbeddingClient>(client =>
{
    client.BaseAddress = new Uri(ollamaOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(ollamaOptions.TimeoutSegundos);
});
builder.Services.AddScoped<IAlmacenContexto>(_ =>
{
    // El proceso de indexación usa credenciales distintas de la API de solo lectura.
    var nombre = indexarCatalogo || indexarActas ? "ContextoVectorialIndexacion" : "ContextoVectorial";
    var cadena = builder.Configuration.GetConnectionString(nombre);
    if (string.IsNullOrWhiteSpace(cadena) && !indexarCatalogo) cadena = connectionString;
    if (string.IsNullOrWhiteSpace(cadena) && (indexarCatalogo || vectorOptions.Habilitada))
        throw new InvalidOperationException($"Configura ConnectionStrings:{nombre}.");
    return new SqlContextoStore(cadena ?? string.Empty, Options.Create(vectorOptions));
});
builder.Services.AddScoped<IRecuperadorContexto, RecuperadorContexto>();
builder.Services.AddScoped<IndexadorCatalogo>();
builder.Services.AddScoped<IBuscadorActas>(sp =>
{
    var nombre = indexarActas ? "ContextoVectorialIndexacion" : "ContextoVectorial";
    var cadena = builder.Configuration.GetConnectionString(nombre);
    if (indexarActas && string.IsNullOrWhiteSpace(cadena))
        throw new InvalidOperationException("Configura ConnectionStrings:ContextoVectorialIndexacion antes de indexar actas.");
    if (string.IsNullOrWhiteSpace(cadena)) cadena = connectionString;
    return new SqlBuscadorActas(connectionString, cadena, sp.GetRequiredService<IEmbeddingClient>(), Options.Create(vectorOptions));
});

// Seguridad SQL: lista blanca de vistas consultables por el LLM
builder.Services.AddSingleton<ISqlValidator>(
    _ => new SqlValidator(["VistaEquipo"]));

builder.Services.AddSingleton(new RagOptions(EstrategiaPrompt.Ajustado,
    Temperature: builder.Configuration.GetValue("Rag:Temperature", 0.1),
    UsarConsultasValidadas: builder.Configuration.GetValue("Rag:UsarConsultasValidadas", true)));
builder.Services.AddScoped<IRagService, RagService>();

var app = builder.Build();

if (indexarActas)
{
    using var scope = app.Services.CreateScope();
    var cantidad = await scope.ServiceProvider.GetRequiredService<IBuscadorActas>().IndexarAsync();
    Console.WriteLine($"Actas indexadas: {cantidad}; versión {SqlBuscadorActas.Version}. No se modifica Acta ni ActaDetalle.");
    await app.DisposeAsync();
    return;
}

if (indexarCatalogo)
{
    using var scope = app.Services.CreateScope();
    var ruta = Path.Combine(AppContext.BaseDirectory, "data", "catalogo-inventario.json");
    var entradas = JsonSerializer.Deserialize<List<EntradaCatalogo>>(
        await File.ReadAllTextAsync(ruta), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Catálogo vacío.");
    await scope.ServiceProvider.GetRequiredService<IndexadorCatalogo>().IndexarAsync(entradas);
    Console.WriteLine($"Catálogo indexado: {entradas.Count} entradas, modelo {vectorOptions.ModeloEmbedding}, versión {vectorOptions.VersionCatalogo}.");
    await app.DisposeAsync();
    return;
}

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
