using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PackageController : MonoBehaviour
{
    [Header("Grid Placement")]
    [SerializeField, Min(0f)] private float moveDuration = 1f;

    public Vector3Int CurrentCell { get; private set; }
    public bool IsInitialized { get; private set; }
    public float ActionDuration => moveDuration;

    private GridHandler gridHandler;
    private Coroutine moveRoutine;
    private readonly Queue<Vector3> queuedMoveTargets = new();

    private void Start()
    {
        gridHandler = GridHandler.Instance;
        if (gridHandler == null)
        {
            return;
        }

        Vector3Int startingCell = gridHandler.WorldToCell(transform.position);
        if (!gridHandler.RegisterPackage(this, startingCell))
        {
            return;
        }

        CurrentCell = startingCell;
        transform.position = gridHandler.CellToWorld(CurrentCell);
        IsInitialized = true;
    }

    public void NotifyGridMoved(Vector3Int newCell)
    {
        CurrentCell = newCell;

        Vector3 target = gridHandler != null ? gridHandler.CellToWorld(CurrentCell) : transform.position;
        if (moveDuration <= 0f)
        {
            queuedMoveTargets.Clear();
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
                moveRoutine = null;
            }

            transform.position = target;
            return;
        }

        queuedMoveTargets.Enqueue(target);

        if (moveRoutine == null)
        {
            moveRoutine = StartCoroutine(ProcessQueuedMoves());
        }
    }

    private IEnumerator ProcessQueuedMoves()
    {
        while (queuedMoveTargets.Count > 0)
        {
            Vector3 targetPosition = queuedMoveTargets.Dequeue();
            Vector3 startPosition = transform.position;
            float elapsed = 0f;

            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / moveDuration);
                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            transform.position = targetPosition;
        }

        moveRoutine = null;
    }

    private void OnDestroy()
    {
        if (gridHandler != null && IsInitialized)
        {
            gridHandler.UnregisterPackage(this, CurrentCell);
        }
    }

    public void ResetToState(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        queuedMoveTargets.Clear();
        transform.SetPositionAndRotation(worldPosition, worldRotation);

        gridHandler = GridHandler.Instance;
        if (gridHandler == null)
        {
            IsInitialized = false;
            return;
        }

        Vector3Int resetCell = gridHandler.WorldToCell(worldPosition);
        CurrentCell = resetCell;
        IsInitialized = gridHandler.RegisterPackage(this, resetCell);
    }
}
