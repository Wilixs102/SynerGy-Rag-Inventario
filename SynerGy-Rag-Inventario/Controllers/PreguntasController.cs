using Microsoft.AspNetCore.Mvc;
using SynerGy.RAG.Application.DTOs;
using SynerGy.RAG.Application.Interfaces;

namespace SynerGy_Rag_Inventario.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PreguntasController : ControllerBase
{
    private readonly IRagService _rag;

    public PreguntasController(IRagService rag)
    {
        _rag = rag;
    }

    public record PreguntaRequest(string Pregunta, string? Modo = null);

    [HttpPost]
    public async Task<ActionResult<RespuestaRag>> Preguntar(
        [FromBody] PreguntaRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Pregunta))
            return BadRequest(new { mensaje = "La pregunta es obligatoria." });

        if (request.Modo is not (null or "inventario" or "actas"))
            return BadRequest(new { mensaje = "Modo de consulta no válido." });
        if (request.Pregunta.Length > 3000)
            return BadRequest(new { mensaje = "La pregunta no puede superar 3000 caracteres." });
        // Conserva la resolución de custodios y la ruta existente, incluso si el tema no dice 'actas'.
        var pregunta = request.Modo == "actas" ? "Busca actas: " + request.Pregunta : request.Pregunta;
        var respuesta = await _rag.PreguntarAsync(pregunta, cancellationToken);
        return Ok(respuesta with { Pregunta = request.Pregunta });
    }
}
