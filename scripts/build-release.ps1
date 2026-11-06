param(
    [string]$UpdateConfigFile,
    [string]$LicensePublicKeyFile,
    [string]$ServiceConfigFile,
    [string]$MakeNsis = 'makensis.exe',
    [string]$MSBuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
[xml]$versionDocument = Get-Content (Join-Path $projectRoot 'version.props')
$productVersion = $versionDocument.Project.PropertyGroup.ProductVersion
if ($productVersion -notmatch '^(0|[1-9][0-9]{0,3})\.(0|[1-9][0-9]{0,3})\.(0|[1-9][0-9]{0,3})$') { throw 'Invalid product version' }
$buildArguments = @((Join-Path $projectRoot 'src\LetSmall\LetSmall.csproj'), '/t:Build', '/p:Configuration=Release', '/p:Platform=x64', '/nologo', '/v:minimal')
foreach ($configName in @('UpdateConfigFile', 'LicensePublicKeyFile', 'ServiceConfigFile')) {
    $configValue = Get-Variable $configName -ValueOnly
    if ($configValue) { $buildArguments += "/p:$configName=$((Resolve-Path -LiteralPath $configValue).Path)" }
}
& $MSBuild @buildArguments
if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed' }
$appPath = Join-Path $projectRoot 'src\LetSmall\bin\Release\letsmall.exe'
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($appPath).ProductVersion -ne $productVersion) { throw 'Application version mismatch' }
& $MakeNsis "/DAPP_VERSION=$productVersion" (Join-Path $projectRoot 'installer\letsmall.nsi')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
$installerPath = Join-Path $projectRoot 'artifacts\letsmall-setup.exe'
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($installerPath).ProductVersion -ne $productVersion) { throw 'Installer version mismatch' }
if ((Get-Item $installerPath).Length -ge 30MB) { throw 'Installer exceeds size limit' }
