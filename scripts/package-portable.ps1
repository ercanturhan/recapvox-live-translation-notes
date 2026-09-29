param([string]$Version = (Get-Date -Format 'yyyy.MM.dd'))

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^[0-9A-Za-z][0-9A-Za-z._-]*$') { throw 'Geçersiz paket sürümü.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'src\Translator.Desktop\Translator.Desktop.csproj'
$dotnetExecutable = Join-Path $projectRoot '.dotnet-sdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExecutable)) { throw 'Kararlı .NET 10 SDK eksik. README içindeki kurulum adımını uygulayın.' }
$packageName = "RecapVox-Portable-win-x64-$Version"
$packageRoot = Join-Path $projectRoot "dist\$packageName"
$zipPath = Join-Path $projectRoot "dist\$packageName.zip"
if (Test-Path -LiteralPath $packageRoot) { throw "Paket klasörü zaten var: $packageRoot" }
if (Test-Path -LiteralPath $zipPath) { throw "Paket dosyası zaten var: $zipPath" }

$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet'
$env:APPDATA = Join-Path $projectRoot '.dotnet\AppData'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.dotnet\packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
New-Item -ItemType Directory -Force -Path $env:APPDATA | Out-Null
& $dotnetExecutable restore $projectFile --ignore-failed-sources -p:NuGetAudit=false --configfile (Join-Path $projectRoot 'NuGet.Config')
if ($LASTEXITCODE -ne 0) { throw 'NuGet geri yükleme başarısız.' }

$appDirectory = Join-Path $packageRoot 'app'
& $dotnetExecutable publish $projectFile -c Release --no-restore --output $appDirectory -p:UseAppHost=false -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Uygulama yayımlanamadı.' }
Get-ChildItem -LiteralPath $appDirectory -Filter '*.pdb' -File | Remove-Item

$runtimeConfig = Get-Content -LiteralPath (Join-Path $appDirectory 'RecapVox.runtimeconfig.json') -Raw | ConvertFrom-Json
$coreRequestedText = ($runtimeConfig.runtimeOptions.frameworks | Where-Object name -EQ 'Microsoft.NETCore.App').version
$desktopRequestedText = ($runtimeConfig.runtimeOptions.frameworks | Where-Object name -EQ 'Microsoft.WindowsDesktop.App').version
$coreRequested = [version](($coreRequestedText -split '-')[0])
$desktopRequested = [version](($desktopRequestedText -split '-')[0])
$dotnetRoot = Split-Path -Parent $dotnetExecutable
$coreVersion = if (Test-Path (Join-Path $dotnetRoot "shared\Microsoft.NETCore.App\$coreRequestedText")) { $coreRequestedText } else { Get-ChildItem (Join-Path $dotnetRoot 'shared\Microsoft.NETCore.App') -Directory | Where-Object { try { $v = [version]$_.Name; $v.Major -eq $coreRequested.Major -and $v.Minor -eq $coreRequested.Minor -and $v -ge $coreRequested } catch { $false } } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1 -ExpandProperty Name }
$desktopVersion = if (Test-Path (Join-Path $dotnetRoot "shared\Microsoft.WindowsDesktop.App\$desktopRequestedText")) { $desktopRequestedText } else { Get-ChildItem (Join-Path $dotnetRoot 'shared\Microsoft.WindowsDesktop.App') -Directory | Where-Object { try { $v = [version]$_.Name; $v.Major -eq $desktopRequested.Major -and $v.Minor -eq $desktopRequested.Minor -and $v -ge $desktopRequested } catch { $false } } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1 -ExpandProperty Name }
if (-not $coreVersion -or -not $desktopVersion) { throw 'Uyumlu kararlı .NET çalışma zamanı bulunamadı.' }
$runtimeRoot = Join-Path $packageRoot 'runtime'
$coreSource = Join-Path $dotnetRoot "shared\Microsoft.NETCore.App\$coreVersion"
$desktopSource = Join-Path $dotnetRoot "shared\Microsoft.WindowsDesktop.App\$desktopVersion"
$fxrVersion = if (Test-Path (Join-Path $dotnetRoot "host\fxr\$coreRequestedText")) { $coreRequestedText } else { Get-ChildItem (Join-Path $dotnetRoot 'host\fxr') -Directory | Where-Object { try { $v = [version]$_.Name; $v.Major -eq $coreRequested.Major -and $v.Minor -eq $coreRequested.Minor } catch { $false } } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1 -ExpandProperty Name }
if (-not $fxrVersion) { throw 'Uyumlu .NET host bulunamadı.' }
$fxrSource = Join-Path $dotnetRoot "host\fxr\$fxrVersion"
foreach ($path in @($coreSource, $desktopSource, $fxrSource))
{
    if (-not (Test-Path -LiteralPath $path)) { throw "Gerekli .NET çalışma zamanı bulunamadı: $path" }
}
New-Item -ItemType Directory -Force -Path (Join-Path $runtimeRoot 'shared\Microsoft.NETCore.App'),
    (Join-Path $runtimeRoot 'shared\Microsoft.WindowsDesktop.App'), (Join-Path $runtimeRoot 'host\fxr') | Out-Null
Copy-Item -LiteralPath (Join-Path $dotnetRoot 'dotnet.exe') -Destination $runtimeRoot
Copy-Item -LiteralPath $coreSource -Destination (Join-Path $runtimeRoot 'shared\Microsoft.NETCore.App') -Recurse
Copy-Item -LiteralPath $desktopSource -Destination (Join-Path $runtimeRoot 'shared\Microsoft.WindowsDesktop.App') -Recurse
Copy-Item -LiteralPath $fxrSource -Destination (Join-Path $runtimeRoot 'host\fxr') -Recurse
Copy-Item -LiteralPath (Join-Path $dotnetRoot 'LICENSE.txt') -Destination (Join-Path $runtimeRoot 'DOTNET-LICENSE.txt')

@'
@echo off
setlocal
set "DOTNET_ROOT=%~dp0runtime"
"%~dp0runtime\dotnet.exe" "%~dp0app\RecapVox.dll"
'@ | Set-Content -LiteralPath (Join-Path $packageRoot 'RecapVox.cmd') -Encoding Ascii

@'
RecapVox — Portable Windows x64
Live translation · Transcription · Audio recording · AI summaries

Extract the ZIP completely, then double-click RecapVox.cmd. No separate .NET installation is required.
Windows 11 x64 is required. Internet and your own provider API key are needed for transcription, live translation and AI summaries.
Settings and recordings are saved under your Windows user profile; the ZIP contains no API keys or personal recordings.

ZIP dosyasını tamamen çıkarın ve RecapVox.cmd dosyasına çift tıklayın.
Ayrıca .NET kurulumu gerekmez. Transkripsiyon, canlı çeviri ve özet için internet ve kendi API anahtarınız gerekir.
Bu pakette API anahtarı, kişisel kayıt veya konuşma bulunmaz.
'@ | Set-Content -LiteralPath (Join-Path $packageRoot 'README.txt') -Encoding UTF8

@'
Bundled third-party components

Microsoft .NET runtime: see runtime/DOTNET-LICENSE.txt.
NAudio 3.1.0 and its component packages: MIT license, https://licenses.nuget.org/MIT
System.Numerics.Tensors: MIT license, https://licenses.nuget.org/MIT
'@ | Set-Content -LiteralPath (Join-Path $packageRoot 'THIRD-PARTY-NOTICES.txt') -Encoding UTF8

Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal
Add-Type -AssemblyName System.IO.Compression
$archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try
{
    if (-not ($archive.Entries.FullName -match 'app/RecapVox.dll$') -or
        -not ($archive.Entries.FullName -match 'runtime/dotnet.exe$') -or
        -not ($archive.Entries.FullName -match 'RecapVox.cmd$'))
        { throw 'ZIP içinde gerekli dosyalar bulunamadı.' }
}
finally { $archive.Dispose() }
$distRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'dist')).TrimEnd('\')
$stagePath = [System.IO.Path]::GetFullPath($packageRoot)
if (-not $stagePath.StartsWith($distRoot + '\', [System.StringComparison]::OrdinalIgnoreCase))
    { throw 'Paket klasörü dist dışında; geçici klasör silinmedi.' }
Remove-Item -LiteralPath $stagePath -Recurse -Force
Write-Output $zipPath
