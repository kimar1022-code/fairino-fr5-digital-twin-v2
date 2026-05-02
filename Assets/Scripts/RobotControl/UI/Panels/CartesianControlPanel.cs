using TMPro;
using UnityEngine;

namespace RobotControl
{
    public class CartesianControlPanel : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private RobotManager robotManager;

        [Header("UI (TMP)")]
        [SerializeField] private TMP_Text xText;
        [SerializeField] private TMP_Text yText;
        [SerializeField] private TMP_Text zText;
        [SerializeField] private TMP_Text rxText;
        [SerializeField] private TMP_Text ryText;
        [SerializeField] private TMP_Text rzText;

        void Start()
        {
            if (robotManager == null)
            {
                Debug.LogError("[CartesianControlPanel] robotManager 참조가 비어있음");
                return;
            }
        }

        void Update()
        {
            if (robotManager == null) return;
            CartesianPose pose = robotManager.GetCurrentTCPPose();
            if (xText != null) xText.text = $"X: {pose.x:F2}";
            if (yText != null) yText.text = $"Y: {pose.y:F2}";
            if (zText != null) zText.text = $"Z: {pose.z:F2}";
            if (rxText != null) rxText.text = $"RX: {pose.rx:F2}";
            if (ryText != null) ryText.text = $"RY: {pose.ry:F2}";
            if (rzText != null) rzText.text = $"RZ: {pose.rz:F2}";
        }

        void OnDestroy()
        {
        }
    }
}
