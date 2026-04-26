using UnityEngine;
using System.Collections;

public class EyesClosing : MonoBehaviour
{
    [Header("Eye Lids")]
    [SerializeField] private RectTransform topLid;
    [SerializeField] private RectTransform bottomLid;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float closedAmount = 360f;
    [SerializeField, Min(0f)] private float firstBlinkDuration = 0.10f;
    [SerializeField, Min(0f)] private float reopenDuration = 0.12f;
    [SerializeField, Min(0f)] private float secondCloseDuration = 0.10f;
    [SerializeField, Min(0f)] private float secondReopenDuration = 0.12f;
    [SerializeField, Min(0f)] private float finalCloseDuration = 0.18f;

    private Coroutine animationRoutine;

    private void Awake()
    {
        ApplyClosedFactor(0f);
    }

    public void TriggerAnimation()
    
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
        }

        animationRoutine = StartCoroutine(BlinkThenCloseRoutine());
    }

    private IEnumerator BlinkThenCloseRoutine()
    {
        yield return LerpClosedFactor(0f, 0.18f, firstBlinkDuration);
        yield return LerpClosedFactor(0.18f, 0f, reopenDuration);
        yield return LerpClosedFactor(0f, 0.34f, secondCloseDuration);
        yield return LerpClosedFactor(0.34f, 0f, secondReopenDuration);
        yield return LerpClosedFactor(0f, 1f, finalCloseDuration);

        yield return new WaitForSeconds(3);
        LevelHandler.Instance.LoadSceneIndex(0);

        animationRoutine = null;
    }

    private IEnumerator LerpClosedFactor(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            ApplyClosedFactor(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            float factor = Mathf.Lerp(from, to, t);
            ApplyClosedFactor(factor);
            yield return null;
        }

        ApplyClosedFactor(to);
    }

    private void ApplyClosedFactor(float factor)
    {
        float targetHeight = Mathf.Clamp01(factor) * closedAmount;

        if (topLid != null)
        {
            Vector2 topSize = topLid.sizeDelta;
            topSize.y = targetHeight;
            topLid.sizeDelta = topSize;
        }

        if (bottomLid != null)
        {
            Vector2 bottomSize = bottomLid.sizeDelta;
            bottomSize.y = targetHeight;
            bottomLid.sizeDelta = bottomSize;
        }
    }
}
