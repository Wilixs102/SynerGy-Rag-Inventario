namespace SynerGy.RAG.Application.DTOs;

public record RespuestaRag(
    string Pregunta,
    string? Sql,
    IReadOnlyList<Dictionary<string, object?>> Filas,
    string? Error,
    long LatenciaMs);
