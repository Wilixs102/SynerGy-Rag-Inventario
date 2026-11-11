-- Ejecutar en la base auxiliar configurada (normalmente RAG_DB).
-- Solo lectura. No modifica Acta ni ActaDetalle.
SELECT DB_NAME() AS BaseVectorial, Modelo, VersionCatalogo, Tipo, COUNT(*) AS Vectores
FROM dbo.RagContextoInventario
GROUP BY Modelo, VersionCatalogo, Tipo;

-- Evidencia del tipo nativo VECTOR; inspección de los valores almacenados.
SELECT c.name AS Columna, t.name AS TipoSQL
FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id
WHERE c.object_id=OBJECT_ID(N'dbo.RagContextoInventario') AND c.name=N'Embedding';
SELECT TOP (5) Id, Modelo, VersionCatalogo,
    LEFT(CAST(Embedding AS nvarchar(max)), 200) AS MuestraVector
FROM dbo.RagContextoInventario WHERE VersionCatalogo=N'actas-v1' ORDER BY Id;

-- Demostración reproducible: documentos similares a un acta existente.
-- No representa una pregunta del usuario: usa el vector de un acta como consulta.
DECLARE @modelo nvarchar(100)=N'nomic-embed-text';
DECLARE @consulta vector(768);
SELECT TOP (1) @consulta=Embedding
FROM dbo.RagContextoInventario
WHERE VersionCatalogo=N'actas-v1' AND Modelo=@modelo ORDER BY Id;
IF @consulta IS NULL THROW 50002, 'No existen actas indexadas con este modelo.', 1;
SELECT TOP (5) Id, VECTOR_DISTANCE('cosine', Embedding, @consulta) AS DistanciaCoseno
FROM dbo.RagContextoInventario
WHERE VersionCatalogo=N'actas-v1' AND Modelo=@modelo
ORDER BY DistanciaCoseno, Id;
-- La aplicación genera @consulta desde la pregunta con Ollama /api/embed;
-- además filtra actas activas, tipo y custodio antes del TOP K.
