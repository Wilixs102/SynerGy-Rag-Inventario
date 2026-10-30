-- Ejecutar por el DBA en una base auxiliar autorizada en SQL Server 2025 (17.x).
-- No cambia VistaEquipo ni los registros del inventario. Requiere permiso CREATE TABLE.
SET XACT_ABORT ON;
IF TRY_CONVERT(int, SERVERPROPERTY('ProductMajorVersion')) < 17
    THROW 50000, 'Esta implementación requiere SQL Server 2025 o posterior.', 1;
GO
IF OBJECT_ID(N'dbo.RagContextoInventario', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RagContextoInventario
    (
        Id nvarchar(100) NOT NULL,
        Tipo nvarchar(32) NOT NULL,
        Contenido nvarchar(4000) NOT NULL,
        Modelo nvarchar(100) NOT NULL,
        VersionCatalogo nvarchar(100) NOT NULL,
        Embedding vector(768) NOT NULL,
        CONSTRAINT PK_RagContextoInventario PRIMARY KEY (Modelo, VersionCatalogo, Id)
    );
END;
GO
-- El DBA debe asignar SELECT sobre esta tabla al usuario lector de contexto.
-- Un usuario separado de indexación necesita SELECT, INSERT y DELETE sobre esta tabla.
-- Ejemplos (sustituir los nombres por usuarios existentes, no ejecutarlos sin revisar):
-- GRANT SELECT ON OBJECT::dbo.RagContextoInventario TO [usuario_contexto_lector];
-- GRANT SELECT, INSERT, DELETE ON OBJECT::dbo.RagContextoInventario TO [usuario_contexto_indexador];
-- La cuenta del inventario mantiene exclusivamente sus permisos sobre dbo.VistaEquipo.
