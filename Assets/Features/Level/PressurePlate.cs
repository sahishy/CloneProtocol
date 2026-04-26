using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class PressurePlate : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private LayerMask activatorLayers;
    [SerializeField, Min(0f)] private float pressActivationDelay = 0.5f;

    [Header("Plate Materials")]
    [SerializeField] private MeshRenderer plateMeshRenderer;
    [SerializeField] private Material unpressedMaterial;
    [SerializeField] private Material pressedMaterial;

    [Header("Progress UI")]
    [SerializeField] private CanvasGroup barHolder;
    [SerializeField] private Image fillBar;
    [SerializeField, Min(0f)] private float alphaLerpSpeed = 8f;
    [SerializeField, Min(0f)] private float fillLerpSpeed = 8f;
    [SerializeField] private Color fullFillColor = Color.yellow;

    [Header("Sound")]
    [SerializeField] private AudioSource activateSound;
    [SerializeField] private AudioSource pressSound;
    [SerializeField] private AudioSource timerSound;

    [Header("Animation")]
    [SerializeField] private Transform plateVisual;
    [SerializeField] private Vector3 unpressedLocalPosition = new(0f, 0.2f, 0f);
    [SerializeField] private Vector3 pressedLocalPosition = new(0f, 0.15f, 0f);
    [SerializeField, Min(0f)] private float plateLerpSpeed = 10f;

    [SerializeField, Min(0f)] private float fallbackRequiredHoldDuration = 2f;

    public bool IsPressed { get; private set; }

    private int activatorCount = 0;
    private float pendingPressTimer;
    private bool pendingPress;
    private float pressedTimer;
    private float displayedBarAlpha;
    private float displayedFillAmount;
    private Color displayedFillColor;
    private Color normalFillColor = Color.white;
    private Vector3 displayedPlateLocalPosition;
    private bool hasPlayedHoldCompleteSound;

    private void Start()
    {
        if (plateMeshRenderer == null)
        {
            plateMeshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        if (barHolder != null)
        {
            displayedBarAlpha = barHolder.alpha;
        }

        if (fillBar != null)
        {
            displayedFillAmount = fillBar.fillAmount;
            displayedFillColor = fillBar.color;
            normalFillColor = fillBar.color;
        }

        if (plateVisual != null)
        {
            displayedPlateLocalPosition = plateVisual.localPosition;
        }

        StopTimerSound(restart: true);

        UpdatePlateMaterial();
    }

    private void Update()
    {
        if (!IsPressed && activatorCount > 0)
        {
            if (!pendingPress)
            {
                pendingPress = true;
                pendingPressTimer = 0f;
            }

            pendingPressTimer += Time.deltaTime;
            if (pendingPressTimer >= pressActivationDelay)
            {
                pendingPress = false;
                pendingPressTimer = 0f;
                IsPressed = true;
                hasPlayedHoldCompleteSound = false;
                StartTimerSound();
                UpdatePlateMaterial();
                Notify(true);
            }
        }
        else if (activatorCount == 0)
        {
            pendingPress = false;
            pendingPressTimer = 0f;
        }

        float requiredHoldDuration = GetRequiredHoldDuration();

        if (IsPressed)
        {
            pressedTimer += Time.deltaTime;

            if (!hasPlayedHoldCompleteSound && (requiredHoldDuration <= 0f || pressedTimer >= requiredHoldDuration))
            {
                hasPlayedHoldCompleteSound = true;
                StopTimerSound(restart: true);
                PlayActivationSound();
            }
        }
        else
        {
            pressedTimer = 0f;
        }

        float targetAlpha = IsPressed ? 1f : 0f;
        float targetFill = IsPressed
            ? (requiredHoldDuration <= 0f ? 1f : Mathf.Clamp01(pressedTimer / requiredHoldDuration))
            : 0f;

        displayedBarAlpha = Mathf.Lerp(displayedBarAlpha, targetAlpha, Time.deltaTime * alphaLerpSpeed);
        displayedFillAmount = Mathf.Lerp(displayedFillAmount, targetFill, Time.deltaTime * fillLerpSpeed);
        Vector3 targetPlateLocalPosition = IsPressed ? pressedLocalPosition : unpressedLocalPosition;
        displayedPlateLocalPosition = Vector3.Lerp(displayedPlateLocalPosition, targetPlateLocalPosition, Time.deltaTime * plateLerpSpeed);

        if (barHolder != null)
        {
            barHolder.alpha = displayedBarAlpha;
        }

        if (fillBar != null)
        {
            Color targetFillColor = targetFill >= 1f ? fullFillColor : normalFillColor;
            displayedFillColor = Color.Lerp(displayedFillColor, targetFillColor, Time.deltaTime * fillLerpSpeed);
            fillBar.fillAmount = displayedFillAmount;
            fillBar.color = displayedFillColor;
        }

        if (plateVisual != null)
        {
            plateVisual.localPosition = displayedPlateLocalPosition;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidActivator(other)) return;

        activatorCount++;

        pressSound.Play();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsValidActivator(other)) return;

        activatorCount = Mathf.Max(0, activatorCount - 1);

        if (activatorCount == 0 && IsPressed)
        {
            IsPressed = false;
            pressedTimer = 0f;
            pendingPress = false;
            pendingPressTimer = 0f;
            hasPlayedHoldCompleteSound = false;
            StopTimerSound(restart: true);
            UpdatePlateMaterial();
            Notify(false);
        }
        else if (activatorCount == 0)
        {
            pendingPress = false;
            pendingPressTimer = 0f;
            hasPlayedHoldCompleteSound = false;
        }

        pressSound.Play();
    }

    private void UpdatePlateMaterial()
    {
        if (plateMeshRenderer == null)
        {
            return;
        }

        Material targetMaterial = IsPressed ? pressedMaterial : unpressedMaterial;
        if (targetMaterial != null)
        {
            plateMeshRenderer.material = targetMaterial;
        }
    }

    private bool IsValidActivator(Collider other)
    {
        return !other.isTrigger && ((1 << other.gameObject.layer) & activatorLayers) != 0;
    }

    private void Notify(bool state)
    {
        if (LevelHandler.Instance != null)
        {
            LevelHandler.Instance.NotifyPlateStateChanged(this, state);
        }
    }

    private float GetRequiredHoldDuration()
    {
        if (LevelHandler.Instance != null)
        {
            return LevelHandler.Instance.RequiredHoldDuration;
        }

        return fallbackRequiredHoldDuration;
    }

    private void PlayActivationSound()
    {
        if (activateSound == null)
        {
            return;
        }

        activateSound.Stop();
        activateSound.Play();
    }

    private void StartTimerSound()
    {
        if (timerSound == null)
        {
            return;
        }

        timerSound.Stop();
        timerSound.Play();
    }

    private void StopTimerSound(bool restart)
    {
        if (timerSound == null)
        {
            return;
        }

        timerSound.Stop();

        if (restart)
        {
            timerSound.time = 0f;
        }
    }

    public void ResetState()
    {
        activatorCount = 0;
        pendingPress = false;
        pendingPressTimer = 0f;
        pressedTimer = 0f;
        hasPlayedHoldCompleteSound = false;
        IsPressed = false;

        StopTimerSound(restart: true);
        UpdatePlateMaterial();

        displayedBarAlpha = 0f;
        displayedFillAmount = 0f;
        displayedFillColor = normalFillColor;
        displayedPlateLocalPosition = unpressedLocalPosition;

        if (barHolder != null)
        {
            barHolder.alpha = displayedBarAlpha;
        }

        if (fillBar != null)
        {
            fillBar.fillAmount = displayedFillAmount;
            fillBar.color = displayedFillColor;
        }

        if (plateVisual != null)
        {
            plateVisual.localPosition = unpressedLocalPosition;
        }
    }
}