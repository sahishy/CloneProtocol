using UnityEngine;
using System.Collections.Generic;

public class GridHandler : MonoBehaviour
{
    public static GridHandler Instance { get; private set; }

    [Header("Grid Settings")]
    [SerializeField, Min(1)] private int width = 7;
    [SerializeField, Min(1)] private int height = 7;
    [SerializeField, Min(0.01f)] private float cellSize = 2.15f;

    private Vector3 origin;

    private readonly Dictionary<Vector3Int, CloneController> occupiedCloneCells = new();
    private readonly Dictionary<Vector3Int, PackageController> occupiedPackageCells = new();
    private readonly Dictionary<Vector3Int, ConveyorTile> conveyorByCell = new();

    private void Awake()
    {
        Instance = this;
        origin = transform.position;
        RebuildConveyorLookup();
    }

    private void Start()
    {
        RebuildConveyorLookup();
        UpdateConveyorOccupancyAudio();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        Vector3 local = worldPosition - origin;
        int x = Mathf.RoundToInt(local.x / cellSize);
        int z = Mathf.RoundToInt(local.z / cellSize);

        return new Vector3Int(x, 0, z);
    }

    public Vector3 CellToWorld(Vector3Int cell)
    {
        return origin + new Vector3(cell.x * cellSize, 0f, cell.z * cellSize);
    }

    public bool IsCellInBounds(Vector3Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.z >= 0 && cell.z < height;
    }

    public bool IsCellWalkable(Vector3Int cell)
    {
        return IsCellInBounds(cell) && !IsCellOccupied(cell);
    }

    public bool IsCellOccupied(Vector3Int cell)
    {
        return occupiedCloneCells.ContainsKey(cell) || occupiedPackageCells.ContainsKey(cell);
    }

    public bool RegisterClone(CloneController clone, Vector3Int cell)
    {
        if (clone == null || !IsCellWalkable(cell))
        {
            return false;
        }

        occupiedCloneCells[cell] = clone;
        UpdateConveyorOccupancyAudio();
        return true;
    }

    public bool TryMoveClone(CloneController clone, Vector3Int fromCell, Vector3Int toCell)
    {
        if (clone == null || !occupiedCloneCells.TryGetValue(fromCell, out CloneController currentClone) || currentClone != clone)
        {
            return false;
        }

        if (fromCell == toCell)
        {
            return true;
        }

        if (!IsCellInBounds(toCell))
        {
            return false;
        }

        if (occupiedCloneCells.ContainsKey(toCell))
        {
            return false;
        }

        bool pushedPackageThisMove = false;

        if (occupiedPackageCells.TryGetValue(toCell, out PackageController pushedPackage))
        {
            Vector3Int pushDirection = toCell - fromCell;
            Vector3Int pushTargetCell = toCell + pushDirection;
            if (!TryMovePackage(pushedPackage, toCell, pushTargetCell))
            {
                return false;
            }

            pushedPackageThisMove = true;
        }

        clone.SetPushingAnimation(pushedPackageThisMove);

        occupiedCloneCells.Remove(fromCell);
        occupiedCloneCells[toCell] = clone;
        UpdateConveyorOccupancyAudio();
        return true;
    }

    public bool RegisterPackage(PackageController package, Vector3Int cell)
    {
        if (package == null || !IsCellWalkable(cell))
        {
            return false;
        }

        occupiedPackageCells[cell] = package;
        UpdateConveyorOccupancyAudio();
        return true;
    }

    public bool TryMovePackage(PackageController package, Vector3Int fromCell, Vector3Int toCell)
    {
        if (package == null || !occupiedPackageCells.TryGetValue(fromCell, out PackageController currentPackage) || currentPackage != package)
        {
            return false;
        }

        if (fromCell == toCell)
        {
            return true;
        }

        if (!IsCellInBounds(toCell) || IsCellOccupied(toCell))
        {
            return false;
        }

        occupiedPackageCells.Remove(fromCell);
        occupiedPackageCells[toCell] = package;
        package.NotifyGridMoved(toCell);
        UpdateConveyorOccupancyAudio();
        return true;
    }

    public void UnregisterPackage(PackageController package, Vector3Int cell)
    {
        if (package == null)
        {
            return;
        }

        if (occupiedPackageCells.TryGetValue(cell, out PackageController currentPackage) && currentPackage == package)
        {
            occupiedPackageCells.Remove(cell);
            UpdateConveyorOccupancyAudio();
        }
    }

    public void UnregisterClone(CloneController clone, Vector3Int cell)
    {
        if (clone == null)
        {
            return;
        }

        if (occupiedCloneCells.TryGetValue(cell, out CloneController currentClone) && currentClone == clone)
        {
            occupiedCloneCells.Remove(cell);
            UpdateConveyorOccupancyAudio();
        }
    }

    public void ClearRuntimeOccupancy()
    {
        occupiedCloneCells.Clear();
        occupiedPackageCells.Clear();
        UpdateConveyorOccupancyAudio();
    }

    public bool ResolveConveyorsForStep()
    {
        bool movedAny = false;
        int conveyorPasses = Mathf.Max(1, width * height);

        for (int pass = 0; pass < conveyorPasses; pass++)
        {
            List<IConveyorMovable> movers = CollectConveyorMovers();
            bool movedThisPass = false;

            for (int i = 0; i < movers.Count; i++)
            {
                IConveyorMovable mover = movers[i];
                if (mover == null || !mover.IsValid)
                {
                    continue;
                }

                if (!TryGetConveyorAtCell(mover.CurrentCell, out ConveyorTile conveyor))
                {
                    continue;
                }

                Vector3Int nextCell = mover.CurrentCell + conveyor.DirectionVector;
                if (!IsCellInBounds(nextCell) || IsCellOccupied(nextCell))
                {
                    continue;
                }

                bool moved = mover.TryMove(this, mover.CurrentCell, nextCell);
                movedAny |= moved;
                movedThisPass |= moved;
            }

            if (!movedThisPass)
            {
                break;
            }
        }

        return movedAny;
    }

    private List<IConveyorMovable> CollectConveyorMovers()
    {
        List<IConveyorMovable> movers = new();

        foreach (KeyValuePair<Vector3Int, CloneController> kvp in occupiedCloneCells)
        {
            if (kvp.Value != null && conveyorByCell.ContainsKey(kvp.Key))
            {
                movers.Add(new CloneConveyorMovable(kvp.Value));
            }
        }

        foreach (KeyValuePair<Vector3Int, PackageController> kvp in occupiedPackageCells)
        {
            if (kvp.Value != null && conveyorByCell.ContainsKey(kvp.Key))
            {
                movers.Add(new PackageConveyorMovable(kvp.Value));
            }
        }

        movers.Sort((a, b) =>
        {
            Vector3Int ac = a.CurrentCell;
            Vector3Int bc = b.CurrentCell;
            int zCompare = ac.z.CompareTo(bc.z);
            if (zCompare != 0)
            {
                return zCompare;
            }

            int xCompare = ac.x.CompareTo(bc.x);
            if (xCompare != 0)
            {
                return xCompare;
            }

            return a.SortId.CompareTo(b.SortId);
        });

        return movers;
    }

    public bool IsConveyorCell(Vector3Int cell)
    {
        return conveyorByCell.TryGetValue(cell, out ConveyorTile conveyor) && conveyor != null;
    }

    public float GetMaxEntityActionDuration()
    {
        float maxDuration = 0f;

        foreach (CloneController clone in occupiedCloneCells.Values)
        {
            if (clone != null)
            {
                maxDuration = Mathf.Max(maxDuration, clone.ActionDuration);
            }
        }

        foreach (PackageController package in occupiedPackageCells.Values)
        {
            if (package != null)
            {
                maxDuration = Mathf.Max(maxDuration, package.ActionDuration);
            }
        }

        return maxDuration;
    }

    private bool TryGetConveyorAtCell(Vector3Int cell, out ConveyorTile conveyor)
    {
        return conveyorByCell.TryGetValue(cell, out conveyor) && conveyor != null;
    }

    private void RebuildConveyorLookup()
    {
        conveyorByCell.Clear();

        ConveyorTile[] conveyors = FindObjectsByType<ConveyorTile>(FindObjectsSortMode.None);
        for (int i = 0; i < conveyors.Length; i++)
        {
            ConveyorTile conveyor = conveyors[i];
            if (conveyor == null)
            {
                continue;
            }

            Vector3Int cell = WorldToCell(conveyor.transform.position);
            conveyorByCell[cell] = conveyor;
        }

        UpdateConveyorOccupancyAudio();
    }

    public void RefreshConveyorsFromScene()
    {
        RebuildConveyorLookup();
    }

    private void UpdateConveyorOccupancyAudio()
    {
        foreach (KeyValuePair<Vector3Int, ConveyorTile> kvp in conveyorByCell)
        {
            ConveyorTile conveyor = kvp.Value;
            if (conveyor == null)
            {
                continue;
            }

            Vector3Int cell = kvp.Key;
            bool isOccupied = occupiedCloneCells.ContainsKey(cell) || occupiedPackageCells.ContainsKey(cell);
            conveyor.SetOccupiedState(isOccupied);
        }
    }

    private interface IConveyorMovable
    {
        Vector3Int CurrentCell { get; }
        bool IsValid { get; }
        int SortId { get; }
        bool TryMove(GridHandler grid, Vector3Int fromCell, Vector3Int toCell);
    }

    private sealed class CloneConveyorMovable : IConveyorMovable
    {
        private readonly CloneController clone;

        public CloneConveyorMovable(CloneController clone)
        {
            this.clone = clone;
        }

        public Vector3Int CurrentCell => clone != null ? clone.CurrentCell : Vector3Int.zero;
        public bool IsValid => clone != null;
        public int SortId => clone != null ? clone.GetInstanceID() : int.MaxValue;

        public bool TryMove(GridHandler grid, Vector3Int fromCell, Vector3Int toCell)
        {
            return clone != null && clone.TryMoveToCell(toCell);
        }
    }

    private sealed class PackageConveyorMovable : IConveyorMovable
    {
        private readonly PackageController package;

        public PackageConveyorMovable(PackageController package)
        {
            this.package = package;
        }

        public Vector3Int CurrentCell => package != null ? package.CurrentCell : Vector3Int.zero;
        public bool IsValid => package != null;
        public int SortId => package != null ? package.GetInstanceID() : int.MaxValue;

        public bool TryMove(GridHandler grid, Vector3Int fromCell, Vector3Int toCell)
        {
            return package != null && grid.TryMovePackage(package, fromCell, toCell);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.gray;

        Vector3 drawOrigin = Application.isPlaying ? origin : transform.position;
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector3 center = drawOrigin + new Vector3(x * cellSize, 0f, z * cellSize);
                Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.05f, cellSize));
            }
        }
    }
#endif

}

