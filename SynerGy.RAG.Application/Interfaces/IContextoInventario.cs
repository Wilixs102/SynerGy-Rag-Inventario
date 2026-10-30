using SynerGy.RAG.Application.DTOs;

namespace SynerGy.RAG.Application.Interfaces;

public interface IEmbeddingClient
{
    Task<float[]> GenerarEmbeddingAsync(string texto, CancellationToken cancellationToken = default);
}

public interface IAlmacenContexto
{
    Task<IReadOnlyList<ContextoInventario>> BuscarAsync(
        float[] embedding, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContextoInventario>> BuscarConIdsAsync(
        float[] embedding, IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("El almacén requiere búsqueda filtrada por IDs.");

    Task ReemplazarCatalogoAsync(
        IReadOnlyList<(EntradaCatalogo Entrada, float[] Embedding)> entradas,
        CancellationToken cancellationToken = default);
}

public interface IRecuperadorContexto
{
    Task<IReadOnlyList<ContextoInventario>> RecuperarAsync(
        string pregunta, CancellationToken cancellationToken = default);
}
