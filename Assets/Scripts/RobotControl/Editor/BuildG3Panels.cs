using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using RobotControl;

namespace RobotControl.EditorTools
{
    /// <summary>
    /// Phase 3-D: G3 2개 컨테이너 패널 (Waypoint/Teach) + WaypointItem prefab 자동 생성.
    /// 화면 하단 좌우 중앙에 G3Container, 가로 2칸 (WaypointPanel + TeachPanel).
    /// WaypointItem은 Assets/Prefabs/WaypointItem.prefab으로 저장됨.
    /// </summary>
    public static class BuildG3Panels
    {
        private const string MenuPath = "Tools/Build G3 Panels";
        private const string ContainerName = "G3Container";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string WaypointItemPrefabPath = "Assets/Prefabs/WaypointItem.prefab";

        private const string BgSpritePath = "Assets/CleanFlatIcon/png_128/button/button_corner_rectangle3/button_corner_rectangle3_25.png";
        private const string CircleSpritePath = "Assets/CleanFlatIcon/png_128/button/button_circle/button_circle_1.png";
        private const string IconWaypointPath = "Assets/CleanFlatIcon/png_128/icon/icon_arrow/icon_arrow_20.png";
        private const string IconTeachPath = "Assets/CleanFlatIcon/png_128/icon/icon_tool/icon_tool_20.png";

        private static readonly Color PanelBgColor = new Color(0.176f, 0.193f, 0.259f, 0.85f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color ButtonBgColor = new Color(0.3f, 0.3f, 0.4f, 1f);
        private static readonly Color StopButtonColor = new Color(0.7f, 0.2f, 0.2f, 1f);
        private static readonly Color RecOnColor = new Color(1f, 0.2f, 0.2f, 1f);
        private static readonly Color RecOffColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

        private const int IconSize = 40;

        [MenuItem(MenuPath)]
        public static void Run()
        {
            Undo.SetCurrentGroupName("Build G3 Panels");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                var canvas = GameObject.Find("MainCanvas");
                if (canvas == null) { Debug.LogError("[BuildG3] MainCanvas 없음."); return; }

                // 매니저 Find
                var teachMgr = Object.FindAnyObjectByType<TeachModeManager>();
                var wpPlayer = Object.FindAnyObjectByType<WaypointPlayer>();
                var wpRecorder = Object.FindAnyObjectByType<WaypointRecorder>();
                if (teachMgr == null) { Debug.LogError("[BuildG3] TeachModeManager 없음."); return; }
                if (wpPlayer == null) { Debug.LogError("[BuildG3] WaypointPlayer 없음."); return; }
                if (wpRecorder == null) { Debug.LogError("[BuildG3] WaypointRecorder 없음."); return; }

                var bgSprite = LoadSprite(BgSpritePath);
                var circleSprite = LoadSprite(CircleSpritePath);
                var iconWp = LoadSprite(IconWaypointPath);
                var iconTeach = LoadSprite(IconTeachPath);

                // 1단계: WaypointItem Prefab 생성/업데이트
                GameObject wpItemPrefab = CreateWaypointItemPrefab();
                if (wpItemPrefab == null) { Debug.LogError("[BuildG3] WaypointItem prefab 생성 실패."); return; }

                // 2단계: 기존 G3Container 제거 후 재생성
                var existing = canvas.transform.Find(ContainerName);
                if (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                    Debug.Log("[BuildG3] 기존 G3Container 제거 후 재생성");
                }

                // 3단계: G3Container — 화면 하단 좌우 중앙
                var container = new GameObject(ContainerName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(container, "Create G3Container");
                container.transform.SetParent(canvas.transform, false);
                var containerRT = container.GetComponent<RectTransform>();
                containerRT.anchorMin = new Vector2(0.5f, 0);
                containerRT.anchorMax = new Vector2(0.5f, 0);
                containerRT.pivot = new Vector2(0.5f, 0);
                containerRT.anchoredPosition = new Vector2(0, 20);
                containerRT.sizeDelta = new Vector2(900, 320);
                var hlg = container.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 10;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;
                hlg.childAlignment = TextAnchor.MiddleCenter;

                // 4단계: 두 패널 빌드
                BuildWaypointPanel(container, bgSprite, iconWp, circleSprite, wpItemPrefab, wpPlayer, wpRecorder);
                BuildTeachPanel(container, bgSprite, iconTeach, circleSprite, teachMgr);

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Undo.CollapseUndoOperations(undoGroup);

                Debug.Log("[BuildG3] ✓ G3 2개 패널 + WaypointItem prefab 생성 완료. Ctrl+S로 씬 저장.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BuildG3] 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        // ========== WaypointItem Prefab ==========

        private static GameObject CreateWaypointItemPrefab()
        {
            // 폴더 보장
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            // 임시 GameObject 빌드
            var temp = new GameObject("WaypointItem", typeof(RectTransform));
            var rt = temp.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 36);

            var img = temp.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.1f);
            img.raycastTarget = true;

            var le = temp.AddComponent<LayoutElement>();
            le.preferredHeight = 36;
            le.flexibleWidth = 1;

            var btn = temp.AddComponent<Button>();
            btn.targetGraphic = img;

            // Label 자식
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(temp.transform, false);
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(12, 0);
            labelRT.offsetMax = new Vector2(-12, 0);
            var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
            labelTMP.text = "Waypoint";
            labelTMP.fontSize = 14;
            labelTMP.color = TextColor;
            labelTMP.alignment = TextAlignmentOptions.MidlineLeft;

            // WaypointItem 컴포넌트
            var item = temp.AddComponent<WaypointItem>();
            SetField(item, "labelText", labelTMP);
            SetField(item, "backgroundImage", img);
            SetField(item, "clickButton", btn);

            // Prefab으로 저장 (이미 있으면 덮어쓰기)
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, WaypointItemPrefabPath);
            Object.DestroyImmediate(temp);

            if (prefab == null) Debug.LogWarning($"[BuildG3] prefab 저장 실패: {WaypointItemPrefabPath}");
            return prefab;
        }

        // ========== WaypointPanel ==========

        private static void BuildWaypointPanel(GameObject parent, Sprite bg, Sprite icon, Sprite circleSprite,
            GameObject wpItemPrefab, WaypointPlayer player, WaypointRecorder recorder)
        {
            var panel = CreatePanelBase(parent, "WaypointPanel", bg);
            var content = AddIconAndContentArea(panel, icon);

            // 재생 제어 행 (2버튼: PlayPause, Stop)
            var playRow = CreateHorizontalRow(content, "PlayRow", 40);
            var playPauseBtn = CreateButtonChild(playRow, "PlayPauseButton", "Play");
            var stopBtn = CreateButtonChild(playRow, "StopButton", "Stop", StopButtonColor);

            // 진행 표시
            var progressText = CreateTextChild(content, "ProgressText", "0 / 0", 14);

            // 리스트 관리 행 (3버튼: Save, Clear, RemoveLast)
            var manageRow = CreateHorizontalRow(content, "ManageRow", 36);
            var saveBtn = CreateButtonChild(manageRow, "SaveButton", "Save");
            var clearBtn = CreateButtonChild(manageRow, "ClearButton", "Clear");
            var removeLastBtn = CreateButtonChild(manageRow, "RemoveLastButton", "Remove Last");

            // ScrollView
            var scrollContent = CreateScrollView(content, "WaypointScroll");

            // WaypointPanel 컴포넌트
            var script = Undo.AddComponent<WaypointPanel>(panel);
            SetField(script, "waypointPlayer", player);
            SetField(script, "waypointRecorder", recorder);
            SetField(script, "playPauseButton", playPauseBtn.GetComponent<Button>());
            SetField(script, "stopButton", stopBtn.GetComponent<Button>());
            SetField(script, "saveButton", saveBtn.GetComponent<Button>());
            SetField(script, "clearButton", clearBtn.GetComponent<Button>());
            SetField(script, "removeLastButton", removeLastBtn.GetComponent<Button>());

            // PlayPause 버튼의 자식 Text를 playPauseLabel로
            var playPauseText = playPauseBtn.transform.Find("Text");
            if (playPauseText != null)
            {
                SetField(script, "playPauseLabel", playPauseText.GetComponent<TMP_Text>());
            }

            SetField(script, "progressText", progressText.GetComponent<TMP_Text>());
            SetField(script, "listContent", scrollContent.transform);
            SetField(script, "waypointItemPrefab", wpItemPrefab);
        }

        // ========== TeachPanel ==========

        private static void BuildTeachPanel(GameObject parent, Sprite bg, Sprite icon, Sprite circleSprite, TeachModeManager teachMgr)
        {
            var panel = CreatePanelBase(parent, "TeachPanel", bg);
            var content = AddIconAndContentArea(panel, icon);

            // REC 표시 행
            var recRow = CreateHorizontalRow(content, "RecRow", 32);
            var recIndicatorGO = new GameObject("RecIndicator", typeof(RectTransform));
            recIndicatorGO.transform.SetParent(recRow.transform, false);
            var recImg = recIndicatorGO.AddComponent<Image>();
            recImg.sprite = circleSprite;
            recImg.color = RecOffColor;
            var recLE = recIndicatorGO.AddComponent<LayoutElement>();
            recLE.preferredWidth = 24;
            recLE.preferredHeight = 24;
            recLE.flexibleWidth = 0;
            var recLabel = CreateTextChild(recRow, "RecLabel", "Ready", 16);

            // PLC 6 버튼 — 2x3 그리드
            var grid = CreateButtonGrid(content, "ButtonGrid", 3, 2);
            var goHomeBtn = CreateButtonChild(grid, "GoHomeButton", "Go Home");
            var recordStartBtn = CreateButtonChild(grid, "RecordStartButton", "Rec Start", StopButtonColor);
            var recordStopBtn = CreateButtonChild(grid, "RecordStopButton", "Rec Stop");
            var saveWpBtn = CreateButtonChild(grid, "SaveWaypointButton", "Save WP");
            var playBtn = CreateButtonChild(grid, "PlayButton", "Play");
            var stopBtn = CreateButtonChild(grid, "StopButton", "Stop", StopButtonColor);

            // TeachPanel 컴포넌트
            var script = Undo.AddComponent<TeachPanel>(panel);
            SetField(script, "teachManager", teachMgr);
            SetField(script, "goHomeButton", goHomeBtn.GetComponent<Button>());
            SetField(script, "recordStartButton", recordStartBtn.GetComponent<Button>());
            SetField(script, "recordStopButton", recordStopBtn.GetComponent<Button>());
            SetField(script, "saveWaypointButton", saveWpBtn.GetComponent<Button>());
            SetField(script, "playButton", playBtn.GetComponent<Button>());
            SetField(script, "stopButton", stopBtn.GetComponent<Button>());
            SetField(script, "recLabel", recLabel.GetComponent<TMP_Text>());
            SetField(script, "recIndicator", recImg);
        }

        // ========== ScrollView 빌더 ==========

        private static GameObject CreateScrollView(GameObject parent, string name)
        {
            // ScrollView 루트
            var scrollGO = new GameObject(name, typeof(RectTransform));
            scrollGO.transform.SetParent(parent.transform, false);
            var scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0, 0);
            scrollRT.anchorMax = new Vector2(1, 1);

            var scrollImg = scrollGO.AddComponent<Image>();
            scrollImg.color = new Color(0.1f, 0.1f, 0.15f, 0.5f);

            var scrollLE = scrollGO.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1;
            scrollLE.flexibleWidth = 1;
            scrollLE.preferredHeight = 100;

            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Viewport (Mask 영역)
            var viewportGO = new GameObject("Viewport", typeof(RectTransform));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRT = viewportGO.GetComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            viewportRT.pivot = new Vector2(0, 1);
            var viewportImg = viewportGO.AddComponent<Image>();
            viewportImg.color = new Color(1, 1, 1, 0.01f);
            viewportGO.AddComponent<RectMask2D>();

            // Content (실제 자식 보관)
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRT = contentGO.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0, 0);

            var contentVLG = contentGO.AddComponent<VerticalLayoutGroup>();
            contentVLG.spacing = 4;
            contentVLG.padding = new RectOffset(4, 4, 4, 4);
            contentVLG.childControlWidth = true;
            contentVLG.childControlHeight = false;
            contentVLG.childForceExpandWidth = true;
            contentVLG.childForceExpandHeight = false;

            var contentFitter = contentGO.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ScrollRect 연결
            scrollRect.viewport = viewportRT;
            scrollRect.content = contentRT;

            return contentGO;
        }

        // ========== 헬퍼 ==========

        private static GameObject CreatePanelBase(GameObject parent, string name, Sprite bg)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.sprite = bg;
            img.type = Image.Type.Sliced;
            img.color = PanelBgColor;
            img.raycastTarget = true;
            return go;
        }

        private static GameObject AddIconAndContentArea(GameObject panel, Sprite icon)
        {
            var hlg = panel.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(20, 16, 12, 12);
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.UpperLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(panel.transform, false);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.preferredWidth = IconSize;
            iconLE.preferredHeight = IconSize;
            iconLE.flexibleWidth = 0;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(panel.transform, false);
            var contentVLG = content.AddComponent<VerticalLayoutGroup>();
            contentVLG.spacing = 8;
            contentVLG.childAlignment = TextAnchor.UpperLeft;
            contentVLG.childControlWidth = true;
            contentVLG.childControlHeight = true;
            contentVLG.childForceExpandWidth = true;
            contentVLG.childForceExpandHeight = false;
            var contentLE = content.AddComponent<LayoutElement>();
            contentLE.flexibleWidth = 1;
            contentLE.flexibleHeight = 1;

            return content;
        }

        private static GameObject CreateTextChild(GameObject parent, string name, string text, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.Left;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = fontSize + 8;
            le.flexibleWidth = 1;
            return go;
        }

        private static GameObject CreateHorizontalRow(GameObject parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1;
            return go;
        }

        private static GameObject CreateButtonGrid(GameObject parent, string name, int cols, int rows)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(120, 36);
            grid.spacing = new Vector2(6, 6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = (36 + 6) * rows + 6;
            le.flexibleWidth = 1;
            return go;
        }

        private static GameObject CreateButtonChild(GameObject parent, string name, string label, Color? bgColor = null, int fontSize = 14)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.color = bgColor ?? ButtonBgColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.Center;
            return go;
        }

        private static Sprite LoadSprite(string path)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null) Debug.LogWarning($"[BuildG3] sprite 못 찾음: {path}");
            return sp;
        }

        private static void SetField(Object owner, string fieldName, Object value)
        {
            var so = new SerializedObject(owner);
            var prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogWarning($"[BuildG3] {owner.GetType().Name}.{fieldName} 없음"); return; }
            if (prop.propertyType != SerializedPropertyType.ObjectReference) { Debug.LogWarning($"[BuildG3] {fieldName} ObjectRef 아님"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
