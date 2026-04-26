using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProgramBlockView : MonoBehaviour
{
    public enum BlockKind
    {
        Command,
        Repeat
    }

    [Header("Block Type")]
    [SerializeField] private BlockKind blockKind = BlockKind.Command;
    [SerializeField] private ProgramCommandType commandType = ProgramCommandType.MoveForward;

    [Header("Repeat")]
    [SerializeField, Min(1)] private int repeatCount = 2;
    [SerializeField] private ProgramDropZone repeatDropZone;
    [SerializeField] private VerticalLayoutGroup sizeRefreshLayoutGroup;

    [Header("UI")]
    [SerializeField] private TMP_Text indexText;
    [SerializeField] private GameObject activeOutline;

    public BlockKind Kind => blockKind;
    public ProgramCommandType CommandType => commandType;
    public int RepeatCount => Mathf.Max(1, repeatCount);
    public ProgramDropZone RepeatDropZone => repeatDropZone;

    public void SetRepeatCount(int value)
    {
        repeatCount = Mathf.Max(1, value);
        UpdateSizeFitter();

        ProgramBlockView[] parentBlocks = GetComponentsInParent<ProgramBlockView>(includeInactive: true);
        for (int i = 0; i < parentBlocks.Length; i++)
        {
            if (parentBlocks[i] != this)
            {
                parentBlocks[i].UpdateSizeFitter();
            }
        }
    }

    public bool TryBuildNode(out ProgramNode node, out string error)
    {
        node = null;
        error = null;

        if (blockKind == BlockKind.Command)
        {
            node = new CommandNode(commandType);
            return true;
        }

        if (repeatDropZone == null)
        {
            error = $"repeat block '{name}' is misig inner drop zone";
            return false;
        }

        if (!repeatDropZone.TryBuildNodes(out List<ProgramNode> children, out string childError))
        {
            error = childError;
            return false;
        }

        node = new RepeatNode(RepeatCount, children);
        return true;
    }

    public void UpdateSizeFitter()
    {
        if (Kind != BlockKind.Repeat || sizeRefreshLayoutGroup == null)
        {
            return;
        }

        sizeRefreshLayoutGroup.enabled = false;
        sizeRefreshLayoutGroup.enabled = true;
    }

    public void SetDisplayIndex(int index)
    {
        if (indexText == null)
        {
            return;
        }

        indexText.text = index.ToString();
    }

    private void OnEnable()
    {
        SetActiveOutline(false);
    }

    public void SetActiveOutline(bool isActive)
    {
        if (activeOutline != null)
        {
            activeOutline.SetActive(isActive);
        }
    }
}
