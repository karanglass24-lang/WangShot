# 편집기 스모크 테스트: WangShot.exe --editor 로 편집기 창이 뜨는지 확인한다.
# v1.5.0 에서 폼 생성자 예외로 편집기가 열리지 않았던 회귀를 막기 위한 검사다.
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\src\Greenshot\bin\Release\net480\WangShot.exe'),
    [int]$TimeoutSeconds = 30
)
$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path

# 단일 인스턴스(Mutex) 때문에 실행 중인 복사본이 있으면 새 빌드가 검증되지 않는다.
Get-Process WangShot -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

$process = Start-Process -FilePath $Exe -ArgumentList '--editor' -PassThru
$found = $false
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    if ($process.HasExited) { break }
    $process.Refresh()
    if ($process.MainWindowTitle -match 'Editor|편집기') {
        $found = $true
        Write-Host "OK: '$($process.MainWindowTitle)'"
        break
    }
    Start-Sleep -Milliseconds 500
}
if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
if (-not $found) {
    throw "Editor window did not open within $TimeoutSeconds s (process exited: $($process.HasExited))."
}
