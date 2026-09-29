$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnetExecutable = Join-Path $projectRoot '.dotnet-sdk\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExecutable)) { throw 'Kararlı .NET 10 SDK eksik. README içindeki kurulum adımını uygulayın.' }
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet'
$env:APPDATA = Join-Path $projectRoot '.dotnet\AppData'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.dotnet\packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
New-Item -ItemType Directory -Force -Path $env:APPDATA | Out-Null
$projectFile = Join-Path $projectRoot 'src\Translator.Desktop\Translator.Desktop.csproj'
$runDirectory = Join-Path $projectRoot ('.dotnet\runs\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
& $dotnetExecutable build $projectFile --configfile (Join-Path $projectRoot 'NuGet.Config') --output $runDirectory -p:UseAppHost=false
if ($LASTEXITCODE -ne 0) { throw 'Uygulama derlenemedi.' }
$app = Join-Path $runDirectory 'RecapVox.dll'
& $dotnetExecutable $app
