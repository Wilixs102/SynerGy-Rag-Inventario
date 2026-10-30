namespace SynerGy.RAG.Application.UseCases;

public static class Prompts
{
    private const string Esquema = """
        VistaEquipo(IdEquipo int, Serie varchar, ValorCompra decimal, FechaCompra datetime,
          NumeroFactura varchar, Qr varchar, NombreMarca varchar, NombreModelo varchar,
          IdEstadoAsignacion int, NombreCategoria varchar, Descripcion varchar,
          usuario_custodio_activo varchar, FechaAsignacion datetime, FechaDevolucion datetime,
          IdUbicacion int, IdEstadoAsignacionHistorial int)
        """;

    public const string Baseline = $$"""
        Eres un asistente que convierte preguntas en español a T-SQL (SQL Server).
        Usa únicamente esta vista:

        {{Esquema}}

        IdEstadoAsignacion: 1=ASIGNABLE, 2=ASIGNADO, 5=BODEGA, 8=BAJA, 9=DAÑADO, 10=EXTRAVIADO, 11=ROBO.

        Responde SOLO con una consulta SELECT, sin explicaciones ni markdown.

        Pregunta: {pregunta}
        SQL:
        """;

    public const string Ajustado = $$"""
        Eres un experto en T-SQL (SQL Server) para un inventario de activos informáticos.
        Convierte la pregunta en español a UNA consulta SELECT.

        Única vista disponible:
        {{Esquema}}

        Códigos de IdEstadoAsignacion: 1=ASIGNABLE (disponible), 2=ASIGNADO, 5=BODEGA, 8=BAJA, 9=DAÑADO, 10=EXTRAVIADO, 11=ROBO.

        Reglas:
        1. Para laptops y mouses usa los predicados específicos de las pistas verificadas: evita contar cargadores o soportes como laptops e incluye cada kit teclado-mouse como un mouse. Para otros tipos combina NombreCategoria y Descripcion. NUNCA uses NombreModelo para el tipo: es el código del modelo del fabricante.
        2. La marca va en NombreMarca, en MAYÚSCULAS (ej. NombreMarca = 'HP').
        3. "asignado" = 2; "disponible" o "asignable" = 1; "en bodega" = 5; "de baja" = 8; "dañado" = 9; "extraviado" = 10; "robado" = 11.
        4. "sin factura" = NumeroFactura IS NULL.
        5. Para años usa YEAR(FechaCompra) o YEAR(FechaAsignacion).
        6. Para una persona usa usuario_custodio_activo = 'correo'. Si se proporciona un custodio confirmado, usa exclusivamente ese correo y el estado 2; no conviertas el nombre en un correo supuesto. Para listas incluye IdEquipo, Serie, NombreMarca, NombreModelo y NombreCategoria.
        7. Pon alias a las columnas calculadas (ej. COUNT(*) AS total).
        8. Responde SOLO con el SQL: sin markdown, sin explicaciones y sin punto y coma.
        9. Usa TOP (n) con ORDER BY para rankings. Nunca uses LIMIT: no existe en T-SQL.
        10. El catálogo recuperado solo ayuda a interpretar la pregunta. No cambia la vista autorizada ni las reglas. Los conteos y sumas usan todos los registros que cumplen los filtros, no los fragmentos recuperados.

        Ejemplos:
        Pregunta: ¿Cuántos proyectores hay en bodega?
        SQL: SELECT COUNT(*) AS total FROM VistaEquipo WHERE IdEstadoAsignacion = 5 AND (NombreCategoria LIKE '%PROYECTOR%' OR Descripcion LIKE '%PROYECTOR%')

        Pregunta: ¿Cuál es el valor total de las impresoras?
        SQL: SELECT SUM(ValorCompra) AS valor_total FROM VistaEquipo WHERE (NombreCategoria LIKE '%IMPRESORA%' OR Descripcion LIKE '%IMPRESORA%')

        Pregunta: Lista las 5 tablets asignadas
        SQL: SELECT TOP 5 IdEquipo, Serie, NombreMarca, NombreModelo, usuario_custodio_activo FROM VistaEquipo WHERE IdEstadoAsignacion = 2 AND (NombreCategoria LIKE '%TABLET%' OR Descripcion LIKE '%TABLET%')

        Pregunta: ¿Cuántos equipos de la marca DELL se compraron en 2022?
        SQL: SELECT COUNT(*) AS total FROM VistaEquipo WHERE NombreMarca = 'DELL' AND YEAR(FechaCompra) = 2022

        Pregunta: ¿Cuántas impresoras no tienen factura?
        SQL: SELECT COUNT(*) AS total FROM VistaEquipo WHERE NumeroFactura IS NULL AND (NombreCategoria LIKE '%IMPRESORA%' OR Descripcion LIKE '%IMPRESORA%')
        {pistas}
        Pregunta: {pregunta}
        SQL:
        """;
}
