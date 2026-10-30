using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SynerGy.RAG.Application.Interfaces;
using SynerGy.RAG.Infrastructure.Configuration;

namespace SynerGy.RAG.Infrastructure.Ollama;

public class OllamaEmbeddingClient(HttpClient http, IOptions<VectorOptions> options) : IEmbeddingClient
{
    public async Task<float[]> GenerarEmbeddingAsync(
        string texto, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texto);
        using var respuesta = await http.PostAsJsonAsync("/api/embed", new
        {
            model = options.Value.ModeloEmbedding,
            input = texto,
            truncate = false
        }, cancellationToken);
        respuesta.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(
            await respuesta.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var embeddings = json.RootElement.GetProperty("embeddings");
        if (embeddings.GetArrayLength() != 1)
            throw new InvalidOperationException("Ollama debe devolver exactamente un embedding.");
        var vector = embeddings[0].EnumerateArray().Select(v => v.GetSingle()).ToArray();
        ValidarVector(vector);
        return vector;
    }

    internal static void ValidarVector(float[] vector)
    {
        if (vector.Length != VectorOptions.Dimensiones || vector.Any(v => !float.IsFinite(v)) ||
            !vector.Any(v => v != 0))
            throw new InvalidOperationException($"Se requiere un embedding finito no nulo de {VectorOptions.Dimensiones} dimensiones.");
    }
}
