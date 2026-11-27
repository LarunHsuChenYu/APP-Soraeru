#Requires -Version 5.1
<#
.SYNOPSIS
  為目前 Windows 使用者安裝宙黑體（Zev Hei TC），供 Word／PowerPoint／Excel 使用。

.EXAMPLE
  .\scripts\install-zev-hei-tc-font.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$src = Join-Path $repoRoot 'docs\fonts\zev-hei-tc'
$dest = Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\Fonts'
$regPath = 'HKCU:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts'

if (-not (Test-Path $src)) {
    throw "找不到字體目錄：$src"
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null
if (-not (Test-Path $regPath)) {
    New-Item -Path $regPath -Force | Out-Null
}

$files = @(
    @{ File = 'ZevHeiTC-N-VF.otf'; Display = 'ZevHeiTC-N (TrueType)' }
    @{ File = 'ZevHeiTC-F-VF.otf'; Display = 'ZevHeiTC-F (TrueType)' }
)

foreach ($item in $files) {
    $from = Join-Path $src $item.File
    if (-not (Test-Path $from)) {
        throw "缺少字體檔：$from"
    }
    $to = Join-Path $dest $item.File
    Copy-Item -LiteralPath $from -Destination $to -Force
    New-ItemProperty -Path $regPath -Name $item.Display -Value $to -PropertyType String -Force | Out-Null
    Write-Host "Installed $($item.File) -> $to"
}

Write-Host '完成。請關閉並重開 Word／PowerPoint／Excel，字體選單搜尋 ZevHeiTC-N。'
