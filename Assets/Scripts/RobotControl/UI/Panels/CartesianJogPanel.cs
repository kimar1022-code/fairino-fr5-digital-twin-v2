using UnityEngine;

namespace RobotControl
{
    public class CartesianJogPanel : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private RobotManager robotManager;

        [Header("Jog 버튼 (12개 권장: X+/-, Y+/-, Z+/-, RX+/-, RY+/-, RZ+/-)")]
        [SerializeField] private JogButton[] jogButtons;

        void Start()
        {
            if (robotManager == null)
            {
                Debug.LogError("[CartesianJogPanel] robotManager 참조가 비어있음");
                return;
            }
            if (jogButtons == null)
            {
                Debug.LogError("[CartesianJogPanel] jogButtons 배열이 비어있음");
                return;
            }

            for (int i = 0; i < jogButtons.Length; i++)
            {
                JogButton btn = jogButtons[i];
                if (btn == null) continue;
                btn.OnJogStart += HandleJogStart;
                btn.OnJogStop += HandleJogStop;
            }
        }

        void HandleJogStart(int axis, int dir)
        {
            Debug.Log($"[CartJog] HandleJogStart axis={axis} dir={dir}");
            if (robotManager == null) return;
            robotManager.StartCartesianJog(axis, dir);
        }

        void HandleJogStop()
        {
            if (robotManager == null) return;
            robotManager.StopJog();
        }

        void OnDestroy()
        {
            if (jogButtons == null) return;
            for (int i = 0; i < jogButtons.Length; i++)
            {
                JogButton btn = jogButtons[i];
                if (btn == null) continue;
                btn.OnJogStart -= HandleJogStart;
                btn.OnJogStop -= HandleJogStop;
            }
        }
    }
}
