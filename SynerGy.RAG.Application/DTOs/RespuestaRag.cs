namespace SynerGy.RAG.Application.DTOs;

public record RespuestaRag(
    string Pregunta,
    string? Sql,
    IReadOnlyList<Dictionary<string, object?>> Filas,
    string? Error,
    long LatenciaMs)
{
    public IReadOnlyList<ContextoInventario> Contexto { get; init; } = [];
    public bool RequiereAclaracion { get; init; }
    public string? Mensaje { get; init; }
    public Custodio? Custodio { get; init; }
    public IReadOnlyList<Custodio> Candidatos { get; init; } = [];
    public bool Truncado { get; init; }
    public string MetodoGeneracion { get; init; } = "sin_generacion";
}
