using System;
using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// Damped Least Squares (DLS) Inverse Kinematics 솔버 (6-DOF 회전 관절 전용).
    /// 
    /// ■ 알고리즘:
    ///   1. 현재 조인트 각도 → FK로 현재 TCP 포즈 계산 (Unity Transform 활용)
    ///   2. 목표 TCP와의 에러 계산 (위치 3D + 회전 3D, 총 6D)
    ///   3. Jacobian J 수치미분 (각 조인트 ±delta로 미세 회전시켜 TCP 변화 측정)
    ///   4. Δθ = J^T (J J^T + λ²I)^-1 e     ← DLS 공식
    ///   5. 조인트 각도 += Δθ, 반복
    /// 
    /// ■ 특징:
    ///   - 특이점 안정 (λ damping)
    ///   - 조인트 한계 고려 (clamp)
    ///   - 수렴 보장 (발산 방지)
    ///   - Unity Transform만 사용 (외부 수학 라이브러리 불필요)
    /// 
    /// ■ 한계:
    ///   - 수치 미분 기반이라 정확한 해석적 Jacobian보다 느림 (대신 URDF 무관하게 범용)
    ///   - IK 해가 없는 위치(팔 길이 초과 등)면 가까운 점으로 수렴
    /// </summary>
    public class InverseKinematicsSolver
    {
        // ── 설정 파라미터 ────────────────────────────────────────────
        /// <summary>DLS 감쇠 상수. 클수록 안정적이지만 수렴 느림. 0.05~0.3 권장.</summary>
        public float damping = 0.1f;

        /// <summary>한 번의 Solve 호출당 최대 반복 횟수.</summary>
        public int maxIterations = 10;

        /// <summary>위치 에러 허용치 (m). 이 이하로 떨어지면 수렴으로 간주.</summary>
        public float positionTolerance = 0.001f;   // 1mm

        /// <summary>회전 에러 허용치 (rad).</summary>
        public float rotationTolerance = 0.01f;    // 약 0.57도

        /// <summary>Jacobian 수치미분용 미세 각도 (rad).</summary>
        public float jacobianDelta = 0.001f;

        /// <summary>조인트 업데이트 최대 스텝 크기 (rad). 큰 에러에서 발산 방지.</summary>
        public float maxStepRad = 0.2f;            // 약 11.5도

        // ── 내부 ─────────────────────────────────────────────────────
        private readonly JointConfig[] joints;
        private readonly Transform tcpTransform;
        private readonly int n;

        public InverseKinematicsSolver(JointConfig[] joints, Transform tcpTransform)
        {
            this.joints = joints;
            this.tcpTransform = tcpTransform;
            this.n = joints.Length;
        }

        /// <summary>
        /// 목표 TCP 포즈(월드 기준)를 위해 필요한 조인트 각도를 계산.
        /// currentAngles를 입력 받아서 해를 찾으면 새 각도 배열을 반환.
        /// </summary>
        /// <summary>
        /// 목표 TCP 포즈(월드 기준)를 위해 필요한 조인트 각도를 계산.
        /// currentAngles를 입력 받아서 해를 찾으면 새 각도 배열을 반환.
        /// 
        /// ■ 중요: 이 함수는 Jacobian 계산을 위해 Transform을 일시적으로 조작하지만,
        ///         종료 시 반드시 원래 Transform 상태로 복원합니다 (ArticulationBody와 충돌 방지).
        /// </summary>
        public float[] Solve(float[] currentAngles, Vector3 targetPosition, Quaternion targetRotation)
        {
            float[] theta = new float[n];
            Array.Copy(currentAngles, theta, n);

            // 원래 Transform 상태 저장 (복원용)
            Quaternion[] savedRots = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                if (joints[i].jointTransform != null)
                    savedRots[i] = joints[i].jointTransform.localRotation;
            }

            for (int iter = 0; iter < maxIterations; iter++)
            {
                // 1) 현재 포즈 (FK)
                ApplyAngles(theta);
                Vector3 currPos = tcpTransform.position;
                Quaternion currRot = tcpTransform.rotation;

                // 2) 에러 6D (위치 + 회전)
                Vector3 posErr = targetPosition - currPos;
                Vector3 rotErr = QuatToRotVec(targetRotation * Quaternion.Inverse(currRot));

                float posMag = posErr.magnitude;
                float rotMag = rotErr.magnitude;

                // 수렴 검사
                if (posMag < positionTolerance && rotMag < rotationTolerance)
                    break;

                // 6D 에러 벡터 (x, y, z, rx, ry, rz)
                float[] e = { posErr.x, posErr.y, posErr.z, rotErr.x, rotErr.y, rotErr.z };

                // 3) Jacobian 6×n 계산 (수치미분)
                float[,] J = ComputeJacobian(theta);

                // 4) DLS: Δθ = J^T (J J^T + λ²I)^-1 e
                float[] deltaTheta = SolveDLS(J, e, damping);

                // 5) 스텝 제한 (발산 방지)
                float stepNorm = 0f;
                for (int i = 0; i < n; i++) stepNorm += deltaTheta[i] * deltaTheta[i];
                stepNorm = Mathf.Sqrt(stepNorm);
                if (stepNorm > maxStepRad)
                {
                    float scale = maxStepRad / stepNorm;
                    for (int i = 0; i < n; i++) deltaTheta[i] *= scale;
                }

                // 6) 조인트 업데이트 (rad → deg) + limit clamp
                for (int i = 0; i < n; i++)
                {
                    theta[i] += deltaTheta[i] * Mathf.Rad2Deg;
                    theta[i] = Mathf.Clamp(theta[i], joints[i].minAngle, joints[i].maxAngle);
                }
            }

            // 원래 Transform 상태 복원 (ArticulationBody/외부 시스템과 충돌 방지)
            for (int i = 0; i < n; i++)
            {
                if (joints[i].jointTransform != null)
                    joints[i].jointTransform.localRotation = savedRots[i];
            }

            return theta;
        }

        // ── Jacobian 수치미분 ────────────────────────────────────────
        float[,] ComputeJacobian(float[] theta)
        {
            float[,] J = new float[6, n];
            float deltaDeg = jacobianDelta * Mathf.Rad2Deg;

            // 기준 포즈
            ApplyAngles(theta);
            Vector3 basePos = tcpTransform.position;
            Quaternion baseRot = tcpTransform.rotation;

            // 각 조인트를 +delta 회전시켰을 때 TCP 변화
            for (int i = 0; i < n; i++)
            {
                float[] perturbed = (float[])theta.Clone();
                perturbed[i] += deltaDeg;
                ApplyAngles(perturbed);

                Vector3 newPos = tcpTransform.position;
                Quaternion newRot = tcpTransform.rotation;

                Vector3 dPos = (newPos - basePos) / jacobianDelta;
                Vector3 dRot = QuatToRotVec(newRot * Quaternion.Inverse(baseRot)) / jacobianDelta;

                J[0, i] = dPos.x; J[1, i] = dPos.y; J[2, i] = dPos.z;
                J[3, i] = dRot.x; J[4, i] = dRot.y; J[5, i] = dRot.z;
            }

            // 원래 상태로 복구
            ApplyAngles(theta);
            return J;
        }

        // ── Transform에 조인트 각도 적용 (FK) ────────────────────────
        // 주의: Jacobian 수치미분 중에 이 함수가 호출되어 일시적으로 Transform을 움직임.
        // 호출 후엔 반드시 원래 각도로 복구되어야 함 (ComputeJacobian 끝에서 처리).
        // 프레임 끝엔 ArticulationBody나 SimulatedRobotController가 자기 값으로 덮어쓰므로
        // 다음 프레임에 문제 없음.
        // 
        // URDF로 임포트된 로봇은 각 링크가 고유한 초기 localRotation을 가지므로,
        // 반드시 initialLocalRots * AngleAxis(current) 형태로 적용해야 함.
        void ApplyAngles(float[] anglesDeg)
        {
            if (initialLocalRots == null) return;
            for (int i = 0; i < n; i++)
            {
                if (joints[i].jointTransform == null) continue;
                float a = joints[i].invertSign ? -anglesDeg[i] : anglesDeg[i];
                joints[i].jointTransform.localRotation = initialLocalRots[i] *
                    Quaternion.AngleAxis(a, joints[i].rotationAxis);
            }
        }

        // 초기 회전 저장 (Awake 시점 localRotation)
        private Quaternion[] initialLocalRots;

        public void SetInitialRotations(Quaternion[] rots)
        {
            initialLocalRots = rots;
        }

        /// <summary>
        /// 외부에서 명시적으로 FK 적용을 요청하는 경우 (SimulatedRobotController의 Transform 모드 갱신 등).
        /// </summary>
        public void ApplyAnglesDirectly(float[] anglesDeg)
        {
            ApplyAngles(anglesDeg);
        }

        // ── 선형대수: DLS 풀이 ──────────────────────────────────────
        // Δθ = J^T (J J^T + λ²I)^-1 e
        // J: 6×n, e: 6, 결과: n
        // 6×6 역행렬은 작으니까 직접 구현 (Cramer's 대신 Gauss-Jordan)
        float[] SolveDLS(float[,] J, float[] e, float lambda)
        {
            // A = J J^T (6×6)
            float[,] A = new float[6, 6];
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    float s = 0;
                    for (int k = 0; k < n; k++) s += J[i, k] * J[j, k];
                    A[i, j] = s;
                    if (i == j) A[i, j] += lambda * lambda;
                }
            }

            // y = A^-1 e (6×6 선형시스템 풀이)
            float[] y = GaussJordanSolve(A, e, 6);
            if (y == null) return new float[n];  // 역행렬 실패 시 0

            // Δθ = J^T y
            float[] result = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = 0;
                for (int j = 0; j < 6; j++) s += J[j, i] * y[j];
                result[i] = s;
            }
            return result;
        }

        // Gauss-Jordan 풀이 (작은 6×6이라 성능 문제 없음)
        float[] GaussJordanSolve(float[,] A, float[] b, int size)
        {
            // 증강 행렬 [A | b]
            float[,] M = new float[size, size + 1];
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++) M[i, j] = A[i, j];
                M[i, size] = b[i];
            }

            for (int i = 0; i < size; i++)
            {
                // 피벗 선택
                int pivot = i;
                float maxVal = Mathf.Abs(M[i, i]);
                for (int k = i + 1; k < size; k++)
                {
                    if (Mathf.Abs(M[k, i]) > maxVal)
                    {
                        maxVal = Mathf.Abs(M[k, i]);
                        pivot = k;
                    }
                }
                if (maxVal < 1e-10f) return null;  // 특이 행렬

                if (pivot != i)
                    for (int j = 0; j <= size; j++)
                        (M[i, j], M[pivot, j]) = (M[pivot, j], M[i, j]);

                // 정규화
                float d = M[i, i];
                for (int j = 0; j <= size; j++) M[i, j] /= d;

                // 다른 행 소거
                for (int k = 0; k < size; k++)
                {
                    if (k == i) continue;
                    float f = M[k, i];
                    for (int j = 0; j <= size; j++) M[k, j] -= f * M[i, j];
                }
            }

            float[] x = new float[size];
            for (int i = 0; i < size; i++) x[i] = M[i, size];
            return x;
        }

        // ── Quaternion → 회전 벡터 (회전축 × 각도) ────────────────
        static Vector3 QuatToRotVec(Quaternion q)
        {
            q = q.normalized;
            // q = (axis * sin(θ/2), cos(θ/2))
            float w = Mathf.Clamp(q.w, -1f, 1f);
            float angle = 2f * Mathf.Acos(w);
            if (angle > Mathf.PI) angle -= 2f * Mathf.PI;

            float s = Mathf.Sqrt(1f - w * w);
            if (s < 1e-6f) return Vector3.zero;
            return new Vector3(q.x, q.y, q.z) * (angle / s);
        }
    }
}
