using System;
using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// PLCButtonHandler의 6개 이벤트를 구독해서 적절한 컴포넌트로 라우팅.
    /// 
    /// 역할:
    /// - Recording 상태 게이트 키핑 (Save Waypoint는 녹화 중일 때만 통과)
    /// - GO HOME, Play, Stop 라우팅
    /// - STOP 이중 안전망 (v5 결정 4: player + robot 둘 다 정지)
    /// 
    /// PLC 버튼 매핑:
    /// - 버튼 1 (DI 0): GO HOME → robotManager.GoToHome()
    /// - 버튼 2 (DI 1): Record Start → IsRecording = true
    /// - 버튼 3 (DI 2): Record Stop → IsRecording = false
    /// - 버튼 4 (CI 0): Save Waypoint → waypointRecorder.SaveCurrentPose() (녹화 중일 때만)
    /// - 버튼 5 (CI 1): Play → waypointPlayer.Play()
    /// - 버튼 6 (CI 2): Stop → waypointPlayer.Stop() + robotManager.StopMotion()
    /// 
    /// 의존성:
    /// - PLCButtonHandler: 6개 이벤트 구독
    /// - RobotManager: GoToHome, StopMotion, IsConnected
    /// - WaypointRecorder: SaveCurrentPose, Count
    /// - WaypointPlayer: Play, Stop
    /// 
    /// 사용법:
    /// 1. GameObject에 이 컴포넌트 부착
    /// 2. Inspector에서 4개 참조 연결
    /// 3. Play → PLC 버튼으로 Teach/Record/Play 제어
    /// </summary>
    public class TeachModeManager : MonoBehaviour
    {
        // ── Inspector 필드 ──────────────────────────────────────

        [Header("참조")]
        [SerializeField] private PLCButtonHandler buttonHandler;
        [SerializeField] private RobotManager robotManager;
        [SerializeField] private WaypointRecorder waypointRecorder;
        [SerializeField] private WaypointPlayer waypointPlayer;

        [Header("디버그")]
        [SerializeField] private bool enableLog = true;

        // ── public 프로퍼티 ─────────────────────────────────────

        /// <summary>현재 녹화 중인지</summary>
        public bool IsRecording { get; private set; }

        // ── 이벤트 (UI 표시용) ──────────────────────────────────

        /// <summary>녹화 시작 시</summary>
        public event Action OnRecordingStarted;

        /// <summary>녹화 종료 시</summary>
        public event Action OnRecordingStopped;

        // ── Start: 이벤트 구독 ──────────────────────────────────

        void Start()
        {
            // 필수 참조 null 체크
            if (buttonHandler == null)
            {
                Debug.LogError("[TeachMgr] PLCButtonHandler가 연결되지 않았습니다!");
                return;
            }
            if (robotManager == null)
            {
                Debug.LogError("[TeachMgr] RobotManager가 연결되지 않았습니다!");
                return;
            }
            if (waypointRecorder == null)
            {
                Debug.LogError("[TeachMgr] WaypointRecorder가 연결되지 않았습니다!");
                return;
            }
            if (waypointPlayer == null)
            {
                Debug.LogError("[TeachMgr] WaypointPlayer가 연결되지 않았습니다!");
                return;
            }

            // PLCButtonHandler 6개 이벤트 구독
            buttonHandler.OnGoHome += HandleGoHome;
            buttonHandler.OnRecordStart += HandleRecordStart;
            buttonHandler.OnRecordStop += HandleRecordStop;
            buttonHandler.OnSaveWaypoint += HandleSaveWaypoint;
            buttonHandler.OnPlay += HandlePlay;
            buttonHandler.OnStop += HandleStop;

            if (enableLog) Debug.Log("[TeachMgr] 준비 완료. PLC 6개 버튼 이벤트 구독됨.");
        }

        // ── OnDestroy: 이벤트 해제 ──────────────────────────────

        void OnDestroy()
        {
            if (buttonHandler != null)
            {
                buttonHandler.OnGoHome -= HandleGoHome;
                buttonHandler.OnRecordStart -= HandleRecordStart;
                buttonHandler.OnRecordStop -= HandleRecordStop;
                buttonHandler.OnSaveWaypoint -= HandleSaveWaypoint;
                buttonHandler.OnPlay -= HandlePlay;
                buttonHandler.OnStop -= HandleStop;
            }
        }

        // ── 핸들러 6개 ─────────────────────────────────────────

        /// <summary>[버튼 1] GO HOME: 홈 자세로 이동</summary>
        private void HandleGoHome()
        {
            if (!robotManager.IsConnected)
            {
                Debug.LogWarning("[TeachMgr] 로봇 연결 안됨 - GO HOME 무시");
                return;
            }

            robotManager.GoToHome();

            if (enableLog) Debug.Log("[TeachMgr] 🏠 GO HOME");
        }

        /// <summary>[버튼 2] Record Start: 녹화 시작</summary>
        private void HandleRecordStart()
        {
            if (IsRecording)
            {
                if (enableLog) Debug.Log("[TeachMgr] 이미 녹화 중");
                return;
            }

            IsRecording = true;
            OnRecordingStarted?.Invoke();

            if (enableLog) Debug.Log("[TeachMgr] 🔴 녹화 시작");
        }

        /// <summary>[버튼 3] Record Stop: 녹화 종료</summary>
        private void HandleRecordStop()
        {
            if (!IsRecording)
            {
                if (enableLog) Debug.Log("[TeachMgr] 녹화 중 아님");
                return;
            }

            IsRecording = false;
            OnRecordingStopped?.Invoke();

            if (enableLog) Debug.Log($"[TeachMgr] ⏹ 녹화 종료 (총 {waypointRecorder.Count}개 저장됨)");
        }

        /// <summary>[버튼 4] Save Waypoint: 현재 자세 저장 (녹화 중일 때만)</summary>
        private void HandleSaveWaypoint()
        {
            if (!IsRecording)
            {
                Debug.LogWarning("[TeachMgr] 녹화 중이 아님. Record Start (버튼 2) 먼저 누르세요.");
                return;
            }

            waypointRecorder.SaveCurrentPose();

            if (enableLog) Debug.Log("[TeachMgr] 💾 Waypoint 저장 요청");
        }

        /// <summary>[버튼 5] Play: 웨이포인트 시퀀스 재생</summary>
        private void HandlePlay()
        {
            if (!robotManager.IsConnected)
            {
                Debug.LogWarning("[TeachMgr] 로봇 연결 안됨 - Play 무시");
                return;
            }

            if (IsRecording)
            {
                Debug.LogWarning("[TeachMgr] 녹화 중에는 재생 불가. Record Stop 먼저.");
                return;
            }

            waypointPlayer.Play();

            if (enableLog) Debug.Log("[TeachMgr] ▶ 재생 시작 요청");
        }

        /// <summary>[버튼 6] Stop: 즉시 정지 (이중 안전망, v5 결정 4)</summary>
        private void HandleStop()
        {
            // 이중 안전망: player와 robot 둘 다 정지
            waypointPlayer.Stop();
            robotManager.StopMotion();

            if (enableLog) Debug.Log("[TeachMgr] ⏹ STOP (player + robot 둘 다 정지)");
        }

        // ===== UI 진입점 (G3-2 TeachPanel용 PLC 시뮬레이션 메서드) =====
        // 각 메서드는 동일한 private 핸들러를 호출 — PLC 물리 버튼 누름과 동일한 동작 트리거.
        // 게이트키핑·이벤트 발행·상태 관리는 핸들러 내부에 그대로 보존.

        /// <summary>UI 진입점: GO HOME (HandleGoHome 호출)</summary>
        public void GoHome()
        {
            HandleGoHome();
        }

        /// <summary>UI 진입점: 녹화 시작 (HandleRecordStart 호출)</summary>
        public void StartRecording()
        {
            HandleRecordStart();
        }

        /// <summary>UI 진입점: 녹화 종료 (HandleRecordStop 호출)</summary>
        public void StopRecording()
        {
            HandleRecordStop();
        }

        /// <summary>UI 진입점: Waypoint 저장 (HandleSaveWaypoint 호출, IsRecording 게이트키핑 보존)</summary>
        public void SaveWaypoint()
        {
            HandleSaveWaypoint();
        }

        /// <summary>UI 진입점: 재생 (HandlePlay 호출, IsConnected + !IsRecording 게이트 보존)</summary>
        public void PlayWaypoints()
        {
            HandlePlay();
        }

        /// <summary>UI 진입점: 정지 (HandleStop 호출, 이중 안전망)</summary>
        public void StopAll()
        {
            HandleStop();
        }
    }
}
