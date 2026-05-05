using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RobotControl;

namespace RobotControl.EditorTools
{
    /// <summary>
    /// Phase 3-B: G1 4개 패널 (Status/Connection/Mode/Speed) 자동 생성.
    /// MainCanvas 자식으로 G1Container를 만들고 그 안에 4개 패널 스택.
    /// 각 패널은 sprite + 컴포넌트 + 매니저 참조까지 모두 자동 연결.
    ///
    /// 전제: ConnectReferences가 이미 실행되어 RobotManager가 씬에 존재.
    /// CleanFlatIcon 자산 임포트 완료 (Assets/CleanFlatIcon/png_128/...).
    /// </summary>
    public static class BuildG1Panels
    {
        private const string MenuPath = "Tools/Build G1 Panels";
        private const string ContainerName = "G1Container";

        // sprite 경로 (사용자 결정)
        private const string BgSpritePath = "Assets/CleanFlatIcon/png_128/button/button_corner_rectangle3/button_corner_rectangle3_25.png";
        private const string IconStatusPath = "Assets/CleanFlatIcon/png_128/icon/icon_common/icon_common_39.png";
        private const string IconConnectionPath = "Assets/CleanFlatIcon/png_128/icon/icon_device/icon_device_46.png";
        private const string IconModePath = "Assets/CleanFlatIcon/png_128/icon/icon_tool/icon_tool_4.png";
        private const string IconSpeedPath = "Assets/CleanFlatIcon/png_128/icon/icon_app/icon_app_200.png";

        // 색상
        private static readonly Color PanelBgColor = new Color(0.176f, 0.193f, 0.259f, 0.85f); // #2D3142 alpha 0.85
        private static readonly Color TextColor = Color.white;
        private static readonly Color ButtonBgColor = new Color(0.3f, 0.3f, 0.4f, 1f);

        // 폰트
        private const int TextSize = 16;
        private const int IconSize = 40;

        [MenuItem(MenuPath)]
        public static void Run()
        {
            Undo.SetCurrentGroupName("Build G1 Panels");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                // 전제 검증
                var canvas = GameObject.Find("MainCanvas");
                if (canvas == null) { Debug.LogError("[BuildG1] MainCanvas 없음. SceneSetup 먼저 실행."); return; }

                var robotMgr = Object.FindAnyObjectByType<RobotManager>();
                if (robotMgr == null) { Debug.LogError("[BuildG1] RobotManager 없음. ConnectReferences 먼저 실행."); return; }

                // sprite 로드
                var bgSprite = LoadSprite(BgSpritePath);
                var iconStatus = LoadSprite(IconStatusPath);
                var iconConnection = LoadSprite(IconConnectionPath);
                var iconMode = LoadSprite(IconModePath);
                var iconSpeed = LoadSprite(IconSpeedPath);

                // 기존 G1Container 있으면 삭제 후 재생성
                var existing = canvas.transform.Find(ContainerName);
                if (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                    Debug.Log("[BuildG1] 기존 G1Container 제거 후 재생성");
                }

                // G1Container
                var container = new GameObject(ContainerName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(container, "Create G1Container");
                container.transform.SetParent(canvas.transform, false);
                var containerRT = container.GetComponent<RectTransform>();
                containerRT.anchorMin = new Vector2(0, 1);
                containerRT.anchorMax = new Vector2(0, 1);
                containerRT.pivot = new Vector2(0, 1);
                containerRT.anchoredPosition = new Vector2(20, -20);
                containerRT.sizeDelta = new Vector2(400, 600);
                var vlg = container.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 10;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                container.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // 4개 패널 생성
                BuildStatusPanel(container, bgSprite, iconStatus, robotMgr);
                BuildConnectionPanel(container, bgSprite, iconConnection, robotMgr);
                BuildModePanel(container, bgSprite, iconMode, robotMgr);
                BuildSpeedPanel(container, bgSprite, iconSpeed, robotMgr);

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Undo.CollapseUndoOperations(undoGroup);

                Debug.Log("[BuildG1] ✓ G1 4개 패널 생성 완료. Ctrl+S로 씬 저장.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BuildG1] 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        // ========== 패널 빌더 ==========

        private static void BuildStatusPanel(GameObject parent, Sprite bg, Sprite icon, RobotManager robotMgr)
        {
            var panel = CreatePanelBase(parent, "StatusPanel", bg, 150);
            var contentArea = AddIconAndContentArea(panel, icon);

            var connText = CreateTextChild(contentArea, "ConnectionText", "Connected", 18);
            var statusText = CreateTextChild(contentArea, "StatusText", "Idle", 18);

            var script = Undo.AddComponent<StatusPanel>(panel);
            SetField(script, "robotManager", robotMgr);
            SetField(script, "connectionText", connText.GetComponent<TMP_Text>());
            SetField(script, "statusText", statusText.GetComponent<TMP_Text>());
        }

        private static void BuildConnectionPanel(GameObject parent, Sprite bg, Sprite icon, RobotManager robotMgr)
        {
            var panel = CreatePanelBase(parent, "ConnectionPanel", bg, 150);
            var contentArea = AddIconAndContentArea(panel, icon);

            var ipText = CreateTextChild(contentArea, "IPDisplayText", "IP: 192.168.58.2", 16);
            var btnRow = CreateHorizontalRow(contentArea, "ButtonRow", 44);
            var connectBtn = CreateButtonChild(btnRow, "ConnectButton", "Connect");
            var disconnectBtn = CreateButtonChild(btnRow, "DisconnectButton", "Disconnect");

            var script = Undo.AddComponent<ConnectionPanel>(panel);
            SetField(script, "robotManager", robotMgr);
            SetField(script, "ipDisplayText", ipText.GetComponent<TMP_Text>());
            SetField(script, "connectButton", connectBtn.GetComponent<Button>());
            SetField(script, "disconnectButton", disconnectBtn.GetComponent<Button>());
        }

        private static void BuildModePanel(GameObject parent, Sprite bg, Sprite icon, RobotManager robotMgr)
        {
            var panel = CreatePanelBase(parent, "ModePanel", bg, 130);
            var contentArea = AddIconAndContentArea(panel, icon);

            // ToggleGroup은 부모(contentArea)에 부착
            var toggleGroup = contentArea.AddComponent<ToggleGroup>();
            toggleGroup.allowSwitchOff = false;

            var simToggle = CreateToggleChild(contentArea, "SimToggle", "Sim Only", toggleGroup);
            var mirrorToggle = CreateToggleChild(contentArea, "MirrorToggle", "Mirror", toggleGroup);
            var modeText = CreateTextChild(contentArea, "CurrentModeText", "Mode: SimOnly", 14);

            var script = Undo.AddComponent<ModePanel>(panel);
            SetField(script, "robotManager", robotMgr);
            SetField(script, "simToggle", simToggle.GetComponent<Toggle>());
            SetField(script, "mirrorToggle", mirrorToggle.GetComponent<Toggle>());
            SetField(script, "currentModeText", modeText.GetComponent<TMP_Text>());
        }

        private static void BuildSpeedPanel(GameObject parent, Sprite bg, Sprite icon, RobotManager robotMgr)
        {
            var panel = CreatePanelBase(parent, "SpeedPanel", bg, 180);
            var contentArea = AddIconAndContentArea(panel, icon);

            var speedText = CreateTextChild(contentArea, "CurrentSpeedText", "Speed: 50%", 16);
            var slider = CreateSliderChild(contentArea, "SpeedSlider");

            var btnRow = CreateHorizontalRow(contentArea, "ButtonRow", 30);
            var minus5 = CreateButtonChild(btnRow, "Minus5Button", "-5");
            var minus1 = CreateButtonChild(btnRow, "Minus1Button", "-1");
            var plus1 = CreateButtonChild(btnRow, "Plus1Button", "+1");
            var plus5 = CreateButtonChild(btnRow, "Plus5Button", "+5");

            var script = Undo.AddComponent<SpeedPanel>(panel);
            SetField(script, "robotManager", robotMgr);
            SetField(script, "speedSlider", slider.GetComponent<Slider>());
            SetField(script, "currentSpeedText", speedText.GetComponent<TMP_Text>());
            SetField(script, "minus5Button", minus5.GetComponent<Button>());
            SetField(script, "minus1Button", minus1.GetComponent<Button>());
            SetField(script, "plus1Button", plus1.GetComponent<Button>());
            SetField(script, "plus5Button", plus5.GetComponent<Button>());
        }

        // ========== 헬퍼: GameObject 빌더 ==========

        private static GameObject CreatePanelBase(GameObject parent, string name, Sprite bg, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, height);

            var img = go.AddComponent<Image>();
            img.sprite = bg;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.5f;
            img.color = PanelBgColor;
            img.raycastTarget = true;

            // LayoutElement로 높이 고정
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1;

            return go;
        }

        private static GameObject AddIconAndContentArea(GameObject panel, Sprite icon)
        {
            // 패널 안에 HorizontalLayoutGroup으로 좌측 아이콘 + 우측 콘텐츠 분할
            var hlg = panel.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(24, 16, 12, 12);
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // 아이콘
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

            // 콘텐츠 영역
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
            hlg.spacing = 4;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1;
            return go;
        }

        private static GameObject CreateButtonChild(GameObject parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.color = ButtonBgColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            // 텍스트 자식
            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 18;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.Center;

            return go;
        }

        private static GameObject CreateToggleChild(GameObject parent, string name, string label, ToggleGroup group)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 24;
            le.flexibleWidth = 1;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // 체크박스 (Image - Background + checkmark Image 자식)
            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            var bgLE = bg.AddComponent<LayoutElement>();
            bgLE.preferredWidth = 20;
            bgLE.preferredHeight = 20;

            var checkmark = new GameObject("Checkmark", typeof(RectTransform));
            checkmark.transform.SetParent(bg.transform, false);
            var checkRT = checkmark.GetComponent<RectTransform>();
            checkRT.anchorMin = Vector2.zero;
            checkRT.anchorMax = Vector2.one;
            checkRT.offsetMin = new Vector2(2, 2);
            checkRT.offsetMax = new Vector2(-2, -2);
            var checkImg = checkmark.AddComponent<Image>();
            checkImg.color = new Color(0.4f, 0.7f, 1f, 1f);

            // 라벨
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(go.transform, false);
            var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
            labelTMP.text = label;
            labelTMP.fontSize = 14;
            labelTMP.color = TextColor;
            labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
            var labelLE = labelGO.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 100;

            // Toggle 컴포넌트
            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.group = group;
            toggle.isOn = false;

            return go;
        }

        private static GameObject CreateSliderChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 24;
            le.flexibleWidth = 1;

            // Background
            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(go.transform, false);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0.4f);
            bgRT.anchorMax = new Vector2(1, 0.6f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

            // Fill Area
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRT = fillArea.GetComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0, 0.4f);
            fillAreaRT.anchorMax = new Vector2(1, 0.6f);
            fillAreaRT.offsetMin = new Vector2(2, 0);
            fillAreaRT.offsetMax = new Vector2(-2, 0);

            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.4f, 0.7f, 1f, 1f);

            // Handle Slide Area
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var handleAreaRT = handleArea.GetComponent<RectTransform>();
            handleAreaRT.anchorMin = new Vector2(0, 0);
            handleAreaRT.anchorMax = new Vector2(1, 1);
            handleAreaRT.offsetMin = new Vector2(5, 0);
            handleAreaRT.offsetMax = new Vector2(-5, 0);

            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(handleArea.transform, false);
            var handleRT = handle.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(20, 0);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;

            // Slider 컴포넌트
            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImg;
            slider.minValue = 1;
            slider.maxValue = 100;
            slider.wholeNumbers = true;
            slider.value = 50;

            return go;
        }

        // ========== 헬퍼: 일반 ==========

        private static Sprite LoadSprite(string path)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null) Debug.LogWarning($"[BuildG1] sprite 못 찾음: {path}");
            return sp;
        }

        private static void SetField(Object owner, string fieldName, Object value)
        {
            var so = new SerializedObject(owner);
            var prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogWarning($"[BuildG1] {owner.GetType().Name}.{fieldName} 없음"); return; }
            if (prop.propertyType != SerializedPropertyType.ObjectReference) { Debug.LogWarning($"[BuildG1] {fieldName} ObjectRef 아님"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
