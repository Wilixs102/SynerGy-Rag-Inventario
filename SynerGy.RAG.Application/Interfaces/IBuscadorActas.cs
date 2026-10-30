using SynerGy.RAG.Application.DTOs;

namespace SynerGy.RAG.Application.Interfaces;

public interface IBuscadorActas
{
    Task<int> IndexarAsync(CancellationToken cancellationToken = default);
    Task<RespuestaRag> BuscarAsync(string pregunta, Custodio? custodio, CancellationToken cancellationToken = default);
}
