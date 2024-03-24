using System.Collections.Generic;
using UnityEngine;

public class TreeNode
{
    public TreeNode(string label, List<TreeNode> children, float? distance, string features)
    {
        Label = label;
        Children = children;
        Distance = distance;
        Features = features;
    }

    public string Label { get; }
    public List<TreeNode> Children { get; }
    public float? Distance { get; }
    public string Features { get; }

    public override string ToString()
    {
        return Label;
    }

    public void Print(int depth = 0)
    {
        Debug.Log($"{new string(' ', depth)}{Label} {Distance} {Features}");

        foreach (TreeNode child in Children)
        {
            child.Print(depth + 1);
        }
    }
}