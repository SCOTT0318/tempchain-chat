#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.1.6',
    [string]$NsisPath,
    [string]$PublishDirectory,
    [switch]$SkipInstaller
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-ChildPath {
    param([string]$Candidate, [string]$Parent)
    $candidatePath = [IO.Path]::GetFullPath($Candidate)
    $parentPath = [IO.Path]::GetFullPath($Parent).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $candidatePath.StartsWith($parentPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw "허용한 폴더 밖의 출력 경로입니다: $candidatePath"
    }
    return $candidatePath
}

function Get-PayloadFiles {
    param([string]$Directory)
    $items = @(Get-ChildItem -LiteralPath $Directory -Force -Recurse)
    if ($items | Where-Object { $_.LinkType -in @('SymbolicLink', 'Junction') -or $_.LinkTarget }) {
        throw '게시 폴더에는 심볼릭 링크 또는 정션을 포함할 수 없습니다.'
    }
    $files = @($items | Where-Object { -not $_.PSIsContainer } | Sort-Object FullName)
    if (-not (Test-Path -LiteralPath (Join-Path $Directory 'TempChain.Chat.exe') -PathType Leaf)) {
        throw '게시 폴더에 TempChain.Chat.exe가 없습니다.'
    }
    foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($Directory, $file.FullName)
        if ($relative -match '(^|[\\/])(WebView2|User Data)([\\/]|$)' -or
            $file.Name -match '^(\.env($|\.)|settings\.json$|Microsoft\.Web\.WebView2\.|WebView2Loader\.)' -or
            $file.Extension -in @('.pfx', '.p12')) {
            throw "배포할 수 없는 프로필·비밀값·WebView2 파일입니다: $relative"
        }
    }
    return $files
}

function Copy-VerifiedFile {
    param([string]$Source, [string]$Destination)
    $destinationParent = Split-Path -Parent $Destination
    New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
    Copy-Item -LiteralPath $Source -Destination $Destination -Force
    $sourceHash = (Get-FileHash -LiteralPath $Source -Algorithm SHA256).Hash
    $destinationHash = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
    if ($sourceHash -ne $destinationHash) { throw "복사 해시가 일치하지 않습니다: $Destination" }
}

function ConvertTo-NsisString {
    param([string]$Value)
    if ($Value -match '[\r\n]') { throw '게시 파일 이름에 줄바꿈을 사용할 수 없습니다.' }
    return $Value.Replace('$', '$$').Replace('"', '$\"')
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$projectPath = Join-Path $repoRoot 'desktop\TempChain.Chat\TempChain.Chat.csproj'
$installerSource = Join-Path $repoRoot 'desktop\packaging\TempChainChat.nsi'
$iconPath = Join-Path $repoRoot 'desktop\TempChain.Chat\Assets\app.ico'
$distRoot = Join-Path $repoRoot 'dist'
$payloadName = "TChat-$Version-win-x64"
$finalPayload = Assert-ChildPath (Join-Path $distRoot $payloadName) $distRoot
$finalZip = Assert-ChildPath (Join-Path $distRoot "$payloadName.zip") $distRoot
$finalInstaller = Assert-ChildPath (Join-Path $distRoot "TChat-Setup-$Version.exe") $distRoot
$stagingParent = 'C:\Users\Public\Documents\ESTsoft\CreatorTemp\TempChainChat-builds'
$stagingName = 'TempChainChat-build-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$stagingRoot = Assert-ChildPath (Join-Path $stagingParent $stagingName) $stagingParent
$publishStage = Join-Path $stagingRoot 'publish'
$buildArtifacts = Join-Path $stagingRoot 'artifacts'
New-Item -ItemType Directory -Path $publishStage -Force | Out-Null

if ($PublishDirectory) {
    $existingPublish = (Resolve-Path -LiteralPath $PublishDirectory).Path
    foreach ($file in @(Get-PayloadFiles $existingPublish)) {
        $relative = [IO.Path]::GetRelativePath($existingPublish, $file.FullName)
        $destination = Assert-ChildPath (Join-Path $publishStage $relative) $publishStage
        Copy-VerifiedFile $file.FullName $destination
    }
} else {
    $dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
    & $dotnetPath publish $projectPath -c Release -r win-x64 --self-contained true `
        --artifacts-path $buildArtifacts -o $publishStage --disable-build-servers `
        '-m:1' '-nr:false' '-p:PublishTrimmed=false' '-p:PublishSingleFile=false' `
        '-p:DebugType=None' '-p:DebugSymbols=false' "-p:Version=$Version"
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish 실패: $LASTEXITCODE" }
}

$payloadFiles = @(Get-PayloadFiles $publishStage)
$relativeFiles = @($payloadFiles | ForEach-Object { [IO.Path]::GetRelativePath($publishStage, $_.FullName) })
if (Test-Path -LiteralPath $finalPayload) {
    $existingItems = @(Get-ChildItem -LiteralPath $finalPayload -Force -Recurse)
    if ($existingItems | Where-Object { $_.LinkType -in @('SymbolicLink', 'Junction') -or $_.LinkTarget }) {
        throw "기존 배포 폴더에 심볼릭 링크 또는 정션이 있습니다: $finalPayload"
    }
    $unexpectedFiles = @($existingItems | Where-Object { -not $_.PSIsContainer } | ForEach-Object {
        [IO.Path]::GetRelativePath($finalPayload, $_.FullName)
    } | Where-Object { $_ -notin $relativeFiles })
    if ($unexpectedFiles.Count -gt 0) {
        throw "기존 배포 폴더에 새 게시물에 없는 파일이 있습니다. 기존 폴더를 별도로 보관한 뒤 다시 실행하세요: $($unexpectedFiles -join ', ')"
    }
}

$uninstallInclude = Join-Path $stagingRoot 'uninstall-payload.nsh'
$deleteLines = [Collections.Generic.List[string]]::new()
$deleteLines.Add('; Exact application payload only. Generated by Build-Desktop.ps1.')
foreach ($relative in $relativeFiles) {
    $target = '$INSTDIR\' + (ConvertTo-NsisString $relative)
    $deleteLines.Add('Delete "' + $target + '"')
    $deleteLines.Add('IfFileExists "' + $target + '" 0 +2')
    $deleteLines.Add('  StrCpy $PayloadDeleteFailed "1"')
}
$directories = @(Get-ChildItem -LiteralPath $publishStage -Force -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending)
foreach ($directory in $directories) {
    $relative = [IO.Path]::GetRelativePath($publishStage, $directory.FullName)
    $deleteLines.Add('RMDir "$INSTDIR\' + (ConvertTo-NsisString $relative) + '"')
}
[IO.File]::WriteAllText($uninstallInclude, ($deleteLines -join "`r`n") + "`r`n", [Text.UTF8Encoding]::new($true))

$stagedInstaller = Join-Path $stagingRoot "TChat-Setup-$Version.exe"
if (-not $SkipInstaller) {
    if (-not $NsisPath) {
        $bundledCompiler = 'C:\Users\Public\Documents\ESTsoft\CreatorTemp\TempChainChat-tools\nsis-3.13\makensis.exe'
        if (Test-Path -LiteralPath $bundledCompiler -PathType Leaf) {
            $NsisPath = $bundledCompiler
        } else {
            $compilerCommand = Get-Command makensis.exe -ErrorAction SilentlyContinue
            if ($compilerCommand) { $NsisPath = $compilerCommand.Source }
        }
    }
    if (-not $NsisPath -or -not (Test-Path -LiteralPath $NsisPath -PathType Leaf)) {
        throw 'NSIS 3 컴파일러를 찾지 못했습니다. -NsisPath 전체경로 또는 -SkipInstaller를 지정하세요.'
    }
    if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) { throw "앱 아이콘이 없습니다: $iconPath" }
    $estimatedSize = [int][Math]::Ceiling(($payloadFiles | Measure-Object -Property Length -Sum).Sum / 1024)
    & $NsisPath '/V3' '/INPUTCHARSET' 'UTF8' "/DAPP_VERSION=$Version" "/DPUBLISH_DIR=$publishStage" "/DOUTPUT_FILE=$stagedInstaller" `
        "/DPAYLOAD_UNINSTALL_INCLUDE=$uninstallInclude" "/DAPP_ICON=$iconPath" "/DESTIMATED_SIZE_KB=$estimatedSize" $installerSource
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $stagedInstaller -PathType Leaf)) {
        throw "NSIS 컴파일 실패: $LASTEXITCODE"
    }
    # Verify the bytes extracted by the actual installer before publishing any release assets.
    $verificationRoot = Assert-ChildPath (Join-Path $stagingRoot 'installer-verification') $stagingRoot
    $verificationArguments = '/S /VERIFYDIR="' + $verificationRoot + '"'
    $verificationProcess = Start-Process -FilePath $stagedInstaller -ArgumentList $verificationArguments -WindowStyle Hidden -Wait -PassThru
    if ($verificationProcess.ExitCode -ne 0) { throw "설치기 압축 해제 검증 실패: $($verificationProcess.ExitCode)" }
    $extractedFiles = @(Get-PayloadFiles $verificationRoot)
    if ($extractedFiles.Count -ne $payloadFiles.Count) { throw '설치기에서 추출한 파일 수가 일치하지 않습니다.' }
    foreach ($file in $payloadFiles) {
        $relative = [IO.Path]::GetRelativePath($publishStage, $file.FullName)
        $extracted = Assert-ChildPath (Join-Path $verificationRoot $relative) $verificationRoot
        if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $extracted).Hash) {
            throw "설치기 압축 해제 파일 해시 불일치: $relative"
        }
    }
}

New-Item -ItemType Directory -Path $finalPayload -Force | Out-Null
foreach ($file in $payloadFiles) {
    $relative = [IO.Path]::GetRelativePath($publishStage, $file.FullName)
    $destination = Assert-ChildPath (Join-Path $finalPayload $relative) $finalPayload
    Copy-VerifiedFile $file.FullName $destination
}
$stagedZip = Join-Path $stagingRoot "$payloadName.zip"
Compress-Archive -LiteralPath $finalPayload -DestinationPath $stagedZip -CompressionLevel Optimal
Copy-VerifiedFile $stagedZip $finalZip
if (-not $SkipInstaller) {
    Copy-VerifiedFile $stagedInstaller $finalInstaller
}

$checksumTargets = [Collections.Generic.List[string]]::new()
$checksumTargets.Add($finalZip)
if (-not $SkipInstaller) { $checksumTargets.Add($finalInstaller) }
foreach ($relative in $relativeFiles) { $checksumTargets.Add((Join-Path $finalPayload $relative)) }
$checksumLines = foreach ($filePath in $checksumTargets) {
    $relative = [IO.Path]::GetRelativePath($distRoot, $filePath).Replace('\', '/')
    (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $relative
}
$checksumsPath = Join-Path $distRoot 'SHA256SUMS.txt'
[IO.File]::WriteAllText($checksumsPath, ($checksumLines -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
foreach ($line in [IO.File]::ReadAllLines($checksumsPath)) {
    $entry = $line -split '  ', 2
    $verifiedPath = Assert-ChildPath (Join-Path $distRoot $entry[1]) $distRoot
    if ((Get-FileHash -LiteralPath $verifiedPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry[0]) {
        throw "최종 체크섬 검증 실패: $verifiedPath"
    }
}

[PSCustomObject]@{
    Version = $Version
    Payload = $finalPayload
    Zip = $finalZip
    Installer = $(if ($SkipInstaller) { $null } else { $finalInstaller })
    Checksums = $checksumsPath
    Staging = $stagingRoot
    PayloadFileCount = $relativeFiles.Count
    Verified = $true
}
