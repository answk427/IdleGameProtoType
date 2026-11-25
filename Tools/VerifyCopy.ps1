<#
.SYNOPSIS
  검증용 사본 프로젝트를 원본과 맞추고(Sync), 같은지 확인하고(Check), 크기를 줄인다(Clean).

.DESCRIPTION
  CLAUDE.md "외부 도구" 2. 사본은 원본 폴더 옆의 "<원본 폴더 이름>_verify"이고, 원본 → 사본 한 방향이다.

  Sync   Assets, Packages, ProjectSettings를 미러 복사하고, 사본 에디터가 떠 있으면 새로 읽고 컴파일하게 한 뒤
         원본과 비교한다. 종료 코드가 0일 때만 검증을 시작한다.
  Check  복사 없이 비교만 한다. 검증 결과를 보고하기 전에, 검증하는 동안 원본이 바뀌지 않았는지 본다.
  Clean  사본 에디터가 꺼져 있을 때 빌드 캐시(Library/Bee)와 가장 최근 APK가 아닌 빌드 결과를 지운다.
         다음 빌드는 처음부터 하고(약 8분), 다음 에디터 시작 때 스크립트를 전부 다시 컴파일한다.

  비교는 파일 내용(SHA-256)으로 한다. 같으면 "지문"(전체 목록 해시의 앞 12자리)을 출력하고, 검증 결과에 이 지문을 적는다.
  사본의 Assets/_ClaudeVerify/(임시 검증 스크립트 자리)는 복사, 삭제, 비교에서 모두 빠진다.
  경로의 대소문자만 다른 것은 같은 파일로 본다(robocopy가 대소문자만 바뀐 이름을 고치지 않으므로).

  종료 코드: 0 같음·완료 / 1 다름 / 2 실행 실패 / 3 사본 컴파일 실패(파일은 같음)

.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File Tools/VerifyCopy.ps1 -Action Sync
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Sync', 'Check', 'Clean')]
    [string]$Action,

    # 기본값: 원본 옆의 "<원본 폴더 이름>_verify"
    [string]$CopyPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$SyncedFolders = @('Assets', 'Packages', 'ProjectSettings')
$LocalOnly = '_ClaudeVerify'   # 사본에만 있는 폴더 이름

$Original = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path.TrimEnd('\')
if (-not $CopyPath) {
    $CopyPath = Join-Path (Split-Path $Original -Parent) ((Split-Path $Original -Leaf) + '_verify')
}
$CopyPath = $CopyPath.Replace('/', '\').TrimEnd('\')

# 사본 프로젝트를 연 에디터 프로세스(가져오기 작업자 제외)
function Get-CopyEditorProcess {
    $pattern = [regex]::Escape($CopyPath.ToLowerInvariant()) + '(?=["\s\\]|$)'
    return @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
        $cmd = ([string]$_.CommandLine).Replace('/', '\').ToLowerInvariant()
        ($cmd -notmatch 'assetimportworker') -and ($cmd -match $pattern)
    })
}

# 상대 경로 → SHA-256
function Get-Manifest([string]$root) {
    $map = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::OrdinalIgnoreCase)
    $localOnly = New-Object System.Text.RegularExpressions.Regex ('(^|/)' + [regex]::Escape($LocalOnly) + '(/|\.meta$)')
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        foreach ($folder in $SyncedFolders) {
            $base = Join-Path $root $folder
            if (-not (Test-Path -LiteralPath $base)) { continue }
            # Get-ChildItem보다 훨씬 빠르다(숨김 파일 포함)
            foreach ($path in [System.IO.Directory]::EnumerateFiles($base, '*', [System.IO.SearchOption]::AllDirectories)) {
                $relative = $path.Substring($root.Length + 1).Replace('\', '/')
                if ($localOnly.IsMatch($relative)) { continue }
                $stream = [System.IO.File]::Open($path, 'Open', 'Read', 'ReadWrite')
                try { $map[$relative] = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
                finally { $stream.Dispose() }
            }
        }
    }
    finally { $sha.Dispose() }
    return , $map
}

function Get-Fingerprint($map) {
    $keys = New-Object 'System.Collections.Generic.List[string]'
    $keys.AddRange([string[]]@($map.Keys))
    $keys.Sort([StringComparer]::OrdinalIgnoreCase)
    $text = New-Object System.Text.StringBuilder
    foreach ($key in $keys) { [void]$text.Append($key.ToLowerInvariant()).Append('|').Append($map[$key]).Append("`n") }
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($text.ToString())) }
    finally { $sha.Dispose() }
    return [BitConverter]::ToString($bytes).Replace('-', '').Substring(0, 12).ToLowerInvariant()
}

function Compare-Trees {
    $a = Get-Manifest $Original
    $b = Get-Manifest $CopyPath
    $diffs = New-Object 'System.Collections.Generic.List[string]'
    foreach ($key in $a.Keys) {
        if (-not $b.ContainsKey($key)) { $diffs.Add("원본에만 있음: $key") }
        elseif ($a[$key] -ne $b[$key]) { $diffs.Add("내용이 다름: $key") }
    }
    foreach ($key in $b.Keys) {
        if (-not $a.ContainsKey($key)) { $diffs.Add("사본에만 있음: $key") }
    }

    if ($diffs.Count -eq 0) {
        Write-Host ('사본 = 원본 (파일 {0}개, 지문 {1})' -f $a.Count, (Get-Fingerprint $a))
        return $true
    }

    Write-Host ('사본이 원본과 다릅니다: {0}건' -f $diffs.Count)
    $diffs | Sort-Object | Select-Object -First 30 | ForEach-Object { Write-Host "  $_" }
    if ($diffs.Count -gt 30) { Write-Host ('  ... 외 {0}건' -f ($diffs.Count - 30)) }
    return $false
}

# 창 없는 에디터는 파일 변경을 스스로 보지 않는다. 새로 읽게 하고 컴파일 결과를 받는다.
function Update-CopyEditor {
    if (-not (Get-Command unity -ErrorAction SilentlyContinue)) {
        throw 'unity CLI를 찾지 못해 사본 에디터에 새로 읽게 하지 못했습니다.'
    }
    # Windows PowerShell 5.1은 Stop일 때 네이티브 명령의 stderr(2>&1)를 예외로 바꾼다. 결과는 종료 코드로 본다.
    $ErrorActionPreference = 'Continue'

    $null = & unity status --until-ready --project-path $CopyPath 2>&1
    $null = & unity command script eval 'UnityEditor.AssetDatabase.Refresh();' --project-path $CopyPath 2>&1
    if ($LASTEXITCODE -ne 0) { throw "사본 에디터가 새로 읽기에 응답하지 않습니다(unity 종료 코드 $LASTEXITCODE)." }

    $null = & unity status --until-ready --project-path $CopyPath 2>&1
    $output = & unity recompile --project-path $CopyPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "사본 에디터 컴파일 실패(unity 종료 코드 $LASTEXITCODE):"
        $output | ForEach-Object { Write-Host "  $_" }
        return $false
    }

    $null = & unity status --until-ready --project-path $CopyPath 2>&1
    Write-Host '사본 에디터: 새로 읽고 컴파일함(에러 없음)'
    return $true
}

function Invoke-Sync {
    if (-not (Test-Path -LiteralPath $CopyPath)) {
        throw "사본이 없습니다: $CopyPath (만드는 법은 CLAUDE.md '외부 도구' 2)"
    }

    foreach ($folder in $SyncedFolders) {
        $null = & robocopy (Join-Path $Original $folder) (Join-Path $CopyPath $folder) /MIR /XD $LocalOnly /XF "$LocalOnly.meta" /R:2 /W:1 /NP /NJH /NJS /NFL /NDL
        if ($LASTEXITCODE -ge 8) { throw "robocopy 실패: $folder (종료 코드 $LASTEXITCODE)" }
    }
    Write-Host ('복사함: {0} → {1}' -f $Original, $CopyPath)

    if (@(Get-CopyEditorProcess).Count -eq 0) {
        Write-Host '사본 에디터가 꺼져 있습니다. 켤 때 바뀐 파일을 읽습니다.'
        return $true
    }
    return Update-CopyEditor
}

# robocopy는 긴 경로(260자 이상, Gradle 빌드 폴더)도 지운다. 빈 폴더를 미러해서 비운 뒤 지운다.
function Remove-Tree([string]$path) {
    $empty = Join-Path ([System.IO.Path]::GetTempPath()) ('empty_' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $empty | Out-Null
    try {
        $null = & robocopy $empty $path /MIR /R:1 /W:1 /NP /NJH /NJS /NFL /NDL
        if ($LASTEXITCODE -ge 8) { throw "비우기 실패: $path (robocopy 종료 코드 $LASTEXITCODE)" }
        Remove-Item -LiteralPath $path -Recurse -Force
    }
    finally { Remove-Item -LiteralPath $empty -Force -ErrorAction SilentlyContinue }
}

function Invoke-Clean {
    if (@(Get-CopyEditorProcess).Count -gt 0) {
        throw "사본 에디터가 켜져 있습니다. 먼저 끄세요: unity close `"$CopyPath`""
    }

    $drive = Get-PSDrive -Name $CopyPath.Substring(0, 1)
    $freeBefore = $drive.Free

    $bee = Join-Path $CopyPath 'Library\Bee'
    if (Test-Path -LiteralPath $bee) {
        Remove-Tree $bee
        Write-Host '지움: Library/Bee (빌드 캐시)'
    }

    $builds = Join-Path $CopyPath 'Builds'
    if (Test-Path -LiteralPath $builds) {
        $latest = Get-ChildItem -LiteralPath $builds -Filter '*.apk' -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        foreach ($item in @(Get-ChildItem -LiteralPath $builds -Force)) {
            if ($latest -and $item.Name -eq $latest.Name) { continue }
            if ($item.PSIsContainer) { Remove-Tree $item.FullName } else { Remove-Item -LiteralPath $item.FullName -Force }
            Write-Host "지움: Builds/$($item.Name)"
        }
        if ($latest) { Write-Host "남김: Builds/$($latest.Name)" }
    }

    $freeAfter = (Get-PSDrive -Name $drive.Name).Free
    Write-Host ('확보한 공간: {0:N1} GB' -f (($freeAfter - $freeBefore) / 1GB))
}

try {
    if ($Action -eq 'Clean') {
        Invoke-Clean
        exit 0
    }

    $compiled = $true
    if ($Action -eq 'Sync') { $compiled = Invoke-Sync }

    if (-not (Compare-Trees)) { exit 1 }
    if (-not $compiled) { exit 3 }
    exit 0
}
catch {
    Write-Host ('실패: ' + $_.Exception.Message)
    exit 2
}
