# graphics-util

A Unity utility for procedural graphics.

Includes a mesh combiner and polygon pool to accelerate graphical performance.

There must be a PolygonFactory and a PolygonPoolMonoBehaviour attached to a gameobject, and the MainMat must be assigned (e.g. Default-Material)

To instantiate

```
Instantiate(PolygonFactory.Instance.icosahedron0)
```


To combine

```
combiner = new GameObject().AddComponent<MeshCombiner>();
List<MeshFilter> fiters = new List<MeshFilter>();

// populate filters with instantiated objects using .GetComponent<MeshFilter>()

combiner.Init(fiters.ToArray(), transform, Color.white);
combiner.RecreateCombines();
combiner.SetDisplayStateCombinesAndIndividuals(true, false);
```

The combiner makes use of a polygon pool so that recently created gameobjects are recycled.