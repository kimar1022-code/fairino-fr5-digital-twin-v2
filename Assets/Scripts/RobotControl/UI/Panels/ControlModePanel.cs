using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobotControl
{
    public class ControlModePanel : MonoBehaviour
    {
        [Header("UI Toggles")]
        [SerializeField] private Toggle jointToggle;
        [SerializeField] private Toggle cartesianToggle;

        [Header("화면 전환 대상 (GameObject)")]
        [SerializeField] private GameObject jointPanel;
        [SerializeField] private GameObject cartesianJogPanel;

        [Header("현재 모드 표시 (선택)")]
        [SerializeField] private TMP_Text currentModeText;

        void Start()
        {
            if (jointToggle == null)
            {
                Debug.LogError("[ControlModePanel] jointToggle 참조가 비어있음");
                return;
            }
            if (cartesianToggle == null)
            {
                Debug.LogError("[ControlModePanel] cartesianToggle 참조가 비어있음");
                return;
            }

            jointToggle.isOn = true;
            cartesianToggle.isOn = false;

            jointToggle.onValueChanged.AddListener(OnJointToggle);
            cartesianToggle.onValueChanged.AddListener(OnCartesianToggle);

            UpdateActivePanel();
        }

        void OnJointToggle(bool isOn)
        {
            if (!isOn) return;
            UpdateActivePanel();
        }

        void OnCartesianToggle(bool isOn)
        {
            if (!isOn) return;
            UpdateActivePanel();
        }

        void UpdateActivePanel()
        {
            bool jointOn = jointToggle.isOn;
            if (jointPanel != null) jointPanel.SetActive(jointOn);
            if (cartesianJogPanel != null) cartesianJogPanel.SetActive(cartesianToggle.isOn);
            if (currentModeText != null) currentModeText.text = jointOn ? "JOINT" : "CARTESIAN";
        }

        void OnDestroy()
        {
            if (jointToggle != null) jointToggle.onValueChanged.RemoveAllListeners();
            if (cartesianToggle != null) cartesianToggle.onValueChanged.RemoveAllListeners();
        }
    }
}
