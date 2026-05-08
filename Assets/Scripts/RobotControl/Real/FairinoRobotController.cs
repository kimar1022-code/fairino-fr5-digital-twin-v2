using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#if FAIRINO_SDK
using fairino;
#endif

namespace RobotControl
{
    /// <summary>
    /// 실 Fairino 로봇 컨트롤러 (확장판).
    /// 
    /// 신규 기능(모두 Fairino SDK 호출):
    ///   - GetActualTCPPose() : 현재 TCP 포즈 읽기
    ///   - StartJOG() / StopJOG() : 데카르트/조인트 JOG (SDK가 IK 자동 처리)
    ///   - Mode(0/1) : 자동/수동 모드 전환
    ///   - SetSpeed() : 글로벌 스피드
    ///   - SetHomePose + MoveJ(home) : 홈 포즈 저장/이동
    /// 
    /// SDK 호출은 전부 워커 스레드에서 수행 — UI는 타겟만 업데이트.
    /// </summary>
    public class FairinoRobotController : MonoBehaviour, IRobotController
    {
        [Header("연결")]
        public string robotIP = "192.168.58.2";

        [Header("전송 주기")]
        public float commandRate = 20f;
        public float stateReadRate = 10f;

        [Header("MoveJ 파라미터 (홈 이동 등)")]
        [Range(1f, 100f)] public float moveJVel = 20f;
        [Range(1f, 100f)] public float moveJAcc = 30f;
        [Range(1f, 100f)] public float moveJOvl = 50f;
        public float blendT = 50f;

        [Header("JOG 파라미터")]
        [Range(1f, 100f)] public float jogVel = 30f;
        [Range(1f, 100f)] public float jogAcc = 30f;
        [Tooltip("JOG 1회 최대 이동량 (deg 또는 mm). StopJog 호출 전에 자동 정지 방지용 큰 값 권장.")]
        public float jogMaxDis = 180f;

        [Header("조인트")]
        public JointConfig[] joints;

        [Header("그리퍼")]
        public int gripperCompany = 4;
        public int gripperDevice = 0;
        public int gripperSoftver = 1;
        public int gripperBus = 1;
        public int gripperIndex = 1;
        public int gripperType = 0;   // 0=평행, 1=회전

        // ── 스레드 공유 상태 ─────────────────────────────────────────
        private readonly object stateLock = new object();
        private float[] currentJointAngles = new float[6];
        private float[] targetJointAngles = new float[6];
        private float[] homeJointAngles = new float[6] { 0, -90, 0, -90, 90, 0 };
        private bool jointDirty = false;

        private CartesianPose currentTCPPose;

        private float currentGripperPercent = 0f;
        private float targetGripperPercent = 0f;
        private bool gripperDirty = false;

        private RobotMode _mode = RobotMode.Auto;
        private int _globalSpeed = 50;

        // JOG 명령 큐 (메인 → 워커로 전달)
        private volatile bool jogStartRequested = false;
        private volatile bool jogStopRequested = false;
        private volatile bool goHomeRequested = false;
        private volatile bool setModeRequested = false;
        private volatile bool setSpeedRequested = false;
        private int jogRefType, jogNb, jogDir;
        private byte jogStopType;

        private volatile bool _isConnected = false;
        private volatile bool _isReady = false;
        private string _statusMessage = "Disconnected";
        private readonly object statusLock = new object();

        // 현재 로봇이 사용하는 Tool/Work Object 번호 (Connect 시 자동 조회)
        private int currentToolNum = 0;
        private int currentWobjNum = 0;

        // 🎯 순차 실행: MoveJ 수행 중일 때 true.
        //   새 명령은 _isBusy=false 가 될 때까지 무시됨.
        private volatile bool _isBusy = false;
        public bool IsBusy => _isBusy;

        private volatile bool workerRunning = false;
        private Thread workerThread;

#if FAIRINO_SDK
        private Robot robot;
#endif

        public bool IsReady => _isReady;
        public bool IsConnected => _isConnected;
        public string StatusMessage { get { lock (statusLock) return _statusMessage; } }
        public event Action<string> OnStatusChanged;

        public int JointCount => joints?.Length ?? 6;
        public string GetJointName(int i) => joints[i].name;
        public float GetJointMinAngle(int i) => joints[i].minAngle;
        public float GetJointMaxAngle(int i) => joints[i].maxAngle;

        public float GetJointAngle(int i)
        {
            lock (stateLock) return currentJointAngles[i];
        }

        public float GetGripperOpenPercent()
        {
            lock (stateLock) return currentGripperPercent;
        }

        void Awake()
        {
            if (joints == null || joints.Length == 0)
                joints = FairinoPresets.GetDefault6Axis();
        }

        // ── 연결 ──────────────────────────────────────────────────────
        public void Connect()
        {
#if FAIRINO_SDK
            if (_isConnected) { SetStatus("Already connected"); return; }
            SetStatus($"Connecting to {robotIP}...");
            Task.Run(() =>
            {
                try
                {
                    robot = new Robot();
                    robot.SetReconnectParam(true, 1000, 20);
                    int rc = robot.RPC(robotIP);
                    if (rc != 0) { SetStatus($"RPC failed ({rc})"); return; }

                    robot.Mode((int)_mode);
                    robot.RobotEnable(1);
                    robot.SetSpeed(_globalSpeed);

                    robot.SetGripperConfig(gripperCompany, gripperDevice, gripperSoftver, gripperBus);
                    Thread.Sleep(200);
                    robot.ActGripper(gripperIndex, 1);

                    var jp = new JointPos(new double[6]);
                    robot.GetActualJointPosDegree(0, ref jp);
                    lock (stateLock)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            currentJointAngles[i] = (float)jp.jPos[i];
                            targetJointAngles[i] = (float)jp.jPos[i];
                        }
                        jointDirty = false;
                    }

                    // 현재 로봇이 사용 중인 Tool/Work Object 번호 조회
                    // MoveJ에서 이 번호와 다른 값을 주면 "관절 명령 포인트 오류" 발생
                    int toolNum = 0, wobjNum = 0;
                    robot.GetActualTCPNum(0, ref toolNum);
                    robot.GetActualWObjNum(0, ref wobjNum);
                    currentToolNum = toolNum;
                    currentWobjNum = wobjNum;
                    UnityEngine.Debug.Log($"[Fairino] Current Tool={currentToolNum}, Wobj={currentWobjNum}");

                    _isConnected = true;
                    _isReady = true;
                    StartWorker();
                    SetStatus($"Connected | {_mode} | Tool={currentToolNum} Wobj={currentWobjNum} | Speed {_globalSpeed}%");
                }
                catch (Exception ex)
                {
                    SetStatus($"Connect error: {ex.Message}");
                    _isConnected = false; _isReady = false;
                }
            });
#else
            SetStatus("FAIRINO_SDK not defined.");
#endif
        }

        public void Disconnect()
        {
            StopWorker();
#if FAIRINO_SDK
            try
            {
                if (robot != null) { robot.RobotEnable(0); robot.CloseRPC(); }
            }
            catch { }
            robot = null;
#endif
            _isConnected = false; _isReady = false;
            SetStatus("Disconnected");
        }

        void OnDestroy() { Disconnect(); }
        void OnApplicationQuit() { Disconnect(); }

        // ── 워커 스레드 ───────────────────────────────────────────────
        void StartWorker()
        {
            if (workerThread != null && workerThread.IsAlive) return;
            workerRunning = true;
            workerThread = new Thread(WorkerLoop) { IsBackground = true, Name = "FairinoWorker" };
            workerThread.Start();
        }

        void StopWorker()
        {
            workerRunning = false;
            if (workerThread != null && workerThread.IsAlive) workerThread.Join(1500);
            workerThread = null;
        }

        void WorkerLoop()
        {
#if FAIRINO_SDK
            long cmdIntervalMs = (long)(1000f / Mathf.Max(1f, commandRate));
            long stateIntervalMs = (long)(1000f / Mathf.Max(1f, stateReadRate));
            var cmdTimer = System.Diagnostics.Stopwatch.StartNew();
            var stateTimer = System.Diagnostics.Stopwatch.StartNew();

            while (workerRunning)
            {
                try
                {
                    // 1) 즉시 처리해야 하는 이벤트성 명령 (JOG, 모드, 스피드, 홈)
                    if (jogStartRequested)
                    {
                        jogStartRequested = false;
                        robot.StartJOG(jogRefType, jogNb, jogDir, jogVel, jogAcc, jogMaxDis);
                    }
                    if (jogStopRequested)
                    {
                        jogStopRequested = false;
                        robot.StopJOG(jogStopType);
                    }
                    if (setModeRequested)
                    {
                        setModeRequested = false;
                        robot.Mode((int)_mode);
                    }
                    if (setSpeedRequested)
                    {
                        setSpeedRequested = false;
                        robot.SetSpeed(_globalSpeed);
                    }
                    if (goHomeRequested)
                    {
                        goHomeRequested = false;
                        float[] home;
                        lock (stateLock) home = (float[])homeJointAngles.Clone();
                        _isBusy = true;         // 🎯 GoHome도 Busy 처리
                        SetStatus($"Going Home... (BUSY)");
                        SendMoveJ(home);
                        _isBusy = false;
                        SetStatus($"Connected | {_mode} | Tool={currentToolNum} Wobj={currentWobjNum} | Speed {_globalSpeed}%");
                    }

                    // 2) 조인트/그리퍼 타겟 (주기적)
                    if (cmdTimer.ElapsedMilliseconds >= cmdIntervalMs)
                    {
                        cmdTimer.Restart();
                        float[] jTarget = null; bool jDirty;
                        float gTarget = 0f; bool gDirty;
                        lock (stateLock)
                        {
                            jDirty = jointDirty;
                            if (jDirty) { jTarget = (float[])targetJointAngles.Clone(); jointDirty = false; }
                            gDirty = gripperDirty;
                            gTarget = targetGripperPercent;
                            if (gDirty) gripperDirty = false;
                        }
                        if (jDirty)
                        {
                            _isBusy = true;       // 🎯 MoveJ 실행 전 Busy
                            SetStatus($"Moving... (BUSY)");
                            SendMoveJ(jTarget);   // 블로킹 모드 (blendT=-1.0) - 도달까지 대기
                            _isBusy = false;      // 🎯 완료 후 해제
                            SetStatus($"Connected | {_mode} | Tool={currentToolNum} Wobj={currentWobjNum} | Speed {_globalSpeed}%");
                        }
                        if (gDirty) SendGripper(gTarget);
                    }

                    // 3) 상태 읽기
                    if (stateTimer.ElapsedMilliseconds >= stateIntervalMs)
                    {
                        stateTimer.Restart();
                        ReadRobotState();
                    }
                }
                catch (Exception ex) { SetStatus($"Worker err: {ex.Message}"); }

                Thread.Sleep(5);
            }
#endif
        }

#if FAIRINO_SDK
        void SendMoveJ(float[] target)
        {
            try
            {
                // 조인트 타겟 설정
                var jp = new JointPos(target[0], target[1], target[2], target[3], target[4], target[5]);
                var ep = new ExaxisPos(new double[4]);
                var op = new DescPose(0, 0, 0, 0, 0, 0);

                Debug.Log($"[MoveJ] Target J=[{target[0]:F2}, {target[1]:F2}, {target[2]:F2}, {target[3]:F2}, {target[4]:F2}, {target[5]:F2}]");
                Debug.Log($"[MoveJ] Tool={currentToolNum}, Wobj={currentWobjNum}, vel={moveJVel}, acc={moveJAcc}, ovl={moveJOvl}");

                // ⭐ 핵심: 현재 로봇이 사용 중인 Tool/Wobj 번호를 사용 (0,0 아님!)
                //   "관절 명령 포인트 오류"의 원인은 Tool/Wobj 번호 불일치
                // DescPose 인자 없는 오버로드 → SDK가 내부에서 자동 FK 계산
                int rc = robot.MoveJ(jp, currentToolNum, currentWobjNum, moveJVel, moveJAcc, moveJOvl, ep, -1.0f, 0, op);

                Debug.Log($"[MoveJ] Return code: {rc}");

                if (rc != 0)
                {
                    SetStatus($"MoveJ rc={rc} (Tool={currentToolNum}, Wobj={currentWobjNum})");
                    Debug.LogError($"[MoveJ] FAILED with code {rc}");
                }
            }
            catch (Exception ex)
            {
                SetStatus($"MoveJ: {ex.Message}");
                Debug.LogError($"[MoveJ] Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        void SendGripper(float percent)
        {
            try
            {
                robot.MoveGripper(gripperIndex, (int)percent, 50, 50, 30000, 1, gripperType, 0, 0, 0);
            }
            catch (Exception ex) { SetStatus($"Gripper: {ex.Message}"); }
        }

        void ReadRobotState()
        {
            try
            {
                var jp = new JointPos(new double[6]);
                if (robot.GetActualJointPosDegree(0, ref jp) == 0)
                {
                    lock (stateLock)
                        for (int i = 0; i < 6; i++) currentJointAngles[i] = (float)jp.jPos[i];
                }

                var dp = new DescPose();
                if (robot.GetActualTCPPose(0, ref dp) == 0)
                {
                    lock (stateLock)
                    {
                        currentTCPPose = new CartesianPose(
                            (float)dp.tran.x, (float)dp.tran.y, (float)dp.tran.z,
                            (float)dp.rpy.rx, (float)dp.rpy.ry, (float)dp.rpy.rz);
                    }
                }

                int fault = 0, pos = 0;
                if (robot.GetGripperCurPosition(ref fault, ref pos) == 0 && fault == 0)
                {
                    lock (stateLock) currentGripperPercent = pos;
                }
            }
            catch { }
        }
#endif

        // ── 외부 API: 조인트/그리퍼 ─────────────────────────────────
        public void SetJointTarget(int i, float angleDeg)
        {
            if (i < 0 || i >= 6) return;
            // 🎯 로봇이 움직이는 중이면 새 명령 무시 (순차 실행)
            if (_isBusy)
            {
                Debug.Log($"[Fairino] Busy: J{i + 1} 명령 무시됨 (현재 MoveJ 실행 중)");
                return;
            }
            float clamped = Mathf.Clamp(angleDeg, joints[i].minAngle, joints[i].maxAngle);
            lock (stateLock)
            {
                if (Math.Abs(targetJointAngles[i] - clamped) > 0.001f)
                {
                    // 🎯 핵심 수정: 변경하지 않는 다른 관절들의 타겟을
                    //   실제 현재 값으로 동기화 (다른 관절이 엉뚱하게 움직이는 것 방지)
                    for (int k = 0; k < 6; k++)
                    {
                        if (k != i) targetJointAngles[k] = currentJointAngles[k];
                    }
                    targetJointAngles[i] = clamped;
                    jointDirty = true;
                }
            }
        }

        public void SetAllJointTargets(float[] angles)
        {
            lock (stateLock)
            {
                int n = Mathf.Min(angles.Length, 6);
                for (int i = 0; i < n; i++)
                    targetJointAngles[i] = Mathf.Clamp(angles[i], joints[i].minAngle, joints[i].maxAngle);
                jointDirty = true;
            }
        }

        public void SetGripperTarget(float percent, int speed = 50, int force = 50)
        {
            lock (stateLock)
            {
                float clamped = Mathf.Clamp(percent, 0f, 100f);
                if (Math.Abs(targetGripperPercent - clamped) > 0.1f)
                {
                    targetGripperPercent = clamped; gripperDirty = true;
                }
            }
        }

        public void StopMotion()
        {
#if FAIRINO_SDK
            try { robot?.StopMotion(); } catch { }
            try { robot?.ImmStopJOG(); } catch { }
            lock (stateLock)
            {
                for (int i = 0; i < 6; i++) targetJointAngles[i] = currentJointAngles[i];
                jointDirty = false;
            }
#endif
        }

        public void ResetToHome() => GoToHome();

        // ── 신규: TCP 포즈 ────────────────────────────────────────────
        public CartesianPose GetCurrentTCPPose()
        {
            lock (stateLock) return currentTCPPose;
        }

        // ── 신규: JOG ─────────────────────────────────────────────────
        // SDK: StartJOG(refType, nb, dir, vel, acc, max_dis)
        //   refType: 0=관절, 2=베이스 데카르트, 4=툴, 8=워크피스
        //   nb:      관절=1~6 / 데카르트=1(X) 2(Y) 3(Z) 4(Rx) 5(Ry) 6(Rz)
        //   dir:     0=음, 1=양

        public void StartCartesianJog(int axis, int dir)
        {
            jogRefType = 2;            // base frame
            jogNb = axis + 1;          // 0~5 → 1~6
            jogDir = (dir > 0) ? 1 : 0;
            jogStartRequested = true;
        }

        public void StartJointJog(int jointIndex, int dir)
        {
            jogRefType = 0;            // joint space
            jogNb = jointIndex + 1;
            jogDir = (dir > 0) ? 1 : 0;
            jogStartRequested = true;
        }

        public void StopJog()
        {
            // stopType: 1=관절, 3=베이스, 5=툴, 9=워크피스
            jogStopType = (byte)(jogRefType == 0 ? 1 : jogRefType + 1);
            jogStopRequested = true;
        }

        // ── 신규: 홈 포즈 ────────────────────────────────────────────
        public float[] GetHomePose()
        {
            lock (stateLock) return (float[])homeJointAngles.Clone();
        }

        public void SetHomePose(float[] anglesDeg)
        {
            if (anglesDeg == null || anglesDeg.Length < 6) return;
            lock (stateLock)
                for (int i = 0; i < 6; i++) homeJointAngles[i] = anglesDeg[i];
        }

        public void SetHomePoseFromCurrent()
        {
            lock (stateLock)
                for (int i = 0; i < 6; i++) homeJointAngles[i] = currentJointAngles[i];
        }

        public void GoToHome() => goHomeRequested = true;

        // ── 신규: 모드 ──────────────────────────────────────────────
        public RobotMode GetMode() => _mode;

        public void SetMode(RobotMode m)
        {
            _mode = m;
            setModeRequested = true;
            SetStatus($"Connected | {_mode} | Speed {_globalSpeed}%");
        }

        // ── 신규: 글로벌 스피드 ─────────────────────────────────────
        public int GetGlobalSpeed() => _globalSpeed;

        public void SetGlobalSpeed(int percent)
        {
            _globalSpeed = Mathf.Clamp(percent, 0, 100);
            setSpeedRequested = true;
            SetStatus($"Connected | {_mode} | Speed {_globalSpeed}%");
        }

        void SetStatus(string msg)
        {
            lock (statusLock) _statusMessage = msg;
            try { OnStatusChanged?.Invoke(msg); } catch { }
        }

        // ── 신규: PLC 디지털 입력 읽기 (학원 검증) ──────────────────
        /// <summary>
        /// 컨트롤 박스 디지털 입력 핀 읽기.
        /// id 0~7  → 결선도의 DI 0~DI 7
        /// id 8~15 → 결선도의 CI 0~CI 7 (별도 채널)
        /// SDK 미연결 또는 비활성 시 0 반환 (PLC SIM 모드 자동 처리, v5 결정 9-2).
        /// </summary>
        public byte ReadDIPin(int diIndex)
        {
#if FAIRINO_SDK
            if (robot == null || !_isConnected) return 0;
            try
            {
                byte level = 0;
                // GetDI(id, block, ref level) — block=0: non-blocking
                int rc = robot.GetDI(diIndex, 0, ref level);
                if (rc != 0) return 0;
                return level;
            }
            catch
            {
                return 0;
            }
#else
            return 0;
#endif
        }

    }
}
