using Microsoft.Extensions.Options;
using SynerGy.RAG.Application.DTOs;
using SynerGy.RAG.Application.Interfaces;
using SynerGy.RAG.Application.UseCases;
using SynerGy.RAG.Infrastructure.Configuration;
using SynerGy.RAG.Infrastructure.Persistence;
using SynerGy.RAG.Infrastructure.Rag;
using SynerGy.RAG.Infrastructure.Security;
using Xunit;

namespace SynerGy.RAG.Tests;

public class InventarioYActasTests
{
    [Theory]
    [InlineData("Dame los equipos de laptops")]
    [InlineData("Dame los equipos de las laptops")]
    [InlineData("Dame los equipos de mouses")]
    [InlineData("Dame los equipos de los ratones")]
    [InlineData("Busca actas de entrega")]
    [InlineData("Busca actas de devolución")]
    public void CategoriasPluralesYTiposActaNoSonPersonas(string pregunta)
        => Assert.Null(DetectarCustodio.Extraer(pregunta));

    [Theory]
    [InlineData("Dame el total de las laptops")]
    [InlineData("¿Cuántos mouses hay?")]
    [InlineData("Lista las laptops asignadas")]
    [InlineData("Muestra las 5 laptops disponibles")]
    [InlineData("Total de laptops y mouses")]
    public void ConsultasHabitualesCreanSqlValido(string pregunta)
    {
        var consulta = ConsultasInventario.Crear(pregunta);
        Assert.NotNull(consulta);
        Assert.Null(new SqlValidator(["VistaEquipo"]).Validar(consulta.Sql));
    }

    [Theory]
    [InlineData("¿Cuántas laptops DELL hay?")]
    [InlineData("¿Cuántas laptops se compraron en 2025?")]
    [InlineData("Lista las laptops que no están asignadas")]
    [InlineData("¿Cuál es el valor total de las laptops?")]
    [InlineData("Laptops por ubicación")]
    public void NoIgnoraFiltrosQueLaPlantillaNoEntiende(string pregunta)
        => Assert.Null(ConsultasInventario.Crear(pregunta));

    [Fact]
    public void MouseIncluyeKitsYLaptopExigeCategoriaCompatible()
    {
        Assert.DoesNotContain("NOT LIKE", ConsultasInventario.Mouse.Predicado);
        Assert.Contains("kit", ConsultasInventario.Mouse.Regla);
        Assert.Contains("COMPUTADOR", ConsultasInventario.Laptop.Predicado);
        Assert.DoesNotContain("OR Descripcion", ConsultasInventario.Laptop.Predicado);
    }

    [Fact]
    public async Task RutaValidadaNoEsperaGeneracionDelModelo()
    {
        var modelo = new ModeloNoInvocable();
        var consultas = new Consultas();
        var rag = new RagService(modelo, new SqlValidator(["VistaEquipo"]), consultas,
            new RagOptions(UsarConsultasValidadas: true));
        var respuesta = await rag.PreguntarAsync("Dame el total de las laptops");
        Assert.Null(respuesta.Error);
        Assert.Equal("consulta_validada", respuesta.MetodoGeneracion);
        Assert.Contains("COUNT(DISTINCT IdEquipo)", respuesta.Sql);
        Assert.Single(respuesta.Filas);
    }

    [Fact]
    public void TextoDeActaSeLimpiaSinIdentidadesNiHtml()
    {
        var texto = TextoActa.Preparar("Entrega", "<p>Yo <b>Ana López</b>, CI 1234567890, ana@example.test, declaro la custodia &amp; cuidado.</p>",
            ["Ana López", "1234567890", "ana@example.test"]);
        Assert.DoesNotContain("Ana", texto);
        Assert.DoesNotContain("1234567890", texto);
        Assert.DoesNotContain("@", texto);
        Assert.DoesNotContain("<p>", texto);
        Assert.Contains("custodia & cuidado", texto);
    }

    [Fact]
    public async Task PreguntaDeActasNoSeEjecutaComoInventario()
    {
        var actas = new Actas();
        var rag = new RagService(new ModeloNoInvocable(), new SqlValidator(["VistaEquipo"]), new Consultas(),
            new RagOptions(), actas: actas);
        var respuesta = await rag.PreguntarAsync("Busca actas sobre responsabilidad de custodia");
        Assert.True(actas.Llamado);
        Assert.Equal("busqueda_vectorial_actas", respuesta.MetodoGeneracion);
    }

    [Fact]
    public async Task TopKVectorialNoSePresentaComoTotalDeActas()
    {
        var servicio = new SqlBuscadorActas("invalida", "invalida", new EmbeddingNoInvocable(),
            Options.Create(new VectorOptions { ActasHabilitadas = true }));
        var respuesta = await servicio.BuscarAsync("¿Cuántas actas hay?", null);
        Assert.True(respuesta.RequiereAclaracion);
        Assert.Null(respuesta.Sql);
        Assert.Empty(respuesta.Filas);
    }

    [Fact]
    public async Task NingunaActaElegibleNoBuscaCoincidenciasDeOtraPersona()
    {
        var almacen = new SqlContextoStore("invalida", Options.Create(new VectorOptions()));
        Assert.Empty(await almacen.BuscarConIdsAsync([1], []));
    }

    private class ModeloNoInvocable : IOllamaClient
    {
        public Task<string> GenerarAsync(string prompt, double? temperature = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Esta prueba no debe invocar el generador.");
    }
    private class EmbeddingNoInvocable : IEmbeddingClient
    {
        public Task<float[]> GenerarEmbeddingAsync(string texto, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No se debe generar embedding para un conteo ambiguo de actas.");
    }
    private class Consultas : ISqlQueryService
    {
        public Task<bool> ProbarConexionAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IReadOnlyList<Dictionary<string, object?>>> ConsultarAsync(string sql, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>([new() { ["total"] = 10 }]);
    }
    private class Actas : IBuscadorActas
    {
        public bool Llamado;
        public Task<int> IndexarAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<RespuestaRag> BuscarAsync(string pregunta, Custodio? custodio, CancellationToken cancellationToken = default)
        {
            Llamado = true;
            return Task.FromResult(new RespuestaRag(pregunta, null, [], null, 0) { MetodoGeneracion = "busqueda_vectorial_actas" });
        }
    }
}
