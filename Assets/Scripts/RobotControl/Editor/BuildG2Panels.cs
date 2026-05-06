using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RobotControl;

namespace RobotControl.EditorTools
{
    /// <summary>
    /// Phase 3-C: G2 9개 패널 자동 생성.
    /// MainCanvas 우측에 G2Container, 단일 컬럼 세로 스택.
    /// JointControlPanel과 CartesianJogPanel은 같은 위치 겹침 (ControlModePanel이 SetActive 토글).
    /// </summary>
    public static class BuildG2Panels
    {
        private const string MenuPath = "Tools/Build G2 Panels";
        private const string ContainerName = "G2Container";

        private const string BgSpritePath = "Assets/CleanFlatIcon/png_128/button/button_corner_rectangle3/button_corner_rectangle3_25.png";
        private const string IconHomePath = "Assets/CleanFlatIcon/png_128/icon/icon_common/icon_common_1.png";
        private const string IconStopPath = "Assets/CleanFlatIcon/png_128/icon/icon_common/icon_common_50.png";
        private const string IconGripperPath = "Assets/CleanFlatIcon/png_128/icon/icon_tool/icon_tool_50.png";
        private const string IconCartCtrlPath = "Assets/CleanFlatIcon/png_128/icon/icon_aim/icon_aim_10.png";
        private const string IconCtrlModePath = "Assets/CleanFlatIcon/png_128/icon/icon_app/icon_app_10.png";
        private const string IconCartJogPath = "Assets/CleanFlatIcon/png_128/icon/icon_arrow/icon_arrow_10.png";
        private const string IconJointPath = "Assets/CleanFlatIcon/png_128/icon/icon_controller/icon_controller_10.png";

        private static readonly Color PanelBgColor = new Color(0.176f, 0.193f, 0.259f, 0.85f);
        private static readonly Color TextColor = Color.white;
        private static readonly Color ButtonBgColor = new Color(0.3f, 0.3f, 0.4f, 1f);
        private static readonly Color StopButtonColor = new Color(0.7f, 0.2f, 0.2f, 1f);

        private const int IconSize = 40;

        // [MenuItem(MenuPath)] // 의도적 비활성화 - 누르면 사용자 수정값 모두 리셋됨
        public static void Run()
        {
            Undo.SetCurrentGroupName("Build G2 Panels");
            int undoGroup = Undo.GetCurrentGroup();

            try
            {
                var canvas = GameObject.Find("MainCanvas");
                if (canvas == null) { Debug.LogError("[BuildG2] MainCanvas 없음."); return; }

                var robotMgr = Object.FindAnyObjectByType<RobotManager>();
                if (robotMgr == null) { Debug.LogError("[BuildG2] RobotManager 없음."); return; }

                var bgSprite = LoadSprite(BgSpritePath);
                var iconHome = LoadSprite(IconHomePath);
                var iconStop = LoadSprite(IconStopPath);
                var iconGripper = LoadSprite(IconGripperPath);
                var iconCartCtrl = LoadSprite(IconCartCtrlPath);
                var iconCtrlMode = LoadSprite(IconCtrlModePath);
                var iconCartJog = LoadSprite(IconCartJogPath);
                var iconJoint = LoadSprite(IconJointPath);

                // 기존 G2Container 제거 후 재생성
                var existing = canvas.transform.Find(ContainerName);
                if (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                    Debug.Log("[BuildG2] 기존 G2Container 제거 후 재생성");
                }

                // G2Container — 우측 상단 anchor
                var container = new GameObject(ContainerName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(container, "Create G2Container");
                container.transform.SetParent(canvas.transform, false);
                var containerRT = container.GetComponent<RectTransform>();
                containerRT.anchorMin = new Vector2(1, 1);
                containerRT.anchorMax = new Vector2(1, 1);
                containerRT.pivot = new Vector2(1, 1);
                containerRT.anchoredPosition = new Vector2(-20, -20);
                containerRT.sizeDelta = new Vector2(500, 800);
                var vlg = container.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 10;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                container.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // 빌드 순서: 의존성 따라
                BuildHomePanel(container, bgSprite, iconHome, robotMgr);
                BuildStopPanel(container, bgSprite, iconStop, robotMgr);
                BuildGripperPanel(container, bgSprite, iconGripper, robotMgr);
                BuildCartesianControlPanel(container, bgSprite, iconCartCtrl, robotMgr);

                // JointControlPanel과 CartesianJogPanel을 먼저 빌드 (ControlModePanel이 참조해야 함)
                var jointPanelGO = BuildJointControlPanel(container, bgSprite, iconJoint, robotMgr);
                var cartJogPanelGO = BuildCartesianJogPanel(container, bgSprite, iconCartJog, robotMgr);

                // ControlModePanel — Joint/CartJog 참조 주입
                BuildControlModePanel(container, bgSprite, iconCtrlMode, jointPanelGO, cartJogPanelGO);

                // 시작 시 Joint 모드 (CartJog 비활성)
                cartJogPanelGO.SetActive(false);

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Undo.CollapseUndoOperations(undoGroup);

                Debug.Log("[BuildG2] ✓ G2 9개 패널 생성 완료. Ctrl+S로 씬 저장.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BuildG2] 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        // ========== 패널 빌더 ==========

        private static void BuildHomePanel(GameObject parent, Sprite bg, Sprite icon, RobotManager robotMgr)
        {
            var panel = CreatePanelBase(parent, "HomePanel", bg, 130);
            var content = AddIconAndContentArea(panel, icon);

            var statusText = CreateTextChild(content, "HomeStatusText", "Home: not set", 16);
            var btnRow = CreateHorizontalRow(content, "ButtonRow", 44);
            var saveBtn = CreateButtonChild(btnRow, "SaveHomeButton", "Save Home");
            var goBtn = CreateButtonChild(btnRow, "GoToHomeButton", "Go To Home");

            var script = Undo.AddComponent<HomePanel>(panel);
            SetField(script, "robotManager", robotMgr);
            SetField(script, "saveHomeButton", saveBtn.GetComponent<Button>());
            SetField(script, "goToHomeButton", goBtn.GetComponent<Button>());
            SetField(script, "homeStatusText", statusText.GetComponent<TMP_Text>());
        }

        private static void BuildStopPanel(GameObject parent, Sprite bg, Sprite icon, RobotManager robotMgr)
        {
            var panel = CreatePanelBase(parent, "StopPanel", bg, 110);
            var content = AddIconAndContentArea(panel, icon);

            var statusText = CreateTextChild(content, "StatusText", "Ready", 16);
            var stopBtn = CreateButtonChild(content, "StopButton", "STOP", StopButtonColor, 50);

            var script = Undo.AddComponent<StopPanel>(panel);
            SetField(script, "robotManager", robotMgr);
            SetField(script, "stopButton", stopBtn.GetComponent<Button>());
            SetField(script, "statusText", statusText.GetComponent<TMP_Text>());
        }

        private static void BuildGripperPanel(GameObject parent, Sprite bg, Sprite icon, RobotManager robotMgr)
        {
            var panel = CreatePanelBase(parent, "GripperPanel", bg, 170);
            var content = AddIconAndContentArea(panel, icon);

            var currentText = CreateTextChild(content, "CurrentPercentText", "Gripper: 50%", 16);

            // 입력 행: InputField + Apply
            var inputRow = CreateHorizontalRow(content, "InputRow", 36);
            var inputField = CreateInputField(inputRow, "PercentInput", "50");
            var applyBtn = CreateButtonChild(inputRow, "ApplyButton", "Apply");

            // 프리셋 행: 0% / 100%
            var presetRow = CreateHorizontalRow(content, "PresetRow", 36);
            var zeroBtn = CreateButtonChild(presetRow, "ZeroPercentButton", "0%");
            var hundredBtn = CreateButtonChild(presetRow, "HundredPercentButton", "100%");

            var script = Undo.AddComponent<GripperPanel>(panel);
            SetField(script, "robotManager", robotMgr);
            SetField(script, "zeroPercentButton", zeroBtn.GetComponent<Button>());
            SetField(script, "hundredPercentButton", hundredBtn.GetComponent<Button>());
            SetField(script, "applyButton", applyBtn.GetComponent<Button>());
            SetField(script, "percentInput", inputField.GetComponent<TMP_InputField>());
            SetField(script, "currentPercentText", currentText.GetComponent<TMP_Text>());
        }

        private static void BuildCartesianControlPanel(GameObject parent, Sprite bg, Sprite icon, RobotManager robotMgr)
        {
            var panel = CreatePanelBase(parent, "CartesianControlPanel", bg, 150);
            var content = AddIconAndContentArea(panel, icon);

            // 6개 좌표 표시 (X/Y/Z 한 행, RX/RY/RZ 한 행)
            var posRow = CreateHorizontalRow(content, "PosRow", 24);
            var xText = CreateTextChild(posRow, "XText", "X: 0.0", 14);
            var yText = CreateTextChild(posRow, "YText", "Y: 0.0", 14);
            var zText = CreateTextChild(posRow, "ZText", "Z: 0.0", 14);

            var rotRow = CreateHorizontalRow(content, "RotRow", 24);
            var rxText = CreateTextChild(rotRow, "RxText", "RX: 0.0", 14);
            var ryText = CreateTextChild(rotRow, "RyText", "RY: 0.0", 14);
            var rzText = CreateTextChild(rotRow, "RzText", "RZ: 0.0", 14);

            var script = Undo.AddComponent<CartesianControlPanel>(panel);
            SetField(script, "robotManager", robotMgr);
            SetField(script, "xText", xText.GetComponent<TMP_Text>());
            SetField(script, "yText", yText.GetComponent<TMP_Text>());
            SetField(script, "zText", zText.GetComponent<TMP_Text>());
            SetField(script, "rxText", rxText.GetComponent<TMP_Text>());
            SetField(script, "ryText", ryText.GetComponent<TMP_Text>());
            SetField(script, "rzText", rzText.GetComponent<TMP_Text>());
        }

        private static void BuildControlModePanel(GameObject parent, Sprite bg, Sprite icon, GameObject jointPanelGO, GameObject cartJogPanelGO)
        {
            var panel = CreatePanelBase(parent, "ControlModePanel", bg, 100);
            var content = AddIconAndContentArea(panel, icon);

            var modeText = CreateTextChild(content, "CurrentModeText", "Mode: Joint", 16);

            var toggleGroup = content.AddComponent<ToggleGroup>();
            toggleGroup.allowSwitchOff = false;
            var jointToggle = CreateToggleChild(content, "JointToggle", "Joint", toggleGroup);
            var cartToggle = CreateToggleChild(content, "CartesianToggle", "Cartesian", toggleGroup);

            // 시작 시 Joint 켜진 상태
            jointToggle.GetComponent<Toggle>().isOn = true;

            var script = Undo.AddComponent<ControlModePanel>(panel);
            SetField(script, "jointToggle", jointToggle.GetComponent<Toggle>());
            SetField(script, "cartesianToggle", cartToggle.GetComponent<Toggle>());
            SetField(script, "jointPanel", jointPanelGO);
            SetField(script, "cartesianJogPanel", cartJogPanelGO);
            SetField(script, "currentModeText", modeText.GetComponent<TMP_Text>());
        }

        private static GameObject BuildCartesianJogPanel(GameObject parent, Sprite bg, Sprite icon, RobotManager robotMgr)
        {
            var panel = CreatePanelBase(parent, "CartesianJogPanel", bg, 200);
            var content = AddIconAndContentArea(panel, icon);

            // 12개 JogButton: X+/-, Y+/-, Z+/-, RX+/-, RY+/-, RZ+/-
            // 6열 × 2행으로 배치 (+/- 한 쌍씩)
            var jogButtons = new JogButton[12];
            string[] axisLabels = { "X", "Y", "Z", "RX", "RY", "RZ" };

            for (int row = 0; row < 2; row++)
            {
                bool isPlus = (row == 0);
                int dir = isPlus ? 1 : -1;
                string sign = isPlus ? "+" : "-";

                var rowGO = CreateHorizontalRow(content, $"JogRow_{sign}", 40);
                for (int axis = 0; axis < 6; axis++)
                {
                    var btnGO = CreateButtonChild(rowGO, $"Jog_{axisLabels[axis]}_{sign}", $"{axisLabels[axis]}{sign}");
                    var jogBtn = Undo.AddComponent<JogButton>(btnGO);
                    SetIntField(jogBtn, "axis", axis);
                    SetIntField(jogBtn, "dir", dir);
                    int idx = isPlus ? axis : axis + 6;
                    jogButtons[idx] = jogBtn;
                }
            }

            var script = Undo.AddComponent<CartesianJogPanel>(panel);
            SetField(script, "robotManager", robotMgr);
            SetArrayField(script, "jogButtons", jogButtons);

            return panel;
        }

        private static GameObject BuildJointControlPanel(GameObject parent, Sprite bg, Sprite icon, RobotManager robotMgr)
        {
            var panel = CreatePanelBase(parent, "JointControlPanel", bg, 360);
            var content = AddIconAndContentArea(panel, icon);

            // Step Slider 행
            var stepRow = CreateHorizontalRow(content, "StepRow", 28);
            var stepLabel = CreateTextChild(stepRow, "StepLabel", "Step:", 14);
            var stepSlider = CreateSimpleSlider(stepRow, "StepSlider", 1, 90, 30);
            var stepValueText = CreateTextChild(stepRow, "StepValueText", "30°", 14);

            // 6개 JointControlRow
            var rows = new JointControlRow[6];
            var jointAngleTexts = new TMP_Text[6];
            for (int i = 0; i < 6; i++)
            {
                var rowGO = CreateHorizontalRow(content, $"J{i + 1}Row", 32);
                var label = CreateTextChild(rowGO, "Label", $"J{i + 1}", 14);
                var minusBtn = CreateButtonChild(rowGO, "Minus", "-");
                var slider = CreateSimpleSlider(rowGO, "Slider", -180, 180, 0);
                var plusBtn = CreateButtonChild(rowGO, "Plus", "+");
                var angleText = CreateTextChild(rowGO, "AngleText", "0°", 14);

                var rowComp = Undo.AddComponent<JointControlRow>(rowGO);
                SetIntField(rowComp, "jointIndex", i);
                SetField(rowComp, "slider", slider.GetComponent<Slider>());
                SetField(rowComp, "minusButton", minusBtn.GetComponent<Button>());
                SetField(rowComp, "plusButton", plusBtn.GetComponent<Button>());

                rows[i] = rowComp;
                jointAngleTexts[i] = angleText.GetComponent<TMP_Text>();
            }

            var script = Undo.AddComponent<JointControlPanel>(panel);
            SetField(script, "robotManager", robotMgr);
            SetArrayField(script, "jointRows", rows);
            SetField(script, "stepSlider", stepSlider.GetComponent<Slider>());
            SetField(script, "stepValueText", stepValueText.GetComponent<TMP_Text>());
            SetArrayField(script, "jointAngleTexts", jointAngleTexts);

            return panel;
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
            img.color = PanelBgColor;
            img.raycastTarget = true;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1;
            return go;
        }

        private static GameObject AddIconAndContentArea(GameObject panel, Sprite icon)
        {
            var hlg = panel.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(24, 16, 12, 12);
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.MiddleLeft;
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

        private static GameObject CreateButtonChild(GameObject parent, string name, string label, Color? bgColor = null, int fontSize = 16)
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

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(go.transform, false);
            var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
            labelTMP.text = label;
            labelTMP.fontSize = 14;
            labelTMP.color = TextColor;
            labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
            var labelLE = labelGO.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 100;

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = checkImg;
            toggle.group = group;
            toggle.isOn = false;

            return go;
        }

        private static GameObject CreateSimpleSlider(GameObject parent, string name, float min, float max, float val)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 24;
            le.flexibleWidth = 1;

            var bg = new GameObject("Background", typeof(RectTransform));
            bg.transform.SetParent(go.transform, false);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0.4f);
            bgRT.anchorMax = new Vector2(1, 0.6f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

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

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImg;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.value = val;

            return go;
        }

        private static GameObject CreateInputField(GameObject parent, string name, string defaultText)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            // TextArea (자식)
            var textArea = new GameObject("Text Area", typeof(RectTransform));
            textArea.transform.SetParent(go.transform, false);
            var taRT = textArea.GetComponent<RectTransform>();
            taRT.anchorMin = Vector2.zero;
            taRT.anchorMax = Vector2.one;
            taRT.offsetMin = new Vector2(8, 4);
            taRT.offsetMax = new Vector2(-8, -4);
            textArea.AddComponent<RectMask2D>();

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(textArea.transform, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = defaultText;
            tmp.fontSize = 16;
            tmp.color = TextColor;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            var input = go.AddComponent<TMP_InputField>();
            input.textComponent = tmp;
            input.textViewport = taRT;
            input.text = defaultText;
            input.contentType = TMP_InputField.ContentType.IntegerNumber;

            return go;
        }

        // ========== 헬퍼: 일반 ==========

        private static Sprite LoadSprite(string path)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null) Debug.LogWarning($"[BuildG2] sprite 못 찾음: {path}");
            return sp;
        }

        private static void SetField(Object owner, string fieldName, Object value)
        {
            var so = new SerializedObject(owner);
            var prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogWarning($"[BuildG2] {owner.GetType().Name}.{fieldName} 없음"); return; }
            if (prop.propertyType != SerializedPropertyType.ObjectReference) { Debug.LogWarning($"[BuildG2] {fieldName} ObjectRef 아님"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIntField(Object owner, string fieldName, int value)
        {
            var so = new SerializedObject(owner);
            var prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogWarning($"[BuildG2] {owner.GetType().Name}.{fieldName} 없음"); return; }
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArrayField(Object owner, string fieldName, Object[] values)
        {
            var so = new SerializedObject(owner);
            var prop = so.FindProperty(fieldName);
            if (prop == null || !prop.isArray) { Debug.LogWarning($"[BuildG2] {fieldName} 배열 아님"); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
