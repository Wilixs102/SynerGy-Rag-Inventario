using Microsoft.Extensions.Options;
using SynerGy.RAG.Application.DTOs;
using SynerGy.RAG.Application.Interfaces;
using SynerGy.RAG.Infrastructure.Configuration;

namespace SynerGy.RAG.Infrastructure.Rag;

public class RecuperadorContexto(
    IEmbeddingClient embeddings, IAlmacenContexto almacen, IOptions<VectorOptions> options) : IRecuperadorContexto
{
    public async Task<IReadOnlyList<ContextoInventario>> RecuperarAsync(
        string pregunta, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Habilitada) return [];
        var vector = await embeddings.GenerarEmbeddingAsync(pregunta, cancellationToken);
        return await almacen.BuscarAsync(vector, cancellationToken);
    }
}
