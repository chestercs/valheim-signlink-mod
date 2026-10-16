using System.Collections.Generic;
using SignLink.Pictures;
using UnityEngine;

namespace SignLink.Signs
{
    /// <summary>What SignLink's signs around a place hold together, for the multiplayer area limit (SaveCost.AreaLimit
    /// within SaveCost.AreaRadiusM), as SaveCost.Bytes counts a stack (its text, the sign itself, a saved animation). Only
    /// SignLink's signs count (0.20.67, the author: count only these into the 600 KB hard limit): a vanilla sign
    /// holds no more than the game's editor takes, and the signs of a village should not take the room of a picture.
    /// Only loaded signs are seen: about 128 m around the player at the classic view distance, 64-90 m at the lower ones
    /// (client/ZNetScene.cs:360-367), so a sign far from the player can miss signs at the area's edge (0.20.152 review).</summary>
    internal static class SignArea
    {
        /// <summary>The bytes of SignLink's loaded signs within the area around at, except the given ones (the stack being
        /// written); stacks: how many of SignLink's signs that is, a stack with its copies counted once.</summary>
        public static long Others(Vector3 at, ICollection<Sign> except, out int stacks)
        {
            long n = 0;
            stacks = 0;
            float r2 = SaveCost.AreaRadiusM * (float)SaveCost.AreaRadiusM;
            ICollection<Sign> skip = except == null || except is HashSet<Sign> ? except : new HashSet<Sign>(except);   // a stack's list: once
            foreach (Sign s in SignFinder.Loaded())
            {
                if (!s || (skip != null && skip.Contains(s))) continue;
                if ((s.transform.position - at).sqrMagnitude > r2) continue;
                ZDO z = SignFinder.Zdo(s);
                if (z == null || !SignMarks.IsSignLinks(s)) continue;
                byte[] blob = z.GetByteArray(Animation.StoredAnims.Key);
                n += SaveCost.PerSign + SignText.TextBytes(s) + (blob != null && blob.Length > 0 ? blob.Length + 8 : 0);
                if (!SignMarks.IsCopy(z)) stacks++;
            }
            return n;
        }
    }
}
