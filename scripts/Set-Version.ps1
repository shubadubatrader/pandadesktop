[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [ValidatePattern('^[0-9A-Za-z.-]*$')]
    [string]$Prerelease = ''
)

$ErrorActionPreference = 'Stop'
$propsPath = Join-Path $PSScriptRoot '..\Directory.Build.props'
[xml]$props = Get-Content -LiteralPath $propsPath -Raw
$propertyGroup = $props.Project.PropertyGroup
$propertyGroup.VersionPrefix = $Version
$propertyGroup.VersionSuffix = $Prerelease
$props.Save((Resolve-Path -LiteralPath $propsPath).Path)

$fullVersion = if ([string]::IsNullOrWhiteSpace($Prerelease)) {
    $Version
} else {
    "$Version-$Prerelease"
}

Write-Output "Версия проекта изменена на $fullVersion"
Write-Output 'Обновите CHANGELOG.md, соберите проект и создайте Git-тег с префиксом v.'
