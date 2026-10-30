using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SynerGy.RAG.Application.DTOs;
using SynerGy.RAG.Application.Interfaces;
using SynerGy.RAG.Infrastructure.Configuration;
using SynerGy.RAG.Infrastructure.Ollama;

namespace SynerGy.RAG.Infrastructure.Persistence;

// SQL fijo y parametrizado: esta tabla nunca se ofrece al SQL generado por el LLM.
public class SqlContextoStore(string connectionString, IOptions<VectorOptions> options) : IAlmacenContexto
{
    public async Task<IReadOnlyList<ContextoInventario>> BuscarAsync(
        float[] embedding, CancellationToken cancellationToken = default)
        => await BuscarInternoAsync(embedding, null, cancellationToken);

    public async Task<IReadOnlyList<ContextoInventario>> BuscarConIdsAsync(
        float[] embedding, IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
        => ids.Count == 0 ? [] : await BuscarInternoAsync(embedding, ids, cancellationToken);

    private async Task<IReadOnlyList<ContextoInventario>> BuscarInternoAsync(
        float[] embedding, IReadOnlyList<string>? ids, CancellationToken cancellationToken)
    {
        var config = options.Value;
        config.Validar();
        OllamaEmbeddingClient.ValidarVector(embedding);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            DECLARE @consulta VECTOR(768) = CAST(@embedding AS VECTOR(768));
            IF NOT EXISTS (SELECT 1 FROM dbo.RagContextoInventario
                           WHERE Modelo = @modelo AND VersionCatalogo = @version)
                THROW 50001, 'Catálogo vectorial no indexado para el modelo y versión configurados.', 1;
            SELECT TOP (@topK) Id, Tipo, Contenido,
                   VECTOR_DISTANCE('cosine', Embedding, @consulta) AS Distancia
            FROM dbo.RagContextoInventario
            WHERE Modelo = @modelo AND VersionCatalogo = @version
              AND (@ids IS NULL OR Id IN (SELECT value FROM OPENJSON(@ids)))
              AND VECTOR_DISTANCE('cosine', Embedding, @consulta) <= @maxDistancia
            ORDER BY Distancia, Id;
            """, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@embedding", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(embedding);
        command.Parameters.Add("@modelo", SqlDbType.NVarChar, 100).Value = config.ModeloEmbedding;
        command.Parameters.Add("@version", SqlDbType.NVarChar, 100).Value = config.VersionCatalogo;
        command.Parameters.Add("@topK", SqlDbType.Int).Value = config.TopK;
        command.Parameters.Add("@maxDistancia", SqlDbType.Float).Value = config.DistanciaMaxima;
        command.Parameters.Add("@ids", SqlDbType.NVarChar, -1).Value = ids is null ? DBNull.Value : JsonSerializer.Serialize(ids);
        var resultados = new List<ContextoInventario>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            resultados.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                Convert.ToDouble(reader.GetValue(3))));
        return resultados;
    }

    public async Task ReemplazarCatalogoAsync(
        IReadOnlyList<(EntradaCatalogo Entrada, float[] Embedding)> entradas,
        CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        config.Validar();
        if (entradas.Count == 0) throw new ArgumentException("No se indexa un catálogo vacío.");
        foreach (var (_, vector) in entradas) OllamaEmbeddingClient.ValidarVector(vector);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        // Serializa dos indexaciones de la misma versión; el reemplazo es atómico.
        await using var command = new SqlCommand("""
            DELETE FROM dbo.RagContextoInventario WITH (TABLOCKX)
            WHERE Modelo = @modelo AND VersionCatalogo = @version;
            """, connection, transaction) { CommandTimeout = 30 };
        command.Parameters.Add("@modelo", SqlDbType.NVarChar, 100).Value = config.ModeloEmbedding;
        command.Parameters.Add("@version", SqlDbType.NVarChar, 100).Value = config.VersionCatalogo;
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = """
            INSERT INTO dbo.RagContextoInventario (Id, Tipo, Contenido, Modelo, VersionCatalogo, Embedding)
            VALUES (@id, @tipo, @contenido, @modelo, @version, CAST(@embedding AS VECTOR(768)));
            """;
        command.Parameters.Add("@id", SqlDbType.NVarChar, 100);
        command.Parameters.Add("@tipo", SqlDbType.NVarChar, 32);
        command.Parameters.Add("@contenido", SqlDbType.NVarChar, 4000);
        command.Parameters.Add("@embedding", SqlDbType.NVarChar, -1);
        foreach (var (entrada, vector) in entradas)
        {
            command.Parameters["@id"].Value = entrada.Id;
            command.Parameters["@tipo"].Value = entrada.Tipo;
            command.Parameters["@contenido"].Value = entrada.Contenido;
            command.Parameters["@embedding"].Value = JsonSerializer.Serialize(vector);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
