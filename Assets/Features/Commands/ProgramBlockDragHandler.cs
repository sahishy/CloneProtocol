using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(ProgramBlockView))]
public class ProgramBlockDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const float GhostAlpha = 0.35f;
    private const float NormalAlpha = 1f;
    private const float HiddenWhileDeleteHoverAlpha = 0f;

    public static event Action<bool> DragStateChanged;
    public static event Action<bool> DeleteZoneHoverChanged;

    private static int activeDragCount;

    private ProgramBuilder builder;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    [Header("Layout Refresh")]
    [SerializeField] private VerticalLayoutGroup layoutGroupToIgnore;

    private bool spawnedFromPalette;
    private bool initialized;

    private Transform originalParent;
    private int originalSiblingIndex;
    private Vector2 originalAnchoredPosition;
    private bool isDragging;
    private bool hasPreviewPlacement;
    private bool hadPreviewPlacement;
    private bool isHoveringDeleteZone;
    private ProgramDropZone lastPreviewZone;

    public ProgramBlockView BlockView { get; private set; }

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        BlockView = GetComponent<ProgramBlockView>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (!initialized)
        {
            Initialize(FindFirstObjectByType<ProgramBuilder>(), spawnedFromPalette: false);
        }
    }

    public void Initialize(ProgramBuilder programBuilder, bool spawnedFromPalette)
    {
        builder = programBuilder;
        this.spawnedFromPalette = spawnedFromPalette;
        initialized = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!EnsureBuilder())
        {
            return;
        }

        CursorHandler.Instance.SetDragging();
        UIGameAudioHandler.Instance.PlayDragSound();
        SetDeleteHover(false);

        originalParent = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalAnchoredPosition = rectTransform.anchoredPosition;

        canvasGroup.blocksRaycasts = false;
        SetGhostMode(true);
        isDragging = true;
        hasPreviewPlacement = false;
        hadPreviewPlacement = false;
        lastPreviewZone = null;

        activeDragCount++;
        if (activeDragCount == 1)
        {
            DragStateChanged?.Invoke(true);
        }

        UpdatePreviewPlacement(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!EnsureBuilder())
        {
            return;
        }

        UpdatePreviewPlacement(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!EnsureBuilder())
        {
            canvasGroup.blocksRaycasts = true;
            SetGhostMode(false);
            SetDeleteHover(false);
            EndDragState();
            return;
        }

        CursorHandler.Instance.SetDefault(true);

        canvasGroup.blocksRaycasts = true;

        bool pointerOverDeleteZone = builder.IsPointerOverDeleteZone(eventData.position, eventData.pressEventCamera, transform);
        if (pointerOverDeleteZone)
        {
            SetDeleteHover(true);
            ProgramDropZone deleteZone = builder.FindDeleteDropZoneAtScreenPosition(eventData.position, eventData.pressEventCamera, transform);
            deleteZone?.InvokeDropEvent();
            EndDragState();
            Destroy(gameObject);
            return;
        }
        SetDeleteHover(false);

        UpdatePreviewPlacement(eventData);

        if (hasPreviewPlacement || hadPreviewPlacement)
        {
            RefreshCurrentPlacementLayout();
            lastPreviewZone?.InvokeDropEvent();
            SetGhostMode(false);
            spawnedFromPalette = false;
            EndDragState();
            return;
        }

        if (spawnedFromPalette)
        {
            if (builder.RootDropZone != null)
            {
                RectTransform root = builder.RootDropZone.ContentRoot;
                PlaceInto(builder.RootDropZone, root, root != null ? root.childCount : 0);
                builder.RootDropZone.InvokeDropEvent();
                SetGhostMode(false);
                spawnedFromPalette = false;
                EndDragState();
                return;
            }

            EndDragState();
            Destroy(gameObject);
            return;
        }

        if (originalParent != null)
        {
            transform.SetParent(originalParent, worldPositionStays: false);
            transform.SetSiblingIndex(Mathf.Clamp(originalSiblingIndex, 0, originalParent.childCount - 1));
            rectTransform.anchoredPosition = originalAnchoredPosition;
            SetGhostMode(false);
            EndDragState();
            return;
        }

        SetGhostMode(false);
        EndDragState();
    }

    private void OnDisable()
    {
        SetDeleteHover(false);
        EndDragState();
    }

    public void PlaceInto(ProgramDropZone zone, RectTransform parent, int siblingIndex)
    {
        transform.SetParent(parent, worldPositionStays: false);
        int clampedIndex = Mathf.Clamp(siblingIndex, 0, parent.childCount);
        transform.SetSiblingIndex(clampedIndex);
        rectTransform.anchoredPosition = Vector2.zero;

        RefreshLayoutForPlacement(parent);
    }

    private void RefreshCurrentPlacementLayout()
    {
        RectTransform parentRect = transform.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        RefreshLayoutForPlacement(parentRect);
    }

    private void RefreshLayoutForPlacement(RectTransform parent)
    {
        if (BlockView != null)
        {
            BlockView.UpdateSizeFitter();
        }

        ProgramBlockView[] parentBlocks = parent.GetComponentsInParent<ProgramBlockView>(includeInactive: true);
        for (int i = 0; i < parentBlocks.Length; i++)
        {
            parentBlocks[i].UpdateSizeFitter();
        }

        VerticalLayoutGroup[] layoutGroups = parent.GetComponentsInParent<VerticalLayoutGroup>(includeInactive: true);
        for (int i = 0; i < layoutGroups.Length; i++)
        {
            VerticalLayoutGroup layoutGroup = layoutGroups[i];
            if (layoutGroup == null || layoutGroup == layoutGroupToIgnore)
            {
                continue;
            }

            layoutGroup.enabled = false;
            layoutGroup.enabled = true;

            RectTransform layoutRect = layoutGroup.transform as RectTransform;
            if (layoutRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRect);
            }
        }

        RectTransform[] parentRects = parent.GetComponentsInParent<RectTransform>(includeInactive: true);
        for (int i = 0; i < parentRects.Length; i++)
        {
            RectTransform parentRect = parentRects[i];
            if (parentRect == null)
            {
                continue;
            }

            VerticalLayoutGroup maybeIgnored = parentRect.GetComponent<VerticalLayoutGroup>();
            if (maybeIgnored != null && maybeIgnored == layoutGroupToIgnore)
            {
                continue;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }
    }

    private bool EnsureBuilder()
    {
        if (builder != null)
        {
            return true;
        }

        builder = FindFirstObjectByType<ProgramBuilder>();
        if (builder == null)
        {
            return false;
        }

        return true;
    }

    private void EndDragState()
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        activeDragCount = Mathf.Max(0, activeDragCount - 1);
        if (activeDragCount == 0)
        {
            DragStateChanged?.Invoke(false);
        }
    }

    private void UpdatePreviewPlacement(PointerEventData eventData)
    {
        if (builder == null || BlockView == null)
        {
            hasPreviewPlacement = false;
            return;
        }

        bool pointerOverDeleteZone = builder.IsPointerOverDeleteZone(eventData.position, eventData.pressEventCamera, transform);
        if (pointerOverDeleteZone)
        {
            hasPreviewPlacement = false;
            canvasGroup.alpha = HiddenWhileDeleteHoverAlpha;
            SetDeleteHover(true);
            return;
        }
        SetDeleteHover(false);

        if (isDragging)
        {
            SetGhostMode(true);
        }

        ProgramDropZone insertionZone = builder.FindInsertionDropZoneAtScreenPosition(eventData.position, eventData.pressEventCamera, BlockView, transform);
        if (insertionZone == null)
        {
            if (!hadPreviewPlacement)
            {
                RefreshCurrentPlacementLayout();
            }
            hasPreviewPlacement = false;
            return;
        }

        RectTransform zoneRoot = insertionZone.ContentRoot;
        if (zoneRoot == null)
        {
            if (!hadPreviewPlacement)
            {
                RefreshCurrentPlacementLayout();
            }
            hasPreviewPlacement = false;
            return;
        }

        lastPreviewZone = insertionZone;

        int insertionIndex = insertionZone.GetInsertionIndexForScreenPoint(eventData.position, eventData.pressEventCamera, transform);

        bool parentChanged = transform.parent != zoneRoot;
        bool indexChanged = transform.GetSiblingIndex() != insertionIndex;
        if (parentChanged || indexChanged)
        {
            PlaceInto(insertionZone, zoneRoot, insertionIndex);
        }
        else if (!hadPreviewPlacement)
        {
            RefreshCurrentPlacementLayout();
        }

        hasPreviewPlacement = true;
        hadPreviewPlacement = true;
    }

    private void SetGhostMode(bool enabled)
    {
        canvasGroup.alpha = enabled ? GhostAlpha : NormalAlpha;
    }

    private void SetDeleteHover(bool isHovering)
    {
        if (isHoveringDeleteZone == isHovering)
        {
            return;
        }

        isHoveringDeleteZone = isHovering;
        DeleteZoneHoverChanged?.Invoke(isHoveringDeleteZone);
    }
}
