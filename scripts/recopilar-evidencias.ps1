param([string]$ApiUrl='http://localhost:5225',[switch]$OmitirIntegracion,[ValidateSet('Real','Demo')][string]$Perfil='Real')
$ErrorActionPreference='Stop'
$raiz=Split-Path $PSScriptRoot -Parent
$salida=Join-Path $raiz 'resultados/consolidacion'
New-Item -ItemType Directory -Path $salida -Force | Out-Null
$trx=Join-Path $raiz '.local/test-results/consolidacion.trx'
if(-not (Test-Path -LiteralPath $trx)){throw 'Ejecuta primero las pruebas con logger TRX según el README.'}
[xml]$xml=Get-Content -LiteralPath $trx -Raw
$contadores=$xml.TestRun.ResultSummary.Counters
$pruebas=[ordered]@{
    fecha_corte='2026-10-29';total=[int]$contadores.total;superadas=[int]$contadores.passed
    fallidas=[int]$contadores.failed;sin_ejecutar=[int]$contadores.notExecuted
    robustez_sql=30;alcance_robustez='30 cargas SQL adversariales del conjunto declarado, no una garantía universal.'
    comando='dotnet test SynerGy-Rag-Inventario.slnx -c Release --logger trx'
    pruebas=@($xml.TestRun.Results.UnitTestResult|ForEach-Object {[ordered]@{nombre=$_.testName;resultado=$_.outcome}})
}
$pruebas|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $salida 'pruebas_automaticas.json') -Encoding utf8
$modelos=(Invoke-RestMethod 'http://127.0.0.1:11434/api/tags' -TimeoutSec 10).models|
    Where-Object {$_.name -match '^(qwen2.5-coder:7b|nomic-embed-text)'}|Select-Object name,digest,size
$cpu=Get-CimInstance Win32_Processor|Select-Object -First 1
$equipo=Get-CimInstance Win32_ComputerSystem
$entorno=[ordered]@{
    fecha_corte='2026-10-29';dotnet_sdk=(& dotnet --version);ollama=(& ollama --version)
    sistema='Windows';cpu=$cpu.Name;procesadores_logicos=$cpu.NumberOfLogicalProcessors
    ram_gb=[math]::Round($equipo.TotalPhysicalMemory/1GB,1);inferencia_validada='CPU'
    modelos=@($modelos);modelo_generacion='qwen2.5-coder:7b';temperatura_generacion=0.1
    modelo_embeddings='nomic-embed-text';dimensiones=768;top_k=5;distancia_maxima=0.65
    catalogo='inventario-v2';actas='actas-v1';sql_server='2025 (17.x)'
    datos_publicados='No se incluyen conexiones, contraseñas, nombres de equipo, custodios ni resultados operacionales.'
}
$entorno|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $salida 'entorno.json') -Encoding utf8
if(-not $OmitirIntegracion){
    $cadena=$env:ConnectionStrings__ActivosInformaticos
    if(-not $cadena){
        $archivo=Join-Path $env:APPDATA 'Microsoft/UserSecrets/3045dc74-5e36-472c-858b-947dd28b8cfd/secrets.json'
        $secretos=Get-Content -LiteralPath $archivo -Raw|ConvertFrom-Json -AsHashtable
        $cadena=$secretos['ConnectionStrings:ActivosInformaticos']
    }
    if(-not $cadena){throw 'Falta la conexión de referencia. No se registra su contenido.'}
    $cs=[Data.Common.DbConnectionStringBuilder]::new();$cs.set_ConnectionString($cadena)
    $servidor=$null;$base=$null;$usuario=$null;$clave=$null;$integrada=$false
    foreach($k in $cs.Keys){
        switch -Regex ($k){
            '^(Server|Data Source)$' {$servidor=[string]$cs.get_Item($k)}
            '^(Database|Initial Catalog)$' {$base=[string]$cs.get_Item($k)}
            '^(User Id|UID)$' {$usuario=[string]$cs.get_Item($k)}
            '^(Password|PWD)$' {$clave=[string]$cs.get_Item($k)}
            '^(Integrated Security|Trusted_Connection)$' {$integrada=[string]$cs.get_Item($k) -match '^(true|sspi|yes)$'}
        }
    }
    function Referencia([string]$consulta){
        $argumentos=@('-S',$servidor,'-d',$base,'-C','-l','8','-t','15','-b','-h','-1','-W','-Q',('SET NOCOUNT ON; '+$consulta))
        $claveAnterior=$env:SQLCMDPASSWORD
        try{
            if($integrada){$argumentos+=@('-E')}else{$argumentos+=@('-U',$usuario);$env:SQLCMDPASSWORD=$clave}
            $resultado=& sqlcmd @argumentos 2>&1
            if($LASTEXITCODE -ne 0){throw 'No se pudo consultar la referencia SQL.'}
            return [int]($resultado|Where-Object {$_ -match '^\s*\d+\s*$'}|Select-Object -First 1)
        }finally{$env:SQLCMDPASSWORD=$claveAnterior}
    }
    $filtroLaptop="(NombreCategoria COLLATE Latin1_General_100_CI_AI=N'LAPTOP' OR (NombreCategoria COLLATE Latin1_General_100_CI_AI=N'COMPUTADOR' AND Descripcion COLLATE Latin1_General_100_CI_AI LIKE N'%Laptop%'))"
    $filtroMouse="(NombreCategoria COLLATE Latin1_General_100_CI_AI=N'MOUSE' OR (NombreCategoria COLLATE Latin1_General_100_CI_AI=N'PERIFERICO' AND Descripcion COLLATE Latin1_General_100_CI_AI LIKE N'%Mouse%'))"
    $referencias=@{
        conteo_laptop=(Referencia "SELECT COUNT(DISTINCT IdEquipo) FROM dbo.VistaEquipo WHERE $filtroLaptop")
        conteo_mouse=(Referencia "SELECT COUNT(DISTINCT IdEquipo) FROM dbo.VistaEquipo WHERE $filtroMouse")
        asignadas=(Referencia "SELECT COUNT(DISTINCT IdEquipo) FROM dbo.VistaEquipo WHERE $filtroLaptop AND IdEstadoAsignacion=2")
    }
    $casos=@(
        @{Id='I01';Pregunta='Dame el total de las laptops';Tipo='conteo_laptop'},
        @{Id='I02';Pregunta='¿Cuántos mouses hay?';Tipo='conteo_mouse'},
        @{Id='I03';Pregunta='Dame los equipos de laptops';Tipo='lista_laptop'},
        @{Id='I04';Pregunta='¿Cuántas laptops hay asignadas?';Tipo='asignadas'},
        @{Id='I05';Pregunta='Muéstrame los equipos asignados a Persona Inexistente Prueba';Tipo='sin_persona'},
        @{Id='I06';Pregunta='Muéstrame los equipos asignados a Juan';Tipo='ambiguo'},
        @{Id='I07';Pregunta='Busca actas de devolución sobre la responsabilidad del cuidado de equipos';Tipo='actas'},
        @{Id='I08';Pregunta='¿Cuántas actas hay?';Tipo='no_total_vectorial'}
    )
    if($Perfil -eq 'Demo'){$casos[5].Pregunta='Muéstrame los equipos asignados a Demo'}
    $resultados=@()
    foreach($caso in $casos){
        $body=@{pregunta=$caso.Pregunta}|ConvertTo-Json
        $r=Invoke-RestMethod ($ApiUrl.TrimEnd('/')+'/api/preguntas') -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 330
        if($r.esDemostracion){throw 'La demo visual no genera evidencias de integración real.'}
        $ok=-not $r.error
        switch($caso.Tipo){
            'conteo_laptop' {$ok=$ok -and $r.metodoGeneracion -eq 'consulta_validada' -and $r.filas.Count -eq 1 -and [int]$r.filas[0].total -eq $referencias.conteo_laptop}
            'conteo_mouse' {$ok=$ok -and $r.metodoGeneracion -eq 'consulta_validada' -and $r.filas.Count -eq 1 -and [int]$r.filas[0].total -eq $referencias.conteo_mouse}
            'lista_laptop' {$ok=$ok -and $r.filas.Count -gt 0 -and $r.filas.Count -le 100 -and -not $r.requiereAclaracion}
            'asignadas' {$ok=$ok -and $r.metodoGeneracion -eq 'consulta_validada' -and $r.filas.Count -eq 1 -and [int]$r.filas[0].total -eq $referencias.asignadas}
            'sin_persona' {$ok=$ok -and $r.requiereAclaracion -and -not $r.sql}
            'ambiguo' {$ok=$ok -and $r.requiereAclaracion -and $r.candidatos.Count -gt 1 -and -not $r.sql}
            'actas' {$ok=$ok -and $r.metodoGeneracion -eq 'busqueda_vectorial_actas' -and $r.contexto.Count -gt 0 -and @($r.filas|Where-Object {$_.tipo_acta -notmatch 'Devol'}).Count -eq 0}
            'no_total_vectorial' {$ok=$ok -and $r.requiereAclaracion -and $r.filas.Count -eq 0}
        }
        $resultados+=[ordered]@{id=$caso.Id;tipo=$caso.Tipo;resultado=if($ok){'superado'}else{'fallido'};metodo=$r.metodoGeneracion;latencia_ms=$r.latenciaMs}
    }
    [ordered]@{fecha_corte='2026-10-29';alcance='Pruebas funcionales seleccionadas en la instalación autorizada. No es exactitud global ni evaluación ciega.';casos=$resultados}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $salida 'integracion.json') -Encoding utf8
    if(@($resultados|Where-Object {$_.resultado -eq 'fallido'}).Count -gt 0){throw 'Hay casos de integración fallidos. Revisar sin publicar datos personales.'}
}
Write-Output 'Evidencias resumidas guardadas sin datos personales ni credenciales.'
