param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 8787
)

$ErrorActionPreference = 'Stop'
$scottPython = Join-Path $env:USERPROFILE '.conda\envs\scott\python.exe'
if (-not (Test-Path -LiteralPath $scottPython -PathType Leaf)) {
    throw 'conda scott 환경의 python.exe를 찾을 수 없습니다.'
}

$configFile = Join-Path $PSScriptRoot 'bot\.env'
if (-not (Test-Path -LiteralPath $configFile)) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bot\.env.example') -Destination $configFile
}

Write-Host "TempChain Chat: http://127.0.0.1:$Port"
& $scottPython -m uvicorn bot.app:app --app-dir $PSScriptRoot --host 127.0.0.1 --port $Port
exit $LASTEXITCODE
