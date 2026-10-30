using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SynerGy.RAG.Application.DTOs;
using SynerGy.RAG.Application.Interfaces;
using SynerGy.RAG.Application.UseCases;
using SynerGy.RAG.Infrastructure.Configuration;
using SynerGy.RAG.Infrastructure.Ollama;
using SynerGy.RAG.Infrastructure.Persistence;
using SynerGy.RAG.Infrastructure.Rag;
using Xunit;

namespace SynerGy.RAG.Tests;

public class BusquedaVectorialTests
{
    [Fact]
    public async Task EmbeddingUsaApiEmbedModeloLocalYRechazaTruncamiento()
    {
        var handler = new Handler(async request =>
        {
            Assert.Equal("/api/embed", request.RequestUri!.AbsolutePath);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("nomic-embed-text", body.RootElement.GetProperty("model").GetString());
            Assert.Equal("compus portátiles", body.RootElement.GetProperty("input").GetString());
            Assert.False(body.RootElement.GetProperty("truncate").GetBoolean());
            return Json(HttpStatusCode.OK, new { embeddings = new[] { Vector() } });
        });
        var cliente = Cliente(handler);
        Assert.Equal(768, (await cliente.GenerarEmbeddingAsync("compus portátiles")).Length);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(3, false)]
    [InlineData(768, true)]
    public async Task EmbeddingRechazaDimensionesIncorrectasOVectorCero(int dimensiones, bool cero)
    {
        var vector = Enumerable.Repeat(cero ? 0f : 1f, dimensiones).ToArray();
        var cliente = Cliente(new Handler(_ => Task.FromResult(
            Json(HttpStatusCode.OK, new { embeddings = new[] { vector } }))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cliente.GenerarEmbeddingAsync("laptops"));
    }

    [Fact]
    public async Task ErrorOllamaNoSeConvierteEnVectorVacio()
    {
        var cliente = Cliente(new Handler(_ => Task.FromResult(Json(HttpStatusCode.NotFound, new { error = "modelo ausente" }))));
        await Assert.ThrowsAsync<HttpRequestException>(() => cliente.GenerarEmbeddingAsync("laptops"));
    }

    [Fact]
    public async Task VectorDeshabilitadoNoLlamaOllamaNiSql()
    {
        var embedding = new Embeddings();
        var almacen = new Almacen();
        var recuperador = new RecuperadorContexto(embedding, almacen, Options.Create(new VectorOptions()));
        Assert.Empty(await recuperador.RecuperarAsync("laptops"));
        Assert.Equal(0, embedding.Llamadas);
        Assert.Equal(0, almacen.Busquedas);
    }

    [Fact]
    public async Task VectorHabilitadoRecuperaAntesDeGenerarSqlYExponeEvidencia()
    {
        var almacen = new Almacen();
        var recuperador = new RecuperadorContexto(new Embeddings(), almacen,
            Options.Create(new VectorOptions { Habilitada = true }));
        var ollama = new Generador();
        var sql = new Consultas();
        var rag = new RagService(ollama, new Validador(), sql, new RagOptions(), recuperador);
        var resultado = await rag.PreguntarAsync("¿Cuántas compus portátiles están libres?");
        Assert.Contains("[categoria-laptop]", ollama.Prompt);
        Assert.Contains("compu portátil", ollama.Prompt);
        Assert.Single(resultado.Contexto);
        Assert.Equal(0.12, resultado.Contexto[0].Distancia);
        Assert.Single(resultado.Filas);
        Assert.Equal(1, sql.Llamadas);
        Assert.Equal(1, almacen.Busquedas);
    }

    [Fact]
    public async Task RechazoSqlConservaContextoYNoEjecutaConsulta()
    {
        var sql = new Consultas();
        var rag = new RagService(new Generador(), new Validador("objeto no permitido"), sql,
            new RagOptions(), new ContextoFijo());
        var resultado = await rag.PreguntarAsync("laptops");
        Assert.NotNull(resultado.Error);
        Assert.Single(resultado.Contexto);
        Assert.Equal(0, sql.Llamadas);
    }

    [Fact]
    public async Task FalloRecuperacionNoEjecutaNiGeneraSqlSinContexto()
    {
        var sql = new Consultas();
        var generador = new Generador();
        var rag = new RagService(generador, new Validador(), sql, new RagOptions(), new ContextoFijo(fallar: true));
        var resultado = await rag.PreguntarAsync("laptops");
        Assert.NotNull(resultado.Error);
        Assert.Null(resultado.Sql);
        Assert.Equal("", generador.Prompt);
        Assert.Equal(0, sql.Llamadas);
    }

    [Fact]
    public async Task BaselineNoRecuperaContextoAunqueHayaRecuperador()
    {
        var generador = new Generador();
        var rag = new RagService(generador, new Validador(), new Consultas(),
            new RagOptions(EstrategiaPrompt.Baseline), new ContextoFijo(fallar: true));
        await rag.GenerarSqlAsync("laptops");
        Assert.DoesNotContain("recuperado por similitud", generador.Prompt);
    }

    [Fact]
    public async Task CancelacionSePropagaSinGenerarSql()
    {
        var generador = new Generador();
        var rag = new RagService(generador, new Validador(), new Consultas(), new RagOptions(), new Cancelado());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rag.PreguntarAsync("laptops", new CancellationToken(true)));
        Assert.Equal("", generador.Prompt);
    }

    [Fact]
    public async Task IndexacionFallidaNoReemplazaCatalogoAnterior()
    {
        var almacen = new Almacen();
        var indexador = new IndexadorCatalogo(new Embeddings(fallarEn: 2), almacen);
        await Assert.ThrowsAsync<HttpRequestException>(() => indexador.IndexarAsync([
            new("uno", "estado", "disponible"), new("dos", "categoria", "laptop")]));
        Assert.False(almacen.Reemplazado);
    }

    [Fact]
    public async Task CatalogoRealEsValidoEIndexacionRecibeTodasLasEntradas()
    {
        var entradas = JsonSerializer.Deserialize<List<EntradaCatalogo>>(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "catalogo.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var almacen = new Almacen();
        var embedding = new Embeddings();
        await new IndexadorCatalogo(embedding, almacen).IndexarAsync(entradas);
        Assert.True(almacen.Reemplazado);
        Assert.Equal(entradas.Count, almacen.Entradas);
        Assert.Equal(entradas.Count, embedding.Llamadas);
    }

    [Fact]
    public async Task CatalogoConIdsDuplicadosSeRechazaAntesDeOllama()
    {
        var embedding = new Embeddings();
        await Assert.ThrowsAsync<ArgumentException>(() => new IndexadorCatalogo(embedding, new Almacen())
            .IndexarAsync([new("uno", "estado", "disponible"), new("UNO", "estado", "asignado")]));
        Assert.Equal(0, embedding.Llamadas);
    }

    [Fact]
    public async Task AlmacenRechazaVectorInvalidoAntesDeAbrirConexion()
    {
        var almacen = new SqlContextoStore("cadena inválida", Options.Create(new VectorOptions()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => almacen.BuscarAsync([1f, 2f]));
    }

    private static float[] Vector() => Enumerable.Repeat(0.1f, 768).ToArray();
    private static HttpResponseMessage Json(HttpStatusCode status, object value) => new(status)
        { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    private static OllamaEmbeddingClient Cliente(HttpMessageHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") }, Options.Create(new VectorOptions()));

    private class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => responder(request);
    }
    private class Embeddings(int fallarEn = -1) : IEmbeddingClient
    {
        public int Llamadas;
        public Task<float[]> GenerarEmbeddingAsync(string texto, CancellationToken cancellationToken = default)
        {
            if (++Llamadas == fallarEn) throw new HttpRequestException("Ollama no disponible");
            return Task.FromResult(Vector());
        }
    }
    private class Almacen : IAlmacenContexto
    {
        public bool Reemplazado;
        public int Entradas;
        public int Busquedas;
        public Task<IReadOnlyList<ContextoInventario>> BuscarAsync(float[] embedding, CancellationToken cancellationToken = default)
        {
            Busquedas++;
            return new ContextoFijo().RecuperarAsync("prueba", cancellationToken);
        }
        public Task ReemplazarCatalogoAsync(IReadOnlyList<(EntradaCatalogo Entrada, float[] Embedding)> entradas,
            CancellationToken cancellationToken = default)
        {
            Reemplazado = true;
            Entradas = entradas.Count;
            return Task.CompletedTask;
        }
    }
    private class ContextoFijo(bool fallar = false) : IRecuperadorContexto
    {
        public Task<IReadOnlyList<ContextoInventario>> RecuperarAsync(string pregunta, CancellationToken cancellationToken = default)
        {
            if (fallar) throw new InvalidOperationException("Catálogo no indexado");
            return Task.FromResult<IReadOnlyList<ContextoInventario>>([
                new("categoria-laptop", "categoria", "compu portátil corresponde a LAPTOP", 0.12)]);
        }
    }
    private class Cancelado : IRecuperadorContexto
    {
        public Task<IReadOnlyList<ContextoInventario>> RecuperarAsync(string pregunta, CancellationToken cancellationToken = default)
            => Task.FromCanceled<IReadOnlyList<ContextoInventario>>(cancellationToken);
    }
    private class Generador : IOllamaClient
    {
        public string Prompt = "";
        public Task<string> GenerarAsync(string prompt, double? temperature = null, CancellationToken cancellationToken = default)
        {
            Prompt = prompt;
            return Task.FromResult("SELECT COUNT(*) AS total FROM VistaEquipo");
        }
    }
    private class Validador(string? rechazo = null) : ISqlValidator
    {
        public string? Validar(string sql) => rechazo;
    }
    private class Consultas : ISqlQueryService
    {
        public int Llamadas;
        public Task<bool> ProbarConexionAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IReadOnlyList<Dictionary<string, object?>>> ConsultarAsync(string sql, CancellationToken cancellationToken = default)
        {
            Llamadas++;
            return Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>([new() { ["total"] = 123 }]);
        }
    }
}
