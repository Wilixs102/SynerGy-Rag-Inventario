namespace SynerGy.RAG.Application.Interfaces;

public interface ISqlValidator
{
    /// <summary>
    /// Devuelve null si el SQL es válido; de lo contrario, el motivo del rechazo.
    /// </summary>
    string? Validar(string sql);
}
