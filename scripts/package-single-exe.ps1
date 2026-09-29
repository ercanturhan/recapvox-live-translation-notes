param([string]$Version = (Get-Date -Format 'yyyy.MM.dd'))

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^[0-9A-Za-z][0-9A-Za-z._-]*$') { throw 'Geçersiz paket sürümü.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework C# derleyicisi bulunamadı.' }
$zipPath = Join-Path $projectRoot "dist\RecapVox-Portable-win-x64-$Version.zip"
$exePath = Join-Path $projectRoot "dist\RecapVox-win-x64-$Version.exe"
if ((Test-Path -LiteralPath $zipPath) -or (Test-Path -LiteralPath $exePath)) { throw 'Bu sürüm için paket zaten var.' }

& (Join-Path $PSScriptRoot 'package-portable.ps1') -Version $Version | Out-Host
if (-not (Test-Path -LiteralPath $zipPath)) { throw 'Taşınabilir paket oluşturulmadı.' }

$compilerArgs = @('/nologo', '/target:winexe', '/platform:x64', "/out:$exePath",
    "/win32icon:$(Join-Path $projectRoot 'assets\recapvox.ico')",
    '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll',
    '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll',
    (Join-Path $PSScriptRoot 'RecapVoxLauncher.cs'))
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Tek dosya başlatıcısı derlenemedi.' }

$zipLength = (Get-Item -LiteralPath $zipPath).Length
$target = [System.IO.File]::Open($exePath, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write)
try
{
    $source = [System.IO.File]::OpenRead($zipPath)
    try { $source.CopyTo($target) } finally { $source.Dispose() }
    $writer = New-Object System.IO.BinaryWriter($target, [System.Text.Encoding]::ASCII, $true)
    try
    {
        $writer.Write([System.Text.Encoding]::ASCII.GetBytes('RVPKG001'))
        $writer.Write([long]$zipLength)
    }
    finally { $writer.Dispose() }
}
finally { $target.Dispose() }

$check = [System.IO.File]::OpenRead($exePath)
try
{
    $check.Seek(-16, [System.IO.SeekOrigin]::End) | Out-Null
    $reader = New-Object System.IO.BinaryReader($check)
    if ([System.Text.Encoding]::ASCII.GetString($reader.ReadBytes(8)) -ne 'RVPKG001' -or $reader.ReadInt64() -ne $zipLength)
        { throw 'Tek dosya paketinin son bilgisi doğrulanamadı.' }
}
finally { $check.Dispose() }

Remove-Item -LiteralPath $zipPath -Force
Write-Output $exePath
