using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RobotControl
{
    public class WaypointPanel : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private WaypointPlayer waypointPlayer;
        [SerializeField] private WaypointRecorder waypointRecorder;

        [Header("재생 제어")]
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private TMP_Text playPauseLabel;

        [Header("리스트 관리")]
        [SerializeField] private Button saveButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button removeLastButton;

        [Header("표시")]
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Transform listContent;
        [SerializeField] private GameObject waypointItemPrefab;

        [Header("로그")]
        [SerializeField] private bool enableLog = true;

        private List<WaypointItem> spawnedItems = new List<WaypointItem>();
        private int lastKnownCount = -1;

        void Start()
        {
            if (waypointPlayer == null)
            {
                Debug.LogError("[WaypointPanel] waypointPlayer 참조가 비어있음");
                return;
            }
            if (waypointRecorder == null)
            {
                Debug.LogError("[WaypointPanel] waypointRecorder 참조가 비어있음");
                return;
            }
            if (listContent == null)
            {
                Debug.LogError("[WaypointPanel] listContent 참조가 비어있음");
                return;
            }
            if (waypointItemPrefab == null)
            {
                Debug.LogError("[WaypointPanel] waypointItemPrefab 참조가 비어있음");
                return;
            }

            if (playPauseButton != null) playPauseButton.onClick.AddListener(HandlePlayPauseClicked);
            if (stopButton != null) stopButton.onClick.AddListener(HandleStopClicked);
            if (saveButton != null) saveButton.onClick.AddListener(HandleSaveClicked);
            if (clearButton != null) clearButton.onClick.AddListener(HandleClearClicked);
            if (removeLastButton != null) removeLastButton.onClick.AddListener(HandleRemoveLastClicked);

            waypointPlayer.OnPlaybackStarted += HandlePlaybackStarted;
            waypointPlayer.OnWaypointReached += HandleWaypointReached;
            waypointPlayer.OnPlaybackCompleted += HandlePlaybackCompleted;
            waypointPlayer.OnPlaybackStopped += HandlePlaybackStopped;

            RebuildList();
        }

        void HandlePlayPauseClicked()
        {
            if (waypointPlayer == null) return;
            if (waypointPlayer.IsPlaying)
            {
                if (waypointPlayer.IsPaused) waypointPlayer.Resume();
                else waypointPlayer.Pause();
            }
            else
            {
                waypointPlayer.Play();
            }
        }

        void HandleStopClicked()
        {
            if (waypointPlayer == null) return;
            waypointPlayer.Stop();
        }

        void HandleSaveClicked()
        {
            if (waypointRecorder == null) return;
            waypointRecorder.SaveCurrentPose();
        }

        void HandleClearClicked()
        {
            if (waypointRecorder == null) return;
            waypointRecorder.ClearAll();
        }

        void HandleRemoveLastClicked()
        {
            if (waypointRecorder == null) return;
            waypointRecorder.RemoveLast();
        }

        void HandlePlaybackStarted()
        {
            if (enableLog) Debug.Log("[WaypointPanel] Playback started");
        }

        void HandleWaypointReached(int idx)
        {
            if (enableLog) Debug.Log($"[WaypointPanel] Waypoint reached: {idx}");
            UpdateHighlight();
        }

        void HandlePlaybackCompleted()
        {
            if (enableLog) Debug.Log("[WaypointPanel] Playback completed");
            UpdateHighlight();
        }

        void HandlePlaybackStopped()
        {
            if (enableLog) Debug.Log("[WaypointPanel] Playback stopped");
            UpdateHighlight();
        }

        void HandleItemClicked(int idx)
        {
            if (enableLog) Debug.Log($"[WaypointPanel] Waypoint item clicked: {idx}");
        }

        void Update()
        {
            if (waypointRecorder == null) return;

            int currentCount = waypointRecorder.Count;
            if (currentCount != lastKnownCount)
            {
                RebuildList();
                lastKnownCount = currentCount;
            }

            if (playPauseLabel != null && waypointPlayer != null)
            {
                if (waypointPlayer.IsPlaying)
                {
                    playPauseLabel.text = waypointPlayer.IsPaused ? "Resume" : "Pause";
                }
                else
                {
                    playPauseLabel.text = "Play";
                }
            }

            if (progressText != null && waypointPlayer != null)
            {
                int curr = waypointPlayer.CurrentIndex;
                int total = waypointRecorder.Count;
                if (curr < 0) progressText.text = $"0 / {total}";
                else progressText.text = $"{curr + 1} / {total}";
            }
        }

        void RebuildList()
        {
            if (listContent == null || waypointItemPrefab == null || waypointRecorder == null) return;

            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] != null)
                {
                    spawnedItems[i].OnClicked -= HandleItemClicked;
                    Destroy(spawnedItems[i].gameObject);
                }
            }
            spawnedItems.Clear();

            var waypoints = waypointRecorder.Waypoints;
            for (int i = 0; i < waypoints.Count; i++)
            {
                GameObject itemObj = Instantiate(waypointItemPrefab, listContent);
                WaypointItem item = itemObj.GetComponent<WaypointItem>();
                if (item == null) continue;
                item.SetWaypoint(i, waypoints[i]);
                item.OnClicked += HandleItemClicked;
                spawnedItems.Add(item);
            }

            UpdateHighlight();
        }

        void UpdateHighlight()
        {
            if (waypointPlayer == null) return;
            int currentIdx = waypointPlayer.CurrentIndex;
            bool isActive = waypointPlayer.IsPlaying && !waypointPlayer.IsPaused;

            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] == null) continue;
                spawnedItems[i].SetHighlight(isActive && i == currentIdx);
            }
        }

        void OnDestroy()
        {
            if (playPauseButton != null) playPauseButton.onClick.RemoveAllListeners();
            if (stopButton != null) stopButton.onClick.RemoveAllListeners();
            if (saveButton != null) saveButton.onClick.RemoveAllListeners();
            if (clearButton != null) clearButton.onClick.RemoveAllListeners();
            if (removeLastButton != null) removeLastButton.onClick.RemoveAllListeners();

            if (waypointPlayer != null)
            {
                waypointPlayer.OnPlaybackStarted -= HandlePlaybackStarted;
                waypointPlayer.OnWaypointReached -= HandleWaypointReached;
                waypointPlayer.OnPlaybackCompleted -= HandlePlaybackCompleted;
                waypointPlayer.OnPlaybackStopped -= HandlePlaybackStopped;
            }

            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] != null) spawnedItems[i].OnClicked -= HandleItemClicked;
            }
        }
    }
}
