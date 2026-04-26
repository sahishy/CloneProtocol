using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class ProgramDropZone : MonoBehaviour
{
    [SerializeField] private UnityEvent onDrop;

    [Header("Hierarchy")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RectTransform dropBoundsRect;

    [Header("Accepted Blocks")]
    [SerializeField] private bool allowCommandBlocks = true;
    [SerializeField] private bool allowRepeatBlocks = true;

    [Header("Optional Behavior")]
    [SerializeField] private bool deleteDroppedBlocks;
    public bool DeleteDroppedBlocks => deleteDroppedBlocks;

    [Header("Empty Zone Assist")]
    [SerializeField, Min(0f)] private float emptyDropPadding = 40f;

    private RectTransform EffectiveContentRoot => contentRoot != null ? contentRoot : GetComponent<RectTransform>();
    private RectTransform EffectiveDropBounds => dropBoundsRect != null ? dropBoundsRect : EffectiveContentRoot;
    public RectTransform ContentRoot => EffectiveContentRoot;
    private ProgramBuilder cachedProgramBuilder;

    public void InvokeDropEvent()
    {
        onDrop?.Invoke();
    }

    public bool TryAcceptDrop(ProgramBlockDragHandler dragHandler, PointerEventData eventData)
    {
        if (dragHandler == null || eventData == null)
        {
            return false;
        }

        if (deleteDroppedBlocks)
        {
            InvokeDropEvent();
            Destroy(dragHandler.gameObject);
            return true;
        }

        ProgramBlockView blockView = dragHandler.BlockView;
        if (blockView == null || !AcceptsBlock(blockView))
        {
            return false;
        }

        InvokeDropEvent();
        RectTransform root = EffectiveContentRoot;
        int insertIndex = GetInsertionIndexForScreenPoint(eventData.position, eventData.pressEventCamera, dragHandler.transform);
        dragHandler.PlaceInto(this, root, insertIndex);
        return true;
    }

    private void OnTransformChildrenChanged()
    {
        if (cachedProgramBuilder == null)
        {
            cachedProgramBuilder = FindFirstObjectByType<ProgramBuilder>();
        }

        cachedProgramBuilder?.RefreshBlockIndices();
    }

    public int GetInsertionIndexForScreenPoint(Vector2 screenPosition, Camera eventCamera, Transform ignoreTransform = null)
    {
        RectTransform root = EffectiveContentRoot;
        return GetInsertionIndex(root, screenPosition, eventCamera, ignoreTransform);
    }

    public bool ContainsScreenPoint(Vector2 screenPosition, Camera eventCamera)
    {
        RectTransform bounds = EffectiveDropBounds;
        if (bounds == null)
        {
            return false;
        }

        if (!deleteDroppedBlocks && GetDirectBlockCount() == 0 && emptyDropPadding > 0f)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(bounds, screenPosition, eventCamera, out Vector2 localPoint))
            {
                Rect expanded = bounds.rect;
                expanded.xMin -= emptyDropPadding;
                expanded.xMax += emptyDropPadding;
                expanded.yMin -= emptyDropPadding;
                expanded.yMax += emptyDropPadding;
                return expanded.Contains(localPoint);
            }

            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(bounds, screenPosition, eventCamera);
    }

    public bool AcceptsBlock(ProgramBlockView block)
    {
        if (deleteDroppedBlocks)
        {
            return true;
        }

        if (block == null)
        {
            return false;
        }

        if (block.Kind == ProgramBlockView.BlockKind.Command)
        {
            return allowCommandBlocks;
        }

        if (IsNestedRepeatZone())
        {
            return false;
        }

        return allowRepeatBlocks;
    }

    private bool IsNestedRepeatZone()
    {
        ProgramBlockView ownerBlock = GetComponentInParent<ProgramBlockView>();
        return ownerBlock != null && ownerBlock.Kind == ProgramBlockView.BlockKind.Repeat;
    }

    public bool TryBuildNodes(out List<ProgramNode> nodes, out string error)
    {
        nodes = new List<ProgramNode>();
        error = null;

        RectTransform root = EffectiveContentRoot;
        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!child.TryGetComponent(out ProgramBlockView blockView))
            {
                continue;
            }

            if (!blockView.TryBuildNode(out ProgramNode node, out string blockError))
            {
                error = blockError;
                return false;
            }

            if (node != null)
            {
                nodes.Add(node);
            }
        }

        return true;
    }

    private static int GetInsertionIndex(RectTransform root, Vector2 screenPosition, Camera eventCamera, Transform ignoreTransform = null)
    {
        int childCount = root.childCount;
        if (childCount <= 0)
        {
            return 0;
        }

        int nonIgnoredCount = 0;
        for (int i = 0; i < childCount; i++)
        {
            if (root.GetChild(i) != ignoreTransform)
            {
                nonIgnoredCount++;
            }
        }

        if (nonIgnoredCount <= 0)
        {
            return 0;
        }

        int runningIndex = 0;

        for (int i = 0; i < childCount; i++)
        {
            Transform childTransform = root.GetChild(i);
            if (childTransform == ignoreTransform)
            {
                continue;
            }

            RectTransform childRect = childTransform as RectTransform;
            if (childRect == null)
            {
                runningIndex++;
                continue;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(childRect, screenPosition, eventCamera))
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(childRect, screenPosition, eventCamera, out Vector2 localPoint);
                return localPoint.y >= 0f ? runningIndex : runningIndex + 1;
            }

            Vector2 childScreen = RectTransformUtility.WorldToScreenPoint(eventCamera, childRect.position);
            if (screenPosition.y >= childScreen.y)
            {
                return runningIndex;
            }

            runningIndex++;
        }

        return nonIgnoredCount;
    }

    private int GetDirectBlockCount()
    {
        RectTransform root = EffectiveContentRoot;
        if (root == null)
        {
            return 0;
        }

        int blockCount = 0;
        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            if (root.GetChild(i).TryGetComponent(out ProgramBlockView _))
            {
                blockCount++;
            }
        }

        return blockCount;
    }
}
