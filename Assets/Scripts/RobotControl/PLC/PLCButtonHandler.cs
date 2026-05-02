using System;
using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// PLC 6개 버튼 신호를 받아 Rising Edge에 이벤트를 발생시키는 핸들러.
    /// 
    /// 동작:
    /// - 매 프레임 6개 DI/CI 핀 상태 폴링
    /// - 0 → 1 (Rising) 감지 시 해당 이벤트 발생
    /// - 다른 컴포넌트가 이 이벤트들을 구독해서 실제 동작 수행
    /// 
    /// 이벤트만 발생, 실제 로봇 명령은 안 보냄. (안전 검증용)
    /// 
    /// 사용법:
    /// 1. 빈 GameObject 생성 → 이 컴포넌트 부착
    /// 2. Inspector에서 Fairino 필드에 FairinoRobotController 드래그
    /// 3. Play → CONNECT → PLC 버튼 누름 → Console에 이벤트 로그 확인
    /// </summary>
    public class PLCButtonHandler : MonoBehaviour
    {
        [Header("참조")]
        public FairinoRobotController fairino;

        [Header("버튼 핀 매핑 (검증된 매핑)")]
        [Tooltip("PLC 버튼 1 = GO HOME (DI 0)")]
        public int pinGoHome = 0;

        [Tooltip("PLC 버튼 2 = Record Start (DI 1)")]
        public int pinRecordStart = 1;

        [Tooltip("PLC 버튼 3 = Record Stop (DI 2)")]
        public int pinRecordStop = 2;

        [Tooltip("PLC 버튼 4 = Save Waypoint (CI 0 = id 8)")]
        public int pinSaveWaypoint = 8;

        [Tooltip("PLC 버튼 5 = Play (CI 1 = id 9)")]
        public int pinPlay = 9;

        [Tooltip("PLC 버튼 6 = Stop (CI 2 = id 10)")]
        public int pinStop = 10;

        [Header("폴링 설정")]
        [Tooltip("폴링 주기 (초). 0.05 = 20Hz")]
        public float pollInterval = 0.05f;

        [Tooltip("디버그 로그 출력")]
        public bool enableDebugLog = true;

        // ── 이벤트 ───────────────────────────────────────────────
        // 다른 컴포넌트가 이 이벤트를 구독해서 실제 동작 수행
        public event Action OnGoHome;
        public event Action OnRecordStart;
        public event Action OnRecordStop;
        public event Action OnSaveWaypoint;
        public event Action OnPlay;
        public event Action OnStop;

        // ── 내부 상태 ────────────────────────────────────────────
        // 6개 버튼의 이전 상태 (Rising edge 감지용)
        byte prevGoHome = 0;
        byte prevRecordStart = 0;
        byte prevRecordStop = 0;
        byte prevSaveWaypoint = 0;
        byte prevPlay = 0;
        byte prevStop = 0;

        float pollTimer = 0f;

        void Update()
        {
            // 연결 안 됐으면 폴링 안 함
            if (fairino == null || !fairino.IsConnected) return;

            // 폴링 주기 체크
            pollTimer += Time.deltaTime;
            if (pollTimer < pollInterval) return;
            pollTimer = 0f;

            PollAndDetectEdges();
        }

        void PollAndDetectEdges()
        {
            // 6개 버튼 상태 읽기
            byte curGoHome = fairino.ReadDIPin(pinGoHome);
            byte curRecordStart = fairino.ReadDIPin(pinRecordStart);
            byte curRecordStop = fairino.ReadDIPin(pinRecordStop);
            byte curSaveWaypoint = fairino.ReadDIPin(pinSaveWaypoint);
            byte curPlay = fairino.ReadDIPin(pinPlay);
            byte curStop = fairino.ReadDIPin(pinStop);

            // Rising edge 감지 (0 → 1)
            if (prevGoHome == 0 && curGoHome == 1)
            {
                if (enableDebugLog) Debug.Log("[PLC] 🔘 버튼 1 (GO HOME) 눌림");
                OnGoHome?.Invoke();
            }
            if (prevRecordStart == 0 && curRecordStart == 1)
            {
                if (enableDebugLog) Debug.Log("[PLC] 🔘 버튼 2 (Record Start) 눌림");
                OnRecordStart?.Invoke();
            }
            if (prevRecordStop == 0 && curRecordStop == 1)
            {
                if (enableDebugLog) Debug.Log("[PLC] 🔘 버튼 3 (Record Stop) 눌림");
                OnRecordStop?.Invoke();
            }
            if (prevSaveWaypoint == 0 && curSaveWaypoint == 1)
            {
                if (enableDebugLog) Debug.Log("[PLC] 🔘 버튼 4 (Save Waypoint) 눌림");
                OnSaveWaypoint?.Invoke();
            }
            if (prevPlay == 0 && curPlay == 1)
            {
                if (enableDebugLog) Debug.Log("[PLC] 🔘 버튼 5 (Play) 눌림");
                OnPlay?.Invoke();
            }
            if (prevStop == 0 && curStop == 1)
            {
                if (enableDebugLog) Debug.Log("[PLC] 🔘 버튼 6 (Stop) 눌림");
                OnStop?.Invoke();
            }

            // 상태 업데이트
            prevGoHome = curGoHome;
            prevRecordStart = curRecordStart;
            prevRecordStop = curRecordStop;
            prevSaveWaypoint = curSaveWaypoint;
            prevPlay = curPlay;
            prevStop = curStop;
        }
    }
}
