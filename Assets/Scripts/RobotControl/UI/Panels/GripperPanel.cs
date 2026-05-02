using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobotControl
{
    public class GripperPanel : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private RobotManager robotManager;

        [Header("UI")]
        [SerializeField] private Button zeroPercentButton;
        [SerializeField] private Button hundredPercentButton;
        [SerializeField] private Button applyButton;
        [SerializeField] private TMP_InputField percentInput;
        [SerializeField] private TMP_Text currentPercentText;

        [Header("그리퍼 설정")]
        [SerializeField, Range(1, 100)] private int gripperSpeed = 50;
        [SerializeField, Range(1, 100)] private int gripperForce = 50;

        void Start()
        {
            if (robotManager == null)
            {
                Debug.LogError("[GripperPanel] robotManager 참조가 비어있음");
                return;
            }
            if (zeroPercentButton == null)
            {
                Debug.LogError("[GripperPanel] zeroPercentButton 참조가 비어있음");
                return;
            }
            if (hundredPercentButton == null)
            {
                Debug.LogError("[GripperPanel] hundredPercentButton 참조가 비어있음");
                return;
            }
            if (applyButton == null)
            {
                Debug.LogError("[GripperPanel] applyButton 참조가 비어있음");
                return;
            }
            if (percentInput == null)
            {
                Debug.LogError("[GripperPanel] percentInput 참조가 비어있음");
                return;
            }

            zeroPercentButton.onClick.AddListener(OnZeroPercentClick);
            hundredPercentButton.onClick.AddListener(OnHundredPercentClick);
            applyButton.onClick.AddListener(OnApplyClick);
        }

        void OnZeroPercentClick()
        {
            percentInput.text = "0";
            ApplyGripper(0f);
        }

        void OnHundredPercentClick()
        {
            percentInput.text = "100";
            ApplyGripper(100f);
        }

        void OnApplyClick()
        {
            if (!float.TryParse(percentInput.text, out float p))
            {
                Debug.LogWarning($"[GripperPanel] 입력값을 숫자로 변환할 수 없음: {percentInput.text}");
                return;
            }
            ApplyGripper(p);
        }

        void ApplyGripper(float p)
        {
            p = Mathf.Clamp(p, 0f, 100f);
            if (percentInput != null) percentInput.text = Mathf.RoundToInt(p).ToString();
            robotManager.SetGripperTarget(p, gripperSpeed, gripperForce);
        }

        void Update()
        {
            if (robotManager == null || currentPercentText == null) return;
            float current = robotManager.GetGripperOpenPercent();
            currentPercentText.text = $"Current: {current:F1}%";
        }

        void OnDestroy()
        {
            if (zeroPercentButton != null) zeroPercentButton.onClick.RemoveAllListeners();
            if (hundredPercentButton != null) hundredPercentButton.onClick.RemoveAllListeners();
            if (applyButton != null) applyButton.onClick.RemoveAllListeners();
        }
    }
}
