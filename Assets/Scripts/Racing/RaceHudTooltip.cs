using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class RaceHudTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public string Caption;
    public Action<string> Show;
    public void OnPointerEnter(PointerEventData data)
    {
        if (data.pointerId < 0) Show?.Invoke(Caption);
    }
    public void OnPointerExit(PointerEventData data) { Show?.Invoke(string.Empty); }
    private void OnDisable() { Show?.Invoke(string.Empty); }
}
