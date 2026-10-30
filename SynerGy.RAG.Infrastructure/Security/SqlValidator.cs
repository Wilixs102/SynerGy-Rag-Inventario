using System.Text.RegularExpressions;
using SynerGy.RAG.Application.Interfaces;
using Microsoft.SqlServer.TransactSql.ScriptDom;

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

        var arbol = SqlSyntax.Parsear(sql);
        if (arbol is null) return "sintaxis T-SQL inválida";
        if (arbol.Batches.Count != 1 || arbol.Batches[0].Statements.Count != 1 ||
            arbol.Batches[0].Statements[0] is not SelectStatement)
            return "solo se permite una sentencia SELECT";
        var nodos = SqlSyntax.Fragmentos(arbol).ToList();
        var tablas = nodos.OfType<NamedTableReference>().ToList();
        if (tablas.Count == 0) return "no se detectó ninguna tabla o vista";
        foreach (var tabla in tablas)
        {
            var ids = tabla.SchemaObject.Identifiers;
            if (ids.Count is < 1 or > 2 ||
                (ids.Count == 2 && !ids[0].Value.Equals("dbo", StringComparison.OrdinalIgnoreCase)) ||
                !_permitidos.Contains(ids[^1].Value) || tabla.TableHints.Count > 0)
                return "objeto o indicación de tabla no permitido";
        }
        foreach (var nodo in nodos)
        {
            if (nodo is TableReference && nodo is not NamedTableReference and not QueryDerivedTable
                and not QualifiedJoin and not UnqualifiedJoin and not JoinParenthesisTableReference)
                return "tipo de fuente no permitido";
            if (nodo is VariableReference || nodo.GetType().Name is "GlobalVariableExpression" or "NextValueForExpression" or "SelectSetVariable" or "ParameterlessCall")
                return "variables o secuencias no permitidas";
            if (nodo.GetType().Name.Contains("OptimizerHint", StringComparison.Ordinal) ||
                nodo.GetType().Name.EndsWith("ForClause", StringComparison.Ordinal))
                return "opciones de ejecución o serialización no permitidas";
            if (nodo is FunctionCall funcion && (funcion.CallTarget is not null ||
                !FuncionesPermitidas.Contains(funcion.FunctionName.Value)))
                return "función no permitida";
        }
        return null;
    }

    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE|MERGE|GRANT|REVOKE|INTO|OPENROWSET|OPENQUERY|XP_\w+|SP_\w+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Prohibidas();

    private static readonly HashSet<string> FuncionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "COUNT", "COUNT_BIG", "SUM", "AVG", "MIN", "MAX", "YEAR", "MONTH", "DAY", "GETDATE",
        "GETUTCDATE", "SYSDATETIME", "ISNULL", "NULLIF", "UPPER", "LOWER", "LTRIM", "RTRIM",
        "LEN", "ROUND", "ABS", "CEILING", "FLOOR", "DATEDIFF", "DATEADD", "CONCAT", "REPLACE"
    };
}
