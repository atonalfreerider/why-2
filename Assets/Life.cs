#nullable enable
using UnityEngine;

public class Life : MonoBehaviour
{
    void Start()
    {
        TextAsset newickText = Resources.Load<TextAsset>("TimetreeOfLife2009");

        TreeNode tree = NewickTreeParser.ParseNewick<TreeNode>(
            newickText.text,
            (label, children, distance, features) =>
                new TreeNode(label, children, distance, features));

        tree.Print();
    }
}