namespace SynerGy.RAG.Application.Interfaces;

public interface IOllamaClient
{
    Task<string> GenerarAsync(
        string prompt,
        double? temperature = null,
        CancellationToken cancellationToken = default);
}
