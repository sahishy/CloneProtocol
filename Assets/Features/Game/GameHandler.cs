using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class GameHandler : MonoBehaviour
{
    public static GameHandler Instance { get; private set; }

    private static readonly int SpawnHash = Animator.StringToHash("spawn");

    [Header("Clone Spawning")]
    [SerializeField] private CloneController clonePrefab;
    [SerializeField] private GameObject cloneGhostPrefab;
    [SerializeField] private Transform cloneParent;
    [SerializeField] private Transform cloneGhostParent;
    [SerializeField] private List<Vector3Int> initialSpawnCells = new();
    [SerializeField, Min(0f)] private float delayBeforeFirstCommand = 2f;

    [Header("Gameplay Reset")]
    [SerializeField] private Transform gameplayHolder;

    [Header("Run UI")]
    [SerializeField] private GameObject runButton;
    [SerializeField] private GameObject endButton;
    [SerializeField] private CanvasGroup runContainer;

    [Header("Program Builder")]
    [SerializeField] private ProgramBuilder programBuilder;

    private readonly List<CloneController> spawnedClones = new();
    private readonly List<GameObject> spawnedCloneGhosts = new();
    private readonly List<PressurePlate> cachedPressurePlates = new();
    private readonly List<PackageResetData> cachedPackages = new();
    private readonly List<ConveyorTile> cachedConveyors = new();
    private Coroutine pendingRunRoutine;
    private int activeRunRequestId;

    private readonly struct PackageResetData
    {
        public readonly PackageController Package;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public PackageResetData(PackageController package, Vector3 position, Quaternion rotation)
        {
            Package = package;
            Position = position;
            Rotation = rotation;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        CacheGameplayState();
        RebuildCloneGhosts();
        SetRunUiState(false);
    }

    private void OnEnable()
    {
        CommandHandler.RunStateChanged += HandleRunStateChanged;
    }

    private void OnDisable()
    {
        CommandHandler.RunStateChanged -= HandleRunStateChanged;
    }

    private void HandleRunStateChanged(bool isRunning)
    {
        SetRunUiState(isRunning);
    }

    public void EndProgram()
    {
        activeRunRequestId++;
        if (pendingRunRoutine != null)
        {
            StopCoroutine(pendingRunRoutine);
            pendingRunRoutine = null;
        }

        CommandHandler.Instance?.CancelRun();
        ClearClones();
        GridHandler.Instance?.ClearRuntimeOccupancy();
        RestoreGameplayState();

        GridHandler.Instance?.RefreshConveyorsFromScene();
        LevelHandler.Instance?.RefreshLevelState();
        SetRunUiState(false);
    }

    public void RestartLevelState()
    {
        EndProgram();
        programBuilder?.ClearProgramBlocks();
        SetRunUiState(false);
    }

    private void CacheGameplayState()
    {
        cachedPressurePlates.Clear();
        cachedPackages.Clear();
        cachedConveyors.Clear();

        if (gameplayHolder == null)
        {
            return;
        }

        PressurePlate[] pressurePlates = gameplayHolder.GetComponentsInChildren<PressurePlate>(includeInactive: true);
        for (int i = 0; i < pressurePlates.Length; i++)
        {
            if (pressurePlates[i] != null)
            {
                cachedPressurePlates.Add(pressurePlates[i]);
            }
        }

        PackageController[] packages = gameplayHolder.GetComponentsInChildren<PackageController>(includeInactive: true);
        for (int i = 0; i < packages.Length; i++)
        {
            PackageController package = packages[i];
            if (package == null)
            {
                continue;
            }

            cachedPackages.Add(new PackageResetData(package, package.transform.position, package.transform.rotation));
        }

        ConveyorTile[] conveyors = gameplayHolder.GetComponentsInChildren<ConveyorTile>(includeInactive: true);
        for (int i = 0; i < conveyors.Length; i++)
        {
            if (conveyors[i] != null)
            {
                cachedConveyors.Add(conveyors[i]);
            }
        }
    }

    private void RestoreGameplayState()
    {
        for (int i = 0; i < cachedPressurePlates.Count; i++)
        {
            PressurePlate plate = cachedPressurePlates[i];
            if (plate != null)
            {
                plate.ResetState();
            }
        }

        for (int i = 0; i < cachedPackages.Count; i++)
        {
            PackageResetData packageData = cachedPackages[i];
            if (packageData.Package == null)
            {
                continue;
            }

            packageData.Package.ResetToState(packageData.Position, packageData.Rotation);
        }

        for (int i = 0; i < cachedConveyors.Count; i++)
        {
            ConveyorTile conveyor = cachedConveyors[i];
            if (conveyor != null)
            {
                conveyor.ResetState();
            }
        }
    }

    public void SubmitProgramFromUI()
    {
        if (CommandHandler.Instance == null)
        {
            return;
        }

        if (programBuilder == null)
        {
            return;
        }

        if (!programBuilder.TryBuildCompiledProgram(out List<ProgramCommandType> compiledCommands, out List<ProgramBlockView> commandSources, out string error))
        {
            return;
        }

        EndProgram();

        for (int i = 0; i < initialSpawnCells.Count; i++)
        {
            SpawnClone(initialSpawnCells[i]);
        }

        int runRequestId = ++activeRunRequestId;
        List<ProgramCommandType> commandsToRun = new List<ProgramCommandType>(compiledCommands);
        List<ProgramBlockView> sourcesToRun = commandSources != null
            ? new List<ProgramBlockView>(commandSources)
            : null;
        pendingRunRoutine = StartCoroutine(BeginRunAfterDelay(commandsToRun, sourcesToRun, runRequestId));
        SetRunUiState(true);
    }

    private IEnumerator BeginRunAfterDelay(List<ProgramCommandType> commands, List<ProgramBlockView> commandSources, int runRequestId)
    {
        if (delayBeforeFirstCommand > 0f)
        {
            yield return new WaitForSeconds(delayBeforeFirstCommand);
        }

        pendingRunRoutine = null;

        if (runRequestId != activeRunRequestId)
        {
            yield break;
        }

        if (CommandHandler.Instance == null)
        {
            SetRunUiState(false);
            yield break;
        }

        if (cloneParent == null || cloneParent.childCount == 0)
        {
            SetRunUiState(false);
            yield break;
        }

        CommandHandler.Instance.RunProgram(commands, commandSources);
    }

    public CloneController SpawnClone(Vector3Int startingCell)
    {
        GridHandler gridHandler = GridHandler.Instance;
        if (gridHandler == null)
        {
            return null;
        }

        if (clonePrefab == null)
        {
            return null;
        }

        Vector3 spawnWorldPosition = gridHandler.CellToWorld(startingCell);
        CloneController cloneInstance = Instantiate(clonePrefab, spawnWorldPosition, Quaternion.identity, cloneParent);

        if (!cloneInstance.InitializeAtCell(startingCell))
        {
            Destroy(cloneInstance.gameObject);
            return null;
        }

        Animator animationController = cloneInstance.GetComponentInChildren<Animator>();
        if (animationController != null)
        {
            animationController.SetTrigger(SpawnHash);
        }

        spawnedClones.Add(cloneInstance);
        return cloneInstance;
    }

    private void ClearClones()
    {
        for (int i = spawnedClones.Count - 1; i >= 0; i--)
        {
            CloneController clone = spawnedClones[i];
            if (clone == null)
            {
                continue;
            }

            clone.gameObject.SetActive(false);
            Destroy(clone.gameObject);
        }

        if (cloneParent != null)
        {
            int childIndex = cloneParent.childCount - 1;
            while (childIndex >= 0)
            {
                Transform child = cloneParent.GetChild(childIndex);
                if (child != null)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }

                childIndex--;
            }
        }

        spawnedClones.Clear();
    }

    private void SetRunUiState(bool isRunning)
    {
        if (runButton != null)
        {
            runButton.SetActive(!isRunning);
        }

        if (endButton != null)
        {
            endButton.SetActive(isRunning);
        }

        if (runContainer != null)
        {
            runContainer.alpha = isRunning ? 0.3f : 1f;
        }

        SetCloneGhostsVisible(!isRunning);
    }

    private void RebuildCloneGhosts()
    {
        ClearCloneGhosts();

        if (cloneGhostPrefab == null)
        {
            return;
        }

        GridHandler gridHandler = GridHandler.Instance;
        if (gridHandler == null)
        {
            return;
        }

        Transform ghostParent = cloneGhostParent != null
            ? cloneGhostParent
            : (gameplayHolder != null ? gameplayHolder : transform);

        Quaternion ghostRotation = clonePrefab != null
            ? clonePrefab.InitialFacingRotation
            : Quaternion.identity;

        for (int i = 0; i < initialSpawnCells.Count; i++)
        {
            Vector3 worldPosition = gridHandler.CellToWorld(initialSpawnCells[i]);
            GameObject ghostInstance = Instantiate(cloneGhostPrefab, worldPosition, ghostRotation, ghostParent);
            spawnedCloneGhosts.Add(ghostInstance);
        }
    }

    private void SetCloneGhostsVisible(bool isVisible)
    {
        for (int i = spawnedCloneGhosts.Count - 1; i >= 0; i--)
        {
            GameObject ghost = spawnedCloneGhosts[i];
            if (ghost == null)
            {
                spawnedCloneGhosts.RemoveAt(i);
                continue;
            }

            if (ghost.activeSelf != isVisible)
            {
                ghost.SetActive(isVisible);
            }
        }
    }

    private void ClearCloneGhosts()
    {
        for (int i = spawnedCloneGhosts.Count - 1; i >= 0; i--)
        {
            GameObject ghost = spawnedCloneGhosts[i];
            if (ghost != null)
            {
                Destroy(ghost);
            }
        }

        spawnedCloneGhosts.Clear();
    }
}