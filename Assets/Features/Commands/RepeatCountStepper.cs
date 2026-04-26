using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_InputField))]
public class RepeatCountStepper : MonoBehaviour
{
    [SerializeField] private ProgramBlockView repeatBlockView;
    [SerializeField, Min(1)] private int minRepeat = 1;
    [SerializeField, Min(1)] private int maxRepeat = 9;

    [SerializeField] private TMP_InputField inputField;

    public void HandleInputSubmitted(string value)
    {
        if (!int.TryParse(value, out int parsed))
        {
            parsed = GetCurrent();
        }

        SetRepeat(parsed);
    }

    public void SetRepeat(int value)
    {
        if (repeatBlockView == null)
        {
            SyncInputFromBlock();
            return;
        }

        int clamped = Mathf.Clamp(value, minRepeat, Mathf.Max(minRepeat, maxRepeat));
        repeatBlockView.SetRepeatCount(clamped);
        SetInputValue(clamped);

        UIGameAudioHandler.Instance.PlayPlaceBlockSound();

    }

    private void SyncInputFromBlock()
    {
        int value = GetCurrent();
        value = Mathf.Clamp(value, minRepeat, Mathf.Max(minRepeat, maxRepeat));
        SetInputValue(value);
    }

    private void SetInputValue(int value)
    {
        if (inputField == null)
        {
            return;
        }

        inputField.text = value.ToString();
        inputField.ForceLabelUpdate();
    }

    private int GetCurrent()
    {
        if (repeatBlockView == null)
        {
            return minRepeat;
        }

        return repeatBlockView.RepeatCount;
    }

    public void HandlePointerEnter()
    {
        CursorHandler.Instance.SetTextHover();
    }

    public void HandlePointerExit()
    {
        CursorHandler.Instance.SetDragHover();
    }

    public void PlayPlaceBlockSound()
    {
        UIGameAudioHandler.Instance.PlayPlaceBlockSound();
    }

    public void InputChange()
    {
        UIGameAudioHandler.Instance.PlayDragSound();
    }

}
