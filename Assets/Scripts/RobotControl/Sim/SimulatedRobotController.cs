using System;
using System.Collections;
using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// 시뮬레이션 로봇 컨트롤러 (IK 통합판).
    /// 조인트/그리퍼/홈/모드/스피드 + 데카르트 JOG (DLS IK 사용)
    /// 
    /// ■ Cartesian JOG 동작:
    ///   1. 현재 TCP 포즈를 기준으로 목표 포즈 = 현재 + (축방향 × 속도 × dt)
    ///   2. DLS IK 솔버가 이 목표를 만족하는 새 조인트 각도 계산
    ///   3. ArticulationBody/Transform drive로 이동
    /// 
    /// ■ 이로써 시뮬-실로봇 동작이 동일 (Mirror 모드에서 정확한 디지털 트윈)
    /// </summary>
    public class SimulatedRobotController : MonoBehaviour, IRobotController
    {
        public enum DriveMode { ArticulationBody, Transform }

        [Header("드라이브 방식")]
        public DriveMode driveMode = DriveMode.ArticulationBody;

        [Header("조인트 설정 (6개)")]
        public JointConfig[] joints;

        [Header("ArticulationBody 드라이브 파라미터")]
        public float stiffness = 10000f;
        public float damping = 1000f;
        public float forceLimit = 1000f;

        [Header("Transform 모드 보간 속도 (deg/sec)")]
        public float transformSmoothSpeed = 180f;

        [Header("그리퍼")]
        public GripperController gripper;

        [Header("TCP (Tool Center Point) 기준점")]
        [Tooltip("엔드이펙터 끝 Transform. 데카르트 포즈 읽기/표시 + IK 기준점.")]
        public Transform tcpTransform;

        [Header("JOG 설정")]
        [Tooltip("데카르트 JOG의 선속도 (mm/sec)")]
        public float jogLinearSpeed = 50f;
        [Tooltip("데카르트/조인트 JOG의 각속도 (deg/sec)")]
        public float jogAngularSpeed = 15f;

        [Header("IK 파라미터 (DLS)")]
        [Tooltip("감쇠 상수. 크면 안정적이지만 수렴 느림. 특이점 근처에서 키우면 좋음.")]
        [Range(0.01f, 0.5f)] public float ikDamping = 0.1f;
        [Tooltip("한 번의 IK 해법당 최대 반복 횟수")]
        [Range(1, 30)] public int ikMaxIterations = 8;

        // ── 내부 상태 ─────────────────────────────────────────────────
        private float[] targetAngles;
        private float[] currentAngles;
        private float[] homeAngles = new float[6];
        private Quaternion[] tfInitialRots;
        private ArticulationBody[] artBodies;
        private float gripperTargetPercent = 0f;

        // 부드러운 가속/감속용 (SmoothDamp의 현재 속도 추적)
        private float[] smoothVelocities;

        private RobotMode _mode = RobotMode.Manual;
        private int _globalSpeed = 50;

        // JOG 상태
        private Coroutine jogCoroutine;
        private int jogAxis = -1;      // 0~5 (Cartesian) or 10~15 (Joint)
        private int jogDir = 0;

        // IK 솔버 (Cartesian JOG에서 사용)
        private InverseKinematicsSolver ikSolver;

        // ── IRobotController 기본 구현 ───────────────────────────────
        public bool IsReady => true;
        public bool IsConnected => true;
        // 시뮬은 즉시 반응하므로 항상 false (블로킹 명령 없음)
        public bool IsBusy => false;
        public string StatusMessage => $"Sim ({driveMode}) | {_mode} | Speed {_globalSpeed}%";
        public event Action<string> OnStatusChanged;

        public int JointCount => joints?.Length ?? 0;
        public string GetJointName(int i) => joints[i].name;
        public float GetJointMinAngle(int i) => joints[i].minAngle;
        public float GetJointMaxAngle(int i) => joints[i].maxAngle;
        public float GetJointAngle(int i) => currentAngles[i];
        public float GetGripperOpenPercent() => gripperTargetPercent;

        void Awake()
        {
            if (joints == null || joints.Length == 0)
                joints = FairinoPresets.GetDefault6Axis();

            int n = joints.Length;
            targetAngles = new float[n];
            currentAngles = new float[n];
            tfInitialRots = new Quaternion[n];
            artBodies = new ArticulationBody[n];
            smoothVelocities = new float[n];

            for (int i = 0; i < n; i++)
            {
                if (joints[i].jointTransform != null)
                {
                    tfInitialRots[i] = joints[i].jointTransform.localRotation;
                    artBodies[i] = joints[i].jointTransform.GetComponent<ArticulationBody>();
                    // ⭐ 연결 검증: 각 슬롯에 연결된 실제 오브젝트 이름을 로그로 출력
                    Debug.Log($"[Joint Mapping] Slot {i} ({joints[i].name}) → Transform='{joints[i].jointTransform.name}', ArticulationBody={(artBodies[i] != null ? "있음" : "없음!")}");
                }
                else
                {
                    Debug.LogError($"[Joint Mapping] Slot {i} ({joints[i].name}) → Joint Transform이 연결되지 않았습니다!");
                }
                targetAngles[i] = joints[i].homeAngle;
                currentAngles[i] = joints[i].homeAngle;
                homeAngles[i] = joints[i].homeAngle;
            }

            // IK 솔버 초기화
            if (tcpTransform != null)
            {
                ikSolver = new InverseKinematicsSolver(joints, tcpTransform)
                {
                    damping = ikDamping,
                    maxIterations = ikMaxIterations,
                };
                ikSolver.SetInitialRotations(tfInitialRots);
            }
        }

        void Start()
        {
            if (driveMode == DriveMode.ArticulationBody) ConfigureArticulationDrives();
            else ApplyTransformRotations();
        }

        void ConfigureArticulationDrives()
        {
            for (int i = 0; i < joints.Length; i++)
            {
                var ab = artBodies[i];
                if (ab == null) continue;
                var drive = ab.xDrive;
                drive.stiffness = stiffness;
                drive.damping = damping;
                drive.forceLimit = forceLimit;
                drive.target = joints[i].invertSign ? -joints[i].homeAngle : joints[i].homeAngle;
                ab.xDrive = drive;
            }
        }

        void Update()
        {
            if (driveMode == DriveMode.ArticulationBody && artBodies != null)
            {
                for (int i = 0; i < artBodies.Length; i++)
                {
                    var ab = artBodies[i];
                    if (ab == null) continue;
                    var d = ab.xDrive;
                    if (d.stiffness < 1f)  // 0으로 리셋된 경우만
                    {
                        d.stiffness = stiffness;
                        d.damping = damping;
                        d.forceLimit = forceLimit;
                        ab.xDrive = d;
                    }
                }
            }
            if (driveMode == DriveMode.Transform)
            {
                for (int i = 0; i < joints.Length; i++)
                    currentAngles[i] = Mathf.MoveTowards(
                        currentAngles[i], targetAngles[i],
                        transformSmoothSpeed * (_globalSpeed / 100f) * Time.deltaTime);
                ApplyTransformRotations();
            }
            else
            {
                for (int i = 0; i < joints.Length; i++)
                {
                    var ab = artBodies[i];
                    if (ab != null && ab.jointType == ArticulationJointType.RevoluteJoint)
                    {
                        float deg = ab.jointPosition[0] * Mathf.Rad2Deg;
                        currentAngles[i] = joints[i].invertSign ? -deg : deg;
                    }
                }
            }
        }

        void ApplyTransformRotations()
        {
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i].jointTransform == null) continue;
                float angle = joints[i].invertSign ? -currentAngles[i] : currentAngles[i];
                joints[i].jointTransform.localRotation = tfInitialRots[i] *
                    Quaternion.AngleAxis(angle, joints[i].rotationAxis);
            }
        }

        public void Connect() { }
        public void Disconnect() { }

        public void SetJointTarget(int i, float angleDeg)
        {
            if (i < 0 || i >= joints.Length) return;
            float clamped = Mathf.Clamp(angleDeg, joints[i].minAngle, joints[i].maxAngle);
            targetAngles[i] = clamped;

            if (driveMode == DriveMode.ArticulationBody && artBodies[i] != null)
            {
                var drive = artBodies[i].xDrive;
                drive.target = joints[i].invertSign ? -clamped : clamped;
                artBodies[i].xDrive = drive;
            }
        }

        /// <summary>
        /// 각도 타겟을 Global Speed에 따라 부드러운 가속/감속으로 이동시킴.
        /// SmoothDamp 사용 → 실로봇의 사다리꼴 속도 프로파일과 유사하게 시작/정지 부드러움.
        /// </summary>
        /// <param name="i">조인트 인덱스</param>
        /// <param name="desiredAngleDeg">IK가 요청한 목표 각도</param>
        /// <param name="maxAngVelDegPerSec">Global Speed에 따른 최대 각속도</param>
        /// <param name="dt">델타 타임</param>
        void SetJointTargetRateLimited(int i, float desiredAngleDeg, float maxAngVelDegPerSec, float dt)
        {
            if (i < 0 || i >= joints.Length) return;

            // SmoothDamp: 현재→목표로 부드럽게 이동하면서 최대 속도 제한.
            // smoothTime이 짧을수록 빠르게, 길수록 느리게 도달.
            // smoothTime = 0.15초 → 약 0.3초에 걸쳐 자연스러운 가속/감속
            float smoothTime = 0.15f;
            float newTarget = Mathf.SmoothDamp(
                targetAngles[i],
                desiredAngleDeg,
                ref smoothVelocities[i],
                smoothTime,
                maxAngVelDegPerSec,  // 최대 속도
                dt
            );
            SetJointTarget(i, newTarget);
        }

        public void SetAllJointTargets(float[] angles)
        {
            int n = Mathf.Min(angles.Length, joints.Length);
            for (int i = 0; i < n; i++) SetJointTarget(i, angles[i]);
        }

        public void SetGripperTarget(float percent, int speed = 50, int force = 50)
        {
            gripperTargetPercent = Mathf.Clamp(percent, 0f, 100f);
            if (gripper != null) gripper.SetOpenValue(gripperTargetPercent / 100f);
        }

        public void StopMotion()
        {
            StopJog();
            for (int i = 0; i < joints.Length; i++)
            {
                targetAngles[i] = currentAngles[i];
                if (driveMode == DriveMode.ArticulationBody && artBodies[i] != null)
                {
                    var d = artBodies[i].xDrive;
                    d.target = joints[i].invertSign ? -currentAngles[i] : currentAngles[i];
                    artBodies[i].xDrive = d;
                }
            }
        }

        public void ResetToHome() => GoToHome();

        // ── 신규: TCP 포즈 (좌표계 변환 포함) ─────────────────────────
        // Unity world (Y-up, LH, m) → Robot base frame (Z-up, RH, mm)
        // RobotRoot 오브젝트 기준 상대 위치로 계산 (로봇이 (0,0,0)에 있지 않아도 정확)
        public CartesianPose GetCurrentTCPPose()
        {
            if (tcpTransform == null) return new CartesianPose();

            // RobotRoot 기준 상대 위치/회전 (로봇 베이스 = 원점이 되도록)
            Transform robotRootTf = this.transform;
            Vector3 localPos = robotRootTf.InverseTransformPoint(tcpTransform.position);
            Quaternion localRot = Quaternion.Inverse(robotRootTf.rotation) * tcpTransform.rotation;

            // Unity local → Robot base frame 변환
            Vector3 robotPos = CoordinateConverter.UnityPositionToRobot(localPos);
            Vector3 robotRPY = CoordinateConverter.UnityRotationToRobotRPY(localRot);

            return new CartesianPose(
                robotPos.x, robotPos.y, robotPos.z,
                Normalize180(robotRPY.x), Normalize180(robotRPY.y), Normalize180(robotRPY.z)
            );
        }

        static float Normalize180(float a)
        {
            while (a > 180f) a -= 360f;
            while (a < -180f) a += 360f;
            return a;
        }

        // ── 신규: JOG ─────────────────────────────────────────────────
        public void StartCartesianJog(int axis, int dir)
        {
            StopJog();
            jogAxis = axis;            // 0~5 = X/Y/Z/Rx/Ry/Rz
            jogDir = dir;
            jogCoroutine = StartCoroutine(JogLoop(isCartesian: true));
        }

        public void StartJointJog(int jointIndex, int dir)
        {
            StopJog();
            jogAxis = 10 + jointIndex; // 10~15 = Joint 1~6
            jogDir = dir;
            jogCoroutine = StartCoroutine(JogLoop(isCartesian: false));
        }

        public void StopJog()
        {
            if (jogCoroutine != null) { StopCoroutine(jogCoroutine); jogCoroutine = null; }
            jogAxis = -1; jogDir = 0;

            // SmoothDamp 속도 리셋 (다음 JOG가 0에서 다시 부드럽게 시작되도록)
            if (smoothVelocities != null)
                for (int i = 0; i < smoothVelocities.Length; i++) smoothVelocities[i] = 0f;
        }

        IEnumerator JogLoop(bool isCartesian)
        {
            while (jogAxis >= 0)
            {
                float dt = Time.deltaTime;
                float speedMul = _globalSpeed / 100f;

                if (isCartesian)
                {
                    // ✅ DLS IK 기반 정확한 Cartesian JOG
                    //    현재 TCP 포즈 → 목표 축 방향으로 한 스텝 이동 → IK로 조인트 해법 계산
                    //    axis: 0=X, 1=Y, 2=Z, 3=Rx, 4=Ry, 5=Rz (로봇 base frame 기준)
                    int axis = jogAxis;

                    if (ikSolver == null || tcpTransform == null)
                    {
                        Debug.LogWarning("[Sim] IK solver 또는 TCP Transform 미설정. Cartesian JOG 불가.");
                        yield return null;
                        continue;
                    }

                    // 현재 TCP 포즈 (RobotRoot 로컬 기준 = 로봇 base frame)
                    Transform baseTf = this.transform;
                    Vector3 currLocalPos = baseTf.InverseTransformPoint(tcpTransform.position);
                    Quaternion currLocalRot = Quaternion.Inverse(baseTf.rotation) * tcpTransform.rotation;

                    // 한 스텝 이동량 계산 (로봇 base frame 기준)
                    Vector3 targetLocalPos = currLocalPos;
                    Quaternion targetLocalRot = currLocalRot;

                    if (axis < 3)
                    {
                        // 선형 이동 (mm → m 단위)
                        float stepMm = jogLinearSpeed * speedMul * dt * jogDir;
                        float stepM = stepMm * 0.001f;

                        // 로봇 base frame의 X/Y/Z → Unity local frame으로 변환
                        // CoordinateConverter의 역변환 적용
                        // Robot X (axis=0) → Unity local Z
                        // Robot Y (axis=1) → Unity local -X
                        // Robot Z (axis=2) → Unity local Y
                        Vector3 localDir = Vector3.zero;
                        if (axis == 0) localDir = Vector3.forward;    // Robot X → Unity Z
                        else if (axis == 1) localDir = -Vector3.right; // Robot Y → Unity -X
                        else if (axis == 2) localDir = Vector3.up;     // Robot Z → Unity Y

                        targetLocalPos += localDir * stepM;
                    }
                    else
                    {
                        // 회전 이동 (deg → rad)
                        float stepDeg = jogAngularSpeed * speedMul * dt * jogDir;

                        // 로봇 base frame의 Rx/Ry/Rz → Unity local 회전축 매핑
                        Vector3 localAxis = Vector3.zero;
                        if (axis == 3) localAxis = Vector3.forward;    // Robot Rx → Unity Z
                        else if (axis == 4) localAxis = -Vector3.right; // Robot Ry → Unity -X
                        else if (axis == 5) localAxis = Vector3.up;     // Robot Rz → Unity Y

                        Quaternion deltaRot = Quaternion.AngleAxis(stepDeg, localAxis);
                        // 월드 기준 회전 누적: new = delta * current
                        targetLocalRot = deltaRot * currLocalRot;
                    }

                    // 목표 포즈를 월드 좌표로 환산
                    Vector3 targetWorldPos = baseTf.TransformPoint(targetLocalPos);
                    Quaternion targetWorldRot = baseTf.rotation * targetLocalRot;

                    // IK 풀기
                    float[] newAngles = ikSolver.Solve(currentAngles, targetWorldPos, targetWorldRot);

                    // 결과 적용 — Global Speed에 따라 각 조인트 속도 제한
                    // 각 조인트당 최대 각속도 = 180°/s × speedMul
                    float maxJointVel = 180f * speedMul;
                    for (int i = 0; i < newAngles.Length; i++)
                    {
                        SetJointTargetRateLimited(i, newAngles[i], maxJointVel, dt);
                    }
                }
                else
                {
                    // Joint JOG — 단순 각도 증가
                    int j = jogAxis - 10; // 0~5
                    float delta = jogAngularSpeed * speedMul * dt * jogDir;
                    SetJointTarget(j, targetAngles[j] + delta);
                }
                yield return null;
            }
        }

        // ── 신규: 홈 포즈 ────────────────────────────────────────────
        public float[] GetHomePose() => (float[])homeAngles.Clone();

        public void SetHomePose(float[] anglesDeg)
        {
            if (anglesDeg == null || anglesDeg.Length < 6) return;
            for (int i = 0; i < 6; i++) homeAngles[i] = anglesDeg[i];
        }

        public void SetHomePoseFromCurrent()
        {
            for (int i = 0; i < 6; i++) homeAngles[i] = currentAngles[i];
        }

        public void GoToHome() => SetAllJointTargets(homeAngles);

        // ── 신규: 모드 ──────────────────────────────────────────────
        public RobotMode GetMode() => _mode;
        public void SetMode(RobotMode m) { _mode = m; OnStatusChanged?.Invoke(StatusMessage); }

        // ── 신규: 글로벌 스피드 ─────────────────────────────────────
        public int GetGlobalSpeed() => _globalSpeed;
        public void SetGlobalSpeed(int percent)
        {
            _globalSpeed = Mathf.Clamp(percent, 0, 100);
            OnStatusChanged?.Invoke(StatusMessage);
        }
    }
}
