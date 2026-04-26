using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class DialogueHandler : MonoBehaviour
{
    [System.Serializable]
    public class DialogueEntry
    {
        public int type;
        [TextArea(2, 6)]
        public string text;
    }

    private enum DialogueVisualType
    {
        Default = 0,
        ProgramHidden = 1,
        ZoomedOut = 2
    }

    private struct VisualState
    {
        public Vector2 dialoguePos;
        public Vector2 programBuilderPos;
        public Vector3 cameraPos;
        public float cameraFov;
        public bool hideProgramBuilder;
    }

    [Header("Dialogue Data")]
    [SerializeField] private List<DialogueEntry> dialogueEntries = new();
    [SerializeField] private TMP_Text dialogueText;

    [Header("References")]
    [SerializeField] private RectTransform dialogueRectTransform;
    [SerializeField] private RectTransform programBuilderRectTransform;
    [SerializeField] private CanvasGroup programBuilderCanvasGroup;
    [SerializeField] private AudioSource dialogueSound;
    private Camera sceneCamera;

    [Header("Interaction")]
    [SerializeField] private bool autoStartOnEnable = false;
    [SerializeField] private bool advanceOnMouseClick = true;

    [Header("Typewriter")]
    private string dialogueLinePrefix = "                       ";
    private float characterDelay = 0.03f;
    private float commaExtraDelay = 0.3f;
    private float periodExtraDelay = 0.6f;

    private float dialogueSoundCooldown = 0.06f;
    private float nextDialogueSoundTime;

    [Header("Animation")]
    private float transitionDuration = 1f;

    [Header("Dialogue Position Targets")]
    private Vector2 dialogueDefaultPos = new(225f, 50f);
    private Vector2 dialogueCenteredPos = new(0f, 50f);
    [SerializeField] private float dialogueIdleY = -150f;

    [Header("Program Builder Position Targets")]
    private Vector2 programBuilderDefaultPos = new(48f, 0f);
    private Vector2 programBuilderHiddenPos = new(-400f, 0f);

    [Header("Camera Targets")]
    private Vector3 cameraDefaultPos = new(-5f, 30f, -11.5f);
    private Vector3 cameraCenteredPos = new(0f, 30f, -11.5f);
    private float cameraDefaultFov = 30f;
    private float cameraZoomedOutFov = 50f;

    [Header("Events")]
    [SerializeField] private UnityEvent onDialogueStarted;
    [SerializeField] private UnityEvent onDialogueFinished;

    private Coroutine typewriterCoroutine;
    private Coroutine visualTransitionCoroutine;
    private Coroutine dialogueIdleCoroutine;

    private bool isDialogueRunning;
    private bool isTyping;
    private int currentIndex = -1;

    public bool IsDialogueRunning => isDialogueRunning;

    private void Awake()
    {
        sceneCamera = Camera.main;
    }

    private void OnEnable()
    {
        SetDialogueToIdleImmediate();

        if (autoStartOnEnable)
        {
            StartDialogue();
        }
    }

    private void Update()
    {
        if (!advanceOnMouseClick || !isDialogueRunning)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            AdvanceDialogue();
        }
    }

    public void StartDialogue()
    {
        if (dialogueEntries == null || dialogueEntries.Count == 0)
        {
            return;
        }

        if (sceneCamera == null)
        {
            sceneCamera = Camera.main;
        }

        if (dialogueIdleCoroutine != null)
        {
            StopCoroutine(dialogueIdleCoroutine);
            dialogueIdleCoroutine = null;
        }

        isDialogueRunning = true;
        currentIndex = 0;
        onDialogueStarted?.Invoke();
        ShowCurrentEntry();
    }

    public void AdvanceDialogue()
    {
        if (!isDialogueRunning)
        {
            return;
        }

        UIGameAudioHandler.Instance.PlayClickSound();

        if (isTyping)
        {
            RevealCurrentLineImmediately();
            return;
        }

        currentIndex++;
        if (currentIndex >= dialogueEntries.Count)
        {
            EndDialogue();
            return;
        }

        ShowCurrentEntry();
    }

    private void ShowCurrentEntry()
    {
        if (currentIndex < 0 || currentIndex >= dialogueEntries.Count)
        {
            return;
        }

        DialogueEntry entry = dialogueEntries[currentIndex];
        DialogueVisualType visualType = ParseVisualType(entry.type);

        if (visualTransitionCoroutine != null)
        {
            StopCoroutine(visualTransitionCoroutine);
        }

        visualTransitionCoroutine = StartCoroutine(AnimateToVisualState(GetVisualState(visualType)));

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
        }

        typewriterCoroutine = StartCoroutine(TypewriterRoutine(entry.text ?? string.Empty));
    }

    private IEnumerator TypewriterRoutine(string fullText)
    {
        if (dialogueText == null)
        {
            yield break;
        }

        string displayText = $"{dialogueLinePrefix}{fullText}";
        isTyping = true;

        dialogueText.text = displayText;

        dialogueText.maxVisibleCharacters = 0;

        for (int i = 0; i < displayText.Length; i++)
        {
            char c = displayText[i];

            dialogueText.maxVisibleCharacters = i + 1;

            bool isPauseCharacter = c == ',' || c == ':' || c == '.' || c == '?';
            bool shouldPlaySound = !char.IsWhiteSpace(c) && !isPauseCharacter;

            if (shouldPlaySound && dialogueSound != null && Time.time >= nextDialogueSoundTime)
            {
                dialogueSound.Stop();
                dialogueSound.Play();
                nextDialogueSoundTime = Time.time + dialogueSoundCooldown;
            }
            else if (isPauseCharacter && dialogueSound != null)
            {
                dialogueSound.Stop();
            }

            float delay = characterDelay;

            if (c == ',' || c == ':')
                delay += commaExtraDelay;
            else if (c == '.' || c == '?')
                delay += periodExtraDelay;

            yield return new WaitForSeconds(delay);

            if (isPauseCharacter && dialogueSound != null)
            {
                dialogueSound.Stop();
            }
        }

        if (dialogueSound != null)
        {
            dialogueSound.Stop();
        }

        isTyping = false;

        typewriterCoroutine = null;
    }

    private void RevealCurrentLineImmediately()
    {
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        if (dialogueText != null)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
        }
        if (dialogueSound != null)
        {
            dialogueSound.Stop();
        }
        isTyping = false;
    }

    private IEnumerator AnimateToVisualState(VisualState target)
    {
        Vector2 dialogueStart = dialogueRectTransform != null ? dialogueRectTransform.anchoredPosition : Vector2.zero;
        Vector2 programStart = programBuilderRectTransform != null ? programBuilderRectTransform.anchoredPosition : Vector2.zero;
        float programAlphaStart = programBuilderCanvasGroup != null ? programBuilderCanvasGroup.alpha : 1f;

        Camera cam = sceneCamera != null ? sceneCamera : Camera.main;
        Vector3 cameraPosStart = cam != null ? cam.transform.position : Vector3.zero;
        float cameraFovStart = cam != null ? cam.fieldOfView : cameraDefaultFov;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = transitionDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / transitionDuration);
            float eased = t * t * (3f - (2f * t));

            if (dialogueRectTransform != null)
            {
                dialogueRectTransform.anchoredPosition = Vector2.Lerp(dialogueStart, target.dialoguePos, eased);
            }

            if (programBuilderRectTransform != null)
            {
                programBuilderRectTransform.anchoredPosition = Vector2.Lerp(programStart, target.programBuilderPos, eased);
            }

            if (programBuilderCanvasGroup != null)
            {
                float targetAlpha = target.hideProgramBuilder ? 0f : 1f;
                programBuilderCanvasGroup.alpha = Mathf.Lerp(programAlphaStart, targetAlpha, eased);
            }

            if (cam != null)
            {
                cam.transform.position = Vector3.Lerp(cameraPosStart, target.cameraPos, eased);
                cam.fieldOfView = Mathf.Lerp(cameraFovStart, target.cameraFov, eased);
            }

            yield return null;
        }

        if (dialogueRectTransform != null)
        {
            dialogueRectTransform.anchoredPosition = target.dialoguePos;
        }

        if (programBuilderRectTransform != null)
        {
            programBuilderRectTransform.anchoredPosition = target.programBuilderPos;
        }

        if (programBuilderCanvasGroup != null)
        {
            programBuilderCanvasGroup.alpha = target.hideProgramBuilder ? 0f : 1f;
            programBuilderCanvasGroup.blocksRaycasts = !target.hideProgramBuilder;
            programBuilderCanvasGroup.interactable = !target.hideProgramBuilder;
        }

        if (cam != null)
        {
            cam.transform.position = target.cameraPos;
            cam.fieldOfView = target.cameraFov;
        }

        visualTransitionCoroutine = null;
    }

    private VisualState GetVisualState(DialogueVisualType type)
    {
        VisualState state = new VisualState
        {
            dialoguePos = dialogueDefaultPos,
            programBuilderPos = programBuilderDefaultPos,
            cameraPos = cameraDefaultPos,
            cameraFov = cameraDefaultFov,
            hideProgramBuilder = false
        };

        switch (type)
        {
            case DialogueVisualType.ProgramHidden:
                state.dialoguePos = dialogueCenteredPos;
                state.programBuilderPos = programBuilderHiddenPos;
                state.cameraPos = cameraCenteredPos;
                state.cameraFov = cameraDefaultFov;
                state.hideProgramBuilder = true;
                break;

            case DialogueVisualType.ZoomedOut:
                state.dialoguePos = dialogueCenteredPos;
                state.programBuilderPos = programBuilderHiddenPos;
                state.cameraPos = cameraCenteredPos;
                state.cameraFov = cameraZoomedOutFov;
                state.hideProgramBuilder = true;
                break;
        }

        return state;
    }

    private DialogueVisualType ParseVisualType(int type)
    {
        if (type < 0 || type > 2)
        {
            return DialogueVisualType.Default;
        }

        return (DialogueVisualType)type;
    }

    private void EndDialogue()
    {
        isDialogueRunning = false;
        isTyping = false;

        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }

        if (dialogueSound != null)
        {
            dialogueSound.Stop();
        }

        if (dialogueIdleCoroutine != null)
        {
            StopCoroutine(dialogueIdleCoroutine);
        }

        dialogueIdleCoroutine = StartCoroutine(AnimateDialogueToIdleY());

        onDialogueFinished?.Invoke();
    }

    private void SetDialogueToIdleImmediate()
    {
        if (dialogueRectTransform == null)
        {
            return;
        }

        Vector2 anchoredPos = dialogueRectTransform.anchoredPosition;
        anchoredPos.y = dialogueIdleY;
        dialogueRectTransform.anchoredPosition = anchoredPos;
    }

    private IEnumerator AnimateDialogueToIdleY()
    {
        if (dialogueRectTransform == null)
        {
            yield break;
        }

        Vector2 start = dialogueRectTransform.anchoredPosition;
        Vector2 target = new(start.x, dialogueIdleY);

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = transitionDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / transitionDuration);
            float eased = t * t * (3f - (2f * t));
            dialogueRectTransform.anchoredPosition = Vector2.Lerp(start, target, eased);
            yield return null;
        }

        dialogueRectTransform.anchoredPosition = target;
        dialogueIdleCoroutine = null;
    }

}