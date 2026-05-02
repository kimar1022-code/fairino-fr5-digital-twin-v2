using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RobotControl
{
    /// <summary>
    /// 확장판 UI. 추가 기능:
    ///   1) 조인트 / 데카르트(X/Y/Z + Rx/Ry/Rz) 컨트롤 탭 전환 — "누르고 있으면 JOG"
    ///   2) 홈 포즈 저장/이동 패널
    ///   3) 수동/자동 모드 + 글로벌 스피드
    ///   4) 현재 TCP 포즈 실시간 표시
    /// </summary>
    public class RobotControlUI : MonoBehaviour
    {
        [Header("컨트롤러")]
        public RobotManager robotManager;

        [Header("UI 스타일")]
        public Vector2 panelSize = new Vector2(480f, 1600f);
        public Color panelColor = new Color(0.08f, 0.08f, 0.12f, 0.92f);
        public Color textColor = Color.white;
        public Color accentColor = new Color(0.3f, 0.7f, 1f);
        public Color dangerColor = new Color(0.85f, 0.25f, 0.25f);
        public Color readyColor = new Color(0.25f, 0.8f, 0.35f);
        public Color warnColor = new Color(0.95f, 0.65f, 0.2f);
        public Color inactiveColor = new Color(0.25f, 0.25f, 0.3f);

        enum ControlTab { Joint, Cartesian }
        ControlTab currentTab = ControlTab.Joint;

        // 참조
        InputField ipInput;
        Text statusText, tcpPoseText, speedValueText, modeText;
        Button connectBtn, disconnectBtn, homeBtn, stopBtn;
        Button modeAutoBtn, modeManualBtn;
        Button jointTabBtn, cartTabBtn;
        // 홈 포즈
        Button setHomeBtn, goHomeBtn, applyHomeBtn;
        InputField[] homeInputs;
        Button[] modeButtons;
        Slider speedSlider;

        // 홈 포즈 슬롯 3개 (런타임 저장, Unity 종료 시 사라짐)
        float[][] homeSlots = new float[3][];      // 각 슬롯이 6축 각도
        bool[] homeSlotsFilled = new bool[3];       // 저장된 슬롯만 true
        Text[] homeSlotStatusTexts;                 // 슬롯 상태 텍스트 (저장됨 / 빈)

        // 조인트 UI
        GameObject jointPanel;
        List<Slider> jointSliders = new List<Slider>();
        List<Text> jointValueTexts = new List<Text>();
        List<InputField> jointInputs = new List<InputField>();  // 각도 직접 입력 필드
        bool draggingJoint = false; int draggingIndex = -1;
        bool editingJoint = false; int editingIndex = -1;   // 입력 중이면 UI 동기화 안 함

        // 데카르트 UI (6쌍 JOG 버튼)
        GameObject cartPanel;

        // 그리퍼
        Slider gripperSlider;
        Text gripperValueText;
        bool draggingGripper = false;

        void Start()
        {
            if (robotManager == null) { Debug.LogError("[UI] RobotManager 미지정"); enabled = false; return; }
            BuildUI();
        }

        void Update()
        {
            if (robotManager == null) return;

            if (statusText != null)
            {
                statusText.text = robotManager.StatusMessage;
                statusText.color = robotManager.IsConnected ? readyColor : textColor;
            }

            // TCP 실시간 표시
            if (tcpPoseText != null)
            {
                var p = robotManager.GetCurrentTCPPose();
                tcpPoseText.text =
                    $"X: {p.x,8:F1}   Y: {p.y,8:F1}   Z: {p.z,8:F1}\n" +
                    $"Rx:{p.rx,7:F1}  Ry:{p.ry,7:F1}  Rz:{p.rz,7:F1}";
            }

            // 조인트 값 표시/동기화
            for (int i = 0; i < jointSliders.Count; i++)
            {
                float actual = robotManager.GetJointAngle(i);
                // 입력 필드 동기화 (편집 중이 아닐 때만)
                if (i < jointInputs.Count && jointInputs[i] != null)
                {
                    if (!(editingJoint && editingIndex == i))
                    {
                        string newText = actual.ToString("F1");
                        if (jointInputs[i].text != newText)
                            jointInputs[i].text = newText;
                    }
                }
                if (!(draggingJoint && draggingIndex == i))
                    if (Mathf.Abs(jointSliders[i].value - actual) > 0.5f)
                        jointSliders[i].SetValueWithoutNotify(actual);
            }

            // 그리퍼 동기화
            if (gripperSlider != null && !draggingGripper)
            {
                float g = robotManager.GetGripperOpenPercent();
                if (gripperValueText != null) gripperValueText.text = $"{g:F0}%";
                if (Mathf.Abs(gripperSlider.value - g) > 0.5f)
                    gripperSlider.SetValueWithoutNotify(g);
            }

            // 스피드/모드 표시
            if (speedValueText != null) speedValueText.text = $"{robotManager.GetGlobalSpeed()}%";
            if (modeText != null) modeText.text = robotManager.GetMode().ToString().ToUpper();
            UpdateModeButtons();
        }

        // ═════════════════════════════════════════════════════════════
        // UI 구성
        // ═════════════════════════════════════════════════════════════
        void BuildUI()
        {
            var canvasGO = new GameObject("RobotControlCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
#if ENABLE_LEGACY_INPUT_MANAGER
                es.AddComponent<StandaloneInputModule>();
#elif ENABLE_INPUT_SYSTEM
                var t = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (t != null) es.AddComponent(t);
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            var panel = Mk("ControlPanel", canvasGO.transform);
            var pr = panel.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0, 1); pr.anchorMax = new Vector2(0, 1); pr.pivot = new Vector2(0, 1);
            pr.anchoredPosition = new Vector2(20, -20);
            pr.sizeDelta = panelSize;
            panel.AddComponent<Image>().color = panelColor;

            Txt("Title", panel.transform, "🤖 FAIRINO ROBOT CONTROL",
                new Vector2(0, -12), new Vector2(panelSize.x, 28),
                18, FontStyle.Bold, TextAnchor.MiddleCenter, accentColor,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            float y = -48f;
            y = BuildConnectionSection(panel.transform, y);
            y = BuildModeSpeedSection(panel.transform, y);
            y = BuildTCPPoseSection(panel.transform, y);
            y = BuildControlTabsSection(panel.transform, y);
            y = BuildJointPanelContent(panel.transform, y);
            y = BuildCartesianPanelContent(panel.transform, y);
            y = BuildGripperSection(panel.transform, y);
            y = BuildHomePoseSection(panel.transform, y);
            BuildBottomButtons(panel.transform, y);

            ShowTab(ControlTab.Joint);
        }

        // ── 섹션 1: Connection ──────────────────────────────────────
        float BuildConnectionSection(Transform parent, float y)
        {
            Txt("_c", parent, "■ CONNECTION", new Vector2(15, y), new Vector2(panelSize.x - 30, 20),
                16, FontStyle.Bold, TextAnchor.MiddleLeft, accentColor, A01(), A01(), A01());
            y -= 24f;

            Txt("_ip", parent, "IP:", new Vector2(15, y), new Vector2(30, 24),
                15, FontStyle.Normal, TextAnchor.MiddleLeft, textColor, A01(), A01(), A01());
            ipInput = MkInputField("IP", parent, new Vector2(50, y), new Vector2(panelSize.x - 65, 24), "192.168.58.2");
            ipInput.onEndEdit.AddListener(s => { if (robotManager.real != null) robotManager.real.robotIP = s; });
            y -= 30f;

            connectBtn = MkButton("Conn", parent, "CONNECT", new Vector2(15, y),
                new Vector2((panelSize.x - 40) / 2f, 28), readyColor);
            connectBtn.onClick.AddListener(() => robotManager.Connect());
            disconnectBtn = MkButton("Disc", parent, "DISCONNECT",
                new Vector2(15 + (panelSize.x - 40) / 2f + 10, y),
                new Vector2((panelSize.x - 40) / 2f, 28), inactiveColor);
            disconnectBtn.onClick.AddListener(() => robotManager.Disconnect());
            y -= 34f;

            // Mode (SIM/REAL/MIRROR)
            Txt("_m", parent, "Mode:", new Vector2(15, y), new Vector2(50, 22),
                14, FontStyle.Normal, TextAnchor.MiddleLeft, textColor, A01(), A01(), A01());
            string[] names = { "SIM", "REAL", "MIRROR" };
            modeButtons = new Button[3];
            float bw = (panelSize.x - 85) / 3f;
            for (int m = 0; m < 3; m++)
            {
                int mi = m;
                var b = MkButton($"M{m}", parent, names[m],
                    new Vector2(65 + m * (bw + 5), y), new Vector2(bw, 22), inactiveColor);
                b.onClick.AddListener(() => robotManager.ChangeMode((RobotManager.Mode)mi));
                modeButtons[m] = b;
            }
            y -= 26f;

            statusText = Txt("Stat", parent, "Disconnected",
                new Vector2(15, y), new Vector2(panelSize.x - 30, 18),
                16, FontStyle.Italic, TextAnchor.MiddleLeft, textColor, A01(), A01(), A01());
            y -= 24f;
            return y;
        }

        // ── 섹션 2: Mode(Auto/Manual) + Speed ──────────────────────
        float BuildModeSpeedSection(Transform parent, float y)
        {
            Txt("_op", parent, "■ OPERATION", new Vector2(15, y), new Vector2(panelSize.x - 30, 20),
                16, FontStyle.Bold, TextAnchor.MiddleLeft, accentColor, A01(), A01(), A01());
            y -= 24f;

            // Auto/Manual
            Txt("_om", parent, "Op Mode:", new Vector2(15, y), new Vector2(80, 22),
                14, FontStyle.Normal, TextAnchor.MiddleLeft, textColor, A01(), A01(), A01());

            modeAutoBtn = MkButton("OpA", parent, "AUTO",
                new Vector2(95, y), new Vector2((panelSize.x - 135) / 2f, 22), inactiveColor);
            modeAutoBtn.onClick.AddListener(() => robotManager.SetMode(RobotMode.Auto));
            modeManualBtn = MkButton("OpM", parent, "MANUAL",
                new Vector2(95 + (panelSize.x - 135) / 2f + 5, y),
                new Vector2((panelSize.x - 135) / 2f, 22), inactiveColor);
            modeManualBtn.onClick.AddListener(() => robotManager.SetMode(RobotMode.Manual));
            y -= 28f;

            // Speed
            Txt("_sp", parent, "Speed:", new Vector2(15, y), new Vector2(60, 18),
                14, FontStyle.Normal, TextAnchor.MiddleLeft, textColor, A01(), A01(), A01());
            speedValueText = Txt("SpVal", parent, "50%",
                new Vector2(-15, y), new Vector2(50, 18),
                14, FontStyle.Bold, TextAnchor.MiddleRight, accentColor, A11(), A11(), A11());

            speedSlider = MkSlider("SpSlider", parent,
                new Vector2(15, y - 20f), new Vector2(panelSize.x - 30, 16f));
            speedSlider.minValue = 0; speedSlider.maxValue = 100;
            speedSlider.wholeNumbers = true;
            speedSlider.value = robotManager.GetGlobalSpeed();
            speedSlider.onValueChanged.AddListener(v => robotManager.SetGlobalSpeed((int)v));
            y -= 42f;
            return y;
        }

        // ── 섹션 3: TCP Pose 표시 ──────────────────────────────────
        float BuildTCPPoseSection(Transform parent, float y)
        {
            Txt("_tcp", parent, "■ TCP POSE (mm / deg)", new Vector2(15, y),
                new Vector2(panelSize.x - 30, 20),
                16, FontStyle.Bold, TextAnchor.MiddleLeft, accentColor, A01(), A01(), A01());
            y -= 22f;
            tcpPoseText = Txt("TcpT", parent, "-",
                new Vector2(15, y), new Vector2(panelSize.x - 30, 36),
                14, FontStyle.Normal, TextAnchor.UpperLeft, textColor, A01(), A01(), A01());
            y -= 42f;
            return y;
        }

        // ── 섹션 4: 컨트롤 탭 (Joint / Cartesian 전환) ────────────
        float BuildControlTabsSection(Transform parent, float y)
        {
            float tw = (panelSize.x - 40) / 2f;
            jointTabBtn = MkButton("TabJ", parent, "JOINT CTRL",
                new Vector2(15, y), new Vector2(tw, 26), accentColor);
            jointTabBtn.onClick.AddListener(() => ShowTab(ControlTab.Joint));
            cartTabBtn = MkButton("TabC", parent, "CARTESIAN",
                new Vector2(15 + tw + 10, y), new Vector2(tw, 26), inactiveColor);
            cartTabBtn.onClick.AddListener(() => ShowTab(ControlTab.Cartesian));
            y -= 32f;
            return y;
        }

        // ── 섹션 5a: Joint 패널 ────────────────────────────────────
        //   레이아웃 (각 조인트 높이 48px):
        //   [이름] ............................... [입력칸]
        //   [-5°] [-1°] ━━━●━━━━━━━━━ [+1°] [+5°]
        float BuildJointPanelContent(Transform parent, float y)
        {
            jointPanel = Mk("JointPanel", parent);
            var r = jointPanel.GetComponent<RectTransform>();
            r.anchorMin = A01(); r.anchorMax = A01(); r.pivot = A01();
            r.anchoredPosition = new Vector2(0, y);
            r.sizeDelta = new Vector2(panelSize.x, 6 * 56f);

            float localY = 0f;
            for (int i = 0; i < robotManager.JointCount; i++)
            {
                int idx = i;
                float minA = robotManager.GetJointMinAngle(i);
                float maxA = robotManager.GetJointMaxAngle(i);
                string nm = robotManager.GetJointName(i);

                // ── 1행: 이름 (왼쪽) + 입력칸 (오른쪽) ──
                Txt($"JL{i}", jointPanel.transform, nm,
                    new Vector2(15, localY), new Vector2(140, 18),
                    14, FontStyle.Normal, TextAnchor.MiddleLeft, textColor, A01(), A01(), A01());

                // 각도 직접 입력 필드
                var inp = MkInputField($"JI{i}", jointPanel.transform,
                    new Vector2(-15, localY), new Vector2(70, 20),
                    robotManager.GetJointAngle(i).ToString("F1"));
                inp.contentType = InputField.ContentType.DecimalNumber;
                inp.onValueChanged.AddListener(_ => { editingJoint = true; editingIndex = idx; });
                inp.onEndEdit.AddListener(val => {
                    editingJoint = false; editingIndex = -1;
                    if (float.TryParse(val, out float angle))
                    {
                        float clamped = Mathf.Clamp(angle, minA, maxA);
                        robotManager.SetJointTarget(idx, clamped);
                        if (Mathf.Abs(clamped - angle) > 0.01f)
                            inp.text = clamped.ToString("F1");
                    }
                });
                // 입력칸을 오른쪽 끝에 배치
                var inpRt = inp.GetComponent<RectTransform>();
                inpRt.anchorMin = A11(); inpRt.anchorMax = A11(); inpRt.pivot = A11();
                inpRt.anchoredPosition = new Vector2(-15, localY);
                inpRt.sizeDelta = new Vector2(70, 20);
                jointInputs.Add(inp);

                // ── 2행: [-5°] [-1°] 슬라이더 [+1°] [+5°] ──
                float row2Y = localY - 24f;
                float smallBtnW = 32f;
                float btnH = 24f;

                // [-5°] 버튼 (맨 왼쪽) — 길게 누르면 연속 이동
                var btnM5 = MkButton($"JM5_{i}", jointPanel.transform, "-5°",
                    new Vector2(15, row2Y), new Vector2(smallBtnW, btnH), inactiveColor);
                AttachHoldToRepeat(btnM5.gameObject, () => IncrementJoint(idx, -5f, minA, maxA));

                // [-1°] 버튼
                var btnM1 = MkButton($"JM1_{i}", jointPanel.transform, "-1°",
                    new Vector2(15 + smallBtnW + 2f, row2Y), new Vector2(smallBtnW, btnH), inactiveColor);
                AttachHoldToRepeat(btnM1.gameObject, () => IncrementJoint(idx, -1f, minA, maxA));

                // 슬라이더 (가운데)
                float sliderStartX = 15 + 2 * (smallBtnW + 2f) + 4f;
                float sliderEndOffset = 2 * (smallBtnW + 2f) + 4f + 15f;
                float sliderW = panelSize.x - sliderStartX - sliderEndOffset;
                var sl = MkSlider($"JS{i}", jointPanel.transform,
                    new Vector2(sliderStartX, row2Y + 1f), new Vector2(sliderW, 14f));
                sl.minValue = minA; sl.maxValue = maxA;
                sl.value = robotManager.GetJointAngle(i);
                sl.onValueChanged.AddListener(v => robotManager.SetJointTarget(idx, v));
                AttachDragEvents(sl.gameObject,
                    () => { draggingJoint = true; draggingIndex = idx; },
                    () => { draggingJoint = false; draggingIndex = -1; });
                jointSliders.Add(sl);

                // [+1°] 버튼 (오른쪽 영역) — 길게 누르면 연속 이동
                var btnP1 = MkButton($"JP1_{i}", jointPanel.transform, "+1°",
                    new Vector2(-(15 + smallBtnW + 2f + smallBtnW), row2Y),
                    new Vector2(smallBtnW, btnH), inactiveColor);
                var rt1 = btnP1.GetComponent<RectTransform>();
                rt1.anchorMin = A11(); rt1.anchorMax = A11(); rt1.pivot = A01();
                rt1.anchoredPosition = new Vector2(-(15 + smallBtnW + 2f + smallBtnW), row2Y);
                rt1.sizeDelta = new Vector2(smallBtnW, btnH);
                AttachHoldToRepeat(btnP1.gameObject, () => IncrementJoint(idx, 1f, minA, maxA));

                // [+5°] 버튼 (맨 오른쪽) — 길게 누르면 연속 이동
                var btnP5 = MkButton($"JP5_{i}", jointPanel.transform, "+5°",
                    new Vector2(-(15 + smallBtnW), row2Y),
                    new Vector2(smallBtnW, btnH), inactiveColor);
                var rt2 = btnP5.GetComponent<RectTransform>();
                rt2.anchorMin = A11(); rt2.anchorMax = A11(); rt2.pivot = A01();
                rt2.anchoredPosition = new Vector2(-(15 + smallBtnW), row2Y);
                rt2.sizeDelta = new Vector2(smallBtnW, btnH);
                AttachHoldToRepeat(btnP5.gameObject, () => IncrementJoint(idx, 5f, minA, maxA));

                localY -= 56f;
            }

            y -= 6 * 56f + 4f;
            return y;
        }

        /// <summary>조인트를 현재 값에서 delta만큼 증분 이동 (관절 한계 자동 클램프)</summary>
        void IncrementJoint(int idx, float delta, float minA, float maxA)
        {
            float cur = robotManager.GetJointAngle(idx);
            float target = Mathf.Clamp(cur + delta, minA, maxA);
            robotManager.SetJointTarget(idx, target);
        }

        // ── 섹션 5b: Cartesian JOG 패널 ────────────────────────────
        float BuildCartesianPanelContent(Transform parent, float y)
        {
            float panelH = 3 * 42f + 3 * 42f + 10f;
            cartPanel = Mk("CartPanel", parent);
            var r = cartPanel.GetComponent<RectTransform>();
            r.anchorMin = A01(); r.anchorMax = A01(); r.pivot = A01();
            r.anchoredPosition = new Vector2(0, y + (6 * 42f));
            r.sizeDelta = new Vector2(panelSize.x, panelH);

            Txt("_info", cartPanel.transform, "Hold button to JOG (base frame)  •  IK-based",
                new Vector2(15, 0), new Vector2(panelSize.x - 30, 18),
                16, FontStyle.Italic, TextAnchor.MiddleLeft, readyColor, A01(), A01(), A01());

            float localY = -22f;
            string[] labels = { "X (mm)", "Y (mm)", "Z (mm)", "Rx (°)", "Ry (°)", "Rz (°)" };
            for (int i = 0; i < 6; i++)
            {
                int axis = i;
                Txt($"CL{i}", cartPanel.transform, labels[i],
                    new Vector2(15, localY), new Vector2(80, 22),
                    14, FontStyle.Normal, TextAnchor.MiddleLeft, textColor, A01(), A01(), A01());

                // - 버튼
                var minus = MkButton($"C-{i}", cartPanel.transform, "◀  -",
                    new Vector2(100, localY), new Vector2((panelSize.x - 130) / 2f, 24), inactiveColor);
                AttachJogEvents(minus.gameObject, () => robotManager.StartCartesianJog(axis, -1));

                // + 버튼
                var plus = MkButton($"C+{i}", cartPanel.transform, "+  ▶",
                    new Vector2(100 + (panelSize.x - 130) / 2f + 5, localY),
                    new Vector2((panelSize.x - 130) / 2f, 24), inactiveColor);
                AttachJogEvents(plus.gameObject, () => robotManager.StartCartesianJog(axis, +1));

                localY -= 30f;
            }
            return y;
        }

        // ── 섹션 6: Gripper ─────────────────────────────────────────
        float BuildGripperSection(Transform parent, float y)
        {
            Txt("_g", parent, "■ GRIPPER",
                new Vector2(15, y), new Vector2(panelSize.x - 30, 20),
                16, FontStyle.Bold, TextAnchor.MiddleLeft, accentColor, A01(), A01(), A01());
            y -= 22f;

            Txt("GL", parent, "Open", new Vector2(15, y), new Vector2(140, 18),
                14, FontStyle.Normal, TextAnchor.MiddleLeft, textColor, A01(), A01(), A01());
            gripperValueText = Txt("GV", parent, "0%",
                new Vector2(-15, y), new Vector2(80, 18),
                14, FontStyle.Normal, TextAnchor.MiddleRight, textColor, A11(), A11(), A11());

            gripperSlider = MkSlider("GS", parent,
                new Vector2(15, y - 20f), new Vector2(panelSize.x - 30, 16f));
            gripperSlider.minValue = 0; gripperSlider.maxValue = 100;
            gripperSlider.value = robotManager.GetGripperOpenPercent();
            gripperSlider.onValueChanged.AddListener(v => robotManager.SetGripperTarget(v));
            AttachDragEvents(gripperSlider.gameObject,
                () => draggingGripper = true, () => draggingGripper = false);

            y -= 44f;
            return y;
        }

        // ── 섹션 7: Home Pose (각 조인트 값 직접 입력) ─────────────
        float BuildHomePoseSection(Transform parent, float y)
        {
            Txt("_h", parent, "■ HOME POSE (각 조인트 각도 입력, 단위: °)",
                new Vector2(15, y), new Vector2(panelSize.x - 30, 20),
                15, FontStyle.Bold, TextAnchor.MiddleLeft, accentColor, A01(), A01(), A01());
            y -= 22f;

            // 6개 조인트 입력 필드를 2열 x 3행 그리드로 배치
            int n = robotManager.JointCount;
            homeInputs = new InputField[n];
            float[] initHome = robotManager.GetHomePose();

            float col1X = 15f;
            float col2X = 15f + (panelSize.x - 30f) / 2f + 5f;
            float fieldW = (panelSize.x - 30f) / 2f - 5f;
            float labelW = 30f;
            float inputW = fieldW - labelW - 5f;
            float rowH = 26f;

            for (int i = 0; i < n; i++)
            {
                int idx = i;
                bool isLeftCol = (i % 2 == 0);
                float x = isLeftCol ? col1X : col2X;
                float rowY = y - (i / 2) * rowH;

                // J1~J6 라벨
                Txt($"HL{i}", parent, $"J{i + 1}:",
                    new Vector2(x, rowY), new Vector2(labelW, 22),
                    14, FontStyle.Bold, TextAnchor.MiddleLeft, textColor, A01(), A01(), A01());

                // 입력 필드
                var inp = MkInputField($"HI{i}", parent,
                    new Vector2(x + labelW, rowY), new Vector2(inputW, 22),
                    initHome[i].ToString("F1"));
                inp.contentType = InputField.ContentType.DecimalNumber;
                homeInputs[i] = inp;
            }
            y -= 3 * rowH + 6f;  // 3행 차지

            // 버튼 3개: SET FROM CURRENT / APPLY / GO HOME
            float btnW = (panelSize.x - 50f) / 3f;
            setHomeBtn = MkButton("SetH", parent, "SET FROM CURRENT",
                new Vector2(15, y), new Vector2(btnW, 28), inactiveColor);
            setHomeBtn.onClick.AddListener(FillHomeInputsFromCurrent);

            applyHomeBtn = MkButton("AppH", parent, "APPLY",
                new Vector2(15 + btnW + 10, y), new Vector2(btnW, 28), warnColor);
            applyHomeBtn.onClick.AddListener(ApplyHomeInputs);

            goHomeBtn = MkButton("GoH", parent, "GO HOME",
                new Vector2(15 + 2 * (btnW + 10), y), new Vector2(btnW, 28), accentColor);
            goHomeBtn.onClick.AddListener(() => robotManager.GoToHome());
            y -= 34f;

            // ── 슬롯 3개 (현재 자세를 임시 저장해서 바로 불러오기) ──
            y -= 6f;
            Txt("_slots", parent, "■ POSE SLOTS (현재 자세 저장/불러오기)",
                new Vector2(15, y), new Vector2(panelSize.x - 30, 20),
                14, FontStyle.Bold, TextAnchor.MiddleLeft, accentColor, A01(), A01(), A01());
            y -= 22f;

            homeSlotStatusTexts = new Text[3];
            float slotRowH = 32f;
            float slotNameW = 60f;
            float slotStatusW = 100f;
            float slotBtnW = (panelSize.x - 30f - slotNameW - slotStatusW - 20f) / 2f;
            for (int s = 0; s < 3; s++)
            {
                int si = s;
                // 슬롯 이름
                Txt($"_sn{s}", parent, $"Slot {s + 1}:",
                    new Vector2(15, y), new Vector2(slotNameW, 22),
                    14, FontStyle.Normal, TextAnchor.MiddleLeft, textColor, A01(), A01(), A01());

                // 상태 텍스트 ("비어있음" / "[J1, J2, ...]")
                var statusT = Txt($"_ss{s}", parent, "비어있음",
                    new Vector2(15 + slotNameW, y), new Vector2(slotStatusW, 22),
                    16, FontStyle.Normal, TextAnchor.MiddleLeft, textColor, A01(), A01(), A01());
                homeSlotStatusTexts[s] = statusT;

                // SAVE 버튼 - 현재 자세를 슬롯에 저장
                var saveBtn = MkButton($"_sSave{s}", parent, "SAVE",
                    new Vector2(15 + slotNameW + slotStatusW, y),
                    new Vector2(slotBtnW, 22), warnColor);
                saveBtn.onClick.AddListener(() => SaveHomeSlot(si));

                // LOAD 버튼 - 슬롯의 자세로 이동
                var loadBtn = MkButton($"_sLoad{s}", parent, "LOAD",
                    new Vector2(15 + slotNameW + slotStatusW + slotBtnW + 5f, y),
                    new Vector2(slotBtnW, 22), accentColor);
                loadBtn.onClick.AddListener(() => LoadHomeSlot(si));

                y -= slotRowH;
            }
            y -= 4f;
            return y;
        }

        /// <summary>현재 로봇 자세를 해당 슬롯에 저장 (메모리만, Unity 종료 시 사라짐)</summary>
        void SaveHomeSlot(int idx)
        {
            if (idx < 0 || idx >= homeSlots.Length) return;
            int n = robotManager.JointCount;
            homeSlots[idx] = new float[n];
            for (int i = 0; i < n; i++)
                homeSlots[idx][i] = robotManager.GetJointAngle(i);
            homeSlotsFilled[idx] = true;
            UpdateSlotStatusText(idx);
            Debug.Log($"[UI] Slot {idx + 1} 저장됨: [{string.Join(", ", System.Array.ConvertAll(homeSlots[idx], v => v.ToString("F1")))}]");
        }

        /// <summary>해당 슬롯의 자세를 홈으로 설정하고 GO HOME 실행</summary>
        void LoadHomeSlot(int idx)
        {
            if (idx < 0 || idx >= homeSlots.Length) return;
            if (!homeSlotsFilled[idx] || homeSlots[idx] == null)
            {
                Debug.LogWarning($"[UI] Slot {idx + 1}은 비어있습니다");
                return;
            }
            // 홈 포즈를 슬롯 값으로 덮어쓴 뒤 GO HOME
            robotManager.SetHomePose((float[])homeSlots[idx].Clone());
            robotManager.GoToHome();
            // 홈 입력칸에도 반영
            if (homeInputs != null)
            {
                for (int i = 0; i < homeInputs.Length && i < homeSlots[idx].Length; i++)
                    homeInputs[i].text = homeSlots[idx][i].ToString("F1");
            }
            Debug.Log($"[UI] Slot {idx + 1} 불러오기: GO HOME 실행");
        }

        /// <summary>슬롯 상태 텍스트 갱신</summary>
        void UpdateSlotStatusText(int idx)
        {
            if (homeSlotStatusTexts == null || idx >= homeSlotStatusTexts.Length) return;
            if (homeSlotStatusTexts[idx] == null) return;
            if (homeSlotsFilled[idx] && homeSlots[idx] != null)
            {
                // 짧게 표시: "J1..J6"
                var vals = homeSlots[idx];
                homeSlotStatusTexts[idx].text =
                    $"저장됨 (J1={vals[0]:F0} ...)";
                homeSlotStatusTexts[idx].color = readyColor;
            }
            else
            {
                homeSlotStatusTexts[idx].text = "비어있음";
                homeSlotStatusTexts[idx].color = textColor;
            }
        }

        /// <summary>현재 로봇 각도를 입력 필드에 채워넣기 (아직 홈으로 저장은 안 됨).</summary>
        void FillHomeInputsFromCurrent()
        {
            if (homeInputs == null) return;
            for (int i = 0; i < homeInputs.Length; i++)
            {
                float cur = robotManager.GetJointAngle(i);
                homeInputs[i].text = cur.ToString("F1");
            }
        }

        /// <summary>입력 필드의 값들을 파싱해서 홈 포즈로 저장. 관절 한계도 체크.</summary>
        void ApplyHomeInputs()
        {
            if (homeInputs == null) return;
            float[] newHome = new float[homeInputs.Length];
            for (int i = 0; i < homeInputs.Length; i++)
            {
                if (!float.TryParse(homeInputs[i].text, out float v))
                {
                    Debug.LogWarning($"[UI] J{i + 1} 홈 포즈 입력값을 숫자로 변환 실패. 기존값 유지.");
                    newHome[i] = robotManager.GetHomePose()[i];
                    continue;
                }
                // 관절 limit 클램프
                float min = robotManager.GetJointMinAngle(i);
                float max = robotManager.GetJointMaxAngle(i);
                float clamped = Mathf.Clamp(v, min, max);
                if (Mathf.Abs(clamped - v) > 0.01f)
                {
                    Debug.LogWarning($"[UI] J{i + 1}={v}°가 관절 한계 [{min}, {max}]를 벗어남. {clamped}°로 조정.");
                    homeInputs[i].text = clamped.ToString("F1");
                }
                newHome[i] = clamped;
            }
            robotManager.SetHomePose(newHome);
            Debug.Log($"[UI] 홈 포즈 저장됨: [{string.Join(", ", newHome)}]");
        }

        void BuildBottomButtons(Transform parent, float y)
        {
            stopBtn = MkButton("Stop", parent, "■ EMERGENCY STOP",
                new Vector2(15, y), new Vector2(panelSize.x - 30, 34), dangerColor);
            stopBtn.onClick.AddListener(() => robotManager.StopMotion());
        }

        // ═════════════════════════════════════════════════════════════
        // 상태 업데이트
        // ═════════════════════════════════════════════════════════════
        void ShowTab(ControlTab t)
        {
            currentTab = t;
            if (jointPanel != null) jointPanel.SetActive(t == ControlTab.Joint);
            if (cartPanel != null) cartPanel.SetActive(t == ControlTab.Cartesian);
            if (jointTabBtn != null)
                jointTabBtn.GetComponent<Image>().color = (t == ControlTab.Joint) ? accentColor : inactiveColor;
            if (cartTabBtn != null)
                cartTabBtn.GetComponent<Image>().color = (t == ControlTab.Cartesian) ? accentColor : inactiveColor;
        }

        void UpdateModeButtons()
        {
            if (modeButtons != null)
            {
                int cur = (int)robotManager.mode;
                for (int i = 0; i < modeButtons.Length; i++)
                    modeButtons[i].GetComponent<Image>().color = (i == cur) ? accentColor : inactiveColor;
            }

            var opMode = robotManager.GetMode();
            if (modeAutoBtn != null)
                modeAutoBtn.GetComponent<Image>().color = (opMode == RobotMode.Auto) ? accentColor : inactiveColor;
            if (modeManualBtn != null)
                modeManualBtn.GetComponent<Image>().color = (opMode == RobotMode.Manual) ? accentColor : inactiveColor;
        }

        // ═════════════════════════════════════════════════════════════
        // 이벤트 바인딩 도우미
        // ═════════════════════════════════════════════════════════════
        void AttachJogEvents(GameObject go, System.Action onDown)
        {
            var trig = go.AddComponent<EventTrigger>();
            var d = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            d.callback.AddListener(_ => onDown?.Invoke());
            trig.triggers.Add(d);
            var u = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            u.callback.AddListener(_ => robotManager.StopJog());
            trig.triggers.Add(u);
            var ex = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            ex.callback.AddListener(_ => robotManager.StopJog());
            trig.triggers.Add(ex);
        }

        void AttachDragEvents(GameObject go, System.Action onDown, System.Action onUp)
        {
            var trig = go.AddComponent<EventTrigger>();
            var d = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            d.callback.AddListener(_ => onDown?.Invoke());
            trig.triggers.Add(d);
            var u = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            u.callback.AddListener(_ => onUp?.Invoke());
            trig.triggers.Add(u);
        }

        /// <summary>
        /// 버튼을 누르고 있으면 일정 주기로 onRepeat를 반복 실행.
        /// 첫 클릭은 즉시 실행, 이후 initialDelay 대기 후 repeatInterval 간격으로 반복.
        /// 버튼에서 손 떼면 정지.
        /// </summary>
        void AttachHoldToRepeat(GameObject go, System.Action onRepeat,
                                float initialDelay = 0.3f, float repeatInterval = 0.1f)
        {
            var trig = go.GetComponent<EventTrigger>();
            if (trig == null) trig = go.AddComponent<EventTrigger>();

            Coroutine[] coroutineRef = new Coroutine[1];

            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => {
                // 첫 번째는 즉시 실행
                onRepeat?.Invoke();
                // 이후 반복은 코루틴으로
                if (coroutineRef[0] != null) StopCoroutine(coroutineRef[0]);
                coroutineRef[0] = StartCoroutine(HoldRepeatCoroutine(onRepeat, initialDelay, repeatInterval));
            });
            trig.triggers.Add(down);

            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => {
                if (coroutineRef[0] != null) { StopCoroutine(coroutineRef[0]); coroutineRef[0] = null; }
            });
            trig.triggers.Add(up);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => {
                if (coroutineRef[0] != null) { StopCoroutine(coroutineRef[0]); coroutineRef[0] = null; }
            });
            trig.triggers.Add(exit);
        }

        System.Collections.IEnumerator HoldRepeatCoroutine(System.Action onRepeat,
                                                           float initialDelay, float repeatInterval)
        {
            yield return new WaitForSeconds(initialDelay);
            while (true)
            {
                onRepeat?.Invoke();
                yield return new WaitForSeconds(repeatInterval);
            }
        }

        // ═════════════════════════════════════════════════════════════
        // UI 생성 헬퍼
        // ═════════════════════════════════════════════════════════════
        static Vector2 A01() => new Vector2(0, 1f);
        static Vector2 A11() => new Vector2(1f, 1f);

        GameObject Mk(string n, Transform p)
        {
            var g = new GameObject(n);
            g.transform.SetParent(p, false);
            g.AddComponent<RectTransform>();
            return g;
        }

        Text Txt(string n, Transform p, string t, Vector2 pos, Vector2 sz,
            int fs, FontStyle fsty, TextAnchor al, Color c,
            Vector2 aMin, Vector2 aMax, Vector2 pvt)
        {
            var g = Mk(n, p);
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = pvt;
            r.anchoredPosition = pos; r.sizeDelta = sz;
            var tx = g.AddComponent<Text>();
            tx.text = t;
            tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tx.fontSize = fs; tx.fontStyle = fsty; tx.alignment = al; tx.color = c;
            return tx;
        }

        Button MkButton(string n, Transform p, string lbl, Vector2 pos, Vector2 sz, Color bg)
        {
            var g = Mk(n, p);
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = A01(); r.anchorMax = A01(); r.pivot = A01();
            r.anchoredPosition = pos; r.sizeDelta = sz;
            var img = g.AddComponent<Image>(); img.color = bg;
            var btn = g.AddComponent<Button>(); btn.targetGraphic = img;
            Txt("_t", g.transform, lbl, Vector2.zero, sz,
                17, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            return btn;
        }

        InputField MkInputField(string n, Transform p, Vector2 pos, Vector2 sz, string def)
        {
            var g = Mk(n, p);
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = A01(); r.anchorMax = A01(); r.pivot = A01();
            r.anchoredPosition = pos; r.sizeDelta = sz;
            g.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f);
            var inp = g.AddComponent<InputField>();
            var txt = Txt("Text", g.transform, def, new Vector2(5, 0), new Vector2(sz.x - 10, sz.y),
                17, FontStyle.Normal, TextAnchor.MiddleLeft, textColor,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
            var rt = txt.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(5, 2); rt.offsetMax = new Vector2(-5, -2);
            inp.textComponent = txt; inp.text = def;
            return inp;
        }

        Slider MkSlider(string n, Transform p, Vector2 pos, Vector2 sz)
        {
            var g = Mk(n, p);
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = A01(); r.anchorMax = A01(); r.pivot = A01();
            r.anchoredPosition = pos; r.sizeDelta = sz;
            var sl = g.AddComponent<Slider>();

            var bg = Mk("Bg", g.transform);
            var bgr = bg.GetComponent<RectTransform>();
            bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one; bgr.sizeDelta = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);

            var fa = Mk("FillArea", g.transform);
            var far = fa.GetComponent<RectTransform>();
            far.anchorMin = new Vector2(0f, 0.25f); far.anchorMax = new Vector2(1f, 0.75f);
            far.offsetMin = new Vector2(5, 0); far.offsetMax = new Vector2(-15, 0);
            var fill = Mk("Fill", fa.transform);
            var fr = fill.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.sizeDelta = Vector2.zero;
            fill.AddComponent<Image>().color = accentColor;

            var ha = Mk("HArea", g.transform);
            var har = ha.GetComponent<RectTransform>();
            har.anchorMin = Vector2.zero; har.anchorMax = Vector2.one;
            har.offsetMin = new Vector2(10, 0); har.offsetMax = new Vector2(-10, 0);
            var handle = Mk("Handle", ha.transform);
            var hr = handle.GetComponent<RectTransform>();
            hr.sizeDelta = new Vector2(14, 18);
            var himg = handle.AddComponent<Image>(); himg.color = Color.white;

            sl.fillRect = fr; sl.handleRect = hr; sl.targetGraphic = himg;
            sl.direction = Slider.Direction.LeftToRight;
            return sl;
        }
    }
}
