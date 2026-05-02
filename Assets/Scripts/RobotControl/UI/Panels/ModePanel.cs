using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobotControl
{
    public class ModePanel : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] RobotManager robotManager;

        [Header("UI")]
        [SerializeField] Toggle simToggle;
        [SerializeField] Toggle mirrorToggle;
        [SerializeField] TMP_Text currentModeText;

        void Start()
        {
            if (robotManager == null)
            {
                Debug.LogError("[ModePanel] robotManager 참조가 비어있음");
                return;
            }
            if (simToggle == null)
            {
                Debug.LogError("[ModePanel] simToggle 참조가 비어있음");
                return;
            }
            if (mirrorToggle == null)
            {
                Debug.LogError("[ModePanel] mirrorToggle 참조가 비어있음");
                return;
            }

            simToggle.isOn = (robotManager.mode == RobotManager.Mode.SimOnly);
            mirrorToggle.isOn = (robotManager.mode == RobotManager.Mode.Mirror);

            simToggle.onValueChanged.AddListener(OnSimToggle);
            mirrorToggle.onValueChanged.AddListener(OnMirrorToggle);

            UpdateModeText();
        }

        void OnSimToggle(bool isOn)
        {
            if (!isOn) return;
            robotManager.ChangeMode(RobotManager.Mode.SimOnly);
            UpdateModeText();
        }

        void OnMirrorToggle(bool isOn)
        {
            if (!isOn) return;
            robotManager.ChangeMode(RobotManager.Mode.Mirror);
            UpdateModeText();
        }

        void UpdateModeText()
        {
            if (currentModeText == null) return;
            currentModeText.text = "Mode: " + robotManager.mode.ToString();
        }

        void OnDestroy()
        {
            if (simToggle != null) simToggle.onValueChanged.RemoveAllListeners();
            if (mirrorToggle != null) mirrorToggle.onValueChanged.RemoveAllListeners();
        }
    }
}
