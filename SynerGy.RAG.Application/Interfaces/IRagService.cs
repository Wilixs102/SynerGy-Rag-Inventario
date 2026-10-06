using SynerGy.RAG.Application.DTOs;

namespace SynerGy.RAG.Application.Interfaces;

public interface IRagService
{
    Task<RespuestaRag> PreguntarAsync(
        string pregunta,
        CancellationToken cancellationToken = default);
}
