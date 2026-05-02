using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// 좌표계 매핑 검증 도구.
    /// 실로봇 연결 전에 Unity 시뮬과 실로봇의 좌표가 일치하는지 확인하는 용도.
    /// 
    /// ■ 사용 순서:
    ///   1. 실로봇을 티치펜던트로 특정 포즈로 이동 (예: 홈 포즈)
    ///   2. 티치펜던트에서 TCP 포즈 값 기록 (X/Y/Z/Rx/Ry/Rz)
    ///   3. Unity도 같은 조인트 각도로 이동
    ///   4. Inspector의 Expected Pose에 티치펜던트 값 입력
    ///   5. Play 중 Inspector에서 Diff 값 확인
    ///      - Diff가 0에 가까우면: 좌표 매핑 OK
    ///      - Diff가 크면: 축 매핑 재조정 필요 (CoordinateConverter.cs 수정)
    /// 
    /// ■ 권장 검증 포즈:
    ///   - 홈 포즈: J1=0, J2=-90, J3=0, J4=-90, J5=90, J6=0
    ///   - 티치펜던트에서 기준 포즈의 정확한 X/Y/Z/Rx/Ry/Rz를 메모
    /// </summary>
    public class CoordinateCalibrator : MonoBehaviour
    {
        [Header("검증 대상")]
        public SimulatedRobotController simController;

        [Header("■ 실로봇 기준값 (티치펜던트에서 읽은 값)")]
        [Tooltip("검증할 자세에서 실로봇 티치펜던트의 TCP 포즈 값")]
        public CartesianPose expectedPose;

        [Header("■ 현재 Unity 계산값 (자동 갱신)")]
        [ReadOnly] public CartesianPose currentUnityPose;

        [Header("■ 차이 (0에 가까울수록 매핑 정확)")]
        [ReadOnly] public Vector3 positionDiffMm;
        [ReadOnly] public Vector3 rotationDiffDeg;

        [Header("허용 오차")]
        public float positionTolMm = 5f;       // 5mm 이내
        public float rotationTolDeg = 2f;      // 2도 이내

        [Header("결과")]
        [ReadOnly] public string calibrationStatus = "Not verified";

        void Update()
        {
            if (simController == null) return;

            currentUnityPose = simController.GetCurrentTCPPose();

            positionDiffMm = new Vector3(
                currentUnityPose.x - expectedPose.x,
                currentUnityPose.y - expectedPose.y,
                currentUnityPose.z - expectedPose.z
            );
            rotationDiffDeg = new Vector3(
                Mathf.DeltaAngle(expectedPose.rx, currentUnityPose.rx),
                Mathf.DeltaAngle(expectedPose.ry, currentUnityPose.ry),
                Mathf.DeltaAngle(expectedPose.rz, currentUnityPose.rz)
            );

            bool posOK = positionDiffMm.magnitude < positionTolMm;
            bool rotOK = Mathf.Abs(rotationDiffDeg.x) < rotationTolDeg
                      && Mathf.Abs(rotationDiffDeg.y) < rotationTolDeg
                      && Mathf.Abs(rotationDiffDeg.z) < rotationTolDeg;

            if (posOK && rotOK) calibrationStatus = "✅ OK - 매핑 정확";
            else if (posOK) calibrationStatus = "⚠ 위치 OK, 회전 불일치 - 축 매핑 확인";
            else if (rotOK) calibrationStatus = "⚠ 회전 OK, 위치 불일치 - TCP Transform 위치 또는 축 스왑 확인";
            else calibrationStatus = "❌ 위치/회전 모두 불일치 - CoordinateConverter 재조정 필요";
        }
    }

    /// <summary>Inspector에서 필드를 읽기 전용으로 표시.</summary>
    public class ReadOnlyAttribute : PropertyAttribute { }
}

#if UNITY_EDITOR
namespace RobotControl.Editor
{
    [UnityEditor.CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect pos, UnityEditor.SerializedProperty prop, GUIContent label)
        {
            GUI.enabled = false;
            UnityEditor.EditorGUI.PropertyField(pos, prop, label, true);
            GUI.enabled = true;
        }
    }
}
#endif
