using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SynerGy.RAG.Application.Interfaces;
using SynerGy.RAG.Infrastructure.Configuration;

namespace SynerGy.RAG.Infrastructure.Ollama;

public class OllamaClient : IOllamaClient
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    public OllamaClient(HttpClient http, IOptions<OllamaOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> GenerarAsync(
        string prompt,
        double? temperature = null,
        CancellationToken cancellationToken = default)
    {
        var body = new
        {
            model = _options.Modelo,
            prompt,
            stream = false,
            options = new { temperature = temperature ?? _options.Temperature }
        };

        using var respuesta = await _http.PostAsJsonAsync("/api/generate", body, cancellationToken);
        respuesta.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await respuesta.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
    }
}
