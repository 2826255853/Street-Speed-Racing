using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class RaceHoldControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Action<bool> Changed;
    public System.Func<bool> Supported;
    private int? activePointer;

    public void OnPointerDown(PointerEventData data)
    {
        Selectable selectable = GetComponent<Selectable>();
        if (activePointer.HasValue || (selectable != null && !selectable.IsInteractable()) ||
            (Supported != null && !Supported())) return;
        activePointer = data.pointerId;
        Changed?.Invoke(true);
    }

    public void OnPointerUp(PointerEventData data)
    {
        if (activePointer == data.pointerId) Release();
    }

    public void OnPointerExit(PointerEventData data)
    {
        if (activePointer == data.pointerId) Release();
    }

    public void Release()
    {
        if (!activePointer.HasValue) return;
        activePointer = null;
        Changed?.Invoke(false);
    }

    private void OnDisable() { Release(); }
    private void OnApplicationFocus(bool focused) { if (!focused) Release(); }
    private void OnApplicationPause(bool paused) { if (paused) Release(); }
}
