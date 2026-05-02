using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobotControl
{
    public class WaypointItem : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Button clickButton;

        [Header("색상")]
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private Color highlightColor = new Color(1f, 0.8f, 0.2f, 0.5f);

        private int waypointIndex = -1;

        public event Action<int> OnClicked;

        void Start()
        {
            if (clickButton == null)
            {
                Debug.LogError("[WaypointItem] clickButton 참조가 비어있음");
                return;
            }
            clickButton.onClick.AddListener(OnButtonClicked);
        }

        public void SetWaypoint(int index, Waypoint wp)
        {
            waypointIndex = index;
            if (labelText != null && wp != null)
            {
                labelText.text = wp.ToShortString();
            }
        }

        public void SetHighlight(bool active)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = active ? highlightColor : normalColor;
            }
        }

        public int GetWaypointIndex()
        {
            return waypointIndex;
        }

        void OnButtonClicked()
        {
            OnClicked?.Invoke(waypointIndex);
        }

        void OnDestroy()
        {
            if (clickButton != null) clickButton.onClick.RemoveAllListeners();
        }
    }
}
