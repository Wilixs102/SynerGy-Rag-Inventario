using System.Text.RegularExpressions;
using SynerGy.RAG.Application.Interfaces;

namespace SynerGy.RAG.Infrastructure.Security;

/// <summary>
/// Solo permite un único SELECT sobre objetos de una lista blanca.
/// </summary>
public partial class SqlValidator : ISqlValidator
{
    private readonly HashSet<string> _permitidos;

    public SqlValidator(IEnumerable<string> objetosPermitidos)
    {
        _permitidos = new HashSet<string>(objetosPermitidos, StringComparer.OrdinalIgnoreCase);
    }

    public string? Validar(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return "consulta vacía";

        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return "solo se permite SELECT";

        if (sql.Contains(';') || sql.Contains("--") || sql.Contains("/*"))
            return "no se permiten ';' ni comentarios";

        if (Prohibidas().IsMatch(sql))
            return "contiene palabras reservadas no permitidas";

        var objetos = Fuentes().Matches(sql)
            .Select(m => m.Groups[1].Value.Replace("[", "").Replace("]", ""))
            .Select(n => n.StartsWith("dbo.", StringComparison.OrdinalIgnoreCase) ? n[4..] : n)
            .ToList();

        if (objetos.Count == 0)
            return "no se detectó ninguna tabla o vista";

        var noPermitido = objetos.FirstOrDefault(o => !_permitidos.Contains(o));
        return noPermitido is null ? null : $"objeto no permitido: {noPermitido}";
    }

    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE|MERGE|GRANT|REVOKE|INTO|OPENROWSET|OPENQUERY|XP_\w+|SP_\w+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Prohibidas();

    [GeneratedRegex(@"\b(?:FROM|JOIN)\s+([\[\]\w\.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex Fuentes();
}
