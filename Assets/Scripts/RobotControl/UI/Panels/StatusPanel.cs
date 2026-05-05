using TMPro;
using UnityEngine;

namespace RobotControl
{
    public class StatusPanel : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] RobotManager robotManager;

        [Header("UI (TMP)")]
        [SerializeField] TMP_Text connectionText;
        [SerializeField] TMP_Text statusText;

        void Start()
        {
            if (robotManager == null)
            {
                Debug.LogError("[StatusPanel] robotManager 참조가 비어있음");
                return;
            }
            robotManager.OnStatusChanged += UpdateText;
            UpdateText(robotManager.StatusMessage);
        }

        void Update()
        {
            if (robotManager == null || connectionText == null) return;
            connectionText.text = robotManager.IsConnected ? "Connected" : "Disconnected";
        }

        void UpdateText(string msg)
        {
            if (statusText != null)
                statusText.text = msg;
        }

        void OnDestroy()
        {
            if (robotManager != null)
                robotManager.OnStatusChanged -= UpdateText;
        }
    }
}
