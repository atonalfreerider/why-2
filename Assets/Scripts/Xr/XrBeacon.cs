using UnityEngine;
using Why.Director;

namespace Why.Xr
{
    /// <summary>
    /// The guided tour's attention arrow in a headset: the 2D arrow is drawn on the screen and cannot point into a
    /// stereo world, so the tour's target is marked where it is instead - a slowly turning ring around the anchor and a
    /// beam rising from it, both sized in metres so they read the same at any scale of the world.
    /// </summary>
    public sealed class XrBeacon
    {
        const int RingSegments = 48;
        const float RingMetres = 0.05f;
        const float BeamMetres = 0.45f;

        readonly LineRenderer ring, beam;
        float alpha, spin;

        public XrBeacon(Transform parent)
        {
            Material mat = new Material(Shader.Find("Sprites/Default"));
            ring = Make(parent, "Beacon Ring", mat, RingSegments + 1);
            beam = Make(parent, "Beacon Beam", mat, 2);
        }

        static LineRenderer Make(Transform parent, string name, Material mat, int points)
        {
            LineRenderer lr = new GameObject(name).AddComponent<LineRenderer>();
            lr.transform.SetParent(parent, false);
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.positionCount = points;
            lr.material = mat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        public void Update(DirectorMode director, CameraRig rig)
        {
            float dt = Time.unscaledDeltaTime;
            Anchor target = director != null ? director.AttentionTarget : null;
            alpha = Mathf.MoveTowards(alpha, target != null && !rig.Flying ? 1 : 0, dt * 2.5f);
            bool show = alpha > 0.01f && target != null;
            ring.enabled = beam.enabled = show;
            if (!show) return;

            float s = rig.XrScale;
            Vector3 p = target.World;
            spin += dt * 40f;
            float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 3f);
            float r = RingMetres * s * pulse;
            Quaternion q = Quaternion.Euler(0, spin, 0);
            for (int i = 0; i <= RingSegments; i++)
            {
                float a = i / (float)RingSegments * Mathf.PI * 2f;
                ring.SetPosition(i, p + q * new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
            }

            beam.SetPosition(0, p);
            beam.SetPosition(1, p + Vector3.up * (BeamMetres * s));
            Color c = new Color(1f, 0.92f, 0.7f, 0.9f * alpha);
            Color clear = new Color(c.r, c.g, c.b, 0);
            ring.startColor = ring.endColor = c;
            beam.startColor = c;
            beam.endColor = clear;
            ring.widthMultiplier = 0.004f * s;
            beam.widthMultiplier = 0.003f * s;
        }
    }
}
