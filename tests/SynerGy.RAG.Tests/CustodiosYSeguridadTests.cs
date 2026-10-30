using SynerGy.RAG.Application.DTOs;
using SynerGy.RAG.Application.Interfaces;
using SynerGy.RAG.Application.UseCases;
using SynerGy.RAG.Infrastructure.Security;
using Xunit;

namespace SynerGy.RAG.Tests;

public class CustodiosYSeguridadTests
{
    [Theory]
    [InlineData("Muéstrame los equipos asignados a María Pérez", "María Pérez")]
    [InlineData("Dame los equipos asignados de Juan Pérez", "Juan Pérez")]
    [InlineData("¿Qué equipos tiene Ana López?", "Ana López")]
    [InlineData("¿Cuántos equipos tiene Ana López?", "Ana López")]
    [InlineData("Equipos del compañero José Muñoz", "José Muñoz")]
    [InlineData("Equipos de María Pérez, incluye marca y serie", "María Pérez")]
    [InlineData("Laptops asignadas a Ana López con marca HP", "Ana López")]
    public void DetectaNombresEnPreguntasHabituales(string pregunta, string nombre)
        => Assert.Equal(nombre, DetectarCustodio.Extraer(pregunta)?.Texto);

    [Theory]
    [InlineData("¿Cuántos equipos de la marca HP hay?")]
    [InlineData("Dame los equipos de bodega")]
    [InlineData("¿Cuántas laptops hay asignadas?")]
    public void NoConfundeFiltrosConPersonas(string pregunta)
        => Assert.Null(DetectarCustodio.Extraer(pregunta));

    [Fact]
    public void CorreoExplicitoTienePrioridad()
        => Assert.Equal(new SolicitudCustodio("ana@example.test", true),
            DetectarCustodio.Extraer("Equipos asignados a ana@example.test"));

    [Theory]
    [InlineData("SELECT COUNT(*) AS total FROM VistaEquipo")]
    [InlineData("SELECT TOP (10) Serie FROM dbo.VistaEquipo ORDER BY ValorCompra DESC")]
    [InlineData("SELECT SUM(ValorCompra) AS total FROM [dbo].[VistaEquipo] WHERE YEAR(FechaCompra)=2025")]
    [InlineData("SELECT NombreMarca FROM VistaEquipo GROUP BY NombreMarca")]
    public void ParserAceptaConsultasLegitimas(string sql)
        => Assert.Null(new SqlValidator(["VistaEquipo"]).Validar(sql));

    [Theory]
    [InlineData("SELECT t.name FROM VistaEquipo v, sys.tables t")]
    [InlineData("SELECT * FROM VistaEquipo SELECT name FROM \"sys\".\"tables\"")]
    [InlineData("SELECT * FROM VistaEquipo ORDER BY IdEquipo LIMIT 1")]
    [InlineData("SELECT * INTO copia FROM VistaEquipo")]
    [InlineData("SELECT * FROM otraBase.dbo.VistaEquipo")]
    [InlineData("SELECT * FROM dbo.Colaborador")]
    [InlineData("SELECT @@VERSION FROM VistaEquipo")]
    [InlineData("SELECT dbo.unaFuncion(Serie) FROM VistaEquipo")]
    [InlineData("SELECT * FROM VistaEquipo WITH (NOLOCK)")]
    [InlineData("SELECT * FROM VistaEquipo; DELETE FROM VistaEquipo")]
    public void ParserBloqueaEvasionesYFuentesNoAutorizadas(string sql)
        => Assert.NotNull(new SqlValidator(["VistaEquipo"]).Validar(sql));

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task NombreNoResueltoPideAclaracionSinLlamarModeloNiInventario(int cantidad)
    {
        var modelo = new Modelo();
        var sql = new Consultas();
        var rag = new RagService(modelo, new SqlValidator(["VistaEquipo"]), sql, new RagOptions(),
            directorio: new Directorio(cantidad));
        var respuesta = await rag.PreguntarAsync("Equipos asignados a Ana López");
        Assert.True(respuesta.RequiereAclaracion);
        Assert.Null(respuesta.Sql);
        Assert.False(modelo.Llamado);
        Assert.Null(sql.Correo);
        Assert.Equal(cantidad, respuesta.Candidatos.Count);
        if (cantidad > 0) Assert.Contains("@example.test", respuesta.Candidatos[0].PreguntaSugerida);
    }

    [Fact]
    public async Task PersonaUnicaUsaCorreoRealYCanalSqlRestringido()
    {
        var modelo = new Modelo();
        var sql = new Consultas();
        var rag = new RagService(modelo, new SqlValidator(["VistaEquipo"]), sql, new RagOptions(),
            directorio: new Directorio(1));
        var respuesta = await rag.PreguntarAsync("Equipos asignados a Ana López");
        Assert.Null(respuesta.Error);
        Assert.Equal("persona0@example.test", sql.Correo);
        Assert.Contains("persona0@example.test", modelo.Prompt);
        Assert.Equal("Ana López", respuesta.Custodio?.Nombre);
    }

    private class Directorio(int cantidad) : IDirectorioCustodios
    {
        public Task<IReadOnlyList<Custodio>> BuscarAsync(string nombreOCorreo, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Custodio>>(Enumerable.Range(0, cantidad)
                .Select(i => new Custodio("Ana López", $"persona{i}@example.test")).ToList());
    }
    private class Modelo : IOllamaClient
    {
        public bool Llamado;
        public string Prompt = "";
        public Task<string> GenerarAsync(string prompt, double? temperature = null, CancellationToken cancellationToken = default)
        {
            Llamado = true;
            Prompt = prompt;
            // Aun cuando el modelo omita el filtro, el canal SQL debe restringir la fuente.
            return Task.FromResult("SELECT IdEquipo, Serie FROM VistaEquipo");
        }
    }
    private class Consultas : ISqlQueryService
    {
        public string? Correo;
        public Task<bool> ProbarConexionAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IReadOnlyList<Dictionary<string, object?>>> ConsultarAsync(string sql, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No debe usar el canal general para una persona resuelta.");
        public Task<IReadOnlyList<Dictionary<string, object?>>> ConsultarCustodioAsync(string sql, string correo,
            CancellationToken cancellationToken = default)
        {
            Correo = correo;
            return Task.FromResult<IReadOnlyList<Dictionary<string, object?>>>([]);
        }
    }
}
