using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobotControl
{
    public class HomePanel : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] RobotManager robotManager;

        [Header("UI")]
        [SerializeField] Button saveHomeButton;
        [SerializeField] Button goToHomeButton;
        [SerializeField] TMP_Text homeStatusText;

        bool isHomeSaved;

        void Start()
        {
            if (robotManager == null)
            {
                Debug.LogError("[HomePanel] robotManager 참조가 비어있음");
                return;
            }
            if (saveHomeButton == null)
            {
                Debug.LogError("[HomePanel] saveHomeButton 참조가 비어있음");
                return;
            }
            if (goToHomeButton == null)
            {
                Debug.LogError("[HomePanel] goToHomeButton 참조가 비어있음");
                return;
            }

            float[] currentHome = robotManager.GetHomePose();
            isHomeSaved = HasNonZeroAngle(currentHome);

            saveHomeButton.onClick.AddListener(OnSaveHomeClick);
            goToHomeButton.onClick.AddListener(OnGoToHomeClick);

            UpdateUI();
        }

        void OnSaveHomeClick()
        {
            robotManager.SetHomePoseFromCurrent();
            isHomeSaved = true;
            UpdateUI();
        }

        void OnGoToHomeClick()
        {
            robotManager.GoToHome();
        }

        void UpdateUI()
        {
            if (goToHomeButton != null) goToHomeButton.interactable = isHomeSaved;
            if (homeStatusText != null)
                homeStatusText.text = isHomeSaved ? "Home Saved" : "Home Empty";
        }

        bool HasNonZeroAngle(float[] angles)
        {
            if (angles == null) return false;
            for (int i = 0; i < angles.Length; i++)
            {
                if (Mathf.Abs(angles[i]) > 0.001f) return true;
            }
            return false;
        }

        void OnDestroy()
        {
            if (saveHomeButton != null) saveHomeButton.onClick.RemoveAllListeners();
            if (goToHomeButton != null) goToHomeButton.onClick.RemoveAllListeners();
        }
    }
}
