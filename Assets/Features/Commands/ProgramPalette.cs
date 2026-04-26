using UnityEngine;

public class ProgramPalette : MonoBehaviour
{
    [SerializeField] private GameObject paletteContainer;
    [SerializeField] private GameObject deleteZoneContainer;
    [SerializeField] private CanvasGroup paletteCanvasGroup;
    [SerializeField] private CanvasGroup deleteZoneCanvasGroup;

    private bool isDeleteZoneActive;

    private void Awake()
    {
        if (paletteCanvasGroup == null && paletteContainer != null)
        {
            paletteCanvasGroup = paletteContainer.GetComponent<CanvasGroup>();
        }

        if (deleteZoneCanvasGroup == null && deleteZoneContainer != null)
        {
            deleteZoneCanvasGroup = deleteZoneContainer.GetComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        ProgramBlockDragHandler.DragStateChanged += HandleDragStateChanged;
        ProgramBlockDragHandler.DeleteZoneHoverChanged += HandleDeleteZoneHoverChanged;
        SetDraggingVisualState(false);
    }

    private void OnDisable()
    {
        ProgramBlockDragHandler.DragStateChanged -= HandleDragStateChanged;
        ProgramBlockDragHandler.DeleteZoneHoverChanged -= HandleDeleteZoneHoverChanged;
    }

    private void HandleDragStateChanged(bool isDragging)
    {
        SetDraggingVisualState(isDragging);
    }

    private void HandleDeleteZoneHoverChanged(bool isHovering)
    {
        if (!isDeleteZoneActive || deleteZoneCanvasGroup == null)
        {
            return;
        }

        deleteZoneCanvasGroup.alpha = isHovering ? 1f : 0.5f;
    }

    private void SetDraggingVisualState(bool isDragging)
    {
        // Important: keep palette GameObject active so in-progress drag callbacks
        // from ProgramPaletteItem are not interrupted mid-drag.
        if (paletteCanvasGroup != null)
        {
            paletteCanvasGroup.alpha = isDragging ? 0f : 1f;
            paletteCanvasGroup.blocksRaycasts = !isDragging;
            paletteCanvasGroup.interactable = !isDragging;
        }
        else if (paletteContainer != null)
        {
            paletteContainer.SetActive(true);
        }

        if (deleteZoneContainer != null)
        {
            deleteZoneContainer.SetActive(isDragging);
        }

        isDeleteZoneActive = isDragging;
        if (deleteZoneCanvasGroup != null)
        {
            deleteZoneCanvasGroup.alpha = isDragging ? 0.5f : 1f;
        }
    }
}
