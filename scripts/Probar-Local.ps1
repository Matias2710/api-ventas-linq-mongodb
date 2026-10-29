param([Parameter(Mandatory=$true)][string]$MongodPath, [int]$Port=27029)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$data=Join-Path $root '.local-test/data'
$log=Join-Path $root '.local-test/mongod.log'
New-Item -ItemType Directory -Path $data -Force | Out-Null
$process=Start-Process -FilePath (Resolve-Path -LiteralPath $MongodPath).Path -ArgumentList @(
    '--dbpath',('"'+$data+'"'),'--bind_ip','127.0.0.1','--port',$Port,'--logpath',('"'+$log+'"'),
    '--setParameter','diagnosticDataCollectionEnabled=false') -WindowStyle Hidden -PassThru
$previousUri=$env:VENTAS_TEST_URI; $previousEvidence=$env:VENTAS_EVIDENCE_DIR
try {
    $ready=$false
    for($i=0;$i -lt 50;$i++){
        if($process.HasExited){throw 'MongoDB no inició. Compruebe el puerto y el archivo .local-test/mongod.log.'}
        $tcp=[System.Net.Sockets.TcpClient]::new()
        try{$tcp.Connect('127.0.0.1',$Port); $ready=$true; break}catch{Start-Sleep -Milliseconds 100}finally{$tcp.Dispose()}
    }
    if(-not $ready){throw 'MongoDB no está disponible.'}
    $env:VENTAS_TEST_URI="mongodb://127.0.0.1:$Port"
    $env:VENTAS_EVIDENCE_DIR=Join-Path $root 'evidencias'
    # Aislar la compilación evita el bloqueo de apphost.exe si la API principal está ejecutándose.
    dotnet test (Join-Path $root 'Ventas.sln') -c Release --artifacts-path (Join-Path $root '.local-test/artifacts') --logger 'trx;LogFileName=pruebas.trx' --results-directory (Join-Path $root 'evidencias')
    if($LASTEXITCODE -ne 0){throw 'Hay pruebas fallidas.'}
}finally{
    $env:VENTAS_TEST_URI=$previousUri; $env:VENTAS_EVIDENCE_DIR=$previousEvidence
    if(-not $process.HasExited){Stop-Process -Id $process.Id}
}
