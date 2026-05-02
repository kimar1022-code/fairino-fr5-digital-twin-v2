using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// Waypoint 영구 저장/로드 시스템.
    /// 
    /// 동작:
    /// - 자동 저장: WaypointRecorder에 WP 추가될 때마다 즉시 JSON 파일에 기록
    /// - 자동 로드: Start 시 파일 있으면 불러와서 Recorder에 복원
    /// - 백업: 새 저장 시 이전 파일을 .bak으로 보존
    /// 
    /// 저장 위치:
    /// - Application.persistentDataPath/Waypoints/waypoints.json
    /// - Windows: C:/Users/사용자/AppData/LocalLow/회사명/제품명/Waypoints/
    /// 
    /// 사용법:
    /// 1. 빈 GameObject 생성 → 이 컴포넌트 부착 (또는 기존 Recorder GameObject에 추가)
    /// 2. Inspector에서 Recorder 필드에 WaypointRecorder 드래그
    /// 3. Play 시 자동 로드 → 이전 데이터 복원
    /// 4. PLC 버튼 4 누름 → 자동 저장 (즉시 파일 기록)
    /// </summary>
    public class WaypointStorage : MonoBehaviour
    {
        [Header("참조")]
        public WaypointRecorder recorder;

        [Header("파일 설정")]
        [Tooltip("파일명 (확장자 포함). 예: waypoints.json")]
        public string fileName = "waypoints.json";

        [Tooltip("폴더명. persistentDataPath 아래에 생성됨")]
        public string folderName = "Waypoints";

        [Header("동작 설정")]
        [Tooltip("Start 시 파일 있으면 자동 로드")]
        public bool autoLoadOnStart = true;

        [Tooltip("WP 추가될 때마다 즉시 자동 저장")]
        public bool autoSaveOnAdd = true;

        [Tooltip("저장 시 이전 파일을 .bak으로 백업")]
        public bool createBackup = true;

        [Tooltip("Console 로그 출력")]
        public bool enableLog = true;

        // ── 내부 ──────────────────────────────────────────────
        // 마지막으로 본 waypoint 개수 (변화 감지용)
        int lastSeenCount = 0;

        /// <summary>전체 파일 경로 (디버그용)</summary>
        public string FilePath => Path.Combine(Application.persistentDataPath, folderName, fileName);

        void Start()
        {
            if (recorder == null)
            {
                Debug.LogError("[Storage] WaypointRecorder가 연결되지 않았습니다!");
                return;
            }

            if (enableLog)
            {
                Debug.Log($"[Storage] 저장 위치: {FilePath}");
            }

            // 폴더 없으면 생성
            EnsureFolderExists();

            // 자동 로드
            if (autoLoadOnStart)
            {
                Load();
            }

            lastSeenCount = recorder.Count;
        }

        void Update()
        {
            // 자동 저장: Recorder의 WP 개수가 늘어나면 즉시 저장
            if (!autoSaveOnAdd) return;
            if (recorder == null) return;

            if (recorder.Count > lastSeenCount)
            {
                // 새 WP가 추가됨
                Save();
                lastSeenCount = recorder.Count;
            }
            else if (recorder.Count < lastSeenCount)
            {
                // 삭제 발생 시에도 저장 (RemoveLast, ClearAll 등)
                Save();
                lastSeenCount = recorder.Count;
            }
        }

        // ── 저장 ──────────────────────────────────────────────
        /// <summary>현재 Recorder의 모든 waypoint를 JSON 파일에 저장</summary>
        [ContextMenu("Save Now")]
        public void Save()
        {
            if (recorder == null) return;

            try
            {
                EnsureFolderExists();

                // 백업: 이전 파일이 있으면 .bak으로
                if (createBackup && File.Exists(FilePath))
                {
                    string bakPath = FilePath + ".bak";
                    if (File.Exists(bakPath)) File.Delete(bakPath);
                    File.Copy(FilePath, bakPath);
                }

                // 데이터 패키징
                var saveData = new SaveData
                {
                    version = "1.1",
                    savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    robotInfo = "Fairino FR5",
                    waypoints = new List<Waypoint>(recorder.Waypoints)
                };

                // JSON 직렬화 (Unity 내장 JsonUtility 사용)
                string json = JsonUtility.ToJson(saveData, prettyPrint: true);

                // 파일에 쓰기
                File.WriteAllText(FilePath, json);

                if (enableLog)
                    Debug.Log($"[Storage] 💾 저장 완료: {recorder.Count}개 → {FilePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Storage] 저장 실패: {ex.Message}");
            }
        }

        // ── 로드 ──────────────────────────────────────────────
        /// <summary>JSON 파일에서 waypoint 불러와 Recorder에 복원</summary>
        [ContextMenu("Load Now")]
        public void Load()
        {
            if (recorder == null) return;

            try
            {
                if (!File.Exists(FilePath))
                {
                    if (enableLog) Debug.Log($"[Storage] 저장된 파일 없음: {FilePath}");
                    return;
                }

                string json = File.ReadAllText(FilePath);
                SaveData saveData = JsonUtility.FromJson<SaveData>(json);

                if (saveData == null || saveData.waypoints == null)
                {
                    Debug.LogWarning("[Storage] 파일 내용 비어있음");
                    return;
                }

                // 기존 데이터 비우고 로드한 거로 교체
                recorder.ClearAll();
                foreach (var wp in saveData.waypoints)
                {
                    recorder.AddWaypoint(wp);
                }

                lastSeenCount = recorder.Count;

                if (enableLog)
                {
                    Debug.Log($"[Storage] 📂 로드 완료: {recorder.Count}개 (저장 시각: {saveData.savedAt})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Storage] 로드 실패: {ex.Message}");
            }
        }

        // ── 백업 복원 ────────────────────────────────────────
        /// <summary>.bak 파일에서 복원 (실수로 잘못 저장한 경우)</summary>
        [ContextMenu("Restore From Backup")]
        public void RestoreFromBackup()
        {
            string bakPath = FilePath + ".bak";
            if (!File.Exists(bakPath))
            {
                Debug.LogWarning("[Storage] 백업 파일 없음");
                return;
            }

            try
            {
                File.Copy(bakPath, FilePath, overwrite: true);
                Load();
                if (enableLog) Debug.Log("[Storage] ↩️ 백업에서 복원 완료");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Storage] 복원 실패: {ex.Message}");
            }
        }

        // ── 파일 삭제 ────────────────────────────────────────
        /// <summary>저장된 파일 삭제 (Recorder는 안 비움)</summary>
        [ContextMenu("Delete Save File")]
        public void DeleteFile()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                    if (enableLog) Debug.Log($"[Storage] 🗑️ 파일 삭제: {FilePath}");
                }
                else
                {
                    if (enableLog) Debug.Log("[Storage] 삭제할 파일 없음");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Storage] 삭제 실패: {ex.Message}");
            }
        }

        // ── 헬퍼 ──────────────────────────────────────────────
        void EnsureFolderExists()
        {
            string folder = Path.Combine(Application.persistentDataPath, folderName);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        // ── 직렬화용 데이터 구조 ─────────────────────────────
        [Serializable]
        public class SaveData
        {
            public string version;
            public string savedAt;
            public string robotInfo;
            public List<Waypoint> waypoints;
        }
    }
}
