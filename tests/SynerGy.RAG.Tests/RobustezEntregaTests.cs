using SynerGy.RAG.Infrastructure.Security;
using Xunit;

namespace SynerGy.RAG.Tests;

public class RobustezEntregaTests
{
    [Theory]
    [InlineData("INSERT INTO VistaEquipo (IdEquipo) VALUES (1)")]
    [InlineData("UPDATE VistaEquipo SET Serie='x'")]
    [InlineData("DELETE FROM VistaEquipo")]
    [InlineData("DROP TABLE VistaEquipo")]
    [InlineData("ALTER TABLE VistaEquipo ADD x int")]
    [InlineData("TRUNCATE TABLE VistaEquipo")]
    [InlineData("CREATE TABLE copia (x int)")]
    [InlineData("GRANT SELECT ON VistaEquipo TO public")]
    [InlineData("EXEC sp_executesql N'SELECT * FROM VistaEquipo'")]
    [InlineData("SELECT * INTO copia FROM VistaEquipo")]
    [InlineData("SELECT * FROM VistaEquipo; SELECT * FROM sys.tables")]
    [InlineData("SELECT * FROM VistaEquipo SELECT * FROM VistaEquipo")]
    [InlineData("SELECT * FROM VistaEquipo -- comentario")]
    [InlineData("SELECT /* comentario */ * FROM VistaEquipo")]
    [InlineData("SELECT * FROM OtraBase.dbo.VistaEquipo")]
    [InlineData("SELECT * FROM OtroServidor.OtraBase.dbo.VistaEquipo")]
    [InlineData("SELECT * FROM rrhh.VistaEquipo")]
    [InlineData("SELECT * FROM dbo.Colaborador")]
    [InlineData("SELECT * FROM VistaEquipo v, sys.tables t")]
    [InlineData("SELECT * FROM VistaEquipo v JOIN sys.objects o ON 1=1")]
    [InlineData("SELECT (SELECT COUNT(*) FROM sys.tables) FROM VistaEquipo")]
    [InlineData("SELECT * FROM VistaEquipo UNION ALL SELECT * FROM sys.tables")]
    [InlineData("SELECT * FROM OPENROWSET('SQLNCLI','x','SELECT 1')")]
    [InlineData("SELECT * FROM VistaEquipo v CROSS APPLY OPENJSON('[1]') j")]
    [InlineData("SELECT @@VERSION FROM VistaEquipo")]
    [InlineData("SELECT SYSTEM_USER FROM VistaEquipo")]
    [InlineData("SELECT NEXT VALUE FOR dbo.secuencia FROM VistaEquipo")]
    [InlineData("SELECT dbo.funcionPrivada(Serie) FROM VistaEquipo")]
    [InlineData("SELECT * FROM VistaEquipo WITH (NOLOCK)")]
    [InlineData("SELECT * FROM VistaEquipo FOR JSON AUTO")]
    public void BloqueaTreintaEscenariosSqlAdversariales(string sql)
        => Assert.NotNull(new SqlValidator(["VistaEquipo"]).Validar(sql));
}
