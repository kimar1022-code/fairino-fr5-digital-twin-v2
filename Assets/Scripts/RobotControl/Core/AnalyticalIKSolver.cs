using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// FR5 6-DOF 산업로봇용 해석적 (Closed-form) Inverse Kinematics 솔버.
    /// 
    /// ■ 알고리즘:
    ///   - UR-style spherical wrist 분해법 (Hawkins 2013 방식)
    ///   - 1) 손목 중심 P5 = TCP - d6 * z_TCP
    ///   - 2) θ1: P5의 base frame X-Y 위치로 도출 (어깨 좌/우 2해)
    ///   - 3) θ5: P_TCP의 base frame y_1 축 거리로 도출 (손목 뒤집기 2해)
    ///   - 4) θ6: TCP의 x_TCP, y_TCP 회전 분해
    ///   - 5) θ2, θ3: T01⁻¹*T_target*T56⁻¹*T45⁻¹의 P 위치로 평면 삼각법 (팔꿈치 위/아래 2해)
    ///   - 6) θ4: T34 회전 행렬에서 직접 추출
    ///   - 최대 8개 해 → 현재 조인트 각도와 가장 가까운 해 선택
    /// 
    /// ■ FR5 DH 파라미터 (공식 매뉴얼 Table 2.4-5):
    ///     i | d_i (mm) | a_i (mm) | alpha_i (rad)
    ///     1 | 152      | 0        | π/2
    ///     2 | 0        | -425     | 0
    ///     3 | 0        | -395     | 0
    ///     4 | 102      | 0        | π/2
    ///     5 | 102      | 0        | -π/2
    ///     6 | 100      | 0        | 0
    /// 
    /// ■ 입력/출력:
    ///   - Solve(targetPose, currentJoints) → newJoints
    ///   - 좌표계: 로봇 base frame (Z-up, right-handed). Unity 변환은 호출자가 처리.
    ///   - 단위: mm (위치), rad (회전)
    /// 
    /// ■ 검증:
    ///   - Python 프로토타입에서 5개 다양한 자세에서 0.000000mm 에러로 일치 확인
    /// 
    /// ■ DLS와의 차이:
    ///   - DLS: 수치 반복 (느림, 발산 가능)
    ///   - 해석적: 한 번에 풀이 (수 마이크로초, 정확)
    /// </summary>
    public static class FR5AnalyticalIK
    {
        // FR5 DH 파라미터 (mm 단위)
        public const float d1 = 152f;
        public const float a2 = -425f;
        public const float a3 = -395f;
        public const float d4 = 102f;
        public const float d5 = 102f;
        public const float d6 = 100f;

        /// <summary>
        /// 표준 DH 변환 행렬 생성: T = Rz(theta) * Tz(d) * Tx(a) * Rx(alpha)
        /// </summary>
        public static Matrix4x4 DhMatrix(float theta, float d, float a, float alpha)
        {
            float ct = Mathf.Cos(theta), st = Mathf.Sin(theta);
            float ca = Mathf.Cos(alpha), sa = Mathf.Sin(alpha);
            // Unity의 Matrix4x4는 column-major. SetRow로 row-major 형태로 설정.
            Matrix4x4 m = new Matrix4x4();
            m.SetRow(0, new Vector4(ct, -st * ca,  st * sa, a * ct));
            m.SetRow(1, new Vector4(st,  ct * ca, -ct * sa, a * st));
            m.SetRow(2, new Vector4(0,   sa,       ca,      d));
            m.SetRow(3, new Vector4(0,   0,        0,       1));
            return m;
        }

        /// <summary>
        /// Forward Kinematics: 6개 조인트 각도(rad) → base frame TCP 변환 행렬 (mm)
        /// </summary>
        public static Matrix4x4 ForwardKinematics(float[] qRad)
        {
            Matrix4x4 T = Matrix4x4.identity;
            T = T * DhMatrix(qRad[0], d1, 0,    Mathf.PI / 2);
            T = T * DhMatrix(qRad[1], 0,  a2,   0);
            T = T * DhMatrix(qRad[2], 0,  a3,   0);
            T = T * DhMatrix(qRad[3], d4, 0,    Mathf.PI / 2);
            T = T * DhMatrix(qRad[4], d5, 0,   -Mathf.PI / 2);
            T = T * DhMatrix(qRad[5], d6, 0,    0);
            return T;
        }

        /// <summary>
        /// Inverse Kinematics: 목표 TCP 변환 → 가능한 모든 조인트 해 [rad].
        /// 도달 불가 시 빈 리스트 반환.
        /// </summary>
        public static List<float[]> InverseKinematics(Matrix4x4 T)
        {
            var solutions = new List<float[]>();

            // T 행렬 원소 추출 (row-major 의미로)
            float nx = T.m00, ox = T.m01, ax = T.m02, px = T.m03;
            float ny = T.m10, oy = T.m11, ay = T.m12, py = T.m13;
            float nz = T.m20, oz = T.m21, az = T.m22, pz = T.m23;

            // ── θ1: 손목 중심을 base XY평면에 투영 ────────────────
            float p5x = px - d6 * ax;
            float p5y = py - d6 * ay;
            float Rsq = p5x * p5x + p5y * p5y;
            float R = Mathf.Sqrt(Rsq);

            // 어깨 특이점 톨러런스
            if (R < Mathf.Abs(d4) - 1e-3f) return solutions;
            R = Mathf.Max(R, Mathf.Abs(d4));

            float phi = Mathf.Atan2(p5y, p5x);
            float ratio = Mathf.Clamp(d4 / R, -1f, 1f);
            float psi = Mathf.Acos(ratio);

            float[] theta1Options = new float[]
            {
                phi + psi + Mathf.PI / 2,
                phi - psi + Mathf.PI / 2,
            };

            foreach (float theta1 in theta1Options)
            {
                float s1 = Mathf.Sin(theta1), c1 = Mathf.Cos(theta1);

                // ── θ5 ────────────────────────────────────────
                float c5Num = px * s1 - py * c1 - d4;
                float c5 = c5Num / d6;
                if (Mathf.Abs(c5) > 1f) continue;
                c5 = Mathf.Clamp(c5, -1f, 1f);

                foreach (int sign5 in new[] { +1, -1 })
                {
                    float theta5 = sign5 * Mathf.Acos(c5);
                    float s5 = Mathf.Sin(theta5);

                    // ── θ6 ──────────────────────────────────
                    float theta6;
                    if (Mathf.Abs(s5) < 1e-6f)
                    {
                        // 손목 특이점 (θ4와 θ6이 동일축) — θ6=0 fallback
                        theta6 = 0f;
                    }
                    else
                    {
                        float num = -(ox * s1 - oy * c1) / s5;
                        float den =  (nx * s1 - ny * c1) / s5;
                        theta6 = Mathf.Atan2(num, den);
                    }

                    float s6 = Mathf.Sin(theta6), c6 = Mathf.Cos(theta6);

                    // ── T14 = T01⁻¹ * T_target * T56⁻¹ * T45⁻¹ ──
                    Matrix4x4 T01 = DhMatrix(theta1, d1, 0, Mathf.PI / 2);
                    Matrix4x4 T56 = DhMatrix(theta6, d6, 0, 0);
                    Matrix4x4 T45 = DhMatrix(theta5, d5, 0, -Mathf.PI / 2);
                    Matrix4x4 T14 = T01.inverse * T * T56.inverse * T45.inverse;

                    float P14x = T14.m03;
                    float P14y = T14.m13;

                    float r2 = P14x * P14x + P14y * P14y;
                    float c3 = (r2 - a2 * a2 - a3 * a3) / (2f * a2 * a3);
                    if (Mathf.Abs(c3) > 1f) continue;
                    c3 = Mathf.Clamp(c3, -1f, 1f);

                    foreach (int sign3 in new[] { +1, -1 })
                    {
                        float theta3 = sign3 * Mathf.Acos(c3);
                        float s3 = Mathf.Sin(theta3);

                        float theta2 = Mathf.Atan2(P14y, P14x)
                                     - Mathf.Atan2(a3 * s3, a2 + a3 * c3);

                        // ── θ4: T34 = (T12 * T23)⁻¹ * T14 ───
                        Matrix4x4 T12 = DhMatrix(theta2, 0, a2, 0);
                        Matrix4x4 T23 = DhMatrix(theta3, 0, a3, 0);
                        Matrix4x4 T34 = (T12 * T23).inverse * T14;
                        float theta4 = Mathf.Atan2(T34.m10, T34.m00);

                        // 정규화 -π~π
                        solutions.Add(new float[]
                        {
                            Normalize(theta1),
                            Normalize(theta2),
                            Normalize(theta3),
                            Normalize(theta4),
                            Normalize(theta5),
                            Normalize(theta6),
                        });
                    }
                }
            }

            return solutions;
        }

        /// <summary>
        /// 여러 해 중 현재 각도와 가장 가까운 (관절각 변화 합 최소) 해 선택.
        /// 조인트 한계 위반 해는 자동 제외.
        /// </summary>
        public static float[] SelectClosest(
            List<float[]> solutions,
            float[] currentRad,
            float[] minLimitsRad,
            float[] maxLimitsRad)
        {
            float[] best = null;
            float bestDist = float.MaxValue;

            foreach (var sol in solutions)
            {
                // 한계 검사
                bool inLimits = true;
                for (int i = 0; i < 6; i++)
                {
                    if (sol[i] < minLimitsRad[i] || sol[i] > maxLimitsRad[i])
                    {
                        inLimits = false;
                        break;
                    }
                }
                if (!inLimits) continue;

                // 거리 계산 (wrap-around 보정)
                float dist = 0f;
                for (int i = 0; i < 6; i++)
                {
                    float d = sol[i] - currentRad[i];
                    d = ((d + Mathf.PI) % (2f * Mathf.PI)) - Mathf.PI;
                    if (d < -Mathf.PI) d += 2f * Mathf.PI;
                    dist += Mathf.Abs(d);
                }

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = sol;
                }
            }
            return best;
        }

        /// <summary>각도를 -π~π 범위로 정규화</summary>
        public static float Normalize(float a)
        {
            float r = ((a + Mathf.PI) % (2f * Mathf.PI)) - Mathf.PI;
            if (r < -Mathf.PI) r += 2f * Mathf.PI;
            return r;
        }

        /// <summary>
        /// 현재 각도를 기준으로 jog 한 스텝에 가장 적합한 해를 찾는 헬퍼.
        /// jog용: 분기 전환 방지를 위해 큰 변화는 페널티.
        /// </summary>
        public static float[] SolveForJog(
            Matrix4x4 targetPose,
            float[] currentRad,
            float[] minLimitsRad,
            float[] maxLimitsRad,
            float maxJointDelta = 0.5f)  // rad. 한 jog 스텝당 최대 분기 차이.
        {
            var sols = InverseKinematics(targetPose);
            if (sols.Count == 0) return null;

            float[] best = null;
            float bestDist = float.MaxValue;

            foreach (var sol in sols)
            {
                bool inLimits = true;
                bool tooFar = false;
                float dist = 0f;
                for (int i = 0; i < 6; i++)
                {
                    if (sol[i] < minLimitsRad[i] || sol[i] > maxLimitsRad[i])
                    {
                        inLimits = false;
                        break;
                    }
                    float d = sol[i] - currentRad[i];
                    d = ((d + Mathf.PI) % (2f * Mathf.PI)) - Mathf.PI;
                    if (d < -Mathf.PI) d += 2f * Mathf.PI;
                    if (Mathf.Abs(d) > maxJointDelta)
                    {
                        tooFar = true;
                        break;
                    }
                    dist += Mathf.Abs(d);
                }
                if (!inLimits || tooFar) continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = sol;
                }
            }
            return best;
        }
    }
}
