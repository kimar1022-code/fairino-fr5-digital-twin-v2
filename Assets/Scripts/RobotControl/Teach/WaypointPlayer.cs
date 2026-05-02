using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// WaypointRecorder에 저장된 Waypoint 시퀀스를 순서대로 재생.
    /// 
    /// 적용된 결정:
    /// - 결정 1: MoveJ 비블로킹 + 도달 폴링 (tolerance 0.5°)
    /// - 결정 3: Waypoint 이동 = MoveJ(조인트) 고정. TCP는 참고용.
    /// - 결정 4: STOP 시 robot.StopMotion() 즉시 정지.
    /// - 결정 9: 한 번 Play = 1회 시퀀스 실행 (loopMode로 반복 선택 가능).
    /// 
    /// 의존성:
    /// - RobotManager: SetAllJointTargets, GetJointAngle, StopMotion, JointCount
    /// - WaypointRecorder: Waypoints (IReadOnlyList), Count
    /// 
    /// 사용법:
    /// 1. GameObject에 이 컴포넌트 부착
    /// 2. Inspector에서 robotManager, waypointRecorder 연결
    /// 3. Play() 호출 → 시퀀스 재생 시작
    /// 4. Stop() 호출 → 즉시 정지
    /// </summary>
    public class WaypointPlayer : MonoBehaviour
    {
        // ── Inspector 필드 ──────────────────────────────────────

        [Header("참조")]
        [SerializeField] private RobotManager robotManager;
        [SerializeField] private WaypointRecorder waypointRecorder;

        [Header("재생 설정")]
        [Tooltip("도달 판정 허용 오차 (도 단위). 모든 조인트가 이 범위 안에 들어와야 도달로 판정.")]
        [SerializeField] private float toleranceDeg = 0.5f;

        [Tooltip("true면 마지막 웨이포인트 도달 후 처음부터 반복 재생.")]
        [SerializeField] private bool loopMode = false;

        [Header("디버그")]
        [Tooltip("Console 로그 출력 여부")]
        [SerializeField] private bool enableLog = true;

        // ── 이벤트 ──────────────────────────────────────────────

        /// <summary>재생 시작 시</summary>
        public event Action OnPlaybackStarted;

        /// <summary>개별 웨이포인트 도달 시 (인덱스 전달)</summary>
        public event Action<int> OnWaypointReached;

        /// <summary>전체 시퀀스 정상 완료 시</summary>
        public event Action OnPlaybackCompleted;

        /// <summary>중도 정지 (Stop 호출) 시</summary>
        public event Action OnPlaybackStopped;

        // ── 상태 프로퍼티 ───────────────────────────────────────

        /// <summary>재생 중 여부</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>일시정지 중 여부</summary>
        public bool IsPaused { get; private set; }

        /// <summary>현재 재생 중인 웨이포인트 인덱스 (재생 중이 아니면 -1)</summary>
        public int CurrentIndex { get; private set; } = -1;

        // ── 내부 변수 ───────────────────────────────────────────

        private Coroutine playbackCoroutine;
        private bool stopRequested;

        // ── public 메서드 ───────────────────────────────────────

        /// <summary>
        /// 재생 시작 (웨이포인트 0번부터 순서대로).
        /// 이미 재생 중이면 무시.
        /// </summary>
        public void Play()
        {
            if (IsPlaying)
            {
                if (enableLog) Debug.LogWarning("[WaypointPlayer] 이미 재생 중입니다.");
                return;
            }

            if (waypointRecorder == null)
            {
                Debug.LogError("[WaypointPlayer] WaypointRecorder가 연결되지 않았습니다!");
                return;
            }

            if (waypointRecorder.Count == 0)
            {
                if (enableLog) Debug.LogWarning("[WaypointPlayer] 재생할 웨이포인트가 없습니다.");
                return;
            }

            if (robotManager == null)
            {
                Debug.LogError("[WaypointPlayer] RobotManager가 연결되지 않았습니다!");
                return;
            }

            stopRequested = false;
            playbackCoroutine = StartCoroutine(PlaybackRoutine());
        }

        /// <summary>일시정지 (재생 중일 때만 동작)</summary>
        public void Pause()
        {
            if (IsPlaying && !IsPaused)
            {
                IsPaused = true;
                if (enableLog) Debug.Log("[WaypointPlayer] ⏸ 일시정지");
            }
        }

        /// <summary>일시정지 해제 (일시정지 중일 때만 동작)</summary>
        public void Resume()
        {
            if (IsPlaying && IsPaused)
            {
                IsPaused = false;
                if (enableLog) Debug.Log("[WaypointPlayer] ▶ 재개");
            }
        }

        /// <summary>
        /// 즉시 정지 (결정 4).
        /// robot.StopMotion() 호출하여 로봇도 즉시 멈춤.
        /// </summary>
        public void Stop()
        {
            if (!IsPlaying) return;

            stopRequested = true;

            // 즉시 정지 명령 (결정 4)
            if (robotManager != null)
                robotManager.StopMotion();

            if (enableLog) Debug.Log("[WaypointPlayer] ⏹ 정지 요청됨");
        }

        // ── 핵심 코루틴 ─────────────────────────────────────────

        private IEnumerator PlaybackRoutine()
        {
            IsPlaying = true;
            IsPaused = false;
            CurrentIndex = 0;

            OnPlaybackStarted?.Invoke();

            if (enableLog)
                Debug.Log($"[WaypointPlayer] ▶ 재생 시작 (총 {waypointRecorder.Count}개, " +
                          $"tolerance={toleranceDeg}°, loop={loopMode})");

            IReadOnlyList<Waypoint> waypoints = waypointRecorder.Waypoints;

            do // loopMode 대응
            {
                for (int i = 0; i < waypoints.Count; i++)
                {
                    if (stopRequested) break;

                    CurrentIndex = i;
                    Waypoint wp = waypoints[i];

                    if (enableLog)
                        Debug.Log($"[WaypointPlayer] → {wp.name} ({i + 1}/{waypoints.Count}) 이동 시작");

                    // 일시정지 대기
                    while (IsPaused && !stopRequested)
                        yield return null;
                    if (stopRequested) break;

                    // MoveJ 비블로킹 명령 (결정 1, 3)
                    robotManager.SetAllJointTargets(wp.joints);
                    robotManager.SetGripperTarget(wp.gripperOpen, wp.gripperSpeed, wp.gripperForce);

                    // 도달 폴링 (tolerance 0.5°)
                    while (!HasReached(wp.joints, toleranceDeg))
                    {
                        if (stopRequested)
                        {
                            robotManager.StopMotion(); // 결정 4
                            break;
                        }

                        // 폴링 중 일시정지 대기
                        while (IsPaused && !stopRequested)
                            yield return null;

                        if (stopRequested)
                        {
                            robotManager.StopMotion(); // 결정 4
                            break;
                        }

                        yield return null;
                    }

                    if (stopRequested) break;

                    // 웨이포인트 도달 알림
                    OnWaypointReached?.Invoke(i);

                    if (enableLog)
                        Debug.Log($"[WaypointPlayer] ✅ {wp.name} 도달 ({i + 1}/{waypoints.Count})");
                }

                if (stopRequested) break;

                if (loopMode && enableLog)
                    Debug.Log("[WaypointPlayer] 🔄 루프 모드: 처음부터 다시 재생");

            } while (loopMode && !stopRequested);

            // ── 종료 처리 ───────────────────────────────────────
            IsPlaying = false;
            IsPaused = false;
            int lastIndex = CurrentIndex;
            CurrentIndex = -1;
            playbackCoroutine = null;

            if (stopRequested)
            {
                OnPlaybackStopped?.Invoke();
                if (enableLog)
                    Debug.Log($"[WaypointPlayer] ⏹ 재생 정지됨 (인덱스 {lastIndex}에서 중단)");
            }
            else
            {
                OnPlaybackCompleted?.Invoke();
                if (enableLog)
                    Debug.Log("[WaypointPlayer] 🏁 재생 완료");
            }
        }

        // ── 도달 판정 ───────────────────────────────────────────

        /// <summary>
        /// 현재 조인트 각도가 목표 각도에 tolerance 이내로 도달했는지 판정.
        /// 모든 조인트가 동시에 만족해야 true.
        /// </summary>
        private bool HasReached(float[] targetJoints, float tolerance)
        {
            int count = Mathf.Min(targetJoints.Length, robotManager.JointCount);
            for (int i = 0; i < count; i++)
            {
                float current = robotManager.GetJointAngle(i);
                if (Mathf.Abs(current - targetJoints[i]) > tolerance)
                    return false;
            }
            return true;
        }

        // ── OnDestroy: 이벤트 해제는 구독자 쪽 책임 ────────────
        // WaypointPlayer는 이벤트를 '발행'하는 쪽이므로,
        // 여기서 -= 처리할 대상이 없음.
        // 단, 코루틴이 남아있으면 정리.

        private void OnDestroy()
        {
            if (playbackCoroutine != null)
            {
                StopCoroutine(playbackCoroutine);
                playbackCoroutine = null;
            }

            IsPlaying = false;
            IsPaused = false;
        }
    }
}
