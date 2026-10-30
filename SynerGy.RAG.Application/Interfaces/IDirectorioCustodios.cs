using SynerGy.RAG.Application.DTOs;

namespace SynerGy.RAG.Application.Interfaces;

public interface IDirectorioCustodios
{
    Task<IReadOnlyList<Custodio>> BuscarAsync(string nombreOCorreo, CancellationToken cancellationToken = default);
}
