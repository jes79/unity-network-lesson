# docs/ 와 README 를 GitHub Wiki 로 복사합니다.
#   사용법:  powershell -ExecutionPolicy Bypass -File Tools/publish-wiki.ps1
#   ※ GitHub 는 위키 첫 페이지를 웹에서 한 번 만들어야 위키 저장소가 생깁니다 (Wiki 탭 ▸ Create the first page ▸ Save).
param([string]$Repo = "jes79/unity-network-lesson")
$ErrorActionPreference = "Stop"
$project = Split-Path $PSScriptRoot -Parent
$work = Join-Path ([IO.Path]::GetTempPath()) ("wiki-" + [guid]::NewGuid().ToString("N"))
$utf8 = New-Object System.Text.UTF8Encoding $false

git clone --quiet "https://github.com/$Repo.wiki.git" $work
if ($LASTEXITCODE -ne 0) { Write-Error "위키 저장소를 받을 수 없습니다. 웹에서 첫 페이지를 먼저 만들어 주세요."; exit 1 }

# 위키는 페이지 이름으로 링크한다: (02-player-sync.md) → (02-player-sync), (../README.md) → (Home)
function Convert-Links([string]$text) {
    $text = $text -replace '\]\(\.\./README\.md\)', '](Home)'
    $text = $text -replace '\]\((?:docs/)?(\d\d-[A-Za-z0-9-]+)\.md\)', ']($1)'
    return $text
}

$pages = Get-ChildItem (Join-Path $project "docs") -Filter "*.md" | Sort-Object Name
foreach ($page in $pages) {
    $text = Convert-Links ([IO.File]::ReadAllText($page.FullName, $utf8))
    [IO.File]::WriteAllText((Join-Path $work $page.Name), $text, $utf8)
}

$homeText = Convert-Links ([IO.File]::ReadAllText((Join-Path $project "README.md"), $utf8))
[IO.File]::WriteAllText((Join-Path $work "Home.md"), $homeText, $utf8)

$sidebar = "**유니티 네트워크 교안**`n`n- [처음으로](Home)`n"
foreach ($page in $pages) {
    $title = ([IO.File]::ReadLines($page.FullName, $utf8) | Select-Object -First 1) -replace '^#\s*', ''
    $sidebar += "- [$title]($($page.BaseName))`n"
}
[IO.File]::WriteAllText((Join-Path $work "_Sidebar.md"), $sidebar, $utf8)

git -C $work add -A
git -C $work commit --quiet -m "docs/ 동기화"
git -C $work push --quiet
Write-Output "위키 게시 완료: https://github.com/$Repo/wiki"
Remove-Item -Recurse -Force $work
