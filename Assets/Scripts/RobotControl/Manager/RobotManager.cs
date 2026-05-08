using System;
using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// Sim/Real/Mirror 모드로 두 컨트롤러를 통합 관리 (확장판).
    /// 신규 기능(JOG/홈/모드/스피드/TCP) 모두 위임.
    /// </summary>
    public class RobotManager : MonoBehaviour, IRobotController
    {
        public enum Mode { SimOnly, RealOnly, Mirror }

        [Header("모드")]
        public Mode mode = Mode.SimOnly;

        [Header("시작 시 자동 연결")]
        [Tooltip("체크 시 Play와 동시에 실로봇까지 연결 시도. 기본은 Sim만 자동 연결.")]
        public bool autoConnectReal = false;

        [Header("컨트롤러 참조")]
        public SimulatedRobotController sim;
        public FairinoRobotController real;

        public event Action<string> OnStatusChanged;
        public void SetModeAuto() { SetMode(RobotMode.Auto); }
        public void SetModeManual() { SetMode(RobotMode.Manual); }
        IRobotController PrimaryReader
        {
            get
            {
                if (mode == Mode.RealOnly) return real;
                if (mode == Mode.Mirror && real != null && real.IsConnected) return real;
                return sim;
            }
        }

        bool SimActive => mode != Mode.RealOnly && sim != null;
        bool RealActive => mode != Mode.SimOnly && real != null;

        void Start()
        {
            if (SimActive) sim.Connect();
            if (autoConnectReal && RealActive) real.Connect();
        }

        public void ChangeMode(Mode newMode)
        {
            mode = newMode;
            // SyncTargets()는 의도적 제거 — Mirror 모드 진입 시 자동 명령 금지 (안전 원칙)
        }

        /// <summary>
        /// Mirror 모드 전용: Sim을 Real의 실제 각도에 자동으로 동기화.
        /// 카티시안 JOG 시 Real은 SDK IK로 움직이고, Sim은 Real 각도를 그대로 따라감
        /// → 완벽한 시각적 일치 달성.
        /// </summary>
        float lastMirrorLogTime = 0f;
        void Update()
        {
            // Mirror 모드이고 실로봇 연결되어 있을 때만 동기화
            if (mode != Mode.Mirror) return;
            if (real == null || !real.IsConnected) return;
            if (sim == null) return;

            // Real의 현재 조인트 각도를 읽어서 Sim에게 그대로 적용
            // → Sim이 IK 계산 없이 Real을 시각적으로 그대로 재현
            int n = real.JointCount;
            for (int i = 0; i < n && i < sim.JointCount; i++)
            {
                float realAngle = real.GetJointAngle(i);
                sim.SetJointTarget(i, realAngle);
            }

            // 디버그: 1초에 한 번씩 Mirror 동기화 상태 로그
            if (Time.time - lastMirrorLogTime > 1f)
            {
                lastMirrorLogTime = Time.time;
                float realJ5 = real.GetJointAngle(4);
                float simJ5 = sim.GetJointAngle(4);
                Debug.Log($"[Mirror Sync] Real J5={realJ5:F2}°, Sim J5={simJ5:F2}°");
            }
        }

        public void SyncTargets()
        {
            if (PrimaryReader == null) return;
            float[] cur = new float[PrimaryReader.JointCount];
            for (int i = 0; i < cur.Length; i++) cur[i] = PrimaryReader.GetJointAngle(i);
            if (SimActive) sim.SetAllJointTargets(cur);
            if (RealActive) real.SetAllJointTargets(cur);
        }

        // ── IRobotController 위임 ──────────────────────────────────
        public bool IsReady => PrimaryReader?.IsReady ?? false;

        public bool IsConnected
        {
            get
            {
                if (mode == Mode.SimOnly) return sim?.IsConnected ?? false;
                if (mode == Mode.RealOnly) return real?.IsConnected ?? false;
                return (sim?.IsConnected ?? false) && (real?.IsConnected ?? false);
            }
        }

        /// <summary>
        /// 순차 실행 중 여부: 실로봇 MoveJ 실행 중이면 true.
        /// UI는 이 값을 체크해서 "BUSY" 상태에 새 명령 안 보내도록 함.
        /// </summary>
        public bool IsBusy
        {
            get
            {
                // Sim-only면 항상 false (시뮬은 즉시 반응)
                if (mode == Mode.SimOnly) return false;
                // Real이 관여하면 real.IsBusy를 그대로 반환
                return real != null && real.IsBusy;
            }
        }

        public string StatusMessage
        {
            get
            {
                string simMsg = sim != null ? $"Sim: {sim.StatusMessage}" : "Sim: null";
                string realMsg = real != null ? $"Real: {real.StatusMessage}" : "Real: null";
                return mode switch
                {
                    Mode.SimOnly => simMsg,
                    Mode.RealOnly => realMsg,
                    Mode.Mirror => $"{simMsg} | {realMsg}",
                    _ => "Unknown mode"
                };
            }
        }

        public void Connect() { if (SimActive) sim.Connect(); if (RealActive) real.Connect(); }
        public void Disconnect() { sim?.Disconnect(); real?.Disconnect(); }

        public int JointCount => PrimaryReader?.JointCount ?? 6;
        public string GetJointName(int i) => PrimaryReader.GetJointName(i);
        public float GetJointMinAngle(int i) => PrimaryReader.GetJointMinAngle(i);
        public float GetJointMaxAngle(int i) => PrimaryReader.GetJointMaxAngle(i);
        public float GetJointAngle(int i) => PrimaryReader.GetJointAngle(i);
        public float GetGripperOpenPercent() => PrimaryReader.GetGripperOpenPercent();

        public void SetJointTarget(int i, float a)
        {
            if (SimActive) sim.SetJointTarget(i, a);
            if (RealActive) real.SetJointTarget(i, a);
        }

        public void SetAllJointTargets(float[] a)
        {
            if (SimActive) sim.SetAllJointTargets(a);
            if (RealActive) real.SetAllJointTargets(a);
        }

        public void SetGripperTarget(float p, int s = 50, int f = 50)
        {
            if (SimActive) sim.SetGripperTarget(p, s, f);
            if (RealActive) real.SetGripperTarget(p, s, f);
        }

        public void StopMotion() { sim?.StopMotion(); real?.StopMotion(); }
        public void ResetToHome() { if (SimActive) sim.ResetToHome(); if (RealActive) real.ResetToHome(); }

        // ── 신규 위임 ──────────────────────────────────────────────
        public CartesianPose GetCurrentTCPPose() => PrimaryReader?.GetCurrentTCPPose() ?? new CartesianPose();

        public void StartCartesianJog(int axis, int dir)
        {
            // Mirror 모드: Sim은 IK 계산 안 하고, Real만 움직임.
            //   Sim은 Update()에서 Real 각도를 자동으로 따라감 → 완벽 동기화
            if (mode == Mode.Mirror && real != null && real.IsConnected)
            {
                real.StartCartesianJog(axis, dir);
                return;
            }
            // SimOnly/RealOnly 모드는 기존대로
            if (SimActive) sim.StartCartesianJog(axis, dir);
            if (RealActive) real.StartCartesianJog(axis, dir);
        }

        public void StartJointJog(int jointIndex, int dir)
        {
            // Mirror: Real만 JOG, Sim은 Update에서 Real 따라감
            if (mode == Mode.Mirror && real != null && real.IsConnected)
            {
                real.StartJointJog(jointIndex, dir);
                return;
            }
            if (SimActive) sim.StartJointJog(jointIndex, dir);
            if (RealActive) real.StartJointJog(jointIndex, dir);
        }

        public void StopJog()
        {
            sim?.StopJog();
            real?.StopJog();
        }

        public float[] GetHomePose() => PrimaryReader?.GetHomePose() ?? new float[6];

        public void SetHomePose(float[] angles)
        {
            if (SimActive) sim.SetHomePose(angles);
            if (RealActive) real.SetHomePose(angles);
        }

        public void SetHomePoseFromCurrent()
        {
            if (SimActive) sim.SetHomePoseFromCurrent();
            if (RealActive) real.SetHomePoseFromCurrent();
        }

        public void GoToHome()
        {
            if (SimActive) sim.GoToHome();
            if (RealActive) real.GoToHome();
        }

        public RobotMode GetMode() => PrimaryReader?.GetMode() ?? RobotMode.Manual;

        public void SetMode(RobotMode m)
        {
            if (SimActive) sim.SetMode(m);
            if (RealActive) real.SetMode(m);
        }

        public int GetGlobalSpeed() => PrimaryReader?.GetGlobalSpeed() ?? 50;

        public void SetGlobalSpeed(int p)
        {
            if (SimActive) sim.SetGlobalSpeed(p);
            if (RealActive) real.SetGlobalSpeed(p);
        }
    }
}
