using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobotControl
{
    public class ConnectionPanel : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] RobotManager robotManager;

        [Header("UI")]
        [SerializeField] TMP_Text ipDisplayText;
        [SerializeField] Button connectButton;
        [SerializeField] Button disconnectButton;

        void Start()
        {
            if (robotManager == null)
            {
                Debug.LogError("[ConnectionPanel] robotManager 참조가 비어있음");
                return;
            }
            if (connectButton == null)
            {
                Debug.LogError("[ConnectionPanel] connectButton 참조가 비어있음");
                return;
            }
            if (disconnectButton == null)
            {
                Debug.LogError("[ConnectionPanel] disconnectButton 참조가 비어있음");
                return;
            }

            if (ipDisplayText != null)
                ipDisplayText.text = "IP: 192.168.58.2";

            connectButton.onClick.AddListener(OnConnectClick);
            disconnectButton.onClick.AddListener(OnDisconnectClick);
        }

        void Update()
        {
            if (robotManager == null) return;
            bool connected = robotManager.IsConnected;
            if (connectButton != null) connectButton.interactable = !connected;
            if (disconnectButton != null) disconnectButton.interactable = connected;
        }

        void OnConnectClick()
        {
            robotManager.Connect();
        }

        void OnDisconnectClick()
        {
            robotManager.Disconnect();
        }

        void OnDestroy()
        {
            if (connectButton != null) connectButton.onClick.RemoveAllListeners();
            if (disconnectButton != null) disconnectButton.onClick.RemoveAllListeners();
        }
    }
}
