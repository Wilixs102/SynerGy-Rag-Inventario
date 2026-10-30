namespace SynerGy.RAG.Application.DTOs;

public record EntradaCatalogo(string Id, string Tipo, string Contenido);

public record ContextoInventario(string Id, string Tipo, string Contenido, double Distancia);
