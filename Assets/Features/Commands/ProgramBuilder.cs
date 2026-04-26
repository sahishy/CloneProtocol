using System.Collections.Generic;
using UnityEngine;

public class ProgramBuilder : MonoBehaviour
{
    [Header("Program Structure")]
    [SerializeField] private ProgramDropZone rootDropZone;
    [SerializeField] private RectTransform programAreaRect;

    [Header("Validation")]
    [SerializeField, Min(1)] private int maxExpandedCommandCount = 24;

    [Header("Drag")]
    [SerializeField] private Canvas dragCanvas;

    public ProgramDropZone RootDropZone => rootDropZone;

    private void Start()
    {
        RefreshBlockIndices();
    }

    public Canvas DragCanvas
    {
        get
        {
            if (dragCanvas != null)
            {
                return dragCanvas;
            }

            dragCanvas = GetComponentInParent<Canvas>();
            return dragCanvas;
        }
    }

    public bool IsPointerInsideProgramArea(Vector2 screenPosition, Camera eventCamera)
    {
        if (programAreaRect == null)
        {
            return true;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(programAreaRect, screenPosition, eventCamera);
    }

    public ProgramDropZone FindDropZoneAtScreenPosition(Vector2 screenPosition, Camera eventCamera, ProgramBlockView block, Transform draggedTransform)
    {
        ProgramDropZone[] zones = FindObjectsByType<ProgramDropZone>(FindObjectsSortMode.None);
        ProgramDropZone bestZone = null;
        float bestArea = float.MaxValue;

        for (int i = 0; i < zones.Length; i++)
        {
            ProgramDropZone zone = zones[i];
            if (zone == null || !zone.AcceptsBlock(block) || !zone.ContainsScreenPoint(screenPosition, eventCamera))
            {
                continue;
            }

            if (draggedTransform != null && (zone.transform == draggedTransform || zone.transform.IsChildOf(draggedTransform)))
            {
                continue;
            }

            RectTransform zoneRect = zone.transform as RectTransform;
            float area = zoneRect != null ? Mathf.Abs(zoneRect.rect.width * zoneRect.rect.height) : float.MaxValue;
            if (area < bestArea)
            {
                bestArea = area;
                bestZone = zone;
            }
        }

        return bestZone;
    }

    public ProgramDropZone FindInsertionDropZoneAtScreenPosition(Vector2 screenPosition, Camera eventCamera, ProgramBlockView block, Transform draggedTransform)
    {
        ProgramDropZone[] zones = FindObjectsByType<ProgramDropZone>(FindObjectsSortMode.None);
        ProgramDropZone bestZone = null;
        float bestArea = float.MaxValue;

        for (int i = 0; i < zones.Length; i++)
        {
            ProgramDropZone zone = zones[i];
            if (zone == null || zone.DeleteDroppedBlocks || !zone.AcceptsBlock(block) || !zone.ContainsScreenPoint(screenPosition, eventCamera))
            {
                continue;
            }

            if (draggedTransform != null && (zone.transform == draggedTransform || zone.transform.IsChildOf(draggedTransform)))
            {
                continue;
            }

            RectTransform zoneRect = zone.transform as RectTransform;
            float area = zoneRect != null ? Mathf.Abs(zoneRect.rect.width * zoneRect.rect.height) : float.MaxValue;
            if (area < bestArea)
            {
                bestArea = area;
                bestZone = zone;
            }
        }

        return bestZone;
    }

    public bool IsPointerOverDeleteZone(Vector2 screenPosition, Camera eventCamera, Transform draggedTransform)
    {
        ProgramDropZone[] zones = FindObjectsByType<ProgramDropZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            ProgramDropZone zone = zones[i];
            if (zone == null || !zone.DeleteDroppedBlocks)
            {
                continue;
            }

            if (draggedTransform != null && (zone.transform == draggedTransform || zone.transform.IsChildOf(draggedTransform)))
            {
                continue;
            }

            if (zone.ContainsScreenPoint(screenPosition, eventCamera))
            {
                return true;
            }
        }

        return false;
    }

    public ProgramDropZone FindDeleteDropZoneAtScreenPosition(Vector2 screenPosition, Camera eventCamera, Transform draggedTransform)
    {
        ProgramDropZone[] zones = FindObjectsByType<ProgramDropZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            ProgramDropZone zone = zones[i];
            if (zone == null || !zone.DeleteDroppedBlocks)
            {
                continue;
            }

            if (draggedTransform != null && (zone.transform == draggedTransform || zone.transform.IsChildOf(draggedTransform)))
            {
                continue;
            }

            if (zone.ContainsScreenPoint(screenPosition, eventCamera))
            {
                return zone;
            }
        }

        return null;
    }

    public bool TryPlaceInRoot(ProgramBlockDragHandler dragHandler, UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (rootDropZone == null || dragHandler == null || dragHandler.BlockView == null)
        {
            return false;
        }

        if (!rootDropZone.AcceptsBlock(dragHandler.BlockView))
        {
            return false;
        }

        return rootDropZone.TryAcceptDrop(dragHandler, eventData);
    }

    public ProgramBlockDragHandler SpawnPaletteBlock(ProgramBlockView blockPrefab)
    {
        if (blockPrefab == null)
        {
            return null;
        }

        Canvas canvas = DragCanvas;
        if (canvas == null)
        {
            return null;
        }

        Transform spawnParent = rootDropZone != null ? rootDropZone.ContentRoot : canvas.transform;
        ProgramBlockView blockInstance = Instantiate(blockPrefab, spawnParent);
        if (spawnParent is RectTransform)
        {
            RectTransform blockRect = blockInstance.transform as RectTransform;
            if (blockRect != null)
            {
                blockRect.anchoredPosition = Vector2.zero;
            }
        }

        ProgramBlockDragHandler dragHandler = blockInstance.GetComponent<ProgramBlockDragHandler>();
        if (dragHandler == null)
        {
            dragHandler = blockInstance.gameObject.AddComponent<ProgramBlockDragHandler>();
        }

        dragHandler.Initialize(this, spawnedFromPalette: true);
        RefreshBlockIndices();

        return dragHandler;
    }

    public void RefreshBlockIndices()
    {
        if (rootDropZone == null)
        {
            return;
        }

        int runningIndex = 0;
        AssignIndicesRecursive(rootDropZone, ref runningIndex);
    }

    private void AssignIndicesRecursive(ProgramDropZone zone, ref int runningIndex)
    {
        if (zone == null)
        {
            return;
        }

        RectTransform root = zone.ContentRoot;
        if (root == null)
        {
            return;
        }

        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!child.TryGetComponent(out ProgramBlockView blockView))
            {
                continue;
            }

            blockView.SetDisplayIndex(runningIndex);
            runningIndex++;

            if (blockView.Kind == ProgramBlockView.BlockKind.Repeat)
            {
                AssignIndicesRecursive(blockView.RepeatDropZone, ref runningIndex);
            }
        }
    }

    public bool TryBuildCompiledProgram(out List<ProgramCommandType> compiledCommands, out string error)
    {
        return TryBuildCompiledProgram(out compiledCommands, out _, out error);
    }

    public void ClearProgramBlocks()
    {
        if (rootDropZone == null)
        {
            return;
        }

        RectTransform root = rootDropZone.ContentRoot;
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child != null)
            {
                Destroy(child.gameObject);
            }
        }

        RefreshBlockIndices();
    }

    public bool TryBuildCompiledProgram(out List<ProgramCommandType> compiledCommands, out List<ProgramBlockView> commandSources, out string error)
    {
        compiledCommands = null;
        commandSources = null;
        error = null;

        if (rootDropZone == null)
        {
            error = $"{nameof(ProgramBuilder)} on {name} is missing root drop zone referecne";
            return false;
        }

        if (!rootDropZone.TryBuildNodes(out List<ProgramNode> rootNodes, out string buildError))
        {
            error = buildError;
            return false;
        }

        if (!ProgramCompiler.TryCompile(rootNodes, maxExpandedCommandCount, out compiledCommands, out string compileError))
        {
            error = compileError;
            return false;
        }

        compiledCommands = new List<ProgramCommandType>();
        commandSources = new List<ProgramBlockView>();
        if (!TryBuildCommandSourcesRecursive(rootDropZone, compiledCommands, commandSources, out string sourceError))
        {
            error = sourceError;
            compiledCommands = null;
            commandSources = null;
            return false;
        }

        return true;
    }

    private bool TryBuildCommandSourcesRecursive(
        ProgramDropZone zone,
        List<ProgramCommandType> compiledCommands,
        List<ProgramBlockView> commandSources,
        out string error)
    {
        error = null;
        if (zone == null)
        {
            return true;
        }

        RectTransform root = zone.ContentRoot;
        if (root == null)
        {
            return true;
        }

        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!child.TryGetComponent(out ProgramBlockView blockView) || blockView == null)
            {
                continue;
            }

            if (blockView.Kind == ProgramBlockView.BlockKind.Command)
            {
                compiledCommands.Add(blockView.CommandType);
                commandSources.Add(blockView);

                if (maxExpandedCommandCount > 0 && compiledCommands.Count > maxExpandedCommandCount)
                {
                    error = $"program too long after repeat ({compiledCommands.Count}/{maxExpandedCommandCount})";
                    return false;
                }

                continue;
            }

            ProgramDropZone repeatZone = blockView.RepeatDropZone;
            if (repeatZone == null)
            {
                error = $"repeat '{blockView.name}' missing inner drop zone";
                return false;
            }

            List<ProgramCommandType> repeatChunkCommands = new List<ProgramCommandType>();
            List<ProgramBlockView> repeatChunkSources = new List<ProgramBlockView>();

            if (!TryBuildCommandSourcesRecursive(repeatZone, repeatChunkCommands, repeatChunkSources, out error))
            {
                return false;
            }

            for (int repeatIndex = 0; repeatIndex < blockView.RepeatCount; repeatIndex++)
            {
                compiledCommands.AddRange(repeatChunkCommands);
                commandSources.AddRange(repeatChunkSources);

                if (maxExpandedCommandCount > 0 && compiledCommands.Count > maxExpandedCommandCount)
                {
                    error = $"program too long after repeat ({compiledCommands.Count}/{maxExpandedCommandCount})";
                    return false;
                }
            }
        }

        return true;
    }
}
