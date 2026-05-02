using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// 조인트 하나의 설정. Sim/Real 공통.
    /// Fairino FR 시리즈 기본 limit을 프리셋으로 제공.
    /// </summary>
    [System.Serializable]
    public class JointConfig
    {
        [Tooltip("UI에 표시될 이름 (J1, Shoulder 등)")]
        public string name = "Joint";

        [Tooltip("최소 회전각 (degree)")]
        public float minAngle = -170f;

        [Tooltip("최대 회전각 (degree)")]
        public float maxAngle = 170f;

        [Tooltip("홈 포즈 각도 (degree)")]
        public float homeAngle = 0f;

        [Header("■ Simulation 전용 (ArticulationBody/Transform)")]
        [Tooltip("시뮬레이션에서 회전시킬 Transform (ArticulationBody면 같은 오브젝트)")]
        public Transform jointTransform;

        [Tooltip("로컬 회전축. Transform 모드에서만 사용. ArticulationBody는 ArticulationBody 자체의 anchorRotation 사용.")]
        public Vector3 rotationAxis = Vector3.up;

        [Tooltip("시뮬레이션 → 실로봇 각도 부호 뒤집기. 모델의 회전 방향이 반대일 때 사용.")]
        public bool invertSign = false;
    }

    /// <summary>
    /// Fairino FR 시리즈 표준 6축 프리셋.
    /// 실제 모델(FR3/FR5/FR10 등)에 따라 미세 조정 필요.
    /// </summary>
    public static class FairinoPresets
    {
        public static JointConfig[] GetDefault6Axis()
        {
            return new JointConfig[]
            {
                new JointConfig { name = "J1 (Base)",     minAngle = -170f, maxAngle = 170f, homeAngle = 0f },
                new JointConfig { name = "J2 (Shoulder)", minAngle = -170f, maxAngle = 80f,  homeAngle = -90f },
                new JointConfig { name = "J3 (Elbow)",    minAngle = -150f, maxAngle = 150f, homeAngle = 0f },
                new JointConfig { name = "J4 (Wrist1)",   minAngle = -170f, maxAngle = 170f, homeAngle = -90f },
                new JointConfig { name = "J5 (Wrist2)",   minAngle = -170f, maxAngle = 170f, homeAngle = 90f },
                new JointConfig { name = "J6 (Wrist3)",   minAngle = -360f, maxAngle = 360f, homeAngle = 0f },
            };
        }
    }
}
