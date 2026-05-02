using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// 시뮬 모델과 실로봇의 "0도 기준점"이 다를 때, 특정 조인트의 Visual 자식을
    /// 회전시켜서 시각적으로 일치시키는 스크립트.
    /// 
    /// 사용법:
    /// 1. J5 오브젝트(또는 보정하고 싶은 조인트)에 컴포넌트 추가
    /// 2. Visual Child에 회전시킬 자식 Transform 드래그 (예: J5 아래의 Visuals)
    /// 3. Rotation Offset에 보정 각도 입력 (예: -90, 0, 0)
    /// 4. Play 누르면 자동으로 Visual 자식에 offset 적용
    /// 
    /// 장점: J5 관절 자체의 물리 동작은 그대로, 시각적 모양만 맞춤
    /// </summary>
    public class JointVisualCalibrator : MonoBehaviour
    {
        [Tooltip("회전시킬 자식 Transform (보통 Visuals 또는 visual 오브젝트)")]
        public Transform visualChild;

        [Tooltip("추가할 회전 오프셋 (Euler Angles, 단위: 도)")]
        public Vector3 rotationOffset = new Vector3(-90f, 0f, 0f);

        [Tooltip("Play 시작 시 자동 적용")]
        public bool applyOnStart = true;

        Quaternion originalRotation;
        bool applied = false;

        void Awake()
        {
            // visualChild가 비어있으면 "Visuals" 또는 "visual" 이름의 자식 자동 검색
            if (visualChild == null)
            {
                var v = transform.Find("Visuals");
                if (v == null) v = transform.Find("visual");
                if (v == null) v = transform.Find("Visual");
                if (v != null) visualChild = v;
            }

            if (visualChild != null)
                originalRotation = visualChild.localRotation;
        }

        void Start()
        {
            if (applyOnStart) ApplyOffset();
        }

        /// <summary>Visual 자식에 rotationOffset을 적용</summary>
        [ContextMenu("Apply Offset")]
        public void ApplyOffset()
        {
            if (visualChild == null)
            {
                Debug.LogWarning($"[JointVisualCalibrator] {name}: Visual Child가 지정되지 않았습니다.");
                return;
            }
            visualChild.localRotation = originalRotation * Quaternion.Euler(rotationOffset);
            applied = true;
        }

        /// <summary>원래 회전으로 복원</summary>
        [ContextMenu("Reset Offset")]
        public void ResetOffset()
        {
            if (visualChild == null) return;
            visualChild.localRotation = originalRotation;
            applied = false;
        }

        void OnValidate()
        {
            // 에디터에서 값 변경 시 실시간 미리보기 (Play 중에만)
            if (Application.isPlaying && applied && visualChild != null)
                visualChild.localRotation = originalRotation * Quaternion.Euler(rotationOffset);
        }
    }
}
