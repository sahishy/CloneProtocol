using UnityEngine;
using System.Collections;

public class OptionsHandler : MonoBehaviour
{
    [Header("Options Menu")]
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private CanvasGroup optionsCanvasGroup;
    private float panelFadeDuration = 0.2f;

    [Header("Menu Button Icons")]
    [SerializeField] private GameObject closedIcon;
    [SerializeField] private GameObject openIcon;

    private bool isOpen;
    private bool isAudioEnabled = true;
    private Coroutine panelFadeRoutine;

    private void Start()
    {
        SetOptionsOpen(false);
    }

    public void ToggleOptions()
    {
        SetOptionsOpen(!isOpen);
    }

    public void OpenOptions()
    {
        SetOptionsOpen(true);
    }

    public void CloseOptions()
    {
        SetOptionsOpen(false);
    }

    public void ToggleAudio()
    {
        isAudioEnabled = !isAudioEnabled;

        AudioSource[] allAudioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        for (int i = 0; i < allAudioSources.Length; i++)
        {
            AudioSource audioSource = allAudioSources[i];
            if (audioSource != null)
            {
                audioSource.enabled = isAudioEnabled;
            }
        }
    }

    public void RestartLevel()
    {
        GameHandler.Instance?.RestartLevelState();
        CloseOptions();
    }

    private void SetOptionsOpen(bool shouldBeOpen)
    {
        isOpen = shouldBeOpen;

        if (panelFadeRoutine != null)
        {
            StopCoroutine(panelFadeRoutine);
            panelFadeRoutine = null;
        }

        if (optionsCanvasGroup != null)
        {
            if (isOpen && optionsPanel != null && !optionsPanel.activeSelf)
            {
                optionsPanel.SetActive(true);
            }

            panelFadeRoutine = StartCoroutine(FadeOptionsPanel(isOpen));
        }
        else if (optionsPanel != null)
        {
            optionsPanel.SetActive(isOpen);
        }

        if (closedIcon != null)
        {
            closedIcon.SetActive(!isOpen);
        }

        if (openIcon != null)
        {
            openIcon.SetActive(isOpen);
        }
    }

    private IEnumerator FadeOptionsPanel(bool show)
    {
        float startAlpha = optionsCanvasGroup.alpha;
        float targetAlpha = show ? 1f : 0f;

        if (show)
        {
            optionsCanvasGroup.interactable = true;
            optionsCanvasGroup.blocksRaycasts = true;
        }

        if (panelFadeDuration <= 0f)
        {
            optionsCanvasGroup.alpha = targetAlpha;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < panelFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / panelFadeDuration);
                optionsCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            optionsCanvasGroup.alpha = targetAlpha;
        }

        if (!show)
        {
            optionsCanvasGroup.interactable = false;
            optionsCanvasGroup.blocksRaycasts = false;

            if (optionsPanel != null)
            {
                optionsPanel.SetActive(false);
            }
        }

        panelFadeRoutine = null;
    }
}
