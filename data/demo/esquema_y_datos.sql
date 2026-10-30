-- Ejecutar únicamente en una base vacía de demostración creada por el administrador.
SET XACT_ABORT ON;
IF DB_NAME() NOT LIKE '%Demo%'
    THROW 51000, 'La base de demostracion debe contener Demo en su nombre.', 1;
IF TRY_CONVERT(int, SERVERPROPERTY('ProductMajorVersion')) < 17
    THROW 51001, 'Se requiere SQL Server 2025.', 1;
IF OBJECT_ID('dbo.VistaEquipo') IS NOT NULL AND OBJECT_ID('dbo.EquipoDemo') IS NULL
    THROW 51002, 'Se detecto una vista ajena a la demostracion. No se modifica.', 1;
BEGIN TRANSACTION;
IF OBJECT_ID('dbo.ColaboradorDemo','U') IS NULL
BEGIN
    CREATE TABLE dbo.ColaboradorDemo(IdColaborador int PRIMARY KEY, NombreCompleto nvarchar(120), CorreoInstitucional nvarchar(254));
    INSERT dbo.ColaboradorDemo VALUES
    (1,N'Ana Demo','persona01@example.test'),(2,N'Bruno Demo','persona02@example.test'),
    (3,N'Carla Demo','persona03@example.test'),(4,N'Diego Demo','persona04@example.test'),
    (5,N'Elena Demo','persona05@example.test'),(6,N'Felipe Demo','persona06@example.test'),
    (7,N'Gabriela Demo','persona07@example.test'),(8,N'Hugo Demo','persona08@example.test'),
    (9,N'Inés Demo','persona09@example.test'),(10,N'Javier Demo','persona10@example.test'),
    (11,N'Karla Demo','persona11@example.test'),(12,N'Luis Demo','persona12@example.test');
END;
IF OBJECT_ID('dbo.EquipoDemo','U') IS NULL
BEGIN
    CREATE TABLE dbo.EquipoDemo(
        IdEquipo int PRIMARY KEY, IdCategoria int, Serie nvarchar(100), ValorCompra decimal(12,2),
        FechaCompra datetime, NumeroFactura nvarchar(50), Qr nvarchar(100), NombreMarca nvarchar(50),
        NombreModelo nvarchar(100), IdEstadoAsignacion int, NombreCategoria nvarchar(100), Descripcion nvarchar(200),
        usuario_custodio_activo nvarchar(254), FechaAsignacion datetime, FechaDevolucion datetime,
        IdUbicacion int, IdEstadoAsignacionHistorial int);
    WITH numeros AS (SELECT 1 AS n UNION ALL SELECT n+1 FROM numeros WHERE n<500)
    INSERT dbo.EquipoDemo
    SELECT n,1,CONCAT('DEMO-',RIGHT(CONCAT('000000',n),6)),100+n,
        DATEADD(day,n%200,CONVERT(datetime,'20250101')),CONCAT('FACT-DEMO-',n),CONCAT('QR-DEMO-',n),
        N'MARCA DEMO',CONCAT('MODELO-DEMO-',n%10),CASE WHEN n%5=0 THEN 1 ELSE 2 END,
        CASE WHEN n<=180 THEN N'LAPTOP' WHEN n<=200 THEN N'CARGADOR' WHEN n<=290 THEN N'MOUSE'
             WHEN n<=350 THEN N'PERIFÉRICO' WHEN n<=450 THEN N'MONITOR' ELSE N'TECLADO' END,
        CASE WHEN n<=180 THEN N'Laptop' WHEN n<=200 THEN N'Cargador de laptop' WHEN n<=290 THEN N'Mouse'
             WHEN n<=350 THEN N'Teclado - Mouse USB' WHEN n<=450 THEN N'Monitor' ELSE N'Teclado' END,
        CASE WHEN n%5=0 THEN N'SIN ASIGNACIÓN' ELSE c.CorreoInstitucional END,
        CASE WHEN n%5=0 THEN NULL ELSE CONVERT(datetime,'20261001') END,NULL,1,
        CASE WHEN n%5=0 THEN 1 ELSE 2 END
    FROM numeros JOIN dbo.ColaboradorDemo c ON c.IdColaborador=1+n%12 OPTION (MAXRECURSION 500);
END;
EXEC(N'CREATE OR ALTER VIEW dbo.VistaEquipo AS SELECT * FROM dbo.EquipoDemo');
EXEC(N'CREATE OR ALTER VIEW dbo.VistaColaboradorInv AS SELECT IdColaborador,NombreCompleto,CorreoInstitucional FROM dbo.ColaboradorDemo');
IF OBJECT_ID('dbo.TipoActa','U') IS NULL
BEGIN
    CREATE TABLE dbo.TipoActa(IdTipoActa int PRIMARY KEY, Nombre nvarchar(100));
    INSERT dbo.TipoActa VALUES(1,N'Entrega'),(2,N'Devolución');
END;
IF OBJECT_ID('dbo.Acta','U') IS NULL
BEGIN
    CREATE TABLE dbo.Acta(IdActa int PRIMARY KEY,IdTipoActa int,NumeroActa varchar(50),IdColaborador int,
        FechaActa datetime,NombreColaborador nvarchar(255),IdentificacionColaborador varchar(32),
        CorreoColaborador varchar(255),TextoActaEntrega nvarchar(max),TextoIntroductorio nvarchar(256),
        TerminosCondiciones nvarchar(max),MotivoDevolucion nvarchar(1000),Observaciones nvarchar(2000),
        UsuarioCreacion varchar(100),VerificadoPor varchar(100),Activo bit);
    WITH numeros AS (SELECT 1 AS n UNION ALL SELECT n+1 FROM numeros WHERE n<24)
    INSERT dbo.Acta
    SELECT n,CASE WHEN n<=12 THEN 1 ELSE 2 END,CONCAT('DEMO-2026-',n),c.IdColaborador,
        DATEADD(day,n-1,CONVERT(datetime,'20261001')),c.NombreCompleto,CONCAT('DEMO-ID-',c.IdColaborador),
        c.CorreoInstitucional,
        CASE WHEN n<=12 THEN CONCAT(N'<p>Yo <b>',c.NombreCompleto,N'</b> asumo la custodia, cuidado y responsabilidad de los equipos entregados. Debo reportar danos y mantener el inventario actualizado.</p>')
             ELSE CONCAT(N'<p>Yo <b>',c.NombreCompleto,N'</b> confirmo la devolucion de los equipos al area de tecnologia. Se verifica el estado, los accesorios y el registro de devolucion.</p>') END,
        N'Acta ficticia para demostracion academica.',N'Uso exclusivo de demostracion.',NULL,NULL,'demo','demo',1
    FROM numeros JOIN dbo.ColaboradorDemo c ON c.IdColaborador=1+(n-1)%12 OPTION (MAXRECURSION 24);
END;
COMMIT TRANSACTION;
SELECT COUNT(*) AS ActivosFicticios FROM dbo.VistaEquipo;
SELECT COUNT(*) AS ActasFicticias FROM dbo.Acta;
