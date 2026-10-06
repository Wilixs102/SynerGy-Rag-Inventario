namespace SynerGy.RAG.Infrastructure.Configuration;

public class OllamaOptions
{
    public const string Seccion = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Modelo { get; set; } = "qwen2.5-coder:7b";
    public double Temperature { get; set; } = 0.0;
    public int TimeoutSegundos { get; set; } = 120;
}
