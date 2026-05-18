# download-libs.ps1
# Descarga EasyMDE y highlight.js al directorio wwwroot del proyecto.
# Ejecutar una sola vez antes del primer build.
#
# Uso: powershell -ExecutionPolicy Bypass -File .\download-libs.ps1

param(
    [switch]$Force  # Re-descarga aunque ya existan los archivos
)

$wwwrootJs  = Join-Path $PSScriptRoot "wwwroot\js"
$wwwrootCss = Join-Path $PSScriptRoot "wwwroot\css"

$libs = @(
    @{
        Url  = "https://unpkg.com/easymde/dist/easymde.min.js"
        Dest = Join-Path $wwwrootJs "easymde.min.js"
        Name = "EasyMDE JS"
    },
    @{
        Url  = "https://unpkg.com/easymde/dist/easymde.min.css"
        Dest = Join-Path $wwwrootCss "easymde.min.css"
        Name = "EasyMDE CSS"
    },
    @{
        Url  = "https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js"
        Dest = Join-Path $wwwrootJs "highlight.min.js"
        Name = "highlight.js"
    },
    @{
        Url  = "https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/github.min.css"
        Dest = Join-Path $wwwrootCss "highlight.min.css"
        Name = "highlight.js CSS (github theme)"
    }
)

$wc = New-Object System.Net.WebClient
$ok = 0
$skip = 0

foreach ($lib in $libs) {
    if ((Test-Path $lib.Dest) -and -not $Force) {
        Write-Host "  [SKIP] $($lib.Name) — ya existe" -ForegroundColor DarkGray
        $skip++
        continue
    }
    try {
        Write-Host "  [DOWN] $($lib.Name)..." -NoNewline
        $wc.DownloadFile($lib.Url, $lib.Dest)
        $size = (Get-Item $lib.Dest).Length / 1KB
        Write-Host " OK ($([math]::Round($size, 1)) KB)" -ForegroundColor Green
        $ok++
    } catch {
        Write-Host " ERROR: $_" -ForegroundColor Red
    }
}

$wc.Dispose()
Write-Host ""
Write-Host "Descarga completada: $ok descargados, $skip omitidos." -ForegroundColor Cyan
Write-Host "Ahora podés compilar el proyecto con: clarioncom-build" -ForegroundColor Cyan
