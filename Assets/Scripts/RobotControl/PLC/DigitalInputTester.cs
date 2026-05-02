using UnityEngine;
using System.Collections;

namespace RobotControl
{
    /// <summary>
    /// Fairino 디지털 입력 (DI) 테스트 스크립트.
    /// PLC 버튼 6개의 DI 신호를 읽어서 Console에 출력만 함.
    /// 검증용 — 어떤 버튼이 어떤 DI 핀에 연결됐는지 확인.
    /// 
    /// 사용법:
    /// 1. 빈 GameObject 생성하고 이 스크립트 부착
    /// 2. Inspector에서 Fairino Controller 드래그 (RobotRoot의 컴포넌트)
    /// 3. Play → Mirror 모드 또는 Real 모드 → Connect
    /// 4. PLC 버튼 누름 → Console에 어느 DI가 변했는지 표시
    /// </summary>
    public class DigitalInputTester : MonoBehaviour
    {
        [Header("참조")]
        public FairinoRobotController fairino;

        [Header("폴링 설정")]
        [Tooltip("DI 읽기 주기 (초). 0.1 = 10Hz")]
        public float pollInterval = 0.1f;

        [Tooltip("테스트할 DI 핀 개수. 16 = DI 0~7 + CI 0~7 모두")]
        [Range(1, 16)]
        public int diCount = 16;

        [Tooltip("상태 변화 시에만 로그 출력 (true 권장, 너무 시끄러우면 false)")]
        public bool logOnlyOnChange = true;

        // 이전 상태 저장 (변화 감지용)
        byte[] previousStates;
        bool isPolling = false;

        void Start()
        {
            previousStates = new byte[diCount];
            for (int i = 0; i < diCount; i++) previousStates[i] = 255; // 초기값 미정의

            StartCoroutine(PollDIRoutine());
        }

        IEnumerator PollDIRoutine()
        {
            // 연결될 때까지 대기
            while (fairino == null || !fairino.IsConnected)
            {
                yield return new WaitForSeconds(0.5f);
            }

            Debug.Log("[DITester] 폴링 시작! 각 PLC 버튼을 눌러보세요.");
            isPolling = true;

            while (isPolling)
            {
                ReadAndLogDI();
                yield return new WaitForSeconds(pollInterval);
            }
        }

        void ReadAndLogDI()
        {
#if FAIRINO_SDK
            if (fairino == null || !fairino.IsConnected) return;

            // FairinoRobotController 내부의 robot 객체에 접근하기 위해
            // public 메서드 ReadDI를 호출 (다음 단계에서 추가 필요)
            for (int i = 0; i < diCount; i++)
            {
                byte state = fairino.ReadDIPin(i);
                
                // 핀 이름 표시: 0~7은 "DI N", 8~15는 "CI N-8"
                string pinLabel = (i < 8) ? $"DI {i}" : $"CI {i - 8}";
                
                if (logOnlyOnChange)
                {
                    if (previousStates[i] != state && previousStates[i] != 255)
                    {
                        string change = (previousStates[i] == 0 && state == 1) ? "🟢 RISING (눌림)" :
                                       (previousStates[i] == 1 && state == 0) ? "🔴 FALLING (뗌)" : "변화";
                        Debug.Log($"[DITester] {pinLabel} {change}: {previousStates[i]} → {state}");
                    }
                }
                else
                {
                    Debug.Log($"[DITester] {pinLabel} = {state}");
                }
                
                previousStates[i] = state;
            }
#endif
        }

        void OnDestroy()
        {
            isPolling = false;
        }
    }
}
