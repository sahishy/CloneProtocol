using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

public class CommandHandler : MonoBehaviour
{
    public static CommandHandler Instance { get; private set; }
    public static event Action<bool> RunStateChanged;

    [Header("Clone Collection")]
    [SerializeField] private Transform cloneParent;

    [Header("Execution")]
    [SerializeField, Min(0f)] private float stepDelay = 0.25f;

    [Header("Running Program View")]
    [SerializeField] private ScrollRect runningBlocksScrollRect;
    [SerializeField, Range(0f, 1f)] private float focusViewportYNormalized = 0.35f;
    [SerializeField, Min(0f)] private float scrollFocusLerpSpeed = 10f;

    private Coroutine runRoutine;
    private readonly List<ProgramCommandType> runtimeProgram = new();
    private readonly List<ProgramBlockView> runtimeCommandSources = new();
    private ProgramBlockView activeOutlinedBlock;
    private Coroutine scrollFocusRoutine;
    public bool IsRunning => runRoutine != null;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RunProgram(IReadOnlyList<ProgramCommandType> commands)
    {
        RunProgram(commands, null);
    }

    public void RunProgram(IReadOnlyList<ProgramCommandType> commands, IReadOnlyList<ProgramBlockView> commandSources)
    {
        CancelRun();

        runtimeProgram.Clear();
        runtimeCommandSources.Clear();

        if (commands != null)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                runtimeProgram.Add(commands[i]);
            }
        }

        if (commandSources != null)
        {
            int sourceCount = Mathf.Min(runtimeProgram.Count, commandSources.Count);
            for (int i = 0; i < sourceCount; i++)
            {
                runtimeCommandSources.Add(commandSources[i]);
            }
        }

        runRoutine = StartCoroutine(RunProgramRoutine());
        RunStateChanged?.Invoke(true);
    }

    public void CancelRun()
    {
        if (scrollFocusRoutine != null)
        {
            StopCoroutine(scrollFocusRoutine);
            scrollFocusRoutine = null;
        }

        if (runRoutine != null)
        {
            StopCoroutine(runRoutine);
            runRoutine = null;
            RunStateChanged?.Invoke(false);
        }

        runtimeProgram.Clear();
        runtimeCommandSources.Clear();
        SetActiveCommandOutline(null);
    }

    private IEnumerator RunProgramRoutine()
    {
        GridHandler gridHandler = GridHandler.Instance;

        for (int i = 0; i < runtimeProgram.Count; i++)
        {
            ProgramBlockView sourceBlock = i < runtimeCommandSources.Count ? runtimeCommandSources[i] : null;
            SetActiveCommandOutline(sourceBlock);
            FocusRunningBlock(sourceBlock);
            ExecuteCommandForAllClones(runtimeProgram[i]);

            if (gridHandler != null)
            {
                gridHandler.ResolveConveyorsForStep();
            }

            float maxEntityDuration = gridHandler != null ? gridHandler.GetMaxEntityActionDuration() : 0f;
            float waitDuration = Mathf.Max(stepDelay, GetMaxCloneActionDuration(), maxEntityDuration);
            if (waitDuration > 0f)
            {
                yield return new WaitForSeconds(waitDuration);
            }
        }

        SetActiveCommandOutline(null);

        while (true)
        {
            yield return null;
        }
    }

    private void FocusRunningBlock(ProgramBlockView blockView)
    {
        if (blockView == null || runningBlocksScrollRect == null)
        {
            return;
        }

        RectTransform content = runningBlocksScrollRect.content;
        RectTransform viewport = runningBlocksScrollRect.viewport != null
            ? runningBlocksScrollRect.viewport
            : runningBlocksScrollRect.GetComponent<RectTransform>();
        RectTransform target = blockView.transform as RectTransform;

        if (content == null || viewport == null || target == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float hiddenHeight = content.rect.height - viewport.rect.height;
        if (hiddenHeight <= 0f)
        {
            runningBlocksScrollRect.verticalNormalizedPosition = 1f;
            return;
        }

        Vector2 contentInViewport = viewport.InverseTransformPoint(content.position);
        Vector2 targetInViewport = viewport.InverseTransformPoint(target.position);
        float targetOffsetY = contentInViewport.y - targetInViewport.y;
        float desiredYInViewport = Mathf.Lerp(viewport.rect.yMin, viewport.rect.yMax, focusViewportYNormalized);
        float desiredOffsetFromTop = viewport.rect.yMax - desiredYInViewport;
        float adjustedOffsetY = targetOffsetY - desiredOffsetFromTop;

        float normalized = Mathf.Clamp01(1f - (adjustedOffsetY / hiddenHeight));
        if (scrollFocusLerpSpeed <= 0f)
        {
            runningBlocksScrollRect.verticalNormalizedPosition = normalized;
            return;
        }

        if (scrollFocusRoutine != null)
        {
            StopCoroutine(scrollFocusRoutine);
        }

        scrollFocusRoutine = StartCoroutine(LerpScrollTo(normalized));
    }

    private IEnumerator LerpScrollTo(float targetNormalized)
    {
        while (Mathf.Abs(runningBlocksScrollRect.verticalNormalizedPosition - targetNormalized) > 0.001f)
        {
            float next = Mathf.Lerp(
                runningBlocksScrollRect.verticalNormalizedPosition,
                targetNormalized,
                Time.deltaTime * scrollFocusLerpSpeed);

            runningBlocksScrollRect.verticalNormalizedPosition = next;
            yield return null;
        }

        runningBlocksScrollRect.verticalNormalizedPosition = targetNormalized;
        scrollFocusRoutine = null;
    }

    private void SetActiveCommandOutline(ProgramBlockView nextBlock)
    {
        if (activeOutlinedBlock != null && activeOutlinedBlock != nextBlock)
        {
            activeOutlinedBlock.SetActiveOutline(false);
        }

        activeOutlinedBlock = nextBlock;

        if (activeOutlinedBlock != null)
        {
            activeOutlinedBlock.SetActiveOutline(true);
        }
    }

    private void ExecuteCommandForAllClones(ProgramCommandType commandType)
    {
        if (cloneParent == null)
        {
            return;
        }

        int executedCount = 0;
        int childCount = cloneParent.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = cloneParent.GetChild(i);
            if (child.TryGetComponent(out CloneController clone))
            {
                clone.ExecuteCommand(commandType);
                executedCount++;
            }
        }

        if (executedCount == 0)
        {
        }
    }

    private float GetMaxCloneActionDuration()
    {
        if (cloneParent == null)
        {
            return 0f;
        }

        float maxDuration = 0f;
        int childCount = cloneParent.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = cloneParent.GetChild(i);
            if (child.TryGetComponent(out CloneController clone))
            {
                if (clone.ActionDuration > maxDuration)
                {
                    maxDuration = clone.ActionDuration;
                }
            }
        }

        return maxDuration;
    }
}