[CmdletBinding()]
param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot '..\dist')
)

$ErrorActionPreference = 'Stop'
$resolvedSource = (Resolve-Path -LiteralPath $SourceDirectory).Path
$sourceExe = Join-Path $resolvedSource 'PandaPet.exe'
if (-not (Test-Path -LiteralPath $sourceExe -PathType Leaf)) {
    throw "Не найден файл сборки: $sourceExe"
}

$expectedInstallDirectory = [IO.Path]::GetFullPath(
    (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\PandaPet'))
$installDirectory = $expectedInstallDirectory
if ($installDirectory -ne $expectedInstallDirectory) {
    throw "Небезопасный путь установки: $installDirectory"
}

Get-Process -Name PandaPet -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null

$installedExe = Join-Path $installDirectory 'PandaPet.exe'
$installedUninstaller = Join-Path $installDirectory 'Uninstall-PandaPet.ps1'
Copy-Item -LiteralPath $sourceExe -Destination $installedExe -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall-PandaPet.ps1') -Destination $installedUninstaller -Force
$displayVersion = (Get-Item -LiteralPath $installedExe).VersionInfo.ProductVersion
if ([string]::IsNullOrWhiteSpace($displayVersion)) {
    $displayVersion = '0.0.0'
}
$displayVersion = ($displayVersion -split '\+')[0]

$shell = New-Object -ComObject WScript.Shell
$shortcutPaths = @(
    (Join-Path ([Environment]::GetFolderPath('Programs')) 'PandaPet.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Desktop')) 'PandaPet.lnk')
)
foreach ($shortcutPath in $shortcutPaths) {
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.IconLocation = "$installedExe,0"
    $shortcut.Description = 'Панда-помощник для рабочего стола'
    $shortcut.Save()
}

$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PandaPet'
New-Item -Path $uninstallKey -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name DisplayName -Value 'PandaPet — панда-помощник' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name DisplayVersion -Value $displayVersion -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name Publisher -Value 'PandaPet' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name DisplayIcon -Value "$installedExe,0" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name InstallLocation -Value $installDirectory -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name UninstallString -Value "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$installedUninstaller`"" -PropertyType String -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $uninstallKey -Name EstimatedSize -Value ([int]((Get-Item -LiteralPath $installedExe).Length / 1KB)) -PropertyType DWord -Force | Out-Null

Start-Process -FilePath $installedExe
Write-Output $installedExe
