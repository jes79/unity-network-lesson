# 씬을 다시 만들고 Windows 실행 파일을 Builds/Win 에 빌드합니다.
#   사용법:  powershell -ExecutionPolicy Bypass -File Tools/build.ps1
param([string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Unity.exe")
$project = Split-Path $PSScriptRoot -Parent
$log = Join-Path $project "Builds\build.log"
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
$p = Start-Process -FilePath $Unity -Wait -PassThru -ArgumentList @(
    '-batchmode', '-projectPath', $project,
    '-executeMethod', 'Lesson.EditorTools.LessonSceneBuilder.BuildWindowsPlayer',
    '-logFile', $log)
Select-String -Path $log -Pattern 'error CS|\[Lesson\]' | ForEach-Object { $_.Line }
exit $p.ExitCode
