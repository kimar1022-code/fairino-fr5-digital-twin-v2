using System.Collections.Generic;
using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// PLC 버튼 4 (Save Waypoint) 이벤트를 받아 현재 로봇 자세를 저장.
    /// 
    /// 동작:
    /// - PLCButtonHandler.OnSaveWaypoint 이벤트 구독
    /// - 이벤트 발생 시 RobotManager에서 현재 자세 읽기
    /// - List<Waypoint>에 추가
    /// - Console에 결과 표시
    /// 
    /// 안전:
    /// - 읽기만 (GetActualJointPosDegree, GetActualTCPPose)
    /// - 명령 안 보냄
    /// - 메모리에만 저장 (Play 종료 시 사라짐)
    /// 
    /// 다음 단계 (앞으로 만들 것):
    /// - JSON 파일로 영구 저장
    /// - UI에 목록 표시
    /// - Play (재생) 기능
    /// 
    /// 사용법:
    /// 1. 빈 GameObject 생성 → 이 컴포넌트 부착
    /// 2. Inspector에서 Button Handler / Robot Manager 연결
    /// 3. Play → CONNECT
    /// 4. 로봇을 원하는 자세로 만듦
    /// 5. PLC 버튼 4 누름
    /// 6. Console에 "[Recorder] WP1 저장됨..." 로그 확인
    /// </summary>
    public class WaypointRecorder : MonoBehaviour
    {
        [Header("참조")]
        public PLCButtonHandler buttonHandler;
        public RobotManager robot;

        [Header("설정")]
        [Tooltip("저장 시 이름 prefix. 예: WP → WP1, WP2, WP3...")]
        public string namePrefix = "WP";

        [Tooltip("Console 로그 출력 여부")]
        public bool enableLog = true;

        [Header("그리퍼 저장 기본값")]
        [Tooltip("저장 시 적용할 그리퍼 속도 (1~100)")]
        [Range(1, 100)] public int defaultGripperSpeed = 50;

        [Tooltip("저장 시 적용할 그리퍼 강도 (1~100)")]
        [Range(1, 100)] public int defaultGripperForce = 50;

        // ── 저장된 Waypoint 목록 (메모리) ──────────────────────
        private List<Waypoint> waypoints = new List<Waypoint>();

        /// <summary>저장된 waypoint 개수 (외부에서 읽기용)</summary>
        public int Count => waypoints.Count;

        /// <summary>저장된 waypoint 목록 (읽기 전용으로 외부 노출)</summary>
        public IReadOnlyList<Waypoint> Waypoints => waypoints;

        void Start()
        {
            if (buttonHandler == null)
            {
                Debug.LogError("[Recorder] PLCButtonHandler가 연결되지 않았습니다!");
                return;
            }
            if (robot == null)
            {
                Debug.LogError("[Recorder] RobotManager가 연결되지 않았습니다!");
                return;
            }

            if (enableLog) Debug.Log("[Recorder] 준비 완료. SaveCurrentPose() 호출 시 현재 자세 저장.");
        }

        void OnDestroy()
        {
            // 이벤트 구독은 TeachModeManager가 관리
        }

        /// <summary>
        /// 현재 로봇 자세를 Waypoint로 저장.
        /// 외부에서 직접 호출도 가능 (예: Unity UI 버튼).
        /// </summary>
        public void SaveCurrentPose()
        {
            if (robot == null || !robot.IsConnected)
            {
                if (enableLog) Debug.LogWarning("[Recorder] 로봇 연결 안됨 - 자세 저장 실패");
                return;
            }

            // 현재 자세 읽기 (안전: 읽기만)
            Waypoint wp = new Waypoint();
            wp.name = $"{namePrefix}{waypoints.Count + 1}";

            // 조인트 각도 (J1~J6)
            for (int i = 0; i < 6; i++)
                wp.joints[i] = robot.GetJointAngle(i);

            // TCP 위치/회전
            CartesianPose tcp = robot.GetCurrentTCPPose();
            wp.tcpX = tcp.x; wp.tcpY = tcp.y; wp.tcpZ = tcp.z;
            wp.tcpRx = tcp.rx; wp.tcpRy = tcp.ry; wp.tcpRz = tcp.rz;

            // 그리퍼 상태
            wp.gripperOpen = robot.GetGripperOpenPercent();

            // 그리퍼 속도/강도 (Inspector 기본값 사용)
            wp.gripperSpeed = defaultGripperSpeed;
            wp.gripperForce = defaultGripperForce;

            // 리스트에 추가
            waypoints.Add(wp);

            if (enableLog)
            {
                Debug.Log($"[Recorder] ✅ {wp.ToShortString()}");
                Debug.Log($"[Recorder] 총 저장: {waypoints.Count}개");
            }
        }

        /// <summary>저장된 waypoint 모두 지우기 (필요 시 사용)</summary>
        public void ClearAll()
        {
            int n = waypoints.Count;
            waypoints.Clear();
            if (enableLog) Debug.Log($"[Recorder] {n}개 waypoint 삭제됨");
        }

        /// <summary>마지막 waypoint만 삭제 (실수로 저장한 경우)</summary>
        public void RemoveLast()
        {
            if (waypoints.Count == 0) return;
            var last = waypoints[waypoints.Count - 1];
            waypoints.RemoveAt(waypoints.Count - 1);
            if (enableLog) Debug.Log($"[Recorder] 마지막 waypoint 삭제: {last.name}");
        }

        /// <summary>특정 인덱스의 waypoint 가져오기 (재생 시 사용 예정)</summary>
        public Waypoint GetWaypoint(int index)
        {
            if (index < 0 || index >= waypoints.Count) return null;
            return waypoints[index];
        }

        /// <summary>
        /// 외부에서 직접 waypoint 추가 (Storage의 Load에서 사용).
        /// 사용자가 PLC 버튼으로 저장하는 SaveCurrentPose와 다름.
        /// </summary>
        public void AddWaypoint(Waypoint wp)
        {
            if (wp == null) return;
            waypoints.Add(wp);
        }
    }
}
