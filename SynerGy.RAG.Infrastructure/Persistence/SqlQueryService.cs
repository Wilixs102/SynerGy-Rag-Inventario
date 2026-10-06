using Microsoft.Data.SqlClient;
using SynerGy.RAG.Application.Interfaces;

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
    {
        var resultados =
            new List<Dictionary<string, object?>>();

        await using var connection =
            new SqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command =
            new SqlCommand(sql, connection);

        command.CommandTimeout = 15;

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var fila =
                new Dictionary<string, object?>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                fila[reader.GetName(i)] =
                    reader.IsDBNull(i)
                        ? null
                        : reader.GetValue(i);
            }

            resultados.Add(fila);
        }

        return resultados;
    }
}