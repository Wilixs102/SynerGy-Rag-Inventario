using System.Data;
using Microsoft.Data.SqlClient;
using SynerGy.RAG.Application.DTOs;
using SynerGy.RAG.Application.Interfaces;

namespace SynerGy.RAG.Infrastructure.Persistence;

// Solo selecciona nombre y correo; no carga identificaciones, teléfonos ni otros datos de RRHH.
public class SqlDirectorioCustodios(string connectionString) : IDirectorioCustodios
{
    public async Task<IReadOnlyList<Custodio>> BuscarAsync(string nombreOCorreo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreOCorreo);
        if (nombreOCorreo.Length > 254) throw new ArgumentException("Nombre o correo demasiado largo.");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 15;
        var correo = nombreOCorreo.Contains('@');
        var filtros = new List<string>();
        if (correo)
        {
            filtros.Add("LTRIM(RTRIM(CorreoInstitucional)) = @correo");
            command.Parameters.Add("@correo", SqlDbType.NVarChar, 254).Value = nombreOCorreo;
        }
        else
        {
            var palabras = nombreOCorreo.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (palabras.Length is < 1 or > 8) return [];
            for (var i = 0; i < palabras.Length; i++)
            {
                // Coincide con palabras completas, sin distinguir mayúsculas ni tildes.
                filtros.Add($"(' ' + REPLACE(REPLACE(REPLACE(NombreCompleto, ',', ' '), '.', ' '), '-', ' ') + ' ') COLLATE Latin1_General_100_CI_AI LIKE @p{i} ESCAPE '~'");
                command.Parameters.Add($"@p{i}", SqlDbType.NVarChar, 260).Value = "% " +
                    palabras[i].Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[") + " %";
            }
        }
        command.CommandText = """
            SELECT TOP (6) LTRIM(RTRIM(CorreoInstitucional)) AS Correo, MIN(NombreCompleto) AS Nombre
            FROM dbo.VistaColaboradorInv
            WHERE NULLIF(LTRIM(RTRIM(CorreoInstitucional)), '') IS NOT NULL AND
            """ + string.Join(" AND ", filtros) + " GROUP BY LTRIM(RTRIM(CorreoInstitucional)) ORDER BY Nombre, Correo";
        var candidatos = new List<Custodio>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            candidatos.Add(new(reader.IsDBNull(1) ? reader.GetString(0) : reader.GetString(1), reader.GetString(0)));
        return candidatos;
    }
}
