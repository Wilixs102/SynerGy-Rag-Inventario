using SynerGy.RAG.Application.DTOs;

namespace SynerGy.RAG.Application.Interfaces;

public interface IRagService
{
    /// <summary>Solo genera el SQL con el LLM, sin validarlo ni ejecutarlo.</summary>
    Task<(string Sql, long LatenciaMs)> GenerarSqlAsync(
        string pregunta,
        CancellationToken cancellationToken = default);

    Task<RespuestaRag> PreguntarAsync(
        string pregunta,
        CancellationToken cancellationToken = default);
}
