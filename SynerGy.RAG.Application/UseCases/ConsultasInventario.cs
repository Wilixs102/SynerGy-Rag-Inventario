using System.Text.RegularExpressions;

namespace SynerGy.RAG.Application.UseCases;

public record TipoInventario(string Nombre, string Patron, string Predicado, string Regla);
public record ConsultaValidada(string Sql, string Explicacion);

public static class ConsultasInventario
{
    public static readonly TipoInventario Laptop = new("laptops",
        @"\b(laptops?|portatil(?:es)?|notebooks?)\b",
        "(NombreCategoria COLLATE Latin1_General_100_CI_AI = N'LAPTOP' OR (NombreCategoria COLLATE Latin1_General_100_CI_AI = N'COMPUTADOR' AND Descripcion COLLATE Latin1_General_100_CI_AI LIKE N'%Laptop%'))",
        "Laptop: categoría LAPTOP, o categoría COMPUTADOR con descripción que contiene Laptop. Excluir cargadores, bases y soportes aunque su descripción mencione laptop.");
    public static readonly TipoInventario Mouse = new("mouses",
        @"\b(mouses?|mice|raton(?:es)?)\b",
        "(NombreCategoria COLLATE Latin1_General_100_CI_AI = N'MOUSE' OR (NombreCategoria COLLATE Latin1_General_100_CI_AI = N'PERIFERICO' AND Descripcion COLLATE Latin1_General_100_CI_AI LIKE N'%Mouse%'))",
        "Mouse: categoría MOUSE, o PERIFÉRICO con Mouse en la descripción. Regla aprobada: cada kit Teclado - Mouse USB cuenta como un mouse, además de los mouses individuales.");
    private static readonly TipoInventario[] Tipos = [Laptop, Mouse];

    public static IReadOnlyList<TipoInventario> Reconocer(string pregunta) => Tipos
        .Where(t => Regex.IsMatch(ConocimientoDominio.Normalizar(pregunta), t.Patron)).ToList();

    public static bool EsReferenciaAEquipos(string texto)
    {
        var normalizado = ConocimientoDominio.Normalizar(texto);
        return Reconocer(texto).Count > 0 || Regex.IsMatch(normalizado,
            @"\b(monitores?|teclados?|celulares?|impresoras?|cargadores?|equipos?|activos?|kits?|categorias?|marcas?|bodega|disponibles?|asignad[oa]s?)\b");
    }

    public static ConsultaValidada? Crear(string pregunta, string? referenciaPersona = null)
    {
        var tipos = Reconocer(pregunta);
        if (tipos.Count == 0) return null;
        var texto = ConocimientoDominio.Normalizar(pregunta);
        if (referenciaPersona is not null) texto = texto.Replace(ConocimientoDominio.Normalizar(referenciaPersona), " ");
        texto = texto.Replace("por favor", " ");
        var estados = new (string Patron, int Id)[]
        {
            (@"\b(asignad[oa]s?)\b", 2), (@"\b(disponibles?|asignables?|libres?)\b", 1),
            (@"\bbodega\b", 5), (@"\bbaja\b", 8), (@"\b(danad[oa]s?|averiad[oa]s?)\b", 9),
            (@"\b(extraviad[oa]s?|perdid[oa]s?)\b", 10), (@"\b(robad[oa]s?)\b", 11)
        }.Where(e => Regex.IsMatch(texto, e.Patron)).Select(e => e.Id).Distinct().ToList();
        if (estados.Count > 1) return null;
        var esConteo = Regex.IsMatch(texto, @"\b(cuant[oa]s?|cantidad|total|numero)\b");
        var limite = Regex.Match(texto, @"\b(?:los|las|primeros|primeras|top)\s+(\d{1,3})\b");
        var top = limite.Success && int.TryParse(limite.Groups[1].Value, out var n) && n is > 0 and <= 100 ? n : 101;
        if (limite.Success) texto = texto.Replace(limite.Value, " ");
        foreach (var tipo in tipos) texto = Regex.Replace(texto, tipo.Patron, " ");
        foreach (var estado in new[] { @"\b(asignad[oa]s?|disponibles?|asignables?|libres?|bodega|baja|danad[oa]s?|averiad[oa]s?|extraviad[oa]s?|perdid[oa]s?|robad[oa]s?)\b" })
            texto = Regex.Replace(texto, estado, " ");
        // Si hay marca, fecha, negación, comparación u otro filtro, dejarlo al generador completo.
        var permitidas = new HashSet<string>(("me puedes puede podria podrias ayudar ayudame mostrar muestrame muestra listar lista listado dame dar entregar entregame entregue quiero necesito ver buscar busca buscame cuanto cuantos cuanta cuantas cantidad total numero hay existen tenemos tengo son de del la el las los un una unos unas todos todas todo equipos equipo activos activo dispositivos dispositivo computador computadora computadoras computadoras portatiles en estado a al para y o tipo companero companera colaborador colaboradora custodio custodia que tiene estan se encuentran favor" ).Split(' '));
        if (Regex.Matches(texto, @"[\p{L}\d@]+").Any(m => !permitidas.Contains(m.Value))) return null;
        var filtroEstado = estados.Count == 1 ? $" AND IdEstadoAsignacion = {estados[0]}" : "";
        var explicacion = string.Join(" ", tipos.Select(t => t.Regla));
        if (esConteo)
        {
            var sql = string.Join(" UNION ALL ", tipos.Select(t =>
                $"SELECT N'{t.Nombre}' AS tipo_equipo, COUNT(DISTINCT IdEquipo) AS total FROM dbo.VistaEquipo WHERE {t.Predicado}{filtroEstado}"));
            return new(sql, explicacion);
        }
        var filtros = string.Join(" OR ", tipos.Select(t => t.Predicado));
        return new($"SELECT TOP ({top}) IdEquipo, Serie, NombreMarca, NombreModelo, NombreCategoria, Descripcion, IdEstadoAsignacion, usuario_custodio_activo FROM dbo.VistaEquipo WHERE ({filtros}){filtroEstado} ORDER BY IdEquipo", explicacion);
    }
}
