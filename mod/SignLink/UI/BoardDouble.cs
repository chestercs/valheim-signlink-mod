using System.Collections.Generic;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>While a tilt or a rotation waits to be applied, the sign's own board (its model, not its text) is hidden on
    /// this computer and a copy of it stands in the pending pose (1.0.2, the author: tilting, the sign post seems to lag
    /// behind the content and sticks into the picture: it is not wrong, it is only shown wrong while editing). The preview
    /// and the frame already show the new pose; the real board stood as it is and cut through them. Anything else (the
    /// same pose, another sign, the window shut) shows the sign as it is again.</summary>
    internal sealed class BoardDouble
    {
        List<Sign> hiddenFor;
        readonly List<Renderer> hidden = new List<Renderer>();
        GameObject copy;

        /// <summary>Layout events only; nothing changes while a put runs (it makes the stack again).</summary>
        public void Follow(Session s, bool busy)
        {
            if (busy) return;
            Sign target = s.HasTarget ? s.Target : null;
            if (!target || Change.SamePose(s.Held.Fit, s.Chosen.Fit))
            {
                End();
                return;
            }
            List<Sign> stack = s.Stack.Count > 0 ? s.Stack : new List<Sign> { target };
            if (hiddenFor == null || !Same(hiddenFor, stack) || !copy)
            {
                End();
                hiddenFor = new List<Sign>(stack);
                copy = MakeCopy(target);
                foreach (Sign o in hiddenFor)
                    foreach (Renderer r in BoardRenderers(o))
                        if (r.enabled)
                        {
                            r.enabled = false;
                            hidden.Add(r);
                        }
            }
            s.PendingPose(target, s.Chosen.Fit, out Vector3 pos, out Quaternion rot);
            copy.transform.SetPositionAndRotation(pos, rot);
        }

        /// <summary>The sign shows as it is again.</summary>
        public void End()
        {
            foreach (Renderer r in hidden)
                if (r) r.enabled = true;
            hidden.Clear();
            hiddenFor = null;
            if (copy) Object.Destroy(copy);
            copy = null;
        }

        /// <summary>The model's renderers: everything under the sign but its text's canvas (the frame lives there too).</summary>
        static IEnumerable<Renderer> BoardRenderers(Sign o)
        {
            if (!o) yield break;
            Transform text = TextRoot(o);
            foreach (Renderer r in o.GetComponentsInChildren<Renderer>(true))
                if (!text || !r.transform.IsChildOf(text)) yield return r;
        }

        /// <summary>The sign's child that holds its text widget (its canvas).</summary>
        static Transform TextRoot(Sign o)
        {
            Transform t = o.m_textWidget ? o.m_textWidget.transform : null;
            while (t && t.parent && t.parent != o.transform) t = t.parent;
            return t && t.parent == o.transform ? t : null;
        }

        /// <summary>The board's meshes as they sit on the sign, under one object placed as the sign would stand.</summary>
        static GameObject MakeCopy(Sign o)
        {
            var root = new GameObject("SignLinkBoardDouble");
            Transform sign = o.transform;
            root.transform.SetPositionAndRotation(sign.position, sign.rotation);
            root.transform.localScale = sign.lossyScale;
            LODGroup lod = o.GetComponentInChildren<LODGroup>();
            LOD[] lods = lod ? lod.GetLODs() : null;
            var nearest = lods != null && lods.Length > 0 ? new HashSet<Renderer>(lods[0].renderers) : null;   // one level only
            foreach (Renderer r in BoardRenderers(o))
            {
                if (nearest != null && lod && r.transform.IsChildOf(lod.transform) && !nearest.Contains(r)) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (!(r is MeshRenderer) || !mf || !mf.sharedMesh || !r.enabled || !r.gameObject.activeInHierarchy) continue;   // a wear
                                                                            // state's mesh switched off stays off (review)
                var part = new GameObject(r.name);
                part.transform.SetParent(root.transform, false);
                part.transform.localPosition = sign.InverseTransformPoint(r.transform.position);
                part.transform.localRotation = Quaternion.Inverse(sign.rotation) * r.transform.rotation;
                Vector3 a = r.transform.lossyScale, b = sign.lossyScale;
                part.transform.localScale = new Vector3(a.x / Safe(b.x), a.y / Safe(b.y), a.z / Safe(b.z));
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = part.AddComponent<MeshRenderer>();
                mr.sharedMaterials = r.sharedMaterials;
                mr.shadowCastingMode = r.shadowCastingMode;
                mr.receiveShadows = r.receiveShadows;
            }
            return root;
        }

        static float Safe(float v) => Mathf.Abs(v) < 1e-6f ? 1f : v;

        static bool Same(List<Sign> a, List<Sign> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }
    }
}
