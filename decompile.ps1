#Requires -Version 5.1
<#
.SYNOPSIS
    把《戴森球计划》的游戏程序集反编译成 C# 源码，输出到仓库根目录的 SourceCode。

.DESCRIPTION
    默认反编译 Assembly-CSharp.dll（游戏主逻辑），用 ilspycmd 以“项目”形式导出：
    生成 SourceCode/*.csproj 和按类型拆分的 .cs 文件，方便在 IDE 里跳转、搜索、
    对照游戏实现写补丁。SourceCode 已在 .gitignore 中忽略，不要提交。

    找不到 ilspycmd 时会尝试 dotnet tool install --global ilspycmd（可用 -NoInstall 关闭）。

.PARAMETER GameDir
    游戏安装目录（包含 DSPGAME.exe 的那一层）。省略时自动在常见 Steam 库位置查找，
    并回退到 DSPMods.Shared.props 里配置的程序集路径。

.PARAMETER OutDir
    输出目录，默认 <仓库根目录>\SourceCode。

.PARAMETER Assembly
    要反编译的程序集名称（不带 .dll），默认 Assembly-CSharp。可传多个，
    例如 -Assembly Assembly-CSharp,UnityEngine.CoreModule。

.PARAMETER NestedDirectories
    按命名空间生成嵌套目录（ilspycmd 支持该参数时生效）。

.PARAMETER Force
    输出目录已存在时先删除再重新生成。

.PARAMETER NoInstall
    找不到 ilspycmd 时不自动安装。

.EXAMPLE
    pwsh -File .\decompile.ps1 -Force

.EXAMPLE
    pwsh -File .\decompile.ps1 -GameDir 'E:\Steam\steamapps\common\Dyson Sphere Program' -Assembly Assembly-CSharp,UnityEngine.CoreModule
#>
[CmdletBinding()]
param(
    [string]$GameDir,
    [string]$OutDir,
    [string[]]$Assembly = @('Assembly-CSharp'),
    [switch]$NestedDirectories,
    [switch]$Force,
    [switch]$NoInstall
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Resolve-GameDir([string]$Explicit) {
    if ($Explicit) {
        if (-not (Test-Path -LiteralPath (Join-Path $Explicit 'DSPGAME_Data\Managed\Assembly-CSharp.dll'))) {
            throw "指定的游戏目录里找不到 DSPGAME_Data\Managed\Assembly-CSharp.dll：$Explicit"
        }
        return (Resolve-Path -LiteralPath $Explicit).Path
    }

    $candidates = @(
        'D:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program',
        'C:\Program Files (x86)\Steam\steamapps\common\Dyson Sphere Program',
        'D:\Steam\steamapps\common\Dyson Sphere Program',
        'E:\Steam\steamapps\common\Dyson Sphere Program'
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath (Join-Path $candidate 'DSPGAME_Data\Managed\Assembly-CSharp.dll')) {
            return $candidate
        }
    }

    # 退而求其次：从 DSPMods.Shared.props 的 HintPath 反推（项目里已经配好了游戏路径）
    $props = Join-Path $PSScriptRoot 'DSPMods.Shared\DSPMods.Shared.props'
    if (Test-Path -LiteralPath $props) {
        $match = [regex]::Match((Get-Content -LiteralPath $props -Raw), 'HintPath>([^<]*Dyson Sphere Program[^<]*)<')
        if ($match.Success) {
            $dllPath = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $props) $match.Groups[1].Value))
            $managed = Split-Path -Parent $dllPath      # ...\DSPGAME_Data\Managed
            $data = Split-Path -Parent $managed         # ...\DSPGAME_Data
            $game = Split-Path -Parent $data            # ...\Dyson Sphere Program
            if (Test-Path -LiteralPath (Join-Path $game 'DSPGAME.exe')) {
                return $game
            }
        }
    }

    throw '找不到游戏目录，请用 -GameDir 指定（包含 DSPGAME.exe 的那一层）。'
}

function Resolve-IlSpyCmd {
    $command = Get-Command ilspycmd -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $local = Join-Path $env:USERPROFILE '.dotnet\tools\ilspycmd.exe'
    if (Test-Path -LiteralPath $local) { return $local }

    return $null
}

Write-Step '定位游戏目录'
$game = Resolve-GameDir $GameDir
$managedDir = Join-Path $game 'DSPGAME_Data\Managed'
Write-Host "    游戏目录: $game"

Write-Step '定位 ilspycmd'
$ilspy = Resolve-IlSpyCmd
if (-not $ilspy) {
    if ($NoInstall) {
        throw '未找到 ilspycmd，请先执行：dotnet tool install --global ilspycmd'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw '未找到 ilspycmd，也没有 dotnet，无法自动安装。请先安装 .NET SDK。'
    }
    Write-Host '    未找到 ilspycmd，正在通过 dotnet tool 安装…'
    & dotnet tool install --global ilspycmd
    $ilspy = Resolve-IlSpyCmd
    if (-not $ilspy) {
        throw 'ilspycmd 安装后仍然找不到，请检查 %USERPROFILE%\.dotnet\tools 是否在 PATH 中。'
    }
}
Write-Host "    ilspycmd: $ilspy"

if (-not $OutDir) {
    $OutDir = Join-Path $PSScriptRoot 'SourceCode'
}
$OutDir = [System.IO.Path]::GetFullPath($OutDir)
if ($OutDir -eq [System.IO.Path]::GetPathRoot($OutDir)) {
    throw "输出目录不能是磁盘根目录：$OutDir"
}

$dllPaths = @()
foreach ($name in $Assembly) {
    $path = if ($name -like '*.dll') { $name } else { Join-Path $managedDir ($name + '.dll') }
    if (-not (Test-Path -LiteralPath $path)) {
        throw "找不到程序集：$path"
    }
    $dllPaths += (Resolve-Path -LiteralPath $path).Path
}

if (Test-Path -LiteralPath $OutDir) {
    if (-not $Force) {
        throw "输出目录已存在：$OutDir。加 -Force 重新生成。"
    }
    Write-Step "清空 $OutDir"
    Remove-Item -LiteralPath $OutDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$helpText = & $ilspy --help 2>&1 | Out-String
$supportsNested = $helpText -match '--nested-directories'
if ($NestedDirectories -and -not $supportsNested) {
    Write-Warning '当前 ilspycmd 不支持 --nested-directories，忽略该参数。'
}

Write-Step ("反编译 " + ($Assembly -join ', '))
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$arguments = @('-p', '-o', $OutDir, '--disable-updatecheck')
if ($NestedDirectories -and $supportsNested) {
    $arguments += '--nested-directories'
}
$arguments += $dllPaths

& $ilspy @arguments
if ($LASTEXITCODE -ne 0) {
    throw "ilspycmd 退出码 $LASTEXITCODE，反编译失败。"
}
$stopwatch.Stop()

$files = @(Get-ChildItem -LiteralPath $OutDir -Recurse -Filter *.cs -File)
if ($files.Count -eq 0) {
    throw '反编译结束但没有生成任何 .cs 文件。'
}
$sizeMb = [math]::Round((($files | Measure-Object -Property Length -Sum).Sum / 1MB), 1)
$ilspyVersion = (& $ilspy -v 2>&1 | Select-Object -First 1)

$info = @(
    "游戏目录: $game",
    "程序集: $($Assembly -join ', ')",
    "生成时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
    "$ilspyVersion",
    "文件数: $($files.Count) 个 .cs，约 $sizeMb MB"
)
foreach ($dll in $dllPaths) {
    $item = Get-Item -LiteralPath $dll
    $info += ("  {0}  {1}  {2} MB" -f $item.Name, $item.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss'), [math]::Round($item.Length / 1MB, 1))
}
$info | Set-Content -LiteralPath (Join-Path $OutDir '_decompile-info.txt') -Encoding UTF8

Write-Step '完成'
Write-Host "    输出: $OutDir"
Write-Host ("    {0} 个 .cs，约 {1} MB，耗时 {2:N1} 秒" -f $files.Count, $sizeMb, $stopwatch.Elapsed.TotalSeconds)
Write-Host '    SourceCode 已被 .gitignore 忽略，不会进仓库。'
