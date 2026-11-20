[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$expectedInstallDirectory = [IO.Path]::GetFullPath(
    (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\PandaPet'))
$installDirectory = [IO.Path]::GetFullPath($PSScriptRoot)
if ($installDirectory -ne $expectedInstallDirectory) {
    throw "Отмена удаления: неожиданный путь $installDirectory"
}

Get-Process -Name PandaPet -ErrorAction SilentlyContinue | Stop-Process -Force

$shortcutPaths = @(
    (Join-Path ([Environment]::GetFolderPath('Programs')) 'PandaPet.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Desktop')) 'PandaPet.lnk')
)
foreach ($shortcutPath in $shortcutPaths) {
    if (Test-Path -LiteralPath $shortcutPath -PathType Leaf) {
        Remove-Item -LiteralPath $shortcutPath -Force
    }
}

$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PandaPet'
if (Test-Path -LiteralPath $uninstallKey) {
    Remove-Item -LiteralPath $uninstallKey -Recurse -Force
}

$cleanupCommand = "Start-Sleep -Milliseconds 800; if ([IO.Path]::GetFullPath('$installDirectory') -eq [IO.Path]::GetFullPath('$expectedInstallDirectory')) { Remove-Item -LiteralPath '$installDirectory' -Recurse -Force }"
Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -ArgumentList '-NoProfile', '-Command', $cleanupCommand
