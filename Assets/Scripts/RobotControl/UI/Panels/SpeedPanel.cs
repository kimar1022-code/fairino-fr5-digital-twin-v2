using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobotControl
{
    public class SpeedPanel : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] RobotManager robotManager;

        [Header("UI")]
        [SerializeField] Slider speedSlider;
        [SerializeField] TMP_Text currentSpeedText;
        [SerializeField] Button minus5Button;
        [SerializeField] Button minus1Button;
        [SerializeField] Button plus1Button;
        [SerializeField] Button plus5Button;

        void Start()
        {
            if (robotManager == null)
            {
                Debug.LogError("[SpeedPanel] robotManager 참조가 비어있음");
                return;
            }
            if (speedSlider == null)
            {
                Debug.LogError("[SpeedPanel] speedSlider 참조가 비어있음");
                return;
            }
            if (minus5Button == null)
            {
                Debug.LogError("[SpeedPanel] minus5Button 참조가 비어있음");
                return;
            }
            if (minus1Button == null)
            {
                Debug.LogError("[SpeedPanel] minus1Button 참조가 비어있음");
                return;
            }
            if (plus1Button == null)
            {
                Debug.LogError("[SpeedPanel] plus1Button 참조가 비어있음");
                return;
            }
            if (plus5Button == null)
            {
                Debug.LogError("[SpeedPanel] plus5Button 참조가 비어있음");
                return;
            }

            int initial = robotManager.GetGlobalSpeed();
            speedSlider.value = initial;

            speedSlider.onValueChanged.AddListener(OnSliderChange);
            minus5Button.onClick.AddListener(OnMinus5);
            minus1Button.onClick.AddListener(OnMinus1);
            plus1Button.onClick.AddListener(OnPlus1);
            plus5Button.onClick.AddListener(OnPlus5);

            UpdateText(initial);
        }

        void OnSliderChange(float value)
        {
            int p = Mathf.RoundToInt(value);
            robotManager.SetGlobalSpeed(p);
            UpdateText(p);
        }

        void ApplyDelta(int delta)
        {
            int current = robotManager.GetGlobalSpeed();
            int newVal = Mathf.Clamp(current + delta, 1, 100);
            robotManager.SetGlobalSpeed(newVal);
            speedSlider.value = newVal;
            UpdateText(newVal);
        }

        void OnMinus5()
        {
            ApplyDelta(-5);
        }

        void OnMinus1()
        {
            ApplyDelta(-1);
        }

        void OnPlus1()
        {
            ApplyDelta(1);
        }

        void OnPlus5()
        {
            ApplyDelta(5);
        }

        void UpdateText(int p)
        {
            if (currentSpeedText == null) return;
            currentSpeedText.text = $"Speed: {p}%";
        }

        void OnDestroy()
        {
            if (speedSlider != null) speedSlider.onValueChanged.RemoveAllListeners();
            if (minus5Button != null) minus5Button.onClick.RemoveAllListeners();
            if (minus1Button != null) minus1Button.onClick.RemoveAllListeners();
            if (plus1Button != null) plus1Button.onClick.RemoveAllListeners();
            if (plus5Button != null) plus5Button.onClick.RemoveAllListeners();
        }
    }
}
