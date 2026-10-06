using System.Diagnostics;
using System.Text.RegularExpressions;
using SynerGy.RAG.Application.DTOs;
using SynerGy.RAG.Application.Interfaces;

namespace SynerGy.RAG.Application.UseCases;

/// <summary>
/// Baseline text-to-SQL: pregunta -> prompt simple -> Ollama -> validación -> SQL Server.
/// </summary>
public class RagService : IRagService
{
    private const int MaxFilas = 100;

    private const string PromptBaseline = """
        Eres un asistente que convierte preguntas en español a T-SQL (SQL Server).
        Usa únicamente esta vista:

        VistaEquipo(IdEquipo int, Serie varchar, ValorCompra decimal, FechaCompra datetime,
          NumeroFactura varchar, Qr varchar, NombreMarca varchar, NombreModelo varchar,
          IdEstadoAsignacion int, NombreCategoria varchar, Descripcion varchar,
          usuario_custodio_activo varchar, FechaAsignacion datetime, FechaDevolucion datetime,
          IdUbicacion int, IdEstadoAsignacionHistorial int)

        IdEstadoAsignacion: 1=ASIGNABLE, 2=ASIGNADO, 5=BODEGA, 8=BAJA, 9=DAÑADO, 10=EXTRAVIADO, 11=ROBO.

        Responde SOLO con una consulta SELECT, sin explicaciones ni markdown.

        Pregunta: {pregunta}
        SQL:
        """;

    private readonly IOllamaClient _ollama;
    private readonly ISqlValidator _validator;
    private readonly ISqlQueryService _sql;

    public RagService(
        IOllamaClient ollama,
        ISqlValidator validator,
        ISqlQueryService sql)
    {
        _ollama = ollama;
        _validator = validator;
        _sql = sql;
    }

    public async Task<RespuestaRag> PreguntarAsync(
        string pregunta,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string? sql = null;

        try
        {
            var prompt = PromptBaseline.Replace("{pregunta}", pregunta);
            var salida = await _ollama.GenerarAsync(prompt, null, cancellationToken);
            sql = LimpiarSql(salida);

            var rechazo = _validator.Validar(sql);
            if (rechazo is not null)
                return new RespuestaRag(pregunta, sql, [], $"SQL rechazado: {rechazo}", sw.ElapsedMilliseconds);

            var filas = await _sql.ConsultarAsync(sql, cancellationToken);
            return new RespuestaRag(pregunta, sql, filas.Take(MaxFilas).ToList(), null, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new RespuestaRag(pregunta, sql, [], ex.Message, sw.ElapsedMilliseconds);
        }
    }

    private static string LimpiarSql(string salida)
    {
        var texto = Regex.Replace(salida, @"```(?:sql)?", "", RegexOptions.IgnoreCase).Trim();
        return texto.TrimEnd(';', ' ', '\r', '\n');
    }
}
