using System;

namespace RobotControl
{
    /// <summary>
    /// 로봇 제어 공통 인터페이스 (확장판).
    /// 기존 조인트/그리퍼 기능 + 데카르트 JOG / 홈 포즈 / 작동모드 / 글로벌 스피드.
    /// 시뮬과 실로봇 모두 이 계약을 따름.
    /// </summary>
    public interface IRobotController
    {
        // ── 연결 ──────────────────────────────────────────────────────
        bool IsReady { get; }
        bool IsConnected { get; }
        string StatusMessage { get; }
        event Action<string> OnStatusChanged;

        void Connect();
        void Disconnect();

        // ── 조인트 (기존) ────────────────────────────────────────────
        int JointCount { get; }
        string GetJointName(int index);
        float GetJointMinAngle(int index);
        float GetJointMaxAngle(int index);
        float GetJointAngle(int index);
        void SetJointTarget(int index, float angleDegrees);
        void SetAllJointTargets(float[] anglesDegrees);

        // ── 그리퍼 (기존) ────────────────────────────────────────────
        float GetGripperOpenPercent();
        void SetGripperTarget(float percent, int speed = 50, int force = 50);

        // ── 비상/리셋 (기존) ─────────────────────────────────────────
        void StopMotion();
        void ResetToHome();

        // ────────────────────────────────────────────────────────────
        // ── 신규 기능 ──
        // ────────────────────────────────────────────────────────────

        // 현재 TCP (Tool Center Point) 포즈 — X/Y/Z (mm), Rx/Ry/Rz (deg)
        CartesianPose GetCurrentTCPPose();

        // ── 데카르트 JOG (티치펜던트의 "한 번 길게 누르기" 버튼 동작 모사) ──
        // axis: 0=X, 1=Y, 2=Z, 3=Rx, 4=Ry, 5=Rz
        // dir:  +1 or -1
        // 호출 후 StopJog()을 호출할 때까지 계속 움직임 (또는 max_dis만큼 이동 후 자동 정지)
        void StartCartesianJog(int axis, int dir);
        void StartJointJog(int jointIndex, int dir);
        void StopJog();

        // ── 홈 포즈 ────────────────────────────────────────────────
        float[] GetHomePose();                      // 6개 조인트 홈 각도
        void SetHomePose(float[] jointAnglesDeg);   // 현재 각도를 홈으로 저장 or 값 직접 지정
        void SetHomePoseFromCurrent();              // 지금 로봇 각도를 홈으로 저장
        void GoToHome();                            // 홈 포즈로 이동 (ResetToHome과 동일하지만 명시적 이름)

        // ── 작동 모드 ──────────────────────────────────────────────
        // 0 = 자동 모드 (프로그램 실행), 1 = 수동 모드 (티치펜던트 조작)
        RobotMode GetMode();
        void SetMode(RobotMode mode);

        // ── 글로벌 스피드 (모든 모션 명령에 곱해지는 비율) ─────────
        int GetGlobalSpeed();
        void SetGlobalSpeed(int percent);   // 0~100
    }

    /// <summary>로봇 작동 모드.</summary>
    public enum RobotMode
    {
        Auto = 0,    // 자동 (프로그램 실행)
        Manual = 1   // 수동 (티치펜던트/UI 조작)
    }

    /// <summary>데카르트 공간 포즈 (Fairino의 DescPose와 1:1 매핑).</summary>
    [System.Serializable]
    public struct CartesianPose
    {
        public float x, y, z;        // mm
        public float rx, ry, rz;     // degree

        public CartesianPose(float x, float y, float z, float rx, float ry, float rz)
        {
            this.x = x; this.y = y; this.z = z;
            this.rx = rx; this.ry = ry; this.rz = rz;
        }

        public override string ToString() =>
            $"X={x:F1} Y={y:F1} Z={z:F1} | Rx={rx:F1} Ry={ry:F1} Rz={rz:F1}";
    }
}
