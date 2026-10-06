using System.Diagnostics;
using System.Text.RegularExpressions;
using SynerGy.RAG.Application.DTOs;
using SynerGy.RAG.Application.Interfaces;

namespace SynerGy.RAG.Application.UseCases;

/// <summary>
/// Text-to-SQL: pregunta -> prompt (baseline o ajustado) -> Ollama -> validación -> SQL Server.
/// </summary>
public class RagService : IRagService
{
    private const int MaxFilas = 100;

    private readonly IOllamaClient _ollama;
    private readonly ISqlValidator _validator;
    private readonly ISqlQueryService _sql;
    private readonly RagOptions _options;

    public RagService(
        IOllamaClient ollama,
        ISqlValidator validator,
        ISqlQueryService sql,
        RagOptions options)
    {
        _ollama = ollama;
        _validator = validator;
        _sql = sql;
        _options = options;
    }

    public async Task<(string Sql, long LatenciaMs)> GenerarSqlAsync(
        string pregunta,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var prompt = ConstruirPrompt(pregunta);
        var salida = await _ollama.GenerarAsync(prompt, _options.Temperature, cancellationToken);
        return (LimpiarSql(salida), sw.ElapsedMilliseconds);
    }

    public async Task<RespuestaRag> PreguntarAsync(
        string pregunta,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string? sql = null;

        try
        {
            (sql, _) = await GenerarSqlAsync(pregunta, cancellationToken);

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

    private string ConstruirPrompt(string pregunta)
    {
        pregunta = pregunta.Trim();

        if (_options.Estrategia == EstrategiaPrompt.Baseline)
            return Prompts.Baseline.Replace("{pregunta}", pregunta);

        var pistas = ConocimientoDominio.Recuperar(pregunta);
        var bloquePistas = pistas.Count == 0
            ? string.Empty
            : "\nPistas del dominio para esta pregunta:\n" + string.Join("\n", pistas.Select(p => "- " + p)) + "\n\n";

        return Prompts.Ajustado
            .Replace("{pistas}", bloquePistas)
            .Replace("{pregunta}", pregunta);
    }

    private static string LimpiarSql(string salida)
    {
        var texto = Regex.Replace(salida, @"```(?:sql)?", "", RegexOptions.IgnoreCase).Trim();
        return texto.TrimEnd(';', ' ', '\r', '\n');
    }
}
