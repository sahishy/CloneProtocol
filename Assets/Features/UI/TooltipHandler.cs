using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipHandler : MonoBehaviour
{
    public static TooltipHandler Instance { get; private set; }

    [Header("Tooltip References")]
    [SerializeField] private GameObject tooltipRoot;
    [SerializeField] private CanvasGroup tooltipCanvasGroup;
    [SerializeField] private VerticalLayoutGroup tooltipLayoutGroup;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text bodyText;

    [Header("Behavior")]
    [SerializeField, Min(0f)] private float fadeDuration = 0.12f;

    private bool isShowing;
    private Object currentSource;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (tooltipRoot != null)
        {
            tooltipRoot.SetActive(false);
        }

        if (tooltipCanvasGroup != null)
        {
            tooltipCanvasGroup.alpha = 0f;
            tooltipCanvasGroup.interactable = false;
            tooltipCanvasGroup.blocksRaycasts = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ShowFrom(Object source, string header, string body)
    {
        if (tooltipRoot == null)
        {
            return;
        }

        currentSource = source;

        if (headerText != null)
        {
            headerText.text = header ?? string.Empty;
            headerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(header));
        }

        if (bodyText != null)
        {
            bodyText.text = body ?? string.Empty;
        }

        RefreshLayout();

        tooltipRoot.SetActive(true);
        StartFade(1f);
        isShowing = true;
    }

    public void Hide()
    {
        currentSource = null;
        isShowing = false;

        if (tooltipRoot != null)
        {
            if (tooltipCanvasGroup == null)
            {
                tooltipRoot.SetActive(false);
            }
            else
            {
                StartFade(0f, disableRootOnComplete: true);
            }
        }
    }

    public void HideFrom(Object source)
    {
        if (source == null || currentSource == source)
        {
            Hide();
        }
    }

    private void StartFade(float targetAlpha, bool disableRootOnComplete = false)
    {
        if (tooltipCanvasGroup == null)
        {
            if (disableRootOnComplete && tooltipRoot != null)
            {
                tooltipRoot.SetActive(false);
            }

            return;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, disableRootOnComplete));
    }

    private System.Collections.IEnumerator FadeRoutine(float targetAlpha, bool disableRootOnComplete)
    {
        float startAlpha = tooltipCanvasGroup.alpha;

        tooltipCanvasGroup.interactable = targetAlpha > 0f;
        tooltipCanvasGroup.blocksRaycasts = targetAlpha > 0f;

        if (fadeDuration <= 0f)
        {
            tooltipCanvasGroup.alpha = targetAlpha;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                float eased = t * t * (3f - (2f * t));
                tooltipCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
                yield return null;
            }

            tooltipCanvasGroup.alpha = targetAlpha;
        }

        if (disableRootOnComplete && tooltipRoot != null)
        {
            tooltipRoot.SetActive(false);
        }

        fadeCoroutine = null;
    }

    public void RefreshLayout()
    {
        if (tooltipLayoutGroup == null)
        {
            return;
        }

        tooltipLayoutGroup.enabled = false;
        tooltipLayoutGroup.enabled = true;
    }
}