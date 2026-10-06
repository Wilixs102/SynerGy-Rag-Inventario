using System.Globalization;
using System.Text;

namespace SynerGy.RAG.Application.UseCases;

/// <summary>
/// Recuperación ligera de conocimiento de dominio: detecta palabras clave en la
/// pregunta y devuelve pistas sobre cómo mapear cada tipo de equipo a las columnas
/// reales (las categorías del inventario están fragmentadas y duplicadas).
/// </summary>
public static class ConocimientoDominio
{
    private static readonly (string[] Claves, string Pista)[] Entradas =
    [
        (["laptop", "portatil", "notebook"],
            "Laptop: filtrar con (NombreCategoria LIKE '%LAPTOP%' OR Descripcion LIKE '%LAPTOP%')."),
        (["celular", "telefono", "movil", "smartphone", "iphone"],
            "Celular: filtrar con (NombreCategoria LIKE '%MÓVIL%' OR Descripcion LIKE '%CELULAR%')."),
        (["monitor", "pantalla"],
            "Monitor: filtrar con (NombreCategoria LIKE '%MONITOR%' OR Descripcion LIKE '%MONITOR%')."),
        (["teclado"],
            "Teclado: filtrar con (NombreCategoria LIKE '%TECLADO%' OR Descripcion LIKE '%TECLADO%')."),
        (["mouse", "raton"],
            "Mouse: filtrar con (NombreCategoria LIKE '%MOUSE%' OR Descripcion LIKE '%MOUSE%')."),
        (["cargador"],
            "Cargador: filtrar con (NombreCategoria LIKE '%CARGADOR%' OR Descripcion LIKE '%CARGADOR%')."),
        (["auricular", "audifono", "diadema"],
            "Auriculares: filtrar con (NombreCategoria LIKE '%AUDIFONOS%' OR Descripcion LIKE '%AURICULAR%' OR Descripcion LIKE '%AUDIFONOS%')."),
        (["tv", "televisor", "television"],
            "TV: filtrar con (Descripcion LIKE '%TV%' OR Descripcion LIKE '%PANTALLA%')."),
    ];

    public static IReadOnlyList<string> Recuperar(string pregunta)
    {
        var texto = Normalizar(pregunta);
        var palabras = texto.Split(
            [' ', ',', '.', '?', '¿', '!', '¡', ';', ':'],
            StringSplitOptions.RemoveEmptyEntries);

        return Entradas
            .Where(e => e.Claves.Any(c => palabras.Any(p => p.StartsWith(c, StringComparison.Ordinal))))
            .Select(e => e.Pista)
            .ToList();
    }

    public static string Normalizar(string texto)
    {
        var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
