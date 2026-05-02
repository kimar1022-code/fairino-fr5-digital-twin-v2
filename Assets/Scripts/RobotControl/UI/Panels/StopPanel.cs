using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobotControl
{
    public class StopPanel : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private RobotManager robotManager;

        [Header("UI")]
        [SerializeField] private Button stopButton;
        [SerializeField] private TMP_Text statusText;

        void Start()
        {
            if (robotManager == null)
            {
                Debug.LogError("[StopPanel] robotManager 참조가 비어있음");
                return;
            }
            if (stopButton == null)
            {
                Debug.LogError("[StopPanel] stopButton 참조가 비어있음");
                return;
            }

            stopButton.onClick.AddListener(OnStopClick);

            if (statusText != null) statusText.text = "Ready";
        }

        void OnStopClick()
        {
            robotManager.StopMotion();
            if (statusText != null) statusText.text = "STOPPED";
        }

        void OnDestroy()
        {
            if (stopButton != null) stopButton.onClick.RemoveAllListeners();
        }
    }
}
