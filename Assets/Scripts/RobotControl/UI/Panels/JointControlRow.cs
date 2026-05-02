using System;
using UnityEngine;
using UnityEngine.UI;

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
        }
    }
}
