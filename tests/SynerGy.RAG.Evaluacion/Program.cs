// Harness de evaluación: genera SQL con cada configuración, N repeticiones por pregunta,
// y calcula métricas por intento. No ejecuta SQL contra la base (solo genera y valida).
//
// Uso:
//   dotnet run --project tests/SynerGy.RAG.Evaluacion -- --reps 3 --salida resultados/resultados.csv
//   Opciones: --configs baseline,prompt_enriquecido,ajustado  --max 5  --modelo qwen2.5-coder:7b

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SynerGy.RAG.Application.Interfaces;
using SynerGy.RAG.Application.UseCases;
using SynerGy.RAG.Infrastructure.Configuration;
using SynerGy.RAG.Infrastructure.Ollama;
using SynerGy.RAG.Infrastructure.Security;

var argumentos = ParsearArgumentos(args);
int reps = int.Parse(argumentos.GetValueOrDefault("reps", "3"));
int max = int.Parse(argumentos.GetValueOrDefault("max", "0"));
string salida = argumentos.GetValueOrDefault("salida", "resultados/resultados.csv");
string modelo = argumentos.GetValueOrDefault("modelo", "qwen2.5-coder:7b");
string url = argumentos.GetValueOrDefault("url", "http://localhost:11434");

var configuraciones = new Dictionary<string, RagOptions>
{
    ["baseline"] = new(EstrategiaPrompt.Baseline, 0.3),
    ["prompt_enriquecido"] = new(EstrategiaPrompt.Ajustado, 0.3),
    ["ajustado"] = new(EstrategiaPrompt.Ajustado, 0.1),
};

var seleccion = argumentos.GetValueOrDefault("configs", string.Join(',', configuraciones.Keys))
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var preguntas = JsonSerializer.Deserialize<List<Pregunta>>(
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "preguntas.json")),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
if (max > 0) preguntas = preguntas.Take(max).ToList();

var http = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(300) };
var ollama = new OllamaClient(http, Options.Create(new OllamaOptions { BaseUrl = url, Modelo = modelo }));
var validador = new SqlValidator(["VistaEquipo"]);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(salida))!);
await using var escritor = new StreamWriter(salida, append: false, new UTF8Encoding(true));
await escritor.WriteLineAsync("config,id_pregunta,rep,sql_valida,componentes_ok,latencia_ms,sql,error");

int total = seleccion.Length * preguntas.Count * reps, hecho = 0;
foreach (var nombre in seleccion)
{
    var opciones = configuraciones[nombre];
    // Servicio sin acceso a BD: solo se usa GenerarSqlAsync.
    var rag = new RagService(ollama, validador, new SinBaseDeDatos(), opciones);

    foreach (var p in preguntas)
    {
        for (int rep = 1; rep <= reps; rep++)
        {
            string sql = "", error = "";
            long ms = 0;
            try { (sql, ms) = await rag.GenerarSqlAsync(p.Texto); }
            catch (Exception ex) { error = ex.Message; }

            bool valida = error == "" && validador.Validar(sql) is null;
            bool componentes = valida && Cumple(sql, p);

            await escritor.WriteLineAsync(string.Join(',',
                nombre, p.Id, rep, valida ? 1 : 0, componentes ? 1 : 0, ms, Csv(sql), Csv(error)));
            await escritor.FlushAsync();

            hecho++;
            Console.WriteLine($"[{hecho}/{total}] {nombre} p{p.Id} r{rep} valida={valida} ok={componentes} {ms}ms");
        }
    }
}

Console.WriteLine($"Listo. Resultados en {Path.GetFullPath(salida)}");

static bool Cumple(string sql, Pregunta p) =>
    p.Requeridos.All(r => Regex.IsMatch(sql, r, RegexOptions.IgnoreCase)) &&
    !p.Prohibidos.Any(r => Regex.IsMatch(sql, r, RegexOptions.IgnoreCase));

static string Csv(string valor) =>
    "\"" + valor.Replace("\r", " ").Replace("\n", " ").Replace("\"", "\"\"") + "\"";

static Dictionary<string, string> ParsearArgumentos(string[] args)
{
    var d = new Dictionary<string, string>();
    for (int i = 0; i + 1 < args.Length; i += 2)
        d[args[i].TrimStart('-')] = args[i + 1];
    return d;
}

record Pregunta(
    int Id,
    [property: System.Text.Json.Serialization.JsonPropertyName("pregunta")] string Texto,
    List<string> Requeridos,
    List<string> Prohibidos);

class SinBaseDeDatos : ISqlQueryService
{
    public Task<bool> ProbarConexionAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Dictionary<string, object?>>> ConsultarAsync(
        string sql, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
