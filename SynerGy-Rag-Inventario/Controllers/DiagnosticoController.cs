using Microsoft.AspNetCore.Mvc;
using SynerGy.RAG.Application.Interfaces;

namespace SynerGy_Rag_Inventario.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagnosticoController : ControllerBase
{
    private readonly ISqlQueryService _sqlQueryService;

    public DiagnosticoController(
        ISqlQueryService sqlQueryService)
    {
        _sqlQueryService = sqlQueryService;
    }

    [HttpGet("sql")]
    public async Task<IActionResult> ProbarSql(
        CancellationToken cancellationToken)
    {
        bool conectado =
            await _sqlQueryService
                .ProbarConexionAsync(cancellationToken);

        if (!conectado)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    conectado = false,
                    mensaje = "No fue posible conectar con SQL Server."
                }
            );
        }

        return Ok(
            new
            {
                conectado = true,
                baseDatos = "ActivosInformaticos",
                usuario = "rag_reader",
                mensaje = "Conexión API -> SQL Server correcta."
            }
        );
    }
}