using System.Security.Cryptography;
using System.Text;

namespace SynerGy.RAG.Application.UseCases;

/// <summary>Caché local acotada de SQL, nunca de resultados del inventario.</summary>
public sealed class CacheSqlGenerado(TimeSpan vigencia, int capacidad = 128, TimeProvider? reloj = null)
{
    private readonly Dictionary<string, (string Sql, DateTimeOffset Expira)> _entradas = new();
    private readonly object _sync = new();
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public static string Clave(string prompt, double? temperatura) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(new { prompt, temperatura }))));

    public string? Buscar(string clave)
    {
        lock (_sync)
        {
            if (!_entradas.TryGetValue(clave, out var entrada)) return null;
            if (entrada.Expira > _reloj.GetUtcNow()) return entrada.Sql;
            _entradas.Remove(clave);
            return null;
        }
    }

    public void Guardar(string clave, string sql)
    {
        if (vigencia <= TimeSpan.Zero || capacidad <= 0) return;
        lock (_sync)
        {
            var ahora = _reloj.GetUtcNow();
            foreach (var key in _entradas.Where(e => e.Value.Expira <= ahora).Select(e => e.Key).ToList())
                _entradas.Remove(key);
            if (!_entradas.ContainsKey(clave) && _entradas.Count >= capacidad)
                _entradas.Remove(_entradas.MinBy(e => e.Value.Expira).Key);
            _entradas[clave] = (sql, ahora + vigencia);
        }
    }
}
