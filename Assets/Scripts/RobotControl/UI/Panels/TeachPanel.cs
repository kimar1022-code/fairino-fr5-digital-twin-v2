using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobotControl
{
    public class TeachPanel : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private TeachModeManager teachManager;

        [Header("PLC 시뮬레이션 버튼 6개")]
        [SerializeField] private Button goHomeButton;
        [SerializeField] private Button recordStartButton;
        [SerializeField] private Button recordStopButton;
        [SerializeField] private Button saveWaypointButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Button stopButton;

        [Header("REC 상태 표시")]
        [SerializeField] private TMP_Text recLabel;
        [SerializeField] private Image recIndicator;

        [Header("REC 색상")]
        [SerializeField] private Color recOnColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color recOffColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

        void Start()
        {
            if (teachManager == null)
            {
                Debug.LogError("[TeachPanel] teachManager 참조가 비어있음");
                return;
            }

            if (goHomeButton != null) goHomeButton.onClick.AddListener(HandleGoHomeClicked);
            if (recordStartButton != null) recordStartButton.onClick.AddListener(HandleRecordStartClicked);
            if (recordStopButton != null) recordStopButton.onClick.AddListener(HandleRecordStopClicked);
            if (saveWaypointButton != null) saveWaypointButton.onClick.AddListener(HandleSaveWaypointClicked);
            if (playButton != null) playButton.onClick.AddListener(HandlePlayClicked);
            if (stopButton != null) stopButton.onClick.AddListener(HandleStopClicked);

            teachManager.OnRecordingStarted += HandleRecordingStarted;
            teachManager.OnRecordingStopped += HandleRecordingStopped;

            UpdateRecDisplay(teachManager.IsRecording);
        }

        void HandleGoHomeClicked()
        {
            if (teachManager == null) return;
            teachManager.GoHome();
        }

        void HandleRecordStartClicked()
        {
            if (teachManager == null) return;
            teachManager.StartRecording();
        }

        void HandleRecordStopClicked()
        {
            if (teachManager == null) return;
            teachManager.StopRecording();
        }

        void HandleSaveWaypointClicked()
        {
            if (teachManager == null) return;
            teachManager.SaveWaypoint();
        }

        void HandlePlayClicked()
        {
            if (teachManager == null) return;
            teachManager.PlayWaypoints();
        }

        void HandleStopClicked()
        {
            if (teachManager == null) return;
            teachManager.StopAll();
        }

        void HandleRecordingStarted()
        {
            UpdateRecDisplay(true);
        }

        void HandleRecordingStopped()
        {
            UpdateRecDisplay(false);
        }

        void UpdateRecDisplay(bool isRecording)
        {
            if (recLabel != null)
            {
                recLabel.text = isRecording ? "REC" : "REC";
                recLabel.color = isRecording ? recOnColor : recOffColor;
            }
            if (recIndicator != null)
            {
                recIndicator.color = isRecording ? recOnColor : recOffColor;
            }
        }

        void OnDestroy()
        {
            if (goHomeButton != null) goHomeButton.onClick.RemoveAllListeners();
            if (recordStartButton != null) recordStartButton.onClick.RemoveAllListeners();
            if (recordStopButton != null) recordStopButton.onClick.RemoveAllListeners();
            if (saveWaypointButton != null) saveWaypointButton.onClick.RemoveAllListeners();
            if (playButton != null) playButton.onClick.RemoveAllListeners();
            if (stopButton != null) stopButton.onClick.RemoveAllListeners();

            if (teachManager != null)
            {
                teachManager.OnRecordingStarted -= HandleRecordingStarted;
                teachManager.OnRecordingStopped -= HandleRecordingStopped;
            }
        }
    }
}
