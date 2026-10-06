namespace SynerGy.RAG.Application.UseCases;

public enum EstrategiaPrompt
{
    /// <summary>Prompt simple: solo esquema y estados.</summary>
    Baseline,

    /// <summary>Reglas de dominio, ejemplos few-shot y recuperación de pistas.</summary>
    Ajustado
}

public record RagOptions(
    EstrategiaPrompt Estrategia = EstrategiaPrompt.Ajustado,
    double? Temperature = 0.1);
