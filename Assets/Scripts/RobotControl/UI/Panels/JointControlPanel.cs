using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobotControl
{
    public class JointControlPanel : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private RobotManager robotManager;
        [SerializeField] private JointControlRow[] jointRows;

        [Header("Step 슬라이더 (±버튼 1회 이동량)")]
        [SerializeField] private Slider stepSlider;
        [SerializeField] Button stepMinusButton;
        [SerializeField] Button stepPlusButton;
        [SerializeField] private TMP_Text stepValueText;
        [SerializeField] private TMP_InputField stepValueInput;

        [Header("각도 입력 (6개)")]
        [SerializeField] private TMP_InputField[] jointAngleInputs;

        void Start()
        {
            if (robotManager == null)
            {
                Debug.LogError("[JointControlPanel] robotManager 참조가 비어있음");
                return;
            }
            if (jointRows == null)
            {
                Debug.LogError("[JointControlPanel] jointRows 배열이 비어있음");
                return;
            }
            if (stepSlider == null)
            {
                Debug.LogError("[JointControlPanel] stepSlider 참조가 비어있음");
                return;
            }

            stepSlider.minValue = 1f;
            stepSlider.maxValue = 90f;
            if (stepSlider.value < 1f) stepSlider.value = 30f;

            if (stepMinusButton != null) stepMinusButton.onClick.AddListener(() =>
            {
                if (stepSlider != null) stepSlider.value = Mathf.Clamp(stepSlider.value - 1f, 1f, 90f);
            });
            if (stepPlusButton != null) stepPlusButton.onClick.AddListener(() =>
            {
                if (stepSlider != null) stepSlider.value = Mathf.Clamp(stepSlider.value + 1f, 1f, 90f);
            });

            for (int i = 0; i < jointRows.Length; i++)
            {
                JointControlRow row = jointRows[i];
                if (row == null) continue;
                if (row.Slider != null)
                {
                    row.Slider.minValue = robotManager.GetJointMinAngle(i);
                    row.Slider.maxValue = robotManager.GetJointMaxAngle(i);
                    row.Slider.SetValueWithoutNotify(robotManager.GetJointAngle(i));
                }
                row.OnSliderChanged += HandleSliderChanged;
                row.OnMinusClicked += HandleMinusClicked;
                row.OnPlusClicked += HandlePlusClicked;
            }
        }

        void HandleSliderChanged(int idx, float value)
        {
            if (robotManager == null) return;
            robotManager.SetJointTarget(idx, value);
        }

        void HandleMinusClicked(int idx)
        {
            if (robotManager == null) return;
            float current = robotManager.GetJointAngle(idx);
            float target = current - stepSlider.value;
            target = Mathf.Clamp(target, robotManager.GetJointMinAngle(idx), robotManager.GetJointMaxAngle(idx));
            robotManager.SetJointTarget(idx, target);
        }

        void HandlePlusClicked(int idx)
        {
            if (robotManager == null) return;
            float current = robotManager.GetJointAngle(idx);
            float target = current + stepSlider.value;
            target = Mathf.Clamp(target, robotManager.GetJointMinAngle(idx), robotManager.GetJointMaxAngle(idx));
            robotManager.SetJointTarget(idx, target);
        }
        
        void OnStepInputEndEdit(string text)
        {
            if (stepSlider == null) return;
            if (float.TryParse(text, out float value))
            {
                value = Mathf.Clamp(value, 1f, 90f);
                stepSlider.value = value;
            }
        }
        void Update()
        {
            if (robotManager == null) return;

            // [DEBUG] 슬라이더 덮어쓰기 임시 비활성화 (Phase 4-A 검증용)
            // if (jointRows != null)
            // {
            //     for (int i = 0; i < jointRows.Length; i++)
            //     {
            //         JointControlRow row = jointRows[i];
            //         if (row != null && row.Slider != null)
            //         {
            //             row.Slider.SetValueWithoutNotify(robotManager.GetJointAngle(i));
            //         }
            //     }
            // }

            if (jointAngleInputs != null && jointRows != null)
            {
                for (int i = 0; i < jointAngleInputs.Length; i++)
                {
                    if (jointAngleInputs[i] == null) continue;

                    // 사용자가 InputField에 입력 중이면 덮어쓰지 않음
                    if (jointAngleInputs[i].isFocused) continue;

                    jointAngleInputs[i].text = $"{robotManager.GetJointAngle(i):F1}";
                }
            }

            if (stepSlider != null)
            {
                if (stepValueText != null)
                {
                    stepValueText.text = $"Step: {stepSlider.value:F0}°";
                }
                if (stepValueInput != null && !stepValueInput.isFocused)
                {
                    stepValueInput.text = $"{stepSlider.value:F0}";
                }
            }
        }

        void OnDestroy()
        {
            if (jointRows == null) return;
            for (int i = 0; i < jointRows.Length; i++)
            {
                JointControlRow row = jointRows[i];
                if (row == null) continue;
                row.OnSliderChanged -= HandleSliderChanged;
                row.OnMinusClicked -= HandleMinusClicked;
                row.OnPlusClicked -= HandlePlusClicked;
            }
            if (stepValueInput != null) stepValueInput.onEndEdit.RemoveAllListeners();
        }
    }
}
