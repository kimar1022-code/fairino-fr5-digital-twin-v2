using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RobotControl.EditorTools
{
    public static class SceneSetup
    {
        [MenuItem("Tools/Setup FR5 Scene")]
        public static void SetupScene()
        {
            Debug.Log("[SceneSetup] 시작");

            // 단계 1: 그룹 3개
            GameObject robotSystemGroup = FindOrCreateGroup("--- ROBOT SYSTEM ---");
            GameObject uiCanvasGroup = FindOrCreateGroup("--- UI CANVAS ---");
            GameObject robotModelGroup = FindOrCreateGroup("--- ROBOT MODEL ---");
            Debug.Log("[SceneSetup] 단계 1 완료: 그룹 3개 (ROBOT SYSTEM / UI CANVAS / ROBOT MODEL)");

            // 단계 2: fairino5_v6_robot 이동
            GameObject robotModel = GameObject.Find("fairino5_v6_robot");
            if (robotModel != null)
            {
                robotModel.transform.SetParent(robotModelGroup.transform, true);
                Debug.Log("[SceneSetup] 단계 2 완료: fairino5_v6_robot → ROBOT MODEL");
            }
            else
            {
                Debug.LogWarning("[SceneSetup] 단계 2: fairino5_v6_robot 못 찾음 (씬에 로봇 모델 없을 수 있음). 진행 계속.");
            }

            // 단계 3: 매니저 5개 GameObject + Component
            GameObject robotManagerGO = FindOrCreateChild(robotSystemGroup, "RobotManager");
            EnsureComponent<RobotControl.RobotManager>(robotManagerGO);

            GameObject teachManagerGO = FindOrCreateChild(robotSystemGroup, "TeachModeManager");
            EnsureComponent<RobotControl.TeachModeManager>(teachManagerGO);

            GameObject playerGO = FindOrCreateChild(robotSystemGroup, "WaypointPlayer");
            EnsureComponent<RobotControl.WaypointPlayer>(playerGO);

            GameObject recorderGO = FindOrCreateChild(robotSystemGroup, "WaypointRecorder");
            EnsureComponent<RobotControl.WaypointRecorder>(recorderGO);

            GameObject storageGO = FindOrCreateChild(robotSystemGroup, "WaypointStorage");
            EnsureComponent<RobotControl.WaypointStorage>(storageGO);

            Debug.Log("[SceneSetup] 단계 3 완료: 매니저 5개 (RobotManager / TeachModeManager / WaypointPlayer / WaypointRecorder / WaypointStorage)");

            // 단계 4: MainCanvas
            GameObject canvasGO = FindOrCreateChild(uiCanvasGroup, "MainCanvas");
            Canvas canvas = EnsureComponent<Canvas>(canvasGO);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            EnsureComponent<CanvasScaler>(canvasGO);
            EnsureComponent<GraphicRaycaster>(canvasGO);
            Debug.Log("[SceneSetup] 단계 4 완료: MainCanvas (Canvas + CanvasScaler + GraphicRaycaster)");

            // 단계 5: EventSystem
            EventSystem existingES = Object.FindObjectOfType<EventSystem>();
            if (existingES == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
                EnsureComponent<EventSystem>(esGO);
                EnsureComponent<StandaloneInputModule>(esGO);
                Debug.Log("[SceneSetup] 단계 5 완료: EventSystem 생성");
            }
            else
            {
                Debug.Log("[SceneSetup] 단계 5 완료: EventSystem 기존 사용");
            }

            // 단계 7: 최종 보고
            Debug.Log("[SceneSetup] 완료 - 그룹 3개, 매니저 5개, Canvas + EventSystem 생성/확인");
        }

        // ───── 헬퍼 메서드 ─────────────────────────────────────────

        private static GameObject FindOrCreateGroup(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null) return existing;
            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Group: " + name);
            return go;
        }

        private static GameObject FindOrCreateChild(GameObject parent, string name)
        {
            if (parent == null) return null;
            Transform existing = parent.transform.Find(name);
            if (existing != null) return existing.gameObject;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, "Create Child: " + name);
            return go;
        }

        private static T EnsureComponent<T>(GameObject go) where T : Component
        {
            if (go == null) return null;
            T existing = go.GetComponent<T>();
            if (existing != null) return existing;
            return go.AddComponent<T>();
        }
    }
}
