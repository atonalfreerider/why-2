#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Render;
using Shapes;
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

        List<float> anglePositions = new();
        foreach (TreeNode treeNode in flatList)
        {
            float birth = 4200 - treeNode.Birth();
            float death = 4200 - treeNode.DistanceToRoot();
            float prctBirth = Main.Instance.ToArbitrary(birth * Mathf.Pow(10, 6));
            float prctDeath = Main.Instance.ToArbitrary(death * Mathf.Pow(10, 6));

            prctBirth = Main.RoundToThree(prctBirth);
            prctDeath = Main.RoundToThree(prctDeath);

            treeNode.BirthDeath = new Tuple<float, float>(prctBirth, prctDeath);
            anglePositions.Add(prctBirth);
            anglePositions.Add(prctDeath);
        }

        anglePositions = anglePositions.Distinct().ToList();
        anglePositions.Sort();
        anglePositions.Reverse();

        // start at root birth

        Dictionary<float, List<TreeNode>> slices = new();

        const float tolerance = 0.001f;
        foreach (float angle in anglePositions)
        {
            if (angle < .4f) break;

            List<TreeNode> slice = new();
            // roll forward
            foreach (TreeNode treeNode in flatList)
            {
                if ((treeNode.BirthDeath.Item1 > angle ||
                     Mathf.Abs(treeNode.BirthDeath.Item1 - angle) < tolerance) &&
                    (treeNode.BirthDeath.Item2 < angle ||
                     Mathf.Abs(treeNode.BirthDeath.Item2 - angle) < tolerance))
                {
                    slice.Add(treeNode);
                }
            }

            slice.Reverse();

            slices.Add(angle, slice);
        }

        Dictionary<string, List<Vector3>> branchesByLabel = new();
        float lastAngle = -1;
        foreach (float angle in anglePositions)
        {
            if (angle < .4f) break;

            List<TreeNode> slice = slices[angle];
            float r = 2f;

            if (lastAngle > 0)
            {
                foreach (TreeNode needsParent in slice.Except(slices[lastAngle])) // new children
                {
                    if (needsParent.Parent != null)
                    {
                        branchesByLabel.TryGetValue(needsParent.Parent.Label, out List<Vector3> branchToParent);
                        if (branchToParent != null)
                        {
                            List<Vector3> branch = new List<Vector3>();
                            branchesByLabel.Add(needsParent.Label, branch);
                            branch.Add(branchToParent.Last());
                        }
                    }
                }
            }

            foreach (TreeNode treeNode in slice)
            {
                branchesByLabel.TryGetValue(treeNode.Label, out List<Vector3> branch);
                if (branch == null)
                {
                    branch = new List<Vector3>();
                    branchesByLabel.Add(treeNode.Label, branch);
                }

                Vector3 pt = new Vector3(
                    r * Mathf.Sin(angle * Mathf.PI * 2f),
                    0f,
                    -r * Mathf.Cos(angle * Mathf.PI * 2f));

                r += .03f;
                branch.Add(pt);
            }

            lastAngle = angle;
        }

        List<MeshFilter> linkFilters = new();
        foreach ((string label, List<Vector3> line) in branchesByLabel)
        {
            Vector3[] bezier = Line.BezierCurve(line.ToArray());
            Line l = new GameObject(label).AddComponent<Line>();
            PolygonFactory.AddMesh(l.gameObject, l, PolygonFactory.Instance.mainMat);
            l.rend = l.gameObject.GetComponent<Renderer>();
            l.DrawLine(bezier, .01f, false, 2);
            if (label == "Hominidae")
            {
                l.SetColor(Color.blue);
            }
            else
            {
                linkFilters.Add(l.meshFilter);
            }
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