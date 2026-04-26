using UnityEngine;
using UnityEngine.UI;

public class CursorHandler : MonoBehaviour
{
    public static CursorHandler Instance { get; private set; }

    public enum CursorState
    {
        Default,
        Dragging,
        DragHover,
        TextHover,
        NotAllowed
    }

    [Header("Cursor UI")]
    [SerializeField] private Image cursorImage;
    [SerializeField] private Canvas canvas;

    [Header("Cursor Sprites")]
    [SerializeField] private Sprite defaultSprite;
    [SerializeField] private Sprite draggingSprite;
    [SerializeField] private Sprite dragHoverSprite;
    [SerializeField] private Sprite textHoverSprite;
    [SerializeField] private Sprite notAllowedSprite;

    [Header("Click Scale")]
    private float clickScale = 0.9f;
    private float clickScaleLerpSpeed = 20f;
    private Vector3 normalScale = Vector3.one;

    public CursorState CurrentState { get; private set; } = CursorState.Default;

    private RectTransform cursorRect;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Cursor.visible = false;

        cursorRect = cursorImage.GetComponent<RectTransform>();
        SetState(CursorState.Default);
    }

    private void Update()
    {
        UpdateCursorPosition();
        UpdateCursorScale();
    }

    private void UpdateCursorScale()
    {
        if (cursorRect == null) return;

        Vector3 targetScale = Input.GetMouseButton(0)
            ? normalScale * clickScale
            : normalScale;

        float t = Time.unscaledDeltaTime * clickScaleLerpSpeed;
        cursorRect.localScale = Vector3.Lerp(cursorRect.localScale, targetScale, t);
    }

    private void UpdateCursorPosition()
    {
        if (cursorRect == null || canvas == null) return;

        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            Input.mousePosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out pos
        );

        cursorRect.anchoredPosition = pos + new Vector2(10, -10);
    }

    private void SetState(CursorState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        UpdateCursorImage();
    }

    private void UpdateCursorImage()
    {
        if (cursorImage == null) return;

        switch (CurrentState)
        {
            case CursorState.Default:
                cursorImage.sprite = defaultSprite;
                break;

            case CursorState.Dragging:
                cursorImage.sprite = draggingSprite;
                break;

            case CursorState.DragHover:
                cursorImage.sprite = dragHoverSprite;
                break;

            case CursorState.TextHover:
                cursorImage.sprite = textHoverSprite;
                break;

            case CursorState.NotAllowed:
                cursorImage.sprite = notAllowedSprite;
                break;

        }
    }

    public void SetDragHover()
    {
        if (CurrentState == CursorState.Dragging) return;
        SetState(CursorState.DragHover);
    }

    public void SetTextHover()
    {
        if (CurrentState == CursorState.Dragging) return;
        SetState(CursorState.TextHover);
    }

    public void SetDefault(bool force)
    {
        if (CurrentState == CursorState.Dragging && !force) return;
        SetState(CursorState.Default);
    }

    public void SetDragging()
    {
        SetState(CursorState.Dragging);
    }

    public void SetNotAllowed()
    {
        SetState(CursorState.NotAllowed);
    }

}
