using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SynerGy.RAG.Application.DTOs;
using SynerGy.RAG.Application.Interfaces;
using SynerGy.RAG.Application.UseCases;
using SynerGy.RAG.Infrastructure.Configuration;
using SynerGy.RAG.Infrastructure.Persistence;

namespace SynerGy.RAG.Infrastructure.Rag;

public class SqlBuscadorActas(
    string sourceConnection, string vectorConnection, IEmbeddingClient embeddings,
    IOptions<VectorOptions> options) : IBuscadorActas
{
    public const string Version = "actas-v1";

    private SqlContextoStore Almacen() => new(vectorConnection, Options.Create(new VectorOptions
    {
        Habilitada = true, ModeloEmbedding = options.Value.ModeloEmbedding, VersionCatalogo = Version,
        TopK = options.Value.TopK, DistanciaMaxima = options.Value.DistanciaMaxima
    }));

    public async Task<int> IndexarAsync(CancellationToken cancellationToken = default)
    {
        var entradas = new List<EntradaCatalogo>();
        await using (var connection = new SqlConnection(sourceConnection))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("""
                SELECT a.IdActa, t.Nombre,
                       CONCAT(a.TextoIntroductorio, ' ', a.TextoActaEntrega, ' ', a.TerminosCondiciones,
                              ' ', a.MotivoDevolucion, ' ', a.Observaciones) AS Texto,
                       a.NombreColaborador, a.IdentificacionColaborador, a.CorreoColaborador,
                       a.UsuarioCreacion, a.VerificadoPor
                FROM dbo.Acta a JOIN dbo.TipoActa t ON t.IdTipoActa=a.IdTipoActa
                WHERE a.Activo=1 ORDER BY a.IdActa
                """, connection) { CommandTimeout = 30 };
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var originales = Enumerable.Range(3, 5).Select(i => reader.IsDBNull(i) ? "" : reader.GetString(i)).ToList();
                var texto = TextoActa.Preparar(reader.IsDBNull(1) ? "Acta" : reader.GetString(1), reader.GetString(2), originales);
                if (texto.Length > 4000)
                    throw new InvalidOperationException($"El acta {reader.GetInt32(0)} supera 4000 caracteres; segmentar antes de indexar, sin truncar.");
                entradas.Add(new("acta-" + reader.GetInt32(0), "acta", texto));
            }
        }
        if (entradas.Count == 0) throw new InvalidOperationException("No hay actas activas para indexar.");
        var cache = new Dictionary<string, float[]>(StringComparer.Ordinal);
        var indexadas = new List<(EntradaCatalogo Entrada, float[] Embedding)>();
        foreach (var entrada in entradas)
        {
            if (!cache.TryGetValue(entrada.Contenido, out var vector))
            {
                vector = await embeddings.GenerarEmbeddingAsync(entrada.Contenido, cancellationToken);
                cache.Add(entrada.Contenido, vector);
            }
            indexadas.Add((entrada, vector));
        }
        await Almacen().ReemplazarCatalogoAsync(indexadas, cancellationToken);
        return entradas.Count;
    }

    public async Task<RespuestaRag> BuscarAsync(string pregunta, Custodio? custodio, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        if (!options.Value.ActasHabilitadas)
            return new(pregunta, null, [], "Las actas todavía no están indexadas o habilitadas.", sw.ElapsedMilliseconds);
        if (Regex.IsMatch(ConocimientoDominio.Normalizar(pregunta), @"\b(cuant[oa]s?|total|cantidad)\b"))
            return new(pregunta, null, [], null, sw.ElapsedMilliseconds)
            {
                RequiereAclaracion = true,
                Mensaje = "La búsqueda semántica devuelve actas relacionadas, no un total exacto. Para buscar, describe el tema del acta; los conteos de equipos se consultan en el inventario actual."
            };
        var normalizada = ConocimientoDominio.Normalizar(pregunta);
        var entrega = Regex.IsMatch(normalizada, @"\bentregas?\b");
        var devolucion = Regex.IsMatch(normalizada, @"\bdevolucion(?:es)?\b");
        var tipo = entrega == devolucion ? null : entrega ? "%entrega%" : "%devolucion%";
        await using var connection = new SqlConnection(sourceConnection);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("""
            SELECT a.IdActa, a.NumeroActa, t.Nombre AS tipo_acta, a.FechaActa
            FROM dbo.Acta a JOIN dbo.TipoActa t ON t.IdTipoActa=a.IdTipoActa
            WHERE a.Activo=1
              AND (@tipo IS NULL OR t.Nombre COLLATE Latin1_General_100_CI_AI LIKE @tipo)
              AND (@correo IS NULL OR a.CorreoColaborador=@correo OR EXISTS (
                   SELECT 1 FROM dbo.VistaColaboradorInv c WHERE c.IdColaborador=a.IdColaborador
                   AND c.CorreoInstitucional=@correo))
            ORDER BY a.IdActa
            """, connection) { CommandTimeout = 15 };
        command.Parameters.Add("@tipo", SqlDbType.NVarChar, 50).Value = (object?)tipo ?? DBNull.Value;
        command.Parameters.Add("@correo", SqlDbType.NVarChar, 254).Value = (object?)custodio?.Correo ?? DBNull.Value;
        var metadatos = new Dictionary<string, Dictionary<string, object?>>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetInt32(0);
                metadatos.Add("acta-" + id, new()
                {
                    ["IdActa"] = id, ["NumeroActa"] = reader.IsDBNull(1) ? null : reader.GetString(1),
                    ["tipo_acta"] = reader.GetString(2), ["FechaActa"] = reader.IsDBNull(3) ? null : reader.GetDateTime(3)
                });
            }
        }
        IReadOnlyList<ContextoInventario> contexto = [];
        if (metadatos.Count > 0)
        {
            var vector = await embeddings.GenerarEmbeddingAsync(pregunta, cancellationToken);
            contexto = await Almacen().BuscarConIdsAsync(vector, metadatos.Keys.ToList(), cancellationToken);
        }
        return new(pregunta, null, contexto.Select(c => metadatos[c.Id]).ToList(), null, sw.ElapsedMilliseconds)
        {
            Contexto = contexto, Custodio = custodio, MetodoGeneracion = "busqueda_vectorial_actas",
            Mensaje = "Actas relacionadas por similitud con su texto. Se muestra hasta el límite configurado; estas coincidencias no son un conteo del inventario ni prueban una asignación actual."
        };
    }
}
