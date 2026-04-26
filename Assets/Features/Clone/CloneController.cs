using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CloneController : MonoBehaviour
{
    private static readonly int MovingHash = Animator.StringToHash("moving");
    private static readonly int BumpHash = Animator.StringToHash("bump");
    private static readonly int PushingHash = Animator.StringToHash("pushing");

    private enum FacingDirection
    {
        North,
        East,
        South,
        West
    }

    [Header("Sound")]
    [SerializeField] private AudioSource pushSound;
    [SerializeField] private AudioSource spawnSound;
    [SerializeField] private AudioSource stepsSound;
    [SerializeField] private AudioSource metalStepSound;
    [SerializeField] private AudioSource bumpSound;
    [SerializeField] private AudioSource turnSound;

    [Header("Animation")]
    [SerializeField] private Animator animationController;

    [Header("Grid Placement")]
    [SerializeField, Min(0f)] private float moveDuration = 1f;
    [SerializeField] private FacingDirection startingFacing = FacingDirection.South;

    public Vector3Int CurrentCell { get; private set; }
    public Animator AnimationController => animationController;
    public bool IsInitialized { get; private set; }
    public float ActionDuration => moveDuration;
    public Quaternion InitialFacingRotation => Quaternion.Euler(0f, FacingToYaw(startingFacing), 0f);

    private Coroutine moveRoutine;
    private Coroutine turnRoutine;
    private Coroutine metalStepsRoutine;
    private GridHandler gridHandler;
    private FacingDirection currentFacing;
    private readonly Queue<QueuedMove> queuedMoves = new();

    private readonly struct QueuedMove
    {
        public readonly Vector3 TargetPosition;
        public readonly bool UseMetalSteps;

        public QueuedMove(Vector3 targetPosition, bool useMetalSteps)
        {
            TargetPosition = targetPosition;
            UseMetalSteps = useMetalSteps;
        }
    }

    private void Awake()
    {
        if (animationController == null)
        {
            animationController = GetComponentInChildren<Animator>();
        }

        spawnSound.Play();

        SetMovingAnimation(false);
        SetPushingAnimation(false);
    }

    public bool InitializeAtCell(Vector3Int startingCell)
    {
        if (IsInitialized)
        {
            return true;
        }

        gridHandler = GridHandler.Instance;

        if (gridHandler == null)
        {
            Debug.LogError($"{nameof(CloneController)} on {name} could not find {nameof(GridHandler)}.{nameof(GridHandler.Instance)} in the scene.");
            return false;
        }

        if (!gridHandler.RegisterClone(this, startingCell))
        {
            Debug.LogError($"{nameof(CloneController)} on {name} failed to register at starting cell {startingCell}. " +
                           "Cell may be out of bounds or already occupied.");
            return false;
        }

        CurrentCell = startingCell;
        transform.position = gridHandler.CellToWorld(CurrentCell);
        currentFacing = startingFacing;
        transform.rotation = Quaternion.Euler(0f, FacingToYaw(currentFacing), 0f);
        IsInitialized = true;
        return true;
    }

    public bool ExecuteCommand(ProgramCommandType commandType)
    {
        switch (commandType)
        {
            case ProgramCommandType.MoveForward:
                return MoveForward();
            case ProgramCommandType.TurnLeft:
                TurnLeft();
                return true;
            case ProgramCommandType.TurnRight:
                TurnRight();
                return true;
            default:
                Debug.LogWarning($"{nameof(CloneController)} on {name} received unknown command type: {commandType}");
                return false;
        }
    }

    public bool ExecuteCommand(int commandId)
    {
        if (!System.Enum.IsDefined(typeof(ProgramCommandType), commandId))
        {
            Debug.LogWarning($"{nameof(CloneController)} on {name} received unknown command id: {commandId}");
            return false;
        }

        return ExecuteCommand((ProgramCommandType)commandId);
    }

    public bool MoveForward()
    {
        Vector3Int targetCell = CurrentCell + FacingToVector(currentFacing);
        return TryMoveToCell(targetCell);
    }

    public void TurnLeft()
    {
        if (!enabled || !IsInitialized)
        {
            return;
        }

        currentFacing = (FacingDirection)(((int)currentFacing + 3) % 4);

        float targetYaw = FacingToYaw(currentFacing);
        if (moveDuration <= 0f)
        {
            transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
            return;
        }

        if (turnRoutine != null)
        {
            StopCoroutine(turnRoutine);
        }

        turnRoutine = StartCoroutine(TurnToYaw(targetYaw, moveDuration));
    }

    public void TurnRight()
    {
        if (!enabled || !IsInitialized)
        {
            return;
        }

        currentFacing = (FacingDirection)(((int)currentFacing + 1) % 4);

        float targetYaw = FacingToYaw(currentFacing);
        if (moveDuration <= 0f)
        {
            transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
            return;
        }

        if (turnRoutine != null)
        {
            StopCoroutine(turnRoutine);
        }

        turnRoutine = StartCoroutine(TurnToYaw(targetYaw, moveDuration));
    }

    private void OnDestroy()
    {
        if (gridHandler != null && IsInitialized)
        {
            gridHandler.UnregisterClone(this, CurrentCell);
        }
    }

    public bool TryMoveToCell(Vector3Int targetCell)
    {
        if (!enabled || gridHandler == null || !IsInitialized)
        {
            return false;
        }

        SetPushingAnimation(false);

        bool useMetalSteps = gridHandler.IsConveyorCell(CurrentCell) || gridHandler.IsConveyorCell(targetCell);

        if (!gridHandler.TryMoveClone(this, CurrentCell, targetCell))
        {
            PlayBlockedMoveFeedback(targetCell);
            return false;
        }

        CurrentCell = targetCell;

        if (moveDuration <= 0f)
        {
            transform.position = gridHandler.CellToWorld(CurrentCell);
            SetMovingAnimation(false, useMetalSteps);
            SetPushingAnimation(false);
        }
        else
        {
            QueueMove(gridHandler.CellToWorld(CurrentCell), useMetalSteps);
        }

        return true;
    }

    private void QueueMove(Vector3 targetPosition, bool useMetalSteps)
    {
        queuedMoves.Enqueue(new QueuedMove(targetPosition, useMetalSteps));

        if (moveRoutine == null)
        {
            moveRoutine = StartCoroutine(ProcessQueuedMoves());
        }
    }

    private IEnumerator ProcessQueuedMoves()
    {
        while (queuedMoves.Count > 0)
        {
            QueuedMove queuedMove = queuedMoves.Dequeue();
            SetMovingAnimation(true, queuedMove.UseMetalSteps);

            Vector3 startPosition = transform.position;
            float elapsed = 0f;

            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / moveDuration);
                transform.position = Vector3.Lerp(startPosition, queuedMove.TargetPosition, t);
                yield return null;
            }

            transform.position = queuedMove.TargetPosition;
        }

        SetMovingAnimation(false);
        SetPushingAnimation(false);
        moveRoutine = null;
    }

    private IEnumerator TurnToYaw(float targetYaw, float duration)
    {
        turnSound.Play();

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, 0f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        transform.rotation = targetRotation;
        turnRoutine = null;
    }

    private void SetMovingAnimation(bool isMoving, bool useMetalSteps = false)
    {
        if (animationController != null)
        {
            animationController.SetBool(MovingHash, isMoving);
        }

        if (isMoving)
        {
            if (useMetalSteps)
            {
                if (stepsSound != null && stepsSound.isPlaying)
                {
                    stepsSound.Stop();
                }

                if (metalStepsRoutine != null)
                {
                    StopCoroutine(metalStepsRoutine);
                }

                metalStepsRoutine = StartCoroutine(PlayMetalStepsSound());
            }
            else
            {
                if (metalStepsRoutine != null)
                {
                    StopCoroutine(metalStepsRoutine);
                    metalStepsRoutine = null;
                }

                if (metalStepSound != null && metalStepSound.isPlaying)
                {
                    metalStepSound.Stop();
                }

                stepsSound.Play();
            }
        }
        else
        {
            if (metalStepsRoutine != null)
            {
                StopCoroutine(metalStepsRoutine);
                metalStepsRoutine = null;
            }

            if (stepsSound != null && stepsSound.isPlaying)
            {
                stepsSound.Stop();
            }

            if (metalStepSound != null && metalStepSound.isPlaying)
            {
                metalStepSound.Stop();
            }
        }
    }

    public void SetPushingAnimation(bool isPushing)
    {
        if (animationController != null)
        {
            animationController.SetBool(PushingHash, isPushing);
        }

        if (isPushing)
        {
            pushSound.Play();
        }
    }

    private void TriggerBumpAnimation()
    {
        if (animationController != null)
        {
            animationController.SetTrigger(BumpHash);
        }

        bumpSound.Play();
    }

    private void PlayBlockedMoveFeedback(Vector3Int attemptedTargetCell)
    {
        SetPushingAnimation(false);

        queuedMoves.Clear();

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        if (moveDuration <= 0f)
        {
            TriggerBumpAnimation();
            SetMovingAnimation(false);
            return;
        }

        Vector3 startPosition = transform.position;
        Vector3 attemptedTargetPosition = gridHandler.CellToWorld(attemptedTargetCell);
        Vector3 halfwayPosition = Vector3.Lerp(startPosition, attemptedTargetPosition, 0.5f);

        moveRoutine = StartCoroutine(PlayBlockedMoveRoutine(startPosition, halfwayPosition, moveDuration));
    }

    private IEnumerator PlayBlockedMoveRoutine(Vector3 startPosition, Vector3 halfwayPosition, float duration)
    {
        SetMovingAnimation(true);

        float bumpWalkDuration = duration * 0.3f;
        float elapsed = 0f;

        while (elapsed < bumpWalkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bumpWalkDuration);
            transform.position = Vector3.Lerp(startPosition, halfwayPosition, t);
            yield return null;
        }

        transform.position = halfwayPosition;
        TriggerBumpAnimation();

        elapsed = 0f;
        while (elapsed < bumpWalkDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bumpWalkDuration);
            transform.position = Vector3.Lerp(halfwayPosition, startPosition, t);
            yield return null;
        }

        transform.position = startPosition;
        SetMovingAnimation(false);
        moveRoutine = null;
    }

    private static Vector3Int FacingToVector(FacingDirection facing)
    {
        return facing switch
        {
            FacingDirection.North => new Vector3Int(0, 0, 1),
            FacingDirection.East => new Vector3Int(1, 0, 0),
            FacingDirection.South => new Vector3Int(0, 0, -1),
            FacingDirection.West => new Vector3Int(-1, 0, 0),
            _ => new Vector3Int(0, 0, 1)
        };
    }

    private static float FacingToYaw(FacingDirection facing)
    {
        return facing switch
        {
            FacingDirection.North => 0f,
            FacingDirection.East => 90f,
            FacingDirection.South => 180f,
            FacingDirection.West => 270f,
            _ => 0f
        };
    }

    private IEnumerator PlayMetalStepsSound()
    {
        if (metalStepSound == null)
        {
            metalStepsRoutine = null;
            yield break;
        }

        metalStepSound.pitch = Random.Range(0.9f, 1.1f);
        metalStepSound.Play();

        yield return new WaitForSeconds(0.5f);

        metalStepSound.pitch = Random.Range(0.9f, 1.1f);
        metalStepSound.Play();
        metalStepsRoutine = null;
    }

}