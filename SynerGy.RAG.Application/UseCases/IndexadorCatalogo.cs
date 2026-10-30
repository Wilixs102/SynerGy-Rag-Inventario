using SynerGy.RAG.Application.DTOs;
using SynerGy.RAG.Application.Interfaces;

namespace SynerGy.RAG.Application.UseCases;

public class IndexadorCatalogo(IEmbeddingClient embeddings, IAlmacenContexto almacen)
{
    public async Task IndexarAsync(
        IReadOnlyList<EntradaCatalogo> entradas, CancellationToken cancellationToken = default)
    {
        if (entradas.Count == 0 || entradas.Any(e => e is null ||
            string.IsNullOrWhiteSpace(e.Id) || e.Id.Length > 100 ||
            string.IsNullOrWhiteSpace(e.Tipo) || e.Tipo.Length > 32 ||
            string.IsNullOrWhiteSpace(e.Contenido) || e.Contenido.Length > 4000) ||
            entradas.Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entradas.Count)
            throw new ArgumentException("Catálogo inválido: entradas obligatorias, IDs únicos y límites 100/32/4000.");

        var indexadas = new List<(EntradaCatalogo Entrada, float[] Embedding)>();
        foreach (var entrada in entradas)
        {
            var vector = await embeddings.GenerarEmbeddingAsync(
                $"{entrada.Tipo}: {entrada.Contenido}", cancellationToken);
            indexadas.Add((entrada, vector));
        }
        // No modifica el catálogo anterior si Ollama falla antes de completar los embeddings.
        await almacen.ReemplazarCatalogoAsync(indexadas, cancellationToken);
    }
}
