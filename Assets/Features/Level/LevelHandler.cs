using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class LevelHandler : MonoBehaviour
{
    public static LevelHandler Instance { get; private set; }

    [Header("Win Condition")]
    [SerializeField, Min(0f)] private float requiredHoldDuration = 2f;
    [SerializeField] private UnityEvent onLevelCompleted;

    [Header("End Screen")]
    [SerializeField] private GameObject endScreenRoot;
    [SerializeField] private CanvasGroup endScreenCanvasGroup;
    [SerializeField] private RectTransform endScreenContainer;
    [SerializeField, Min(0f)] private float endScreenFadeDuration = 0.35f;
    [SerializeField, Min(0f)] private float endScreenScaleDelay = 1f;
    [SerializeField, Min(0f)] private float endScreenScaleDuration = 0.2f;

    public float RequiredHoldDuration => requiredHoldDuration;

    private readonly List<PressurePlate> pressurePlates = new();
    private readonly Dictionary<PressurePlate, bool> plateHeldStates = new();

    private float allHeldTimer;
    private bool levelCompleted;
    private Coroutine endScreenRoutine;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RebuildPressurePlateList();
        InitializeEndScreen();
    }

    private void Update()
    {
        bool allPlatesHeld = pressurePlates.Count > 0 && AreAllPlatesHeld();

        if (!levelCompleted && pressurePlates.Count > 0)
        {
            if (allPlatesHeld)
            {
                allHeldTimer += Time.deltaTime;
                if (allHeldTimer >= requiredHoldDuration)
                {
                    CompleteLevel();
                }
            }
            else
            {
                allHeldTimer = 0f;
            }
        }
    }

    public void NotifyPlateStateChanged(PressurePlate pressurePlate, bool isHeld)
    {
        if (pressurePlate == null)
        {
            return;
        }

        if (!plateHeldStates.ContainsKey(pressurePlate))
        {
            pressurePlates.Add(pressurePlate);
        }

        plateHeldStates[pressurePlate] = isHeld;

        if (!isHeld)
        {
            allHeldTimer = 0f;
        }
    }

    private void RebuildPressurePlateList()
    {
        pressurePlates.Clear();
        plateHeldStates.Clear();
        allHeldTimer = 0f;
        levelCompleted = false;

        PressurePlate[] discoveredPlates = FindObjectsByType<PressurePlate>(FindObjectsSortMode.None);
        for (int i = 0; i < discoveredPlates.Length; i++)
        {
            PressurePlate plate = discoveredPlates[i];
            pressurePlates.Add(plate);
            plateHeldStates[plate] = plate.IsPressed;
        }

        Debug.Log($"{pressurePlates.Count} pressure plates");
    }

    public void RefreshLevelState()
    {
        RebuildPressurePlateList();
    }

    private bool AreAllPlatesHeld()
    {
        for (int i = 0; i < pressurePlates.Count; i++)
        {
            PressurePlate plate = pressurePlates[i];
            if (plate == null)
            {
                continue;
            }

            if (!plateHeldStates.TryGetValue(plate, out bool isHeld) || !isHeld)
            {
                return false;
            }
        }

        return true;
    }

    private void CompleteLevel()
    {
        if (levelCompleted)
        {
            return;
        }

        levelCompleted = true;
        Debug.Log($"level complete");
        ShowEndScreen();
        onLevelCompleted?.Invoke();
    }

    private void InitializeEndScreen()
    {
        if (endScreenRoot != null)
        {
            endScreenRoot.SetActive(false);
        }

        if (endScreenCanvasGroup != null)
        {
            endScreenCanvasGroup.alpha = 0f;
        }

        if (endScreenContainer != null)
        {
            Vector3 startScale = endScreenContainer.localScale;
            startScale.y = 0f;
            endScreenContainer.localScale = startScale;
        }
    }

    private void ShowEndScreen()
    {
        if (endScreenRoutine != null)
        {
            StopCoroutine(endScreenRoutine);
        }

        endScreenRoutine = StartCoroutine(ShowEndScreenRoutine());
    }

    private IEnumerator ShowEndScreenRoutine()
    {
        if (endScreenRoot != null)
        {
            endScreenRoot.SetActive(true);
        }

        if (endScreenCanvasGroup != null)
        {
            endScreenCanvasGroup.alpha = 0f;

            if (endScreenFadeDuration <= 0f)
            {
                endScreenCanvasGroup.alpha = 1f;
            }
            else
            {
                float elapsedFade = 0f;
                while (elapsedFade < endScreenFadeDuration)
                {
                    elapsedFade += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedFade / endScreenFadeDuration);
                    endScreenCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
                    yield return null;
                }

                endScreenCanvasGroup.alpha = 1f;
            }
        }

        if (endScreenScaleDelay > 0f)
        {
            yield return new WaitForSeconds(endScreenScaleDelay);
        }

        if (endScreenContainer != null)
        {
            Vector3 fromScale = endScreenContainer.localScale;
            fromScale.y = 0f;
            endScreenContainer.localScale = fromScale;

            Vector3 toScale = fromScale;
            toScale.y = 1f;

            if (endScreenScaleDuration <= 0f)
            {
                endScreenContainer.localScale = toScale;
            }
            else
            {
                float elapsedScale = 0f;
                while (elapsedScale < endScreenScaleDuration)
                {
                    elapsedScale += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedScale / endScreenScaleDuration);
                    endScreenContainer.localScale = Vector3.Lerp(fromScale, toScale, t);
                    yield return null;
                }

                endScreenContainer.localScale = toScale;
            }
        }

        endScreenRoutine = null;
    }

    public void LoadSceneIndex(int sceneIndex)
    {
        if (sceneIndex < 0)
        {
            return;
        }

        SceneManager.LoadScene(sceneIndex);
    }
}