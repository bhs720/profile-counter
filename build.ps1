<#
.SYNOPSIS
    Builds ProFile Counter: the native pfc-tool solution first, then the managed one.

.DESCRIPTION
    The managed and native projects live in separate solutions, because the dotnet
    CLI cannot build C++ projects. Splitting them removed the ordering guarantee a
    single solution build used to give: that a fresh pfc-tool.exe ends up beside a
    fresh ProFile Counter.exe. This script restores that, and is what to use before
    cutting a release. Both solutions write to app\x64\<Configuration>\.

    The native build is the slow half. It is only necessary after editing main.c,
    bumping the mupdf tag or refreshing a patch; day to day,
    `dotnet build app\ProFileCounter.sln` on its own is enough.

.PARAMETER Configuration
    Debug or Release. Defaults to Release.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        throw "vswhere.exe not found at $vswhere. Building pfc-tool needs Visual Studio with the C++ desktop workload."
    }

    $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
        -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1

    if (-not $msbuild) {
        throw 'No MSBuild found. Install the C++ desktop workload, and the v142 toolset (see CLAUDE.md).'
    }

    return $msbuild
}

$msbuild = Find-MSBuild

Write-Host "==> pfc-tool ($Configuration|x64)" -ForegroundColor Cyan
& $msbuild (Join-Path $PSScriptRoot 'app\pfc-tool\pfc-tool.sln') `
    /p:Configuration=$Configuration /p:Platform=x64 /nologo /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Native build failed with exit code $LASTEXITCODE." }

Write-Host "==> ProFile Counter ($Configuration)" -ForegroundColor Cyan
& dotnet build (Join-Path $PSScriptRoot 'app\ProFileCounter.sln') `
    -c $Configuration --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Managed build failed with exit code $LASTEXITCODE." }

Write-Host "==> Built to $(Join-Path $PSScriptRoot "app\x64\$Configuration")" -ForegroundColor Green
