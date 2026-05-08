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
            Debug.Log("[ConnectionPanel] OnConnectClick 호출됨");
            if (robotManager == null)
            {
                Debug.LogError("[ConnectionPanel] robotManager가 null!");
                return;
            }
            Debug.Log($"[ConnectionPanel] robotManager.Connect() 호출 직전. mode={robotManager}");
            robotManager.Connect();
            Debug.Log("[ConnectionPanel] Connect() 호출 완료");
        }

        void OnDisconnectClick()
        {
            Debug.Log("[ConnectionPanel] OnDisconnectClick 호출됨");
            if (robotManager == null) return;
            robotManager.Disconnect();
            Debug.Log("[ConnectionPanel] Disconnect() 호출 완료");
        }

        void OnDestroy()
        {
            if (connectButton != null) connectButton.onClick.RemoveAllListeners();
            if (disconnectButton != null) disconnectButton.onClick.RemoveAllListeners();
        }
    }
}
