using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Tooltip Content")]
    [SerializeField] private string tooltipHeader;
    [TextArea(2, 6)]
    [SerializeField] private string tooltipText;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipHandler.Instance == null)
        {
            return;
        }

        TooltipHandler.Instance.ShowFrom(this, tooltipHeader, tooltipText);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipHandler.Instance?.HideFrom(this);
    }

    private void OnDisable()
    {
        TooltipHandler.Instance?.HideFrom(this);
    }
}