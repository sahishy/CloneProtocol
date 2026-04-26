using System.Collections.Generic;
using UnityEngine;

public enum ProgramCommandType
{
    MoveForward = 0,
    TurnLeft = 1,
    TurnRight = 2
}

public abstract class ProgramNode
{
    public abstract void AppendCommands(List<ProgramCommandType> output);
}

public sealed class CommandNode : ProgramNode
{
    public CommandNode(ProgramCommandType commandType)
    {
        CommandType = commandType;
    }

    public ProgramCommandType CommandType { get; }

    public override void AppendCommands(List<ProgramCommandType> output)
    {
        output.Add(CommandType);
    }
}

public sealed class RepeatNode : ProgramNode
{
    public RepeatNode(int repeatCount, List<ProgramNode> children)
    {
        RepeatCount = Mathf.Max(1, repeatCount);
        Children = children ?? new List<ProgramNode>();
    }

    public int RepeatCount { get; }
    public List<ProgramNode> Children { get; }

    public override void AppendCommands(List<ProgramCommandType> output)
    {
        if (Children.Count == 0)
        {
            return;
        }

        List<ProgramCommandType> chunk = new();
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i]?.AppendCommands(chunk);
        }

        for (int i = 0; i < RepeatCount; i++)
        {
            output.AddRange(chunk);
        }
    }
}

public static class ProgramCompiler
{
    public static bool TryCompile(
        IReadOnlyList<ProgramNode> rootNodes,
        int maxExpandedCommandCount,
        out List<ProgramCommandType> compiledCommands,
        out string error)
    {
        compiledCommands = new List<ProgramCommandType>();
        error = null;

        int rootCount = rootNodes != null ? rootNodes.Count : 0;
        for (int i = 0; i < rootCount; i++)
        {
            rootNodes[i]?.AppendCommands(compiledCommands);

            if (maxExpandedCommandCount > 0 && compiledCommands.Count > maxExpandedCommandCount)
            {
                error = $"prgram too long after repeat ({compiledCommands.Count}/{maxExpandedCommandCount})";
                return false;
            }
        }

        return true;
    }
}
