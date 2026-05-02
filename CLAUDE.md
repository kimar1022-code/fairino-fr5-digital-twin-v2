# CLAUDE.md — Fairino FR5 Unity 디지털 트윈 프로젝트

> 이 문서는 Claude Code 세션 시작 시 **최우선으로 읽어야 할** 인계 문서입니다.
> 진실의 원천: handover_v5.md + 이 문서.
> 둘이 충돌하면 **이 문서가 우선** (이 채팅에서 v5 누락을 발견한 부분이 반영됨).

> 📎 **보충 자료 자동 로드**: @handover_v5.md (v5 원본 인수인계 — 의존성 트리, 검증/결정 9개 상세, 폴더 구조 등)

---

## 🎯 0. 프로젝트 개요

- **목표**: Fairino FR5 6축 로봇팔의 Unity 디지털 트윈 (포트폴리오용)
- **사용자 특성**: 코딩 모름. 한국어로 자연어 명령. 실수 시 추적 어려움.
- **Unity 버전**: 6000.4.3f1 (v5 명시는 6000.0.43f1, 호환 동작 확인됨)
- **프로젝트 경로**: `D:\UNITY\FR5_Controller_NEW`
- **포트폴리오 가치**: 본인이 코드를 설명할 수 있어야 함. 자동화로 한 번에 다 만들지 말 것.

---

## ⚠️ 1. 안전 원칙 (절대 어기지 말 것)

### v5 안전 원칙 9번 (검증됨)
1. **자동 명령 금지**: ChangeMode에서 SyncTargets() 호출 X
2. **검증된 것만 사용**: Drag Mode, sdkLock, 별도 stateThread → 처음부터 안 넣음
3. **파일 중복 방지**: 드래그 시 "(1)" 붙는지 확인
4. **한 번에 한 가지만 변경**: 컴파일 확인 → 다음 작업
5. **추측 금지**: 모르면 사용자에게 묻거나, dll/원본 코드를 직접 확인

### 이 채팅에서 추가된 원칙
6. **v5도 100% 완벽하지 않음**: ReadDIPin 메서드가 v5에서 누락됐었음 (실증). 의심 시 검증.
7. **v3 사고 재발 방지**: 줄수, 시그니처, 함수 존재 여부는 **반드시 직접 확인 후** 답변.
8. **사용자가 코딩을 모름**: 코드 직접 보여주기보다 zip으로 묶어서 드래그&드롭 가능하게.

---

## 📂 2. 검증된 자산 (Phase 1 완료)

### 코어 11개 (이전 프로젝트 검증, namespace `RobotControl`)
| # | 파일 | 줄수 | 폴더 |
|---|------|------|------|
| 1 | JointConfig.cs | 54 | Core/ |
| 2 | IRobotController.cs | 92 | Core/ |
| 3 | CoordinateConverter.cs | 178 | Core/ |
| 4 | InverseKinematicsSolver.cs | 303 | Core/ |
| 5 | GripperController.cs | 131 | Core/ |
| 6 | CoordinateCalibrator.cs | 92 | Calibration/ |
| 7 | JointZeroCalibrator.cs | 40 | Calibration/ |
| 8 | SimulatedRobotController.cs | 437 | Sim/ |
| 9 | FairinoRobotController.cs | **556** (원본 527 + ReadDIPin 패치 +29) | Real/ |
| 10 | RobotManager.cs | 239 | Manager/ |
| 11 | RobotControlUI.cs | 866 | UI/ |

### PLC/Teach 6개 (학원 검증, namespace `RobotControl`)
| # | 파일 | 줄수 | 폴더 |
|---|------|------|------|
| 12 | Waypoint.cs | 49 | PLC/ |
| 13 | WaypointRecorder.cs | 153 | PLC/ |
| 14 | PLCButtonHandler.cs | 136 | PLC/ |
| 15 | DigitalInputTester.cs | 101 | PLC/ |
| 16 | WaypointStorage.cs | 255 | Teach/ |
| 17 | JointVisualCalibrator.cs | 80 | Calibration/ |

### Fairino SDK
- `Assets/Plugins/libfairino.dll` (.NET assembly, 292KB)
- `Assets/Plugins/CookComputing.XmlRpcV2.dll` (120KB)
- **dll 안에 모든 SDK 타입이 컴파일되어 들어있음** (FRRobot, RPCHandle, DescTran, Rpy 등)
- ⚠️ **.cs 형태의 SDK 소스(FRRobot.cs 등)는 절대 추가하지 말 것** — dll과 충돌 + Unity 비호환 using 포함
- `FAIRINO_SDK` Scripting Define Symbol 추가됨 (Player Settings)

### 그리퍼
- `Assets/Robot/EndEffectors/PGEA_100_40` 모델만 wrist3_link 자식으로 부착됨
- **시각만**, GripperController.cs의 시뮬 그리퍼 미사용 (사용자 결정)

---

## 🔑 3. IRobotController 29 멤버 시그니처 (검증됨)

```csharp
namespace RobotControl
{
    public enum RobotMode { Auto = 0, Manual = 1 }

    public struct CartesianPose  // x,y,z mm / rx,ry,rz degree, Fixed XYZ Euler
    {
        public float x, y, z, rx, ry, rz;
    }

    public interface IRobotController
    {
        // 연결/상태 (6)
        void Connect();
        void Disconnect();
        bool IsConnected { get; }
        bool IsReady { get; }
        bool IsBusy { get; }
        string StatusMessage { get; }

        // 조인트 (7)
        int JointCount { get; }
        float[] CurrentJointAngles { get; }
        float[] TargetJointAngles { get; }
        void SetTargetJointAngles(float[] angles);
        void MoveJ(float[] anglesDeg);  // 비블로킹 (결정 1)
        void StopMotion();              // 결정 4
        JointConfig[] JointConfigs { get; }

        // 그리퍼 (2)
        void OpenGripper();
        void CloseGripper();

        // 모션 (2) — 위 MoveJ + StopMotion과 별개로 사용 안 함

        // TCP/JOG (4)
        CartesianPose GetActualTCPPose();
        void StartJOG(int refType, int nb, int dir);
        void StopJOG(byte stopType);
        void SyncTargetsFromCurrent();

        // 홈 (4)
        void SetHomePose();
        bool HasHomePose { get; }
        void GoToHome();
        float[] HomeAngles { get; }

        // 모드 (2)
        RobotMode GetMode();
        void SetMode(RobotMode m);

        // 스피드 (2)
        int GetGlobalSpeed();
        void SetGlobalSpeed(int percent);
    }
}
```

⚠️ FairinoRobotController는 추가로 `byte ReadDIPin(int diIndex)` 메서드를 가짐 (v5 누락, ReadDIPin 패치로 추가).

---

## ✅ 4. 결정 9개 (재논의 X)

| # | 결정 | 근거 |
|---|------|------|
| 1 | **MoveJ 비블로킹** (blendT=0) | WaypointPlayer 도달 폴링 필수 (tolerance 0.5°) |
| 2 | **PLC SIM 모드** = 코드로 자동 처리 | `if (fairino == null \|\| !fairino.IsConnected) return;` |
| 3 | **Waypoint 이동 = MoveJ 고정** | TCP 표시는 참고용 |
| 4 | **STOP 즉시 정지** | `robot.StopMotion()` 호출 |
| 5 | **enum Mode 2개만**: `SimOnly`, `Mirror` | RealOnly 제거 |
| 6 | **Joint/Cartesian 완전 화면 전환** | SetActive로 토글 |
| 7 | **Pose Slot 제거**, Waypoint로 통일 | 단일 데이터 구조 |
| 8 | **임계값 분리 2개** | positionThresholdMm=30, rotationThresholdDeg=10 |
| 9 | **한 번 누름 = 1회 이동** | 길게 누르기 X |
| 10 | **Tool=1, Wobj=0 Inspector 고정** | 변경 X |

---

## 🐛 5. 코드 패턴 (필수)

### 5-1. `#if FAIRINO_SDK` 가드 (필수)
SDK 호출 코드는 반드시 `#if FAIRINO_SDK ... #else ... #endif`로 감쌀 것.

```csharp
#if FAIRINO_SDK
    int rc = robot.GetDI(diIndex, 0, ref level);
#else
    return 0;
#endif
```

### 5-2. SDK 호출 = 워커 스레드, UI = 메인 스레드만
- FairinoRobotController.cs는 워커 스레드에서 SDK 호출
- UI는 타겟 변수만 업데이트
- WaypointStorage는 메인 스레드 Update 폴링 자동저장 (검증됨)

### 5-3. 백업
- 자동 저장은 .bak 한 단계만

### 5-4. 이벤트 해제
- OnDestroy에서 `event -= handler` 필수

### 5-5. PLC SIM 모드 자동 처리
- `if (fairino == null || !fairino.IsConnected) return;` 일관성 있게

---

## 🚀 6. Phase 2 작업 목록 (지금부터)

### 신규 작성 13개

#### 로직 2개 (먼저)
| # | 파일 | 폴더 | 의존성 |
|---|------|------|------|
| 1 | **WaypointPlayer.cs** | Teach/ | RobotManager, WaypointStorage, Waypoint |
| 2 | **TeachModeManager.cs** | Teach/ | PLCButtonHandler, WaypointRecorder, WaypointPlayer |

#### UI 패널 11개 (그 다음)
| # | 파일 | 폴더 | 의존성 |
|---|------|------|------|
| 3 | StatusPanel.cs | UI/Panels/ | RobotManager 이벤트 |
| 4 | ConnectionPanel.cs | UI/Panels/ | RobotManager |
| 5 | ModePanel.cs | UI/Panels/ | RobotManager (Sim/Mirror) |
| 6 | SpeedPanel.cs | UI/Panels/ | RobotManager |
| 7 | JointControlPanel.cs | UI/Panels/ | 6축 슬라이더 |
| 8 | CartesianControlPanel.cs | UI/Panels/ | TCP XYZ/RPY |
| 9 | JogPanel.cs | UI/Panels/ | TCP/Joint JOG |
| 10 | HomePanel.cs | UI/Panels/ | Home 저장/이동 |
| 11 | GripperPanel.cs | UI/Panels/ | (그리퍼 시각만이라 SetActive 토글) |
| 12 | WaypointPanel.cs | UI/Panels/ | WaypointStorage 리스트 |
| 13 | TeachPanel.cs | UI/Panels/ | TeachModeManager |

### 작업 진행 순서 (필수)
1. **WaypointPlayer.cs 먼저** — 결정 1, 3, 4 적용
2. 사용자 컴파일 확인
3. **TeachModeManager.cs**
4. 사용자 컴파일 확인
5. UI 패널은 11개를 한꺼번에 X. 의존성 단순한 것부터.

---

## 🔍 7. WaypointPlayer.cs 사양 (다음 작업)

### 책임
- WaypointStorage의 리스트를 순서대로 재생
- MoveJ 비블로킹 호출 (결정 1)
- 도달까지 폴링하며 다음 웨이포인트로 진행 (tolerance 0.5°)
- STOP 시 즉시 정지 (결정 4)
- 일시정지/재개 지원
- PLC SIM 모드 자동 처리 (결정 2)

### 의존성
- `RobotManager` (조인트 각도 읽기, MoveJ 호출, StopMotion)
- `WaypointStorage` (재생할 리스트)
- `Waypoint` (데이터 클래스)

### 핵심 로직
```
Play():
  for each waypoint in storage:
    if (stopRequested) break;
    while (paused) yield;
    robot.MoveJ(waypoint.jointAngles);  // 비블로킹
    while (!Reached(waypoint, 0.5°)):
      if (stopRequested) { robot.StopMotion(); break; }
      while (paused) yield;
      yield return null;
```

### Inspector 노출
- `RobotManager robotManager` (필수)
- `WaypointStorage waypointStorage` (필수)
- `float toleranceDeg = 0.5f` (결정 1)
- `bool loopMode = false`

### 이벤트
- `OnPlaybackStarted`, `OnWaypointReached(int index)`, `OnPlaybackCompleted`, `OnPlaybackStopped`

### 상태
- `bool IsPlaying`, `bool IsPaused`, `int CurrentIndex`

---

## 🛡 8. 작업 시 검증 체크리스트

### 파일 작성 시
- [ ] namespace `RobotControl` 사용
- [ ] 필요한 using 문만 추가 (UnityEngine, System.Collections.Generic 등)
- [ ] **위험 using 절대 금지**: System.Windows.Forms, System.Drawing.Drawing2D, System.Runtime.Remoting
- [ ] SDK 호출이 있다면 `#if FAIRINO_SDK` 가드 적용
- [ ] OnDestroy에서 이벤트 해제
- [ ] Inspector 필드는 `[SerializeField]` 또는 `public`

### 컴파일 후
- [ ] 빨간 에러 0개
- [ ] 새 워닝 발생 시 사용자에게 알림

### 사용자에게 결과 보고 시
- [ ] 추측 금지: 모르는 건 "확인이 필요하다"고 솔직히
- [ ] 동작 보장 X 표현: "잘 동작할 것 같다"가 아니라 "컴파일 통과했고, 동작 검증은 씬 구성 후"

---

## 📌 9. 메타 정보

### 작업 환경
- OS: Windows 10/11
- Unity: 6000.4.3f1
- IDE: Visual Studio 또는 Rider (Unity가 자동 연동)
- 프로젝트 경로: `D:\UNITY\FR5_Controller_NEW`

### 사용자 소통 방식
- 한국어로 답할 것
- 코드를 직접 보여주기보다, 파일 만들고 위치 알려주는 게 효과적
- 변경 전 git commit 권장
- 의심스러우면 묻기: "이렇게 진행해도 될까요?"

### 진척도
```
✅ Phase 0: 환경 셋업
✅ Phase 1: 17개 자산 이식 + ReadDIPin 패치
🔄 Phase 2: 신규 13개 작성
   ✅ WaypointPlayer.cs (그리퍼 강도/속도 완성판, 281줄)
   ✅ TeachModeManager.cs (PLC 6버튼 라우팅, 202줄)
   ⬜ UI 패널 11개:
      ⬜ G1: StatusPanel, ConnectionPanel, ModePanel, SpeedPanel  ← 지금 여기
      ⬜ G2: JointControlPanel, CartesianControlPanel, JogPanel, HomePanel, GripperPanel
      ⬜ G3: WaypointPanel, TeachPanel
⬜ Phase 3: 씬 구성 + 컴포넌트 부착
⬜ Phase 4: 동작 검증
```

### Phase 2 진행 중 발견·결정 사항 (Antigravity에서 진행)

**그리퍼 데이터 비대칭 발견 (사용자 직관)**:
- Waypoint.cs는 gripperOpen 저장하는데 WaypointPlayer가 재생 시 무시하던 비대칭
- 해결: 단계 A~E (5단계)
  - 단계 A: WaypointPlayer.cs +1줄 (SetGripperTarget 호출)
  - 단계 B: Waypoint.cs +2줄 (gripperSpeed, gripperForce 필드, 둘 다 int=50)
  - 단계 C: WaypointRecorder.cs +11줄 (Inspector 기본값 + Save 로직, [Range(1,100)])
  - 단계 D: WaypointPlayer.cs 1줄 수정 (3인자 SetGripperTarget)
  - 단계 E: WaypointStorage.cs version "1.0" → "1.1"

**PLC 라우팅 재설계 (사용자 결정)**:
- 단계 F-1: PLCButtonHandler.cs - 버튼 1 의미 변경 Teach Toggle → GO HOME
  - pinTeachToggle → pinGoHome
  - OnTeachToggle → OnGoHome
  - 9군데 rename, 핀 매핑(DI 0)은 유지
- 단계 F-2a: WaypointRecorder.cs - PLC 직접 구독 끊기 (-5줄)
  - OnSaveWaypoint += SaveCurrentPose 줄 삭제
  - OnDestroy 본체 비우기
  - buttonHandler 필드는 유지 (Inspector 호환성)
- 단계 F-2b: TeachModeManager.cs 신규 작성 (202줄)
  - PLC 6버튼 게이트 키핑 + 라우팅
  - 책임: 버튼 1→GoToHome, 버튼 2/3→Recording상태, 버튼 4→Save (Recording중에만), 버튼 5→Play, 버튼 6→Stop이중

### v5와의 미해결 차이 (Phase 3 이후 별도 처리 예정)

이미 인지하고 있는 차이로, 한 번에 한 가지 원칙에 따라 별도 단계로 미룸:

1. **RobotManager.Mode enum**: 현재 `{ SimOnly, RealOnly, Mirror }` 3개. v5 결정 5는 2개만(SimOnly, Mirror). UI에서는 Mirror/SimOnly만 노출하지만 enum 자체는 코드 그대로 둠.
2. **RobotManager L49 SyncTargets() 호출**: v5 안전 원칙 1번과 충돌. Phase 4(동작 검증)에서 실제 영향 확인 후 결정.
3. **FairinoRobotController L285 SendMoveJ blendT=-1.0f (블로킹)**: v5 결정 1은 비블로킹. WaypointPlayer는 결정 1 가정으로 작성 — 시뮬은 비블로킹이라 정상 동작, 실로봇 모드는 Phase 4에서 패치.

### 알려진 워닝 (무해)
- `CS0618 FindObjectOfType<T>() obsolete` — RobotControlUI.cs 138줄
- `CS0067 OnStatusChanged is never used` — RobotManager.cs 25줄

### 알려진 워닝 (무해, Phase 2~4에서 자연 해결)
- `CS0618 FindObjectOfType<T>() obsolete` — RobotControlUI.cs 138줄 (Unity 6 deprecated)
- `CS0067 OnStatusChanged is never used` — RobotManager.cs 25줄 (UI 패널이 구독하면 사라짐)

### 첫 작업
**그룹 G1: 단순 표시 UI 패널 4개 작성**

파일 위치: `Assets/Scripts/RobotControl/UI/Panels/`

작성 순서:
1. **StatusPanel.cs** — 연결 상태 + 메시지 표시
2. **ConnectionPanel.cs** — Connect/Disconnect 버튼 + IP 표시
3. **ModePanel.cs** — SimOnly ↔ Mirror 토글 (RealOnly 노출 X)
4. **SpeedPanel.cs** — 속도 슬라이더 (1~100%) + ±1/±5 버튼

자세한 사양은 G1 작업 시 사용자가 별도 명령으로 전달.

⚠️ 매우 중요한 검증된 사실 (G1 작성 시 추측 금지):
- RobotManager.Mode enum (SimOnly/RealOnly/Mirror)와 RobotMode enum (Auto/Manual)는 다름
- 모드 변경: `manager.ChangeMode(RobotManager.Mode.SimOnly)` 사용 (L46)
- `SetMode(RobotMode m)` (L225)는 다른 일, 헷갈리지 마
- robotManager.Connect() (L137), Disconnect() (L138)
- robotManager.IsConnected (L96, 프로퍼티)
- robotManager.GetGlobalSpeed() (L231) / SetGlobalSpeed(int p) (L233)
- robotManager.OnStatusChanged (L25, event Action<string>)
- robotManager.StatusMessage (L121, 프로퍼티)
- FairinoRobotController.robotIP = "192.168.58.2" (L27, 검증된 기본값)

---

## 🚨 10. 절대 하지 말 것

1. ❌ v5 문서를 100% 신뢰하지 말 것 (이미 ReadDIPin 누락 사례)
2. ❌ 추측으로 SDK 함수 시그니처 만들지 말 것 (dll 직접 확인)
3. ❌ 한 번에 여러 파일 변경하지 말 것 (한 번에 하나)
4. ❌ "괜찮을 거예요" 같은 추측성 발언 금지 (검증 후 답변)
5. ❌ 사용자 결정 무시하고 자동 진행 X (예: ChangeMode에서 SyncTargets X)
6. ❌ Plugins/dll 안의 SDK 타입과 충돌하는 .cs 파일 추가 X
7. ❌ 검증 안 된 코드를 "이전에 됐었다"고 가정 X
