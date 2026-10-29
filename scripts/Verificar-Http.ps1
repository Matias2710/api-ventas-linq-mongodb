param([string]$BaseUrl='http://127.0.0.1:5088', [string]$OutputDirectory='', [switch]$ExpectAtlas)
$ErrorActionPreference='Stop'
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $PSScriptRoot '../evidencias/http' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$routes=@('/health/live','/health/ready','/api/ventas','/api/ventas/DEMO-V001',
    '/api/reportes/resumen','/api/reportes/estados','/api/reportes/productos',
    '/api/reportes/productos?ordenarPor=ingresos','/api/reportes/categorias','/api/reportes/clientes',
    '/api/reportes/ciudades','/api/reportes/vendedores','/api/reportes/canales','/api/reportes/tendencias',
    '/api/reportes/extremos','/api/reportes/ciudades-distintas',
    '/api/reportes/resumen?Desde=2026-07-02&Hasta=2026-07-02',
    '/api/reportes/resumen?Ciudad=Inexistente')
$responses=@(); $index=0
foreach($route in $routes){
    $r=Invoke-WebRequest ($BaseUrl.TrimEnd('/')+$route)
    if($r.StatusCode -ne 200 -or $r.Headers['Content-Type'] -notmatch 'json'){throw "HTTP inesperado: $route"}
    $json=$r.Content | ConvertFrom-Json
    if($route -eq '/health/ready' -and $ExpectAtlas -and $json.origen -ne 'MongoDB Atlas'){throw 'La conexión no es Atlas.'}
    if($route -eq '/api/ventas/DEMO-V001' -and [decimal]$json.datos.total -ne 1006830){throw 'Ejemplo de referencia incorrecto.'}
    if($route -eq '/api/ventas' -and $json.datos.total -lt 18){throw 'Faltan documentos de demostración.'}
    if($route -eq '/api/reportes/resumen?Ciudad=Inexistente' -and $json.datos.ventas -ne 0){throw 'Filtro vacío incorrecto.'}
    $index++
    $record=@{ruta=$route;codigo=[int]$r.StatusCode;respuesta=$json}
    $responses += $record
}
foreach($case in @(@{ruta='/api/ventas/no-existe';codigo=404},@{ruta='/api/reportes/resumen?Desde=mal';codigo=400})){
    $r=Invoke-WebRequest ($BaseUrl.TrimEnd('/')+$case.ruta) -SkipHttpErrorCheck
    if([int]$r.StatusCode -ne $case.codigo){throw "Código incorrecto: $($case.ruta)"}
    $responses+=@{ruta=$case.ruta;codigo=[int]$r.StatusCode;respuesta=($r.Content|ConvertFrom-Json)}
}
@{ejecutadoUtc=[DateTime]::UtcNow.ToString('o');baseUrl=$BaseUrl;solicitudes=$responses;correctas=$responses.Count} |
    ConvertTo-Json -Depth 30 | Set-Content (Join-Path $OutputDirectory 'verificacion.json') -Encoding utf8
Write-Output "$($responses.Count) solicitudes HTTP verificadas. Evidencias: $OutputDirectory"
