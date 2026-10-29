param([string]$Database = 'VentasAcademicas')
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../src/Ventas.Api/Ventas.Api.csproj'
if ($Database -notmatch '^[A-Za-z][A-Za-z0-9_]{0,62}$') { throw 'Nombre de base inválido.' }
$secure = Read-Host 'Pegue la URI SRV de Atlas (entrada oculta; no se guarda en el historial)' -AsSecureString
try {
    $uri = [System.Net.NetworkCredential]::new('', $secure).Password
    if ($uri -notmatch '^mongodb\+srv://[^\s]+\.mongodb\.net(?:[/:?]|$)') { throw 'Se requiere una URI SRV de MongoDB Atlas.' }
    @{ MongoDB = @{ ConnectionString=$uri; Database=$Database; LocalTest='false' } } |
        ConvertTo-Json -Depth 3 | dotnet user-secrets set --project $project
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo guardar la configuración.' }
    Write-Output 'Configuración guardada en .NET User Secrets, fuera del repositorio.'
} finally { $uri=$null; $secure.Dispose() }
