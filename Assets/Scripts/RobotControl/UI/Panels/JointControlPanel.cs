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
        [SerializeField] private TMP_Text stepValueText;

        [Header("각도 표시 (6개)")]
        [SerializeField] private TMP_Text[] jointAngleTexts;

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

        void Update()
        {
            if (robotManager == null) return;

            if (jointRows != null)
            {
                for (int i = 0; i < jointRows.Length; i++)
                {
                    JointControlRow row = jointRows[i];
                    if (row != null && row.Slider != null)
                    {
                        row.Slider.SetValueWithoutNotify(robotManager.GetJointAngle(i));
                    }
                }
            }

            if (jointAngleTexts != null)
            {
                for (int i = 0; i < jointAngleTexts.Length; i++)
                {
                    if (jointAngleTexts[i] != null)
                    {
                        jointAngleTexts[i].text = $"{robotManager.GetJointAngle(i):F1}";
                    }
                }
            }

            if (stepValueText != null && stepSlider != null)
            {
                stepValueText.text = $"Step: {stepSlider.value:F0}°";
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
        }
    }
}
