namespace SynerGy.RAG.Infrastructure.Configuration;

public class VectorOptions
{
    public const string Seccion = "BusquedaVectorial";
    public const int Dimensiones = 768;
    public bool Habilitada { get; set; }
    public string ModeloEmbedding { get; set; } = "nomic-embed-text";
    // Cambiar esta versión al reindexar con otros pesos del modelo o contenido.
    public string VersionCatalogo { get; set; } = "inventario-v2";
    public bool ActasHabilitadas { get; set; }
    public int TopK { get; set; } = 5;
    public double DistanciaMaxima { get; set; } = 0.65;

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(ModeloEmbedding) || ModeloEmbedding.Length > 100 ||
            string.IsNullOrWhiteSpace(VersionCatalogo) || VersionCatalogo.Length > 100 ||
            TopK is < 1 or > 20 || !double.IsFinite(DistanciaMaxima) ||
            DistanciaMaxima is < 0 or > 2)
            throw new InvalidOperationException("Configuración de búsqueda vectorial inválida.");
    }
}
