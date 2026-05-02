using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// 그리퍼 시각 제어 스크립트 (재설계 버전).
    /// 
    /// ■ 동작 방식:
    ///   - 초기 위치(씬에 설정된 손가락 위치)를 "100% 열림"으로 고정
    ///   - 닫힐 때는 Close Distance 만큼 Move Axis 방향으로 이동
    ///   - 0% (완전 닫힘) ~ 100% (완전 열림) 퍼센트로 제어
    /// 
    /// ■ 설정 분리:
    ///   - Move Axis: 닫힐 때 이동하는 "방향"만 정의 (단위 벡터)
    ///   - Close Distance: 닫힐 때 이동하는 "거리"만 정의
    ///   - 두 값을 독립적으로 조정 가능
    /// </summary>
    public class GripperController : MonoBehaviour
    {
        public enum GripperType { Hinge, Parallel }

        [Header("그리퍼 타입")]
        public GripperType gripperType = GripperType.Parallel;

        [Header("손가락 Transform (Pivot 오브젝트)")]
        public Transform fingerL;
        public Transform fingerR;

        // ── Hinge (회전형) ───────────────────────────────────────────
        [Header("■ Hinge 방식 (회전형)")]
        [Tooltip("닫힐 때 회전하는 축 (로컬). 퍼센트가 0이 될 때 이 축을 따라 maxCloseAngle만큼 회전")]
        public Vector3 fingerLCloseAxis = Vector3.forward;
        public Vector3 fingerRCloseAxis = Vector3.forward;
        [Tooltip("100%(열림) → 0%(닫힘) 시 회전하는 각도 (degree)")]
        public float maxCloseAngle = 45f;

        // ── Parallel (평행 이동형) ──────────────────────────────────
        [Header("■ Parallel 방식 (평행 이동)")]
        [Tooltip("Finger L이 닫힐 때 이동하는 방향 (단위 벡터). 크기는 무시되고 방향만 사용됨.")]
        public Vector3 fingerLCloseDirection = new Vector3(-1, 0, 0);
        [Tooltip("Finger R이 닫힐 때 이동하는 방향 (단위 벡터). 일반적으로 L의 반대 방향.")]
        public Vector3 fingerRCloseDirection = new Vector3(1, 0, 0);

        [Header("■ 닫힘 거리 (Move Axis와 분리)")]
        [Tooltip("100%(열림)에서 0%(닫힘)까지 각 손가락이 이동하는 거리 (로컬 단위)")]
        public float closeDistance = 50f;

        // ── 현재 상태 ────────────────────────────────────────────────
        [Header("현재 상태")]
        [Range(0f, 100f)]
        [Tooltip("0% = 완전 닫힘 (두 손가락이 가운데서 만남) / 100% = 완전 열림 (초기 위치)")]
        public float openPercent = 100f;

        // ── 내부 상태 (초기 Transform 저장) ─────────────────────────
        private Quaternion fingerLInitialRot, fingerRInitialRot;
        private Vector3 fingerLInitialPos, fingerRInitialPos;
        private bool initialized = false;

        void Awake() { CacheInitial(); }

        void CacheInitial()
        {
            if (fingerL != null)
            {
                fingerLInitialRot = fingerL.localRotation;
                fingerLInitialPos = fingerL.localPosition;
            }
            if (fingerR != null)
            {
                fingerRInitialRot = fingerR.localRotation;
                fingerRInitialPos = fingerR.localPosition;
            }
            initialized = true;
        }

        void Update()
        {
            if (!initialized) CacheInitial();
            Apply();
        }

        // ── Public API ───────────────────────────────────────────────
        /// <summary>0~100 퍼센트로 그리퍼 개폐 설정. 0=닫힘, 100=열림.</summary>
        public void SetOpenPercent(float percent)
        {
            openPercent = Mathf.Clamp(percent, 0f, 100f);
        }

        /// <summary>0~1 비율로 그리퍼 개폐 설정 (기존 API 호환). 0=닫힘, 1=열림.</summary>
        public void SetOpenValue(float normalized)
        {
            openPercent = Mathf.Clamp01(normalized) * 100f;
        }

        /// <summary>현재 열린 정도를 0~1 비율로 반환.</summary>
        public float GetOpenValue() => openPercent / 100f;

        public void Open() => SetOpenPercent(100f);
        public void Close() => SetOpenPercent(0f);

        // ── 핵심 적용 로직 ────────────────────────────────────────────
        void Apply()
        {
            // closeRatio = 0 (완전 열림) ~ 1 (완전 닫힘)
            // openPercent=100 → closeRatio=0 → 초기 위치 유지
            // openPercent=0   → closeRatio=1 → closeDistance만큼 이동
            float closeRatio = 1f - (openPercent / 100f);

            if (gripperType == GripperType.Hinge)
            {
                if (fingerL != null)
                    fingerL.localRotation = fingerLInitialRot *
                        Quaternion.AngleAxis(closeRatio * maxCloseAngle, fingerLCloseAxis);

                if (fingerR != null)
                    fingerR.localRotation = fingerRInitialRot *
                        Quaternion.AngleAxis(closeRatio * maxCloseAngle, fingerRCloseAxis);
            }
            else // Parallel
            {
                if (fingerL != null)
                    fingerL.localPosition = fingerLInitialPos +
                        fingerLCloseDirection.normalized * closeRatio * closeDistance;

                if (fingerR != null)
                    fingerR.localPosition = fingerRInitialPos +
                        fingerRCloseDirection.normalized * closeRatio * closeDistance;
            }
        }
    }
}
