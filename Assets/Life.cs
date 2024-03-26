#nullable enable
using System.Collections.Generic;
using System.Linq;
using Render;
using Shapes.Lines;
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

        List<TreeNode> flatList = GetAllNodes(tree).ToList();

        Dictionary<string, Vector3> lastTransformByName = new();
        List<string> hasAppeared = new();
        List<MeshFilter> linkFilters = new();
        
        for (float i = .7f; i > .3f; i -= .01f)
        {
            float mya = Main.Instance.FromArbitrary(i) / Mathf.Pow(10, 6);

            List<TreeNode> currentSliceStack = new();
            foreach (TreeNode treeNode in flatList)
            {
                if (4200 - treeNode.Birth() >= mya && // born before the tick mark
                    treeNode.DistanceToRoot() > 4200 - mya) // died after the tick mark
                {
                    currentSliceStack.Add(treeNode);
                }
            }

            currentSliceStack = currentSliceStack.OrderByDescending(x => x.Birth()).ToList();

            Dictionary<string, Vector3> currentTransformByName = new();

            float r = 2;
            foreach (TreeNode treeNode in currentSliceStack)
            {
                Vector3 pt = new Vector3(
                    r * Mathf.Sin(i * Mathf.PI * 2f),
                    0f,
                    -r * Mathf.Cos(i * Mathf.PI * 2f));

                r += .03f;

                TreeNode? labeledParent = treeNode.Parent;
                if (!hasAppeared.Contains(treeNode.Label) && labeledParent != null)
                {
                    if (lastTransformByName.ContainsKey(labeledParent.Label))
                    {
                        StaticLink link = Instantiate(StaticLink.prototypeStaticLink);
                        link.name = $"{labeledParent.Label} -> {treeNode.Label}";
                        link.transform.SetParent(transform, false);
                        link.LW = .001f;
                        link.DrawFromTo(pt, lastTransformByName[labeledParent.Label]);
                        linkFilters.Add(link.meshFilter);
                    }
                }
                else if (lastTransformByName.ContainsKey(treeNode.Label))
                {
                    StaticLink link = Instantiate(StaticLink.prototypeStaticLink);
                    link.name = $"{treeNode.Label} -> {treeNode.Label}";
                    link.gameObject.SetActive(true);
                    link.transform.SetParent(transform, false);
                    link.LW = .001f;
                    link.DrawFromTo(pt, lastTransformByName[treeNode.Label]);

                    if (treeNode.Label == "Hominidae")
                    {
                        link.SetColor(Color.blue);
                    }
                    else
                    {
                        linkFilters.Add(link.meshFilter);
                    }
                }

                if (!hasAppeared.Contains(treeNode.Label))
                {
                    hasAppeared.Add(treeNode.Label);
                }

                currentTransformByName.Add(treeNode.Label, pt);
            }

            lastTransformByName = currentTransformByName;
        }

        MeshCombiner meshCombiner = gameObject.AddComponent<MeshCombiner>();
        meshCombiner.Init(linkFilters.ToArray(), transform, Color.green);
        meshCombiner.RecreateCombines();
        meshCombiner.SetDisplayStateCombinesAndIndividuals(true, false);

        foreach (MeshFilter filter in linkFilters)
        {
            Destroy(filter.gameObject);
        }
    }

    static IEnumerable<TreeNode> GetAllNodes(TreeNode node)
    {
        List<TreeNode> nodes = new() { node };
        foreach (TreeNode child in node.Children)
        {
            nodes.AddRange(GetAllNodes(child));
        }

        return nodes;
    }
}