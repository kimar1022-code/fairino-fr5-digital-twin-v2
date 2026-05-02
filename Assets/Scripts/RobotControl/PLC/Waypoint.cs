using System;
using UnityEngine;

namespace RobotControl
{
    /// <summary>
    /// 로봇의 한 자세를 나타내는 데이터 구조.
    /// 조인트 각도 + TCP 위치 + 그리퍼 상태를 포함.
    /// 
    /// 사용 예:
    /// - Save Waypoint 버튼 누르면 현재 자세를 이 구조로 저장
    /// - List<Waypoint>로 모션 시퀀스 구성
    /// - JSON으로 직렬화하여 영구 저장 (나중에)
    /// </summary>
    [Serializable]
    public class Waypoint
    {
        public string name;          // "WP1", "WP2", ... (자동 부여)
        public float[] joints;       // J1~J6 (6개, 도 단위)
        public float tcpX, tcpY, tcpZ;        // TCP 위치 (mm)
        public float tcpRx, tcpRy, tcpRz;     // TCP 회전 (도)
        public float gripperOpen;    // 그리퍼 개폐 (0~100%)
        public int gripperSpeed = 50;   // 그리퍼 속도 (1~100, SDK 기본값 50)
        public int gripperForce = 50;   // 그리퍼 강도 (1~100, SDK 기본값 50)
        public string timestamp;     // 저장 시각 (ISO 8601)

        public Waypoint()
        {
            joints = new float[6];
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>한 줄 요약 (디버그/로그용)</summary>
        public string ToShortString()
        {
            return $"{name}: J=[{joints[0]:F1}, {joints[1]:F1}, {joints[2]:F1}, " +
                   $"{joints[3]:F1}, {joints[4]:F1}, {joints[5]:F1}], " +
                   $"TCP=({tcpX:F1}, {tcpY:F1}, {tcpZ:F1}), " +
                   $"Gripper={gripperOpen:F0}%";
        }

        /// <summary>두 줄 상세 표시 (UI용)</summary>
        public string ToDetailString()
        {
            return $"{name}\n" +
                   $"  J: [{joints[0]:F2}, {joints[1]:F2}, {joints[2]:F2}, {joints[3]:F2}, {joints[4]:F2}, {joints[5]:F2}]\n" +
                   $"  TCP: ({tcpX:F2}, {tcpY:F2}, {tcpZ:F2}, {tcpRx:F2}, {tcpRy:F2}, {tcpRz:F2})\n" +
                   $"  Gripper: {gripperOpen:F0}%, Time: {timestamp}";
        }
    }
}
