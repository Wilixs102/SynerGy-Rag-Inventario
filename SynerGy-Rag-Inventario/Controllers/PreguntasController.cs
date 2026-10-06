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

    public record PreguntaRequest(string Pregunta);

    [HttpPost]
    public async Task<ActionResult<RespuestaRag>> Preguntar(
        [FromBody] PreguntaRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Pregunta))
            return BadRequest(new { mensaje = "La pregunta es obligatoria." });

        return Ok(await _rag.PreguntarAsync(request.Pregunta, cancellationToken));
    }
}
