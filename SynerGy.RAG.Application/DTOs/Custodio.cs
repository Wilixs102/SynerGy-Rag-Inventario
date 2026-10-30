namespace SynerGy.RAG.Application.DTOs;

public record Custodio(string Nombre, string Correo)
{
    public string? PreguntaSugerida { get; init; }
}
public record SolicitudCustodio(string Texto, bool EsCorreo);
