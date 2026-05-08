using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RobotControl
{
    public class JointControlRow : MonoBehaviour
    {
        [Header("Joint 설정")]
        [SerializeField, Range(0, 5)] private int jointIndex;

        [Header("UI 컴포넌트")]
        [SerializeField] private Slider slider;
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private TMPro.TMP_InputField angleInputField;

        public Slider Slider => slider;

        public event Action<int, float> OnSliderChanged;
        public event Action<int> OnMinusClicked;
        public event Action<int> OnPlusClicked;

        void Start()
        {
            if (slider == null)
            {
                Debug.LogError("[JointControlRow] slider 참조가 비어있음");
                return;
            }
            if (minusButton == null)
            {
                Debug.LogError("[JointControlRow] minusButton 참조가 비어있음");
                return;
            }
            if (plusButton == null)
            {
                Debug.LogError("[JointControlRow] plusButton 참조가 비어있음");
                return;
            }
            if (angleInputField != null)
                angleInputField.onEndEdit.AddListener(OnAngleInputEndEdit);

            slider.onValueChanged.AddListener(OnSliderValueChanged);
            minusButton.onClick.AddListener(OnMinusButtonClicked);
            plusButton.onClick.AddListener(OnPlusButtonClicked);
        }

        void OnSliderValueChanged(float value)
        {
            OnSliderChanged?.Invoke(jointIndex, value);
        }

        void OnMinusButtonClicked()
        {
            OnMinusClicked?.Invoke(jointIndex);
        }

        void OnPlusButtonClicked()
        {
            OnPlusClicked?.Invoke(jointIndex);
        }

        void OnDestroy()
        {
            if (slider != null) slider.onValueChanged.RemoveAllListeners();
            if (minusButton != null) minusButton.onClick.RemoveAllListeners();
            if (plusButton != null) plusButton.onClick.RemoveAllListeners();
            if (angleInputField != null) angleInputField.onEndEdit.RemoveAllListeners();
        }

        void OnAngleInputEndEdit(string text)
        {
            if (slider == null) return;
            if (float.TryParse(text, out float value))
            {
                value = Mathf.Clamp(value, slider.minValue, slider.maxValue);
                slider.value = value;
                // 슬라이더 onValueChanged → OnSliderValueChanged → OnSliderChanged event 자동 발화
            }
            // 입력값 유효성 검증 후 표시 갱신은 JointControlPanel.Update가 처리
        }

        public bool IsInputFieldFocused()
        {
            return angleInputField != null && angleInputField.isFocused;
        }
    }
}
