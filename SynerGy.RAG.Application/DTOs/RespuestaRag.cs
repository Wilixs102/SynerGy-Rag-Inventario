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
    public bool SqlReutilizado { get; init; }
    public string MetodoGeneracion { get; init; } = "sin_generacion";
    public EvidenciaVectorial? BusquedaVectorial { get; init; }
}

public record EvidenciaVectorial(string Tabla, string VersionCatalogo, string Modelo,
    int Dimensiones, int TopK, double DistanciaMaxima, int ActasElegibles,
    string Metrica = "cosine", string Funcion = "VECTOR_DISTANCE", string Estrategia = "exacta");
