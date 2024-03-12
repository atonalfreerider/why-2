#nullable enable
using UnityEngine;

public class Thing
{
    public GameObject What;
    public float When;
    public Thing? Cause;
    public Thing? Effect;
        
    public Thing(GameObject what, float when, Thing? cause, Thing? effect)
    {
        What = what;
        When = when;
        Cause = cause;
        Effect = effect;
    }
}