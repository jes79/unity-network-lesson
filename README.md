# 유니티 네트워크 교안 — Netcode for GameObjects 로 배우는 멀티플레이어

"코드를 따라 치는 것"이 아니라 **네트워크가 왜 이렇게 동작하는지** 이해하는 것을 목표로 한 단계별 교안입니다.
탑다운 3D 아레나(캡슐 캐릭터가 걷고, 쏘고, 맞고, 부활하는 게임)를 만들면서 접속 → 동기화 → 지연 숨기기 → 인터넷 연결까지 순서대로 배웁니다.

- 엔진: **Unity 6000.6.4f1** (Built-in 렌더 파이프라인, 구 Input Manager)
- 네트워크: **Netcode for GameObjects 2.13.3**, Unity Transport 2.7.4
- 도구: Multiplayer Play Mode 2.0.2, Multiplayer Tools 2.2.12 (Network Simulator), Multiplayer Services 2.4.1 (Relay 세션)
- 위키: [GitHub Wiki](https://github.com/jes79/unity-network-lesson/wiki) (이 저장소 `docs/` 와 같은 내용)

## 목차

| 장 | 주제 | 배우는 네트워크 개념 | 코드 |
|---|---|---|---|
| 00 | [네트워크 기초](docs/00-network-basics.md) | 패킷, IP/포트, TCP vs UDP, 지연·지터·손실, 서버 구조, 권한, 틱 | — |
| 01 | [접속하기](docs/01-connection.md) | Host/Server/Client, 접속 승인, 연결 이벤트 | `ch01` |
| 02 | [플레이어 생성과 이동 동기화](docs/02-player-sync.md) | NetworkObject, 소유권, 서버 권한, NetworkTransform | `ch02` |
| 03 | [RPC — 공격과 채팅](docs/03-rpc.md) | RPC(사건), Spawn/Despawn, 서버 판정, 씬 배치 오브젝트 | `ch03` |
| 04 | [NetworkVariable — 체력과 이름](docs/04-network-variable.md) | 상태 동기화, 쓰기 권한, 늦은 참가, OnValueChanged | `ch04` |
| 05 | [지연과 싸우기](docs/05-latency.md) | 지연 시뮬레이션, 보간, 클라이언트 예측, 서버 보정 | `ch05` |
| 06 | [인터넷 너머로](docs/06-relay.md) | NAT, Relay, 세션과 참가 코드 | `ch06` |
| 07 | [코드 리뷰로 배우는 보안·정확성](docs/07-code-review.md) | RPC 권한, 입력 검증(NaN), 연속 충돌, 예측 규칙 일치 | `ch07` |

## 공부하는 방법

각 장의 코드는 **Git 태그**로 저장되어 있습니다. 그 장까지 만든 상태로 돌아가 볼 수 있습니다.

> 태그는 "그 장을 처음 만든 순간"의 기록입니다. 7장의 리뷰 수정은 `ch07`(= `main`)에만 들어 있고, 문서(`docs/`)는 항상 `main` 의 최신본을 보세요. 옛 태그에서는 아직 없는 다음 장 링크가 열리지 않을 수 있습니다.

```bash
git clone https://github.com/jes79/unity-network-lesson.git
cd unity-network-lesson
git checkout ch02        # 2장까지 만든 상태
git checkout main        # 완성본
git diff ch02 ch03       # 3장에서 무엇이 바뀌었는지
```

1. `docs/NN-*.md` 를 읽고 → 2. `git checkout chNN` → 3. Unity Hub 에서 프로젝트 열기 → 4. `Assets/_Lesson/Scenes/Main.unity` 실행 → 5. 문서의 "직접 해 볼 것" 실험.

## 실행하기

**에디터 하나로 여러 플레이어** — `Window ▸ Multiplayer ▸ Multiplayer Play Mode` 에서 Player 2~4 를 켜고 ▶ Play.
한 창에서 **Host**, 나머지에서 **Client**.

**실행 파일 여러 개** (Windows PowerShell)

```powershell
powershell -ExecutionPolicy Bypass -File Tools/build.ps1                    # Builds/Win 에 빌드
powershell -ExecutionPolicy Bypass -File Tools/launch-test.ps1              # 서버 1 + 클라이언트 2 창
powershell -ExecutionPolicy Bypass -File Tools/launch-test.ps1 -Headless -Bot -Seconds 15 -LateJoinAfter 8 -ClientArgs "-lag 100 -loss 5"
```

| 실행 옵션 | 뜻 |
|---|---|
| `-mode host\|server\|client` `-ip` `-port` `-name` | 버튼 없이 바로 시작 (1장) |
| `-batchmode -nographics` | 화면 없는 전용 서버 |
| `-bot` | 자동 입력(원을 그리며 걷고 가끔 발사) |
| `-report` | 2초마다 보이는 네트워크 오브젝트 위치, 3초마다 RTT 를 로그에 기록 |
| `-lag ms` `-jitter ms` `-loss %` | 지연 시뮬레이션 (5장, Development 빌드) |
| `-noPredict` `-noReconcile` `-noInterp` | 5장 기술 끄기 |
| `-quitAfter 초` | 자동 종료 |

## 조작

| 키 | 동작 |
|---|---|
| WASD / 방향키 | 이동 |
| Space | 발사 |
| C | 색 바꾸기 (4장) |
| F1 | 보이는 네트워크 오브젝트 목록을 로그에 출력 |
| 채팅창 + Enter | 채팅 (3장) |

## 폴더 구조

```text
Assets/_Lesson/
  Scripts/Ch01_Connection   ConnectionHud, CommandLineStarter
  Scripts/Ch02_Player       PlayerMovement(2~4장), PlayerLabel
  Scripts/Ch03_Rpc          PlayerCombat, Projectile, ChatSystem
  Scripts/Ch04_NetworkVariable  PlayerStats
  Scripts/Ch05_Latency      LatencyLab, PredictedMovement
  Scripts/Ch06_Relay        SessionHud
  Scripts/Common            LessonInput, SpawnedObjectsReport
  Editor/LessonSceneBuilder.cs   씬과 프리팹을 코드로 만드는 도구 (메뉴 Lesson ▸ Rebuild Scene)
docs/                       교안 본문 (위키와 같은 내용)
Tools/                      빌드·다중 실행 스크립트
```
