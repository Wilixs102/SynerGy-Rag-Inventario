using Microsoft.Data.SqlClient;
using SynerGy.RAG.Application.Interfaces;
using System.Data;
using SynerGy.RAG.Infrastructure.Security;

namespace SynerGy.RAG.Infrastructure.Persistence;

public class SqlQueryService : ISqlQueryService
{
    private readonly string _connectionString;

    public SqlQueryService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> ProbarConexionAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            Console.WriteLine("SQL Server conectado correctamente.");
            Console.WriteLine($"Servidor: {connection.DataSource}");
            Console.WriteLine($"Base: {connection.Database}");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("========== ERROR SQL ==========");
            Console.WriteLine(ex.GetType().Name);
            Console.WriteLine(ex.Message);
            Console.WriteLine("===============================");

            return false;
        }
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ConsultarAsync(
        string sql,
        CancellationToken cancellationToken = default)
        => await EjecutarAsync(sql, null, cancellationToken);

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ConsultarCustodioAsync(
        string sql, string correo, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correo);
        var consulta = SqlSyntax.RestringirACustodio(sql);
        return await EjecutarAsync("""
            SELECT * INTO #EquiposCustodio FROM dbo.VistaEquipo
            WHERE usuario_custodio_activo = @correo AND IdEstadoAsignacion = 2;
            """ + consulta, correo, cancellationToken);
    }

    private async Task<IReadOnlyList<Dictionary<string, object?>>> EjecutarAsync(
        string sql, string? correo, CancellationToken cancellationToken)
    {
        var resultados =
            new List<Dictionary<string, object?>>();

        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command =
            new SqlCommand(sql, connection);

        command.CommandTimeout = 15;
        if (correo is not null) command.Parameters.Add("@correo", SqlDbType.NVarChar, 254).Value = correo;

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (resultados.Count < 101 && await reader.ReadAsync(cancellationToken))
        {
            var fila =
                new Dictionary<string, object?>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var nombre = reader.GetName(i);
                if (string.IsNullOrEmpty(nombre) || fila.ContainsKey(nombre))
                    nombre = $"columna{i + 1}";

                fila[nombre] =
                    reader.IsDBNull(i)
                        ? null
                        : reader.GetValue(i);
            }

            resultados.Add(fila);
        }

        return resultados;
    }
}
