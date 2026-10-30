using System.Text.RegularExpressions;
using SynerGy.RAG.Application.DTOs;

namespace SynerGy.RAG.Application.UseCases;

public static class DetectarCustodio
{
    public static SolicitudCustodio? Extraer(string pregunta)
    {
        var correo = Regex.Match(pregunta, @"[\w.!#$%&'*+/=?^`{|}~-]+@[\w.-]+\.[\p{L}]{2,}");
        if (correo.Success) return new(correo.Value, true);
        var persona = Regex.Match(pregunta,
            @"(?:asignad[oa]s?\s+(?:a|de|para)|(?:equipos|activos|dispositivos)\s+(?:de|del|para)|actas(?:\s+de\s+(?:entrega|devoluci[oó]n))?\s+(?:de|del|para)|(?:que\s+)?tiene)\s+(?:(?:el|la|al)\s+)?(?:(?:compañero|compañera|colaborador|colaboradora|custodio|custodia)\s+)?(?<nombre>.+)",
            RegexOptions.IgnoreCase);
        if (!persona.Success) return null;
        var nombre = Regex.Split(persona.Groups["nombre"].Value,
            @"[?¿!¡,;:]|\s+(?:incluye|incluyendo|con|mostrando|y\s+(?:su|sus|la|el)|que\s+est)\b", RegexOptions.IgnoreCase)[0]
            .Trim().Trim('.', '"', '\'');
        var normalizado = ConocimientoDominio.Normalizar(nombre);
        if (ConsultasInventario.EsReferenciaAEquipos(nombre)) return null;
        if (Regex.IsMatch(normalizado, @"^(?:(?:la|el|los|las)\s+)?(?:entrega|devolucion|responsabilidad|custodia|cuidado|tecnologia)\b")) return null;
        if (Regex.IsMatch(normalizado, @"^(?:(?:la|el|los|las|un|una)\s+)?(?:marca|categoria|tipo|estado|bodega|oficina|sede|empresa|laptop|monitor|mouse|teclado|celular|impresora|todos|todas|mis|mi)\b"))
            return null;
        if (!Regex.IsMatch(nombre, @"^[\p{L}\p{M}][\p{L}\p{M}\s.'-]{1,119}$")) return null;
        return new(nombre, false);
    }
}
