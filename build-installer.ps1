<#
.SYNOPSIS
    Membuat installer WinToolbar (artifacts\installer\WinToolbar-Setup-<versi>.exe).

.DESCRIPTION
    1. Menjalankan test.
    2. Publish aplikasi (Release, win-x64) ke artifacts\publish.
    3. Membungkusnya dengan Inno Setup memakai installer\WinToolbar.iss.

    Butuh Inno Setup 6:  winget install JRSoftware.InnoSetup

.PARAMETER FrameworkDependent
    Installer kecil, tapi komputer tujuan harus punya .NET 10 Desktop Runtime.
    Tanpa switch ini runtime ikut dibundel (installer lebih besar, jalan di komputer mana pun).

.PARAMETER SkipTests
    Lewati dotnet test.

.EXAMPLE
    .\build-installer.ps1
.EXAMPLE
    .\build-installer.ps1 -FrameworkDependent
#>
[CmdletBinding()]
param(
    [switch]$FrameworkDependent,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'src\WinToolbar\WinToolbar.csproj'
$publishDir = Join-Path $root 'artifacts\publish'
$installerDir = Join-Path $root 'artifacts\installer'
$issFile = Join-Path $root 'installer\WinToolbar.iss'

function Find-InnoSetup {
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    foreach ($path in $candidates) {
        if (Test-Path $path) { return $path }
    }
    return $null
}

function Invoke-Step([string]$Name, [scriptblock]$Action) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name gagal (exit code $LASTEXITCODE)." }
}

# Cek dulu supaya tidak menunggu publish hanya untuk gagal di langkah terakhir.
$iscc = Find-InnoSetup
if (-not $iscc) {
    throw "Inno Setup 6 tidak ditemukan. Pasang dengan: winget install JRSoftware.InnoSetup"
}

[xml]$csproj = Get-Content $project
$version = ($csproj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw "Properti <Version> tidak ada di $project." }
Write-Host "WinToolbar versi $version"

if (-not $SkipTests) {
    Invoke-Step 'Test' { dotnet test (Join-Path $root 'WinToolbar.slnx') -c Release --nologo -v q }
}

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
Invoke-Step 'Publish' {
    dotnet publish $project -c Release -r win-x64 --self-contained $selfContained -o $publishDir --nologo -v q -p:DebugType=none
}

New-Item -ItemType Directory -Force $installerDir | Out-Null
Invoke-Step 'Inno Setup' {
    & $iscc /Q "/DAppVersion=$version" "/DSourceDir=$publishDir" "/DOutputDir=$installerDir" $issFile
}

$setup = Get-Item (Join-Path $installerDir "WinToolbar-Setup-$version.exe")
Write-Host ("Selesai: {0} ({1:N1} MB)" -f $setup.FullName, ($setup.Length / 1MB)) -ForegroundColor Green
