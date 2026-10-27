param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist'),
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\HYBurningStudio\HYBurningStudio.csproj'))
$buildRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\artifacts\installer'))
$publish = Join-Path $buildRoot 'publish'
$manifest = Join-Path $buildRoot 'uninstall-files.nsh'
$output = [IO.Path]::GetFullPath($OutputDirectory)
$installer = Join-Path $output 'HY-Burning-Studio-Setup-v0.2.0-beta-x64.exe'
$compiler = Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $command = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if (!$command) { throw 'NSIS (makensis.exe) tidak ditemukan. Instal NSIS 3 untuk membuat installer.' }
    $compiler = $command.Source
}
if (!(Test-Path -LiteralPath $project)) { throw "Proyek tidak ditemukan: $project" }
New-Item -ItemType Directory -Force -Path $buildRoot, $output | Out-Null
if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }

& dotnet publish $project --configuration $Configuration --runtime win-x64 --self-contained true --output $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish gagal (kode $LASTEXITCODE)." }
if (!(Test-Path -LiteralPath (Join-Path $publish 'HYBurningStudio.exe'))) { throw 'HYBurningStudio.exe tidak ditemukan dalam hasil publish.' }

# Generate a precise uninstall list. Do not recursively remove the installation
# directory, which may contain files created by the user or another program.
$escapedFiles = Get-ChildItem -LiteralPath $publish -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($publish.Length).TrimStart('\')
    if ($relative.Contains('"')) { throw "Nama file tidak didukung NSIS: $relative" }
    $relative = $relative.Replace('$', '$$')
    '  Delete "$INSTDIR\' + $relative + '"'
}
$escapedDirectories = Get-ChildItem -LiteralPath $publish -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
    $relative = $_.FullName.Substring($publish.Length).TrimStart('\').Replace('$', '$$')
    '  RMDir "$INSTDIR\' + $relative + '"'
}
@($escapedFiles) + @($escapedDirectories) | Set-Content -LiteralPath $manifest -Encoding utf8
& $compiler "/DPUBLISH_DIR=$publish" "/DOUTPUT_FILE=$installer" "/DUNINSTALL_INCLUDE=$manifest" (Join-Path $PSScriptRoot 'HYBurningStudio.nsi')
if ($LASTEXITCODE -ne 0) { throw "Pembuatan installer gagal (kode $LASTEXITCODE)." }
if (!(Test-Path -LiteralPath $installer)) { throw 'Installer tidak ditemukan setelah proses selesai.' }
Write-Host "Installer selesai: $installer"
