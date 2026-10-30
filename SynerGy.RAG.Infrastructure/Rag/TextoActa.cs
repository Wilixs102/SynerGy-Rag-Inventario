using System.Net;
using System.Text.RegularExpressions;

namespace SynerGy.RAG.Infrastructure.Rag;

public static class TextoActa
{
    private static string Plano(string texto) => Regex.Replace(
        WebUtility.HtmlDecode(Regex.Replace(texto, "<[^>]+>", " ")), @"\s+", " ").Trim();

    public static string Preparar(string tipo, string texto, IEnumerable<string> identidades)
    {
        var contenido = Plano(texto);
        foreach (var valor in identidades.Select(Plano).Where(v => v.Length >= 3).OrderByDescending(v => v.Length))
            contenido = contenido.Replace(valor, "[dato omitido]", StringComparison.OrdinalIgnoreCase);
        contenido = Regex.Replace(contenido, @"[\w.!#$%&'*+/=?^`{|}~-]+@[\w.-]+\.[\p{L}]{2,}", "[correo omitido]");
        contenido = Regex.Replace(contenido, @"\b\d{6,}\b", "[identificador omitido]");
        return "Tipo de acta: " + Plano(tipo) + ". " + contenido;
    }
}
