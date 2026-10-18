# 빌드한 게임을 서버 1개 + 클라이언트 여러 개로 한 번에 띄웁니다.
#   창으로 보기:     powershell -ExecutionPolicy Bypass -File Tools/launch-test.ps1
#   화면 없이 검사:  powershell -ExecutionPolicy Bypass -File Tools/launch-test.ps1 -Headless -Seconds 15
#   -Mode host 로 바꾸면 첫 창이 Host(서버+플레이어)가 됩니다.
param(
    [int]$Clients = 2,
    [int]$Seconds = 0,          # 0 이면 자동 종료하지 않음
    [string]$Mode = "server",   # server | host
    [switch]$Headless,
    [switch]$Bot,               # 클라이언트를 자동 입력(-bot)으로 움직이고, 모두 -report 로 오브젝트 위치를 기록
    [int]$LateJoinAfter = 0,    # N초 뒤에 클라이언트 하나를 더 접속 (4장: 늦게 들어온 사람이 무엇을 받는지 확인)
    [string]$ClientArgs = ""    # 클라이언트에만 붙일 옵션. 예: "-lag 100 -loss 5 -noReconcile" (5장)
)
$project = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $project "Builds\Win\UnityNetworkLesson.exe"
$logs = Join-Path $project "Builds\testlogs"
New-Item -ItemType Directory -Force $logs | Out-Null
Remove-Item (Join-Path $logs "*.log") -ErrorAction SilentlyContinue

function Launch([string[]]$extra, [string]$name, [int]$quit) {
    $a = @('-port', '7777', '-logFile', "`"$(Join-Path $logs "$name.log")`"") + $extra # 공백 있는 경로 대비
    if ($Headless) { $a += @('-batchmode', '-nographics') }
    if ($quit -gt 0) { $a += @('-quitAfter', "$quit") }
    if ($Bot) { $a += @('-report'); if ($name -ne 'server') { $a += @('-bot') } }
    if ($name -ne 'server' -and $ClientArgs) { $a += ($ClientArgs -split '\s+') }
    Start-Process $exe -ArgumentList $a -PassThru
}

$procs = @(Launch @('-mode', $Mode, '-name', 'HostPlayer') 'server' ($(if ($Seconds) { $Seconds + 4 } else { 0 })))
Start-Sleep 3
for ($i = 1; $i -le $Clients; $i++) { $procs += Launch @('-mode', 'client', '-ip', '127.0.0.1', '-name', "Client$i") "client$i" $Seconds }
if ($LateJoinAfter -gt 0) {
    Start-Sleep $LateJoinAfter
    $lateQuit = if ($Seconds -gt 0) { [Math]::Max(1, $Seconds - $LateJoinAfter) } else { 0 } # 0 = 자동 종료 안 함
    $procs += Launch @('-mode', 'client', '-ip', '127.0.0.1', '-name', 'Latecomer') 'late' $lateQuit
}

if ($Seconds -gt 0) {
    $procs | ForEach-Object { $_.WaitForExit(($Seconds + 20) * 1000) | Out-Null }
    Get-ChildItem $logs -Filter *.log | ForEach-Object {
        "== $($_.BaseName)"
        Select-String -Path $_.FullName -Pattern '\[Lesson\]|Exception' | ForEach-Object { $_.Line }
    }
}
