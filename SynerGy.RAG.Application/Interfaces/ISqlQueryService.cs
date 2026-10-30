using System;
using System.Collections.Generic;
using System.Text;

namespace SynerGy.RAG.Application.Interfaces;

public interface ISqlQueryService
{
    Task<bool> ProbarConexionAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Dictionary<string, object?>>> ConsultarAsync(
        string sql,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Dictionary<string, object?>>> ConsultarCustodioAsync(
        string sql, string correo, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("La consulta de custodio requiere ejecución SQL parametrizada.");
}
