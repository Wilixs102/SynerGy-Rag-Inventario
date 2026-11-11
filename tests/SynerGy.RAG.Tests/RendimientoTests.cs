using SynerGy.RAG.Application.DTOs;
using SynerGy.RAG.Application.Interfaces;
using SynerGy.RAG.Application.UseCases;
using SynerGy.RAG.Infrastructure.Security;
using Xunit;

namespace SynerGy.RAG.Tests;

public class RendimientoTests
{
    [Theory]
    [InlineData("Que equipos tiene asignado Ana López")]
    [InlineData("Equipos de Ana López con marca y modelo")]
    [InlineData("Equipos de Ana López, incluye marca, modelo y serie")]
    public void ListaPersonalConservaDetalleCompleto(string pregunta)
    {
        var consulta = ConsultasInventario.Crear(pregunta, "Ana López");
        Assert.NotNull(consulta);
        Assert.Null(new SqlValidator(["VistaEquipo"]).Validar(consulta.Sql));
        Assert.DoesNotContain("COUNT(", consulta.Sql);
        foreach (var campo in new[] { "NombreCategoria", "NombreMarca", "NombreModelo", "Serie", "Descripcion", "FechaAsignacion", "ValorCompra", "FechaCompra", "NumeroFactura", "IdUbicacion" })
            Assert.Contains(campo, consulta.Sql);
    }

    [Theory]
    [InlineData("Que equipos tiene asignado Ana López")]
    [InlineData("Cuantos equipos tiene Ana López")]
    public async Task CompaneroResueltoUsaConsultaRestringidaSinLlmNiVectores(string pregunta)
    {
        var sql = new Sql(); var modelo = new Modelo { Fallar = true }; var contexto = new Contexto { Fallar = true };
        var rag = new RagService(modelo, new SqlValidator(["VistaEquipo"]), sql,
            new RagOptions(UsarConsultasValidadas: true), contexto, new Directorio());
        var r = await rag.PreguntarAsync(pregunta);
        Assert.Null(r.Error); Assert.Equal("consulta_validada", r.MetodoGeneracion);
        Assert.Equal("ana@example.test", sql.Correo);
        Assert.Equal(0, modelo.Llamadas); Assert.Equal(0, contexto.Llamadas);
    }

    [Theory]
    [InlineData("Equipos de Ana López con marca DELL")]
    [InlineData("Equipos de Ana López comprados en 2025")]
    [InlineData("Valor total de los equipos de Ana López")]
    public void RutaPersonalNoDescartaCondicionesAdicionales(string pregunta)
        => Assert.Null(ConsultasInventario.Crear(pregunta, "Ana López"));

    [Fact]
    public async Task TotalControladoFuncionaAunqueOllamaYVectoresNoRespondan()
    {
        var modelo = new Modelo { Fallar = true };
        var contexto = new Contexto { Fallar = true };
        var r = await Servicio(modelo, new Sql(), contexto).PreguntarAsync("Dame el total de las laptops");
        Assert.Null(r.Error);
        Assert.Equal("consulta_validada", r.MetodoGeneracion);
        Assert.Empty(r.Contexto);
        Assert.Equal(0, modelo.Llamadas);
        Assert.Equal(0, contexto.Llamadas);
    }

    [Fact]
    public async Task ReutilizarSqlVuelveAConsultarDatosActuales()
    {
        var modelo = new Modelo(); var sql = new Sql(); var rag = Servicio(modelo, sql, new Contexto());
        var primera = await rag.PreguntarAsync("Cuantos equipos hay en total");
        var segunda = await rag.PreguntarAsync("Cuantos equipos hay en total");
        Assert.Null(segunda.Error);
        Assert.False(primera.SqlReutilizado); Assert.True(segunda.SqlReutilizado);
        Assert.Equal(1, modelo.Llamadas); Assert.Equal(2, sql.Llamadas);
        Assert.Equal(1, primera.Filas[0]["total"]); Assert.Equal(2, segunda.Filas[0]["total"]);
    }

    [Fact]
    public async Task CambioDeContextoObligaANuevaGeneracion()
    {
        var modelo = new Modelo(); var contexto = new Contexto(); var rag = Servicio(modelo, new Sql(), contexto);
        await rag.PreguntarAsync("Cuantos equipos hay en total");
        contexto.Texto = "Nueva regla del catálogo";
        Assert.False((await rag.PreguntarAsync("Cuantos equipos hay en total")).SqlReutilizado);
        Assert.Equal(2, modelo.Llamadas);
    }

    [Fact]
    public async Task SqlRechazadoNoSeGuardaNiSeEjecuta()
    {
        var modelo = new Modelo { Texto = "DELETE FROM VistaEquipo" }; var sql = new Sql();
        var rag = Servicio(modelo, sql, new Contexto());
        Assert.NotNull((await rag.PreguntarAsync("Cuantos equipos hay en total")).Error);
        Assert.NotNull((await rag.PreguntarAsync("Cuantos equipos hay en total")).Error);
        Assert.Equal(2, modelo.Llamadas); Assert.Equal(0, sql.Llamadas);
    }

    [Fact]
    public async Task EvaluadorGeneraCadaMuestraSinCacheNiPlantilla()
    {
        var modelo = new Modelo(); var rag = Servicio(modelo, new Sql(), new Contexto());
        await rag.GenerarSqlAsync("Dame el total de las laptops");
        await rag.GenerarSqlAsync("Dame el total de las laptops");
        Assert.Equal(2, modelo.Llamadas);
    }

    [Fact]
    public async Task CacheNoPermiteOmitirLaValidacionEnCadaPeticion()
    {
        var cache = new CacheSqlGenerado(TimeSpan.FromMinutes(5));
        var modelo = new Modelo(); var sql = new Sql();
        var rag = Servicio(modelo, sql, new Contexto(), cache);
        await rag.PreguntarAsync("Cuantos equipos hay en total");
        var bloqueado = new RagService(modelo, new SqlValidator(["OtraVista"]), sql,
            new RagOptions(UsarConsultasValidadas: true), new Contexto(), cache: cache);
        var r = await bloqueado.PreguntarAsync("Cuantos equipos hay en total");
        Assert.NotNull(r.Error); Assert.Equal(1, modelo.Llamadas); Assert.Equal(1, sql.Llamadas);
    }

    [Fact]
    public void ExpiracionCapacidadYDesactivacionDeCache()
    {
        var reloj = new Reloj(); var cache = new CacheSqlGenerado(TimeSpan.FromSeconds(5), 1, reloj);
        cache.Guardar("a", "sql-a"); cache.Guardar("b", "sql-b");
        Assert.Null(cache.Buscar("a")); Assert.Equal("sql-b", cache.Buscar("b"));
        reloj.Ahora += TimeSpan.FromSeconds(5); Assert.Null(cache.Buscar("b"));
        var desactivada = new CacheSqlGenerado(TimeSpan.Zero);
        desactivada.Guardar("a", "sql"); Assert.Null(desactivada.Buscar("a"));
    }

    private static RagService Servicio(Modelo modelo, Sql sql, Contexto contexto, CacheSqlGenerado? cache = null) =>
        new(modelo, new SqlValidator(["VistaEquipo"]), sql, new RagOptions(UsarConsultasValidadas: true),
            contexto, cache: cache ?? new CacheSqlGenerado(TimeSpan.FromMinutes(5)));
    private class Reloj : TimeProvider
    {
        public DateTimeOffset Ahora = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
    private class Modelo : IOllamaClient
    {
        public int Llamadas; public bool Fallar; public string Texto = "SELECT COUNT(*) AS total FROM VistaEquipo";
        public Task<string> GenerarAsync(string prompt, double? temperature = null, CancellationToken cancellationToken = default)
        { Llamadas++; if (Fallar) throw new InvalidOperationException("Modelo no disponible"); return Task.FromResult(Texto); }
    }
    private class Contexto : IRecuperadorContexto
    {
        public int Llamadas; public bool Fallar; public string Texto = "Inventario actual";
        public Task<IReadOnlyList<ContextoInventario>> RecuperarAsync(string pregunta, CancellationToken cancellationToken = default)
        { Llamadas++; if (Fallar) throw new InvalidOperationException("Vectores no disponibles");
          return Task.FromResult<IReadOnlyList<ContextoInventario>>([new("regla", "dominio", Texto, 0.1)]); }
    }
    private class Sql : ISqlQueryService
    {
        public int Llamadas;
        public string? Correo;
        public Task<IReadOnlyList<Dictionary<string, object?>>> ConsultarCustodioAsync(string sql, string correo, CancellationToken cancellationToken = default)
        { Correo = correo; return ConsultarAsync(sql, cancellationToken); }
        public Task<bool> ProbarConexionAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IReadOnlyList<Dictionary<string, object?>>> ConsultarAsync(string sql, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>([new() { ["total"] = ++Llamadas }]);
    }
    private class Directorio : IDirectorioCustodios
    {
        public Task<IReadOnlyList<Custodio>> BuscarAsync(string nombreOCorreo, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Custodio>>([new("Ana López", "ana@example.test")]);
    }
}
