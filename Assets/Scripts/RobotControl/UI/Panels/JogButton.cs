using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RobotControl
{
    public class JogButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("축 설정")]
        [SerializeField, Range(0, 5)] private int axis;
        [SerializeField] private int dir = 1;

        public event Action<int, int> OnJogStart;
        public event Action OnJogStop;

        public void OnPointerDown(PointerEventData eventData)
        {
            OnJogStart?.Invoke(axis, dir);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            OnJogStop?.Invoke();
        }
    }
}
