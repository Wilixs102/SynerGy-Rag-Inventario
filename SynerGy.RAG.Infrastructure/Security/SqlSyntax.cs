using System.Collections;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SynerGy.RAG.Infrastructure.Security;

internal static class SqlSyntax
{
    public static TSqlScript? Parsear(string sql)
    {
        var fragmento = new TSql160Parser(true).Parse(new StringReader(sql), out var errores);
        return errores.Count == 0 ? fragmento as TSqlScript : null;
    }

    // Recorre los hijos del AST, incluidos tipos de fuente que no conocemos: se deniegan por defecto.
    public static IEnumerable<TSqlFragment> Fragmentos(TSqlFragment raiz)
    {
        var pendientes = new Stack<TSqlFragment>();
        var vistos = new HashSet<TSqlFragment>(ReferenceEqualityComparer.Instance);
        pendientes.Push(raiz);
        while (pendientes.TryPop(out var nodo))
        {
            if (!vistos.Add(nodo)) continue;
            yield return nodo;
            foreach (var propiedad in nodo.GetType().GetProperties())
            {
                if (propiedad.GetIndexParameters().Length != 0) continue;
                if (propiedad.GetValue(nodo) is TSqlFragment hijo) pendientes.Push(hijo);
                else if (propiedad.GetValue(nodo) is IEnumerable lista)
                    foreach (var elemento in lista)
                        if (elemento is TSqlFragment fragmento) pendientes.Push(fragmento);
            }
        }
    }

    public static string RestringirACustodio(string sql)
    {
        var rechazo = new SqlValidator(["VistaEquipo"]).Validar(sql);
        if (rechazo is not null) throw new InvalidOperationException($"SQL rechazado: {rechazo}");
        var arbol = Parsear(sql)!;
        foreach (var tabla in Fragmentos(arbol).OfType<NamedTableReference>())
        {
            tabla.Alias ??= new Identifier { Value = "VistaEquipo" };
            tabla.SchemaObject.Identifiers.Clear();
            tabla.SchemaObject.Identifiers.Add(new Identifier { Value = "#EquiposCustodio" });
        }
        new Sql160ScriptGenerator().GenerateScript(arbol, out var salida);
        return salida;
    }
}
