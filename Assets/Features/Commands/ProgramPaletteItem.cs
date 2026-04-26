using UnityEngine;
using UnityEngine.EventSystems;

public class ProgramPaletteItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private ProgramBuilder programBuilder;
    [SerializeField] private ProgramBlockView blockPrefab;

    private ProgramBlockDragHandler activeDragHandler;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (programBuilder == null || blockPrefab == null)
        {
            return;
        }

        activeDragHandler = programBuilder.SpawnPaletteBlock(blockPrefab);
        if (activeDragHandler == null)
        {
            return;
        }

        activeDragHandler.OnBeginDrag(eventData);
        activeDragHandler.OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (activeDragHandler == null)
        {
            return;
        }

        activeDragHandler.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (activeDragHandler == null)
        {
            return;
        }

        activeDragHandler.OnEndDrag(eventData);
        activeDragHandler = null;
    }
}
