using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// Unity 좌표계 ↔ Fairino FR5 로봇 base frame 변환 유틸.
    /// 
    /// ■ 좌표계 규약:
    ///   Unity:   Left-handed, Y-up,     meter,  Euler ZXY
    ///   FR5:     Right-handed, Z-up,    mm,     Fixed-XYZ Euler (Rx→Ry→Rz)
    /// 
    /// ■ 축 매핑 (URDF Importer 기본 설정 기준):
    ///   URDF Importer는 ROS/URDF의 X-forward, Y-left, Z-up 좌표계를
    ///   Unity의 좌표계로 바꿀 때 다음 축 교체를 적용:
    ///     Robot X (앞) → Unity Z (앞)
    ///     Robot Y (왼) → Unity -X (왼쪽은 Unity에서 -X)
    ///     Robot Z (위) → Unity Y (위)
    /// 
    /// ■ 그래서 양방향 변환은:
    ///   Unity→Robot: (Rx, Ry, Rz) = (-Uy_unity_translated...)  ← 아래 함수 참조
    /// 
    /// ■ 중요: URDF Importer 버전/옵션에 따라 축 매핑이 조금씩 다를 수 있음.
    ///   → 로봇 연결 전에 "좌표 확인 모드"로 검증 필수 (README 참조).
    /// </summary>
    public static class CoordinateConverter
    {
        /// <summary>
        /// Unity 월드 위치(m) → Robot base frame 위치(mm).
        /// URDF Importer 기본 축 매핑: Robot X ← Unity Z, Robot Y ← -Unity X, Robot Z ← Unity Y
        /// </summary>
        public static Vector3 UnityPositionToRobot(Vector3 unityWorld)
        {
            // m → mm 단위 변환 포함
            return new Vector3(
                unityWorld.z * 1000f,      // Robot X = Unity Z (앞)
                -unityWorld.x * 1000f,     // Robot Y = -Unity X (왼쪽)
                unityWorld.y * 1000f       // Robot Z = Unity Y (위)
            );
        }

        /// <summary>
        /// Robot base frame 위치(mm) → Unity 월드 위치(m).
        /// </summary>
        public static Vector3 RobotPositionToUnity(Vector3 robotMm)
        {
            return new Vector3(
                -robotMm.y * 0.001f,       // Unity X = -Robot Y
                robotMm.z * 0.001f,        // Unity Y = Robot Z
                robotMm.x * 0.001f         // Unity Z = Robot X
            );
        }

        /// <summary>
        /// Unity 회전(Quaternion) → Robot RPY (Rx, Ry, Rz) 고정축 XYZ (degree).
        /// 축 매핑 + 오일러 순서 변환을 모두 처리.
        /// </summary>
        public static Vector3 UnityRotationToRobotRPY(Quaternion unityRot)
        {
            // Unity 좌표계의 회전을 Robot 좌표계의 회전으로 변환.
            // 1. Unity Quaternion을 Robot frame에서 바라본 Quaternion으로 변환
            // 2. Fixed XYZ Euler (Rx-Ry-Rz)로 분해
            // 
            // 축 교체 행렬 M (Unity → Robot):
            //   |  0  0  1 |  (Unity X → ?)
            //   | -1  0  0 |  (Unity Y → ?)
            //   |  0  1  0 |  (Unity Z → ?)
            // Robot 회전 = M * Unity회전행렬 * M^T
            //
            // Quaternion 형태로 축 교체:
            //   q_robot = (qz, -qx, qy, qw)  (축 스왑에 따른 성분 재배치)

            Quaternion qRobot = new Quaternion(
                unityRot.z,
                -unityRot.x,
                unityRot.y,
                unityRot.w
            );

            // Right-handed ↔ Left-handed 변환 (부호 반전)
            qRobot = new Quaternion(-qRobot.x, -qRobot.y, -qRobot.z, qRobot.w);

            // Fixed XYZ Euler 추출 (Fairino DescPose.rpy 규약)
            return QuaternionToFixedXYZEuler(qRobot);
        }

        /// <summary>
        /// Robot RPY (Rx, Ry, Rz) → Unity 회전(Quaternion).
        /// MoveL/MoveCart로 목표 포즈 보낼 때 Unity에서 지정한 자세를 변환할 때 사용.
        /// </summary>
        public static Quaternion RobotRPYToUnityRotation(Vector3 robotRPY)
        {
            Quaternion qRobot = FixedXYZEulerToQuaternion(robotRPY);

            // Right-handed ↔ Left-handed (부호 반전)
            qRobot = new Quaternion(-qRobot.x, -qRobot.y, -qRobot.z, qRobot.w);

            // Robot → Unity 축 교체 (위 변환의 역)
            return new Quaternion(
                -qRobot.y,
                qRobot.z,
                qRobot.x,
                qRobot.w
            );
        }

        // ── Cartesian JOG 축 매핑 ────────────────────────────────────
        /// <summary>
        /// UI에서 사용자가 "월드 X+" 버튼을 눌렀을 때, 
        /// Fairino SDK의 StartJOG에 보낼 축 번호(1=X,2=Y,3=Z,4=Rx,5=Ry,6=Rz)와 방향(0/1)을 반환.
        /// 
        /// ■ 규약: UI의 X/Y/Z는 "로봇 base frame 기준"으로 명명됨.
        ///    (사용자는 티치펜던트와 동일한 방향을 기대하므로)
        ///    따라서 1:1 매핑이면 됨.
        /// </summary>
        public static (int nb, int dir) CartesianUIToFairinoJog(int uiAxis, int uiDir)
        {
            // uiAxis: 0=X, 1=Y, 2=Z, 3=Rx, 4=Ry, 5=Rz (로봇 base frame 기준)
            // Fairino nb: 1~6 (1=X/관절1, 2=Y/관절2 ...)
            int nb = uiAxis + 1;
            int dir = (uiDir > 0) ? 1 : 0;  // 양=1, 음=0
            return (nb, dir);
        }

        // ── 오일러 변환 (Fixed XYZ == ZYX Intrinsic) ──────────────────
        /// <summary>
        /// Quaternion → Fixed XYZ Euler (degree).
        /// Fairino의 DescPose.rpy 규약: 고정 X축 회전 먼저, 고정 Y축, 고정 Z축 순서.
        /// 
        /// (Fixed XYZ Euler == Intrinsic ZYX Euler, 수학적으로 동일)
        /// </summary>
        static Vector3 QuaternionToFixedXYZEuler(Quaternion q)
        {
            // Fixed XYZ (= Intrinsic ZYX) 분해 공식
            float sinP = 2f * (q.w * q.y - q.z * q.x);
            sinP = Mathf.Clamp(sinP, -1f, 1f);

            float ry = Mathf.Asin(sinP);
            float rx, rz;

            if (Mathf.Abs(sinP) > 0.9999f)
            {
                // Gimbal lock: ry ≈ ±90°
                rx = 0f;
                rz = Mathf.Atan2(2f * (q.w * q.z + q.x * q.y), 1f - 2f * (q.x * q.x + q.z * q.z));
            }
            else
            {
                rx = Mathf.Atan2(2f * (q.w * q.x + q.y * q.z), 1f - 2f * (q.x * q.x + q.y * q.y));
                rz = Mathf.Atan2(2f * (q.w * q.z + q.x * q.y), 1f - 2f * (q.y * q.y + q.z * q.z));
            }

            return new Vector3(
                rx * Mathf.Rad2Deg,
                ry * Mathf.Rad2Deg,
                rz * Mathf.Rad2Deg
            );
        }

        /// <summary>
        /// Fixed XYZ Euler (degree) → Quaternion.
        /// </summary>
        static Quaternion FixedXYZEulerToQuaternion(Vector3 rpy)
        {
            // Fixed XYZ = X 회전 먼저, 그 다음 Y, 마지막 Z (고정 월드축 기준)
            // Unity Quaternion 곱셈은 왼쪽에서 오른쪽으로 적용되므로:
            float rx = rpy.x * Mathf.Deg2Rad;
            float ry = rpy.y * Mathf.Deg2Rad;
            float rz = rpy.z * Mathf.Deg2Rad;

            Quaternion qx = Quaternion.AngleAxis(rpy.x, Vector3.right);
            Quaternion qy = Quaternion.AngleAxis(rpy.y, Vector3.up);
            Quaternion qz = Quaternion.AngleAxis(rpy.z, Vector3.forward);

            // Fixed XYZ (extrinsic): z * y * x 순서로 곱함
            return qz * qy * qx;
        }
    }
}
