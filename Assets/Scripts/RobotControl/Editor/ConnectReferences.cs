using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RobotControl;

namespace RobotControl.EditorTools
{
    /// <summary>
    /// Phase 3-A 2단계: 매니저 5개 + 코어 3개의 참조를 자동으로 연결.
    /// 전제조건: SceneSetup.cs (1단계)이 먼저 실행되어
    ///   RobotManager, TeachModeManager, WaypointPlayer, WaypointRecorder, WaypointStorage,
    ///   MainCanvas, EventSystem, fairino5_v6_robot 이 씬에 존재해야 함.
    ///
    /// B-1 결정에 따른 배치:
    ///   - RobotManager GameObject       += FairinoRobotController 컴포넌트
    ///   - fairino5_v6_robot GameObject  += SimulatedRobotController 컴포넌트
    ///   - PLCButtons (신규 GameObject)  += PLCButtonHandler 컴포넌트
    ///
    /// SimulatedRobotController 슬롯:
    ///   - joints[6] = handover_v5 검증값 (limit/home) + URDF Transform 자동 연결
    ///   - tcpTransform = ToolMount/GripperPoint
    ///   - gripper = null (Phase 4 이후 결정)
    /// </summary>
    public static class ConnectReferences
    {
        private const string MenuPath = "Tools/Connect Manager References";
        private const string RobotRootName = "fairino5_v6_robot";
        private const string PlcButtonsName = "PLCButtons";
        private const string RobotSystemHeader = "--- ROBOT SYSTEM ---";

        // SimulatedRobotController joints[]에 들어갈 6개 Transform 경로 (URDF 계층 검증 결과)
        private static readonly string[] JointPaths = new string[]
        {
            "base_link/shoulder_link",
            "base_link/shoulder_link/upperarm_link",
            "base_link/shoulder_link/upperarm_link/forearm_link",
            "base_link/shoulder_link/upperarm_link/forearm_link/wrist1_link",
            "base_link/shoulder_link/upperarm_link/forearm_link/wrist1_link/wrist2_link",
            "base_link/shoulder_link/upperarm_link/forearm_link/wrist1_link/wrist2_link/wrist3_link",
        };

        // tcpTransform 경로
        private const string TcpPath =
            "base_link/shoulder_link/upperarm_link/forearm_link/wrist1_link/wrist2_link/wrist3_link/ToolMount/GripperPoint";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Connect Manager References");

            try
            {
                // ===== Phase A: 코어 컴포넌트 추가 =====
                var robotMgr = FindRequired<RobotManager>("RobotManager");
                if (robotMgr == null) return;

                var teachMgr = FindRequired<TeachModeManager>("TeachModeManager");
                if (teachMgr == null) return;

                var wpPlayer = FindRequired<WaypointPlayer>("WaypointPlayer");
                if (wpPlayer == null) return;

                var wpRecorder = FindRequired<WaypointRecorder>("WaypointRecorder");
                if (wpRecorder == null) return;

                var wpStorage = FindRequired<WaypointStorage>("WaypointStorage");
                if (wpStorage == null) return;

                var robotRoot = GameObject.Find(RobotRootName);
                if (robotRoot == null)
                {
                    Debug.LogError($"[ConnectReferences] '{RobotRootName}' GameObject를 찾을 수 없습니다. SceneSetup이 먼저 실행되었는지 확인하세요.");
                    return;
                }

                // 1) RobotManager에 FairinoRobotController (Real) 추가
                var real = EnsureComponent<FairinoRobotController>(robotMgr.gameObject);

                // 2) fairino5_v6_robot에 SimulatedRobotController (Sim) 추가
                var sim = EnsureComponent<SimulatedRobotController>(robotRoot);

                // 3) PLCButtons GameObject + PLCButtonHandler
                var plcGO = GameObject.Find(PlcButtonsName);
                if (plcGO == null)
                {
                    plcGO = new GameObject(PlcButtonsName);
                    Undo.RegisterCreatedObjectUndo(plcGO, "Create PLCButtons");
                    // ROBOT SYSTEM 헤더 옆에 두기 (헤더가 있으면 그 형제로, 없으면 루트)
                    var header = GameObject.Find(RobotSystemHeader);
                    if (header != null && header.transform.parent == null)
                    {
                        // 헤더는 루트에 있음 → PLCButtons도 루트에 둠 (헤더는 단순 구분자)
                    }
                    Debug.Log($"[ConnectReferences] '{PlcButtonsName}' GameObject 생성");
                }
                var plcHandler = EnsureComponent<PLCButtonHandler>(plcGO);

                // ===== Phase B: SimulatedRobotController 슬롯 =====
                FillSimSlots(sim, robotRoot.transform);

                // ===== Phase C: 매니저 간 참조 연결 =====
                // RobotManager
                SetRef(robotMgr, "sim", sim);
                SetRef(robotMgr, "real", real);

                // WaypointRecorder
                SetRef(wpRecorder, "buttonHandler", plcHandler);
                SetRef(wpRecorder, "robot", robotMgr);

                // WaypointStorage
                SetRef(wpStorage, "recorder", wpRecorder);

                // WaypointPlayer
                SetRef(wpPlayer, "robotManager", robotMgr);
                SetRef(wpPlayer, "waypointRecorder", wpRecorder);

                // TeachModeManager
                SetRef(teachMgr, "buttonHandler", plcHandler);
                SetRef(teachMgr, "robotManager", robotMgr);
                SetRef(teachMgr, "waypointRecorder", wpRecorder);
                SetRef(teachMgr, "waypointPlayer", wpPlayer);

                // ===== Phase D: 변경사항 저장 =====
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Undo.CollapseUndoOperations(undoGroup);

                Debug.Log("[ConnectReferences] ✓ 매니저 참조 연결 완료. Ctrl+S로 씬 저장.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ConnectReferences] 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        // -------- 헬퍼 --------

        private static T FindRequired<T>(string label) where T : Component
        {
            var c = Object.FindAnyObjectByType<T>();
            if (c == null)
            {
                Debug.LogError($"[ConnectReferences] {label} 컴포넌트를 가진 GameObject를 씬에서 찾을 수 없습니다. SceneSetup을 먼저 실행하세요.");
            }
            return c;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null)
            {
                c = Undo.AddComponent<T>(go);
                Debug.Log($"[ConnectReferences] {go.name}에 {typeof(T).Name} 추가");
            }
            return c;
        }

        private static void SetRef(Object owner, string fieldName, Object value)
        {
            var so = new SerializedObject(owner);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[ConnectReferences] {owner.GetType().Name}.{fieldName} 필드를 찾을 수 없습니다.");
                return;
            }
            if (prop.propertyType != SerializedPropertyType.ObjectReference)
            {
                Debug.LogWarning($"[ConnectReferences] {owner.GetType().Name}.{fieldName}이 ObjectReference 타입이 아닙니다.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FillSimSlots(SimulatedRobotController sim, Transform robotRoot)
        {
            var so = new SerializedObject(sim);

            // joints[6] 배열 새로 채우기
            var jointsProp = so.FindProperty("joints");
            if (jointsProp == null || !jointsProp.isArray)
            {
                Debug.LogError("[ConnectReferences] SimulatedRobotController.joints 배열을 찾을 수 없습니다.");
                return;
            }
            jointsProp.arraySize = 6;

            // handover_v5 검증값 (limit/home)
            string[] names = { "J1 (Base)", "J2 (Shoulder)", "J3 (Elbow)", "J4 (Wrist1)", "J5 (Wrist2)", "J6 (Wrist3)" };
            float[] minA = { -175f, -265f, -162f, -265f, -175f, -175f };
            float[] maxA = { 175f, 85f, 162f, 85f, 175f, 175f };
            float[] homeA = { -90f, -90f, 90f, -90f, -90f, 0f };

            int found = 0;
            for (int i = 0; i < 6; i++)
            {
                var elem = jointsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("name").stringValue = names[i];
                elem.FindPropertyRelative("minAngle").floatValue = minA[i];
                elem.FindPropertyRelative("maxAngle").floatValue = maxA[i];
                elem.FindPropertyRelative("homeAngle").floatValue = homeA[i];

                var t = robotRoot.Find(JointPaths[i]);
                elem.FindPropertyRelative("jointTransform").objectReferenceValue = t;
                if (t != null) found++;
                else Debug.LogWarning($"[ConnectReferences] joints[{i}] Transform 못 찾음: {JointPaths[i]}");

                // rotationAxis와 invertSign은 디폴트 유지 (Vector3.up, false)
            }

            // tcpTransform
            var tcp = robotRoot.Find(TcpPath);
            var tcpProp = so.FindProperty("tcpTransform");
            if (tcpProp != null) tcpProp.objectReferenceValue = tcp;
            if (tcp == null) Debug.LogWarning($"[ConnectReferences] tcpTransform 못 찾음: {TcpPath}");

            // gripper는 null 유지 (3번 결정)

            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[ConnectReferences] Sim joints {found}/6, TCP {(tcp != null ? "✓" : "✗")}");
        }
    }
}
