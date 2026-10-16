using System;
using System.Collections.Generic;
using SignLink.Pictures;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SignLink.Signs
{
    /// <summary>How a sign stands: tipped over its bottom edge by a tilt (0° upright as built, 90° face up, -90° face
    /// down) and turned in its own plane (a sign draws its text in its plane, so a sign lying face up carries a picture
    /// on the floor). The edge stays where it is, so a sign tipped flat lies at the foot of where it stood, whichever way
    /// it tips. The pose belongs to the sign and its whole stack; SignLink never chooses where the sign is. The edge is the
    /// board's lowest point as built (where it rests on a floor), FloorPlan.Lift behind its text: face up, the picture
    /// lies Lift over that floor, the board under it, its top away from someone who faced the sign; face down, Lift under
    /// that line, readable from below (not mirrored), its top towards them, the board over it. Turning goes clockwise
    /// around the sign's centre, as seen by whoever reads it. The sign never moves for a picture's place: the picture
    /// slides along the sign's face (Fit.OnSign); a shift kept on older stacks (0.16-0.17) is read back, new ones write 0.
    ///
    /// Other players' games place a piece only when it loads (ZNetScene.CreateObject reads the ZDO's position and
    /// rotation once), so a new pose rebuilds the stack: every sign is made again at its new place with all its data
    /// (text, author, creator, health, saved animation) through the game's own initial-ZDO path, and the old ones go.
    /// The pose as built is kept on the first sign (BaseP, BaseR), so 0° and 0° give back exactly that.</summary>
    internal static class SignPose
    {
        /// <summary>The pose as built (BaseP, BaseR) and the pose SignLink gave it (tilt, turn, shift), on the first sign;
        /// PivotKey: how the tilt tips it, 1 over its bottom edge (and unset: every pose before 0.20.30), 2 about the
        /// board's middle (0.20.30-0.20.82), 3 about the middle of its text's plane (0.20.83: the picture stays where it is
        /// whatever the tilt; about the board's middle, the text 7 cm in front of it went 7 cm over it face up and 7 cm
        /// under it face down).</summary>
        static readonly int BaseP = "signlink_base_p".GetStableHashCode(), BaseR = "signlink_base_r".GetStableHashCode(),
                            PoseKey = "signlink_pose".GetStableHashCode(), PivotKey = "signlink_pivot".GetStableHashCode();
        const int EdgePivot = 1, BoardPivot = 2, FacePivot = 3;

        /// <summary>How the stack's kept pose tips it: EdgePivot, BoardPivot or FacePivot.</summary>
        static int PivotOf(ZDO zdo)
        {
            int v = zdo?.GetInt(PivotKey, 0) ?? 0;
            return v == BoardPivot || v == FacePivot ? v : EdgePivot;
        }

        /// <summary>Does the stack's tilt tip it over its bottom edge (else about a middle)?</summary>
        static bool Pivot(ZDO zdo) => PivotOf(zdo) == EdgePivot;

        /// <summary>The pivot a pose asked for takes: over the edge, else the middle of the text's plane (0.20.83). The
        /// pose a stack keeps, asked again, keeps its own pivot: a stack posed about the board's middle before is not
        /// made again by a refit, only by a new pose.</summary>
        static int PivotFor(Sign root, float tilt, float turn, bool edge)
        {
            if (edge) return EdgePivot;
            ZDO zdo = SignFinder.Zdo(root);
            if (PivotOf(zdo) == BoardPivot && Kept(zdo, out Vector3 pose) && Mathf.Abs(pose.x - tilt) < 0.05f &&
                Mathf.Abs(Mathf.DeltaAngle(pose.y, turn)) < 0.05f) return BoardPivot;
            return FacePivot;
        }

        static int mask;
        /// <summary>The build hammer's surfaces (Player.m_placeRayMask) without ships and non-solid pieces.</summary>
        public static int Mask => mask != 0 ? mask : mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");

        /// <summary>Flat: lying face up or face down (building in the game only stands signs upright).</summary>
        public static bool Flat(Sign s) => s && Mathf.Abs(s.transform.forward.y) > 0.9f;

        /// <summary>Tipped at all: its copies stack FloorPlan.Step apart (a flat stack's boards stay in the floor).</summary>
        // the same threshold as a pose's own (|tilt| > 0.05°: sin 0.05° = 0.00087): copies added later sit as the first
        // ones did (0.20.193, math review: 0.01 is 0.57°, and a 0.5° tilt fell between the two)
        public static bool Tipped(Sign s) => s && Mathf.Abs(s.transform.forward.y) > 0.0008f;

        /// <summary>The stack's tilt (-90..90, + face up) and turn (-180..180, clockwise), in degrees: as SignLink set them
        /// while the sign still shows that pose (the world save keeps rotations to half a degree), else read from how it
        /// stands.</summary>
        public static void Read(Sign s, out float tilt, out float turn) => Read(s, out tilt, out turn, out _);

        /// <summary>The same with the pivot: over its bottom edge (every pose SignLink made before 0.20.30, and a sign
        /// never posed that leans: Base reads it as tipped over its edge) or about its middle.</summary>
        public static void Read(Sign s, out float tilt, out float turn, out bool edge)
        {
            tilt = turn = 0f;
            edge = false;
            if (!s) return;
            ZDO zdo = SignFinder.Zdo(s);
            Base(s, out _, out Quaternion br);
            Quaternion q = Quaternion.Inverse(br) * s.transform.rotation;
            Vector3 f = q * Vector3.forward;
            tilt = Normalize(Mathf.Atan2(f.y, f.z) * Mathf.Rad2Deg);
            turn = Normalize((Quaternion.Inverse(Tip(tilt)) * q).eulerAngles.z);
            edge = StoredBase(zdo, Vector3.zero, out _, out _) ? Pivot(zdo) : Mathf.Abs(tilt) > 0.05f;
            if (Kept(zdo, out Vector3 pose) && Mathf.Abs(pose.x - tilt) < 0.75f &&
                Mathf.Abs(Mathf.DeltaAngle(pose.y, turn)) < 0.75f)
            {
                tilt = pose.x;
                turn = pose.y;
            }
        }

        /// <summary>"lying face up, rotated 30°", "tilted back 20°", "standing".</summary>
        public static string Describe(float tilt, float turn)
        {
            string t = Fit.TiltWords(tilt);
            return (t.Length > 0 ? t : "standing") + (Mathf.Abs(turn) > 0.05f ? $", rotated {turn:0.#}°" : "");
        }

        /// <summary>The tipping as a rotation in the sign's frame as built: about its x axis, + turning its face up.</summary>
        static Quaternion Tip(float tilt) => Quaternion.Euler(-tilt, 0f, 0f);

        /// <summary>An angle in (-180, 180], to 0.01° (rotations read back from floats).</summary>
        static float Normalize(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            if (deg <= -180f) deg += 360f;
            deg = Mathf.Round(deg * 100f) / 100f;
            return deg == 0f ? 0f : deg;
        }

        static Vector3 Level(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }

        /// <summary>The pose SignLink last gave the stack (tilt, turn, shift), kept on its first sign since 0.17.0. Any
        /// player's game can write it: one whose numbers are not sane (StackMath.SanePose: NaN, infinities, a shift past
        /// MaxReach) counts as none kept (0.20.132, review).</summary>
        static bool Kept(ZDO zdo, out Vector3 pose)
        {
            var none = new Vector3(float.NaN, 0f, 0f);
            pose = zdo != null ? zdo.GetVec3(PoseKey, none) : none;
            if (!float.IsNaN(pose.x) && StackMath.SanePose(pose.x, pose.y, pose.z, MaxReach)) return true;
            pose = none;
            return false;
        }

        /// <summary>How far from its sign a kept base (or a kept shift) may be. The base moves along with every move written
        /// (MoveBase), so it stands off the sign's written place by the pose's own offset (well under a metre) and a shift
        /// kept by 0.16-0.17; a stack moves at most SignMove.MaxM from where it was built: a generous bound.</summary>
        const float MaxReach = SignMove.MaxM + 1f;

        /// <summary>The pose as built, as SignLink kept it on the first sign (BaseP, BaseR), if it did. here: the base's
        /// position when none is kept with its rotation. A base past MaxReach from the sign's written place (its ZDO's: moves
        /// not written yet move neither; ahead: a move written to the ZDO a moment ago, not yet to the base) or without a
        /// unit rotation counts as none kept (StackMath.SaneBase, 0.20.132, review): a forged one, taken as it was, made the
        /// next pose rebuild the stack there.</summary>
        static bool StoredBase(ZDO zdo, Vector3 here, out Vector3 pos, out Quaternion rot, Vector3 ahead = default)
        {
            var none = new Quaternion(0, 0, 0, 0);
            rot = zdo != null ? zdo.GetQuaternion(BaseR, none) : none;
            pos = zdo != null ? zdo.GetVec3(BaseP, here) : here;
            if (rot.x == 0 && rot.y == 0 && rot.z == 0 && rot.w == 0) return false;
            Vector3 d = pos - (zdo.GetPosition() - ahead);
            if (StackMath.SaneBase(d.x, d.y, d.z, rot.x, rot.y, rot.z, rot.w, MaxReach)) return true;
            rot = none;
            pos = here;
            return false;
        }

        /// <summary>The pose the sign was built in. SignLink keeps it on the first sign once it changed the pose; a move
        /// or turn SignMove made but has not written yet is counted in (Pending); if the stack was moved since by
        /// something else (another mod), the kept pose is undone from where it is now, so the base moves along. A
        /// sign SignLink never posed is as built where it stands, however it leans (tipped over its bottom edge, as
        /// SignLink would tip it).</summary>
        static void Base(Sign s, out Vector3 pos, out Quaternion rot)
        {
            ZDO zdo = SignFinder.Zdo(s);
            Transform t = s.transform;
            if (StoredBase(zdo, t.position, out pos, out rot))
            {
                Pending(s, ref pos, ref rot);
                if (!Kept(zdo, out Vector3 pose)) return;                                   // posed by 0.12-0.16
                int pivot = PivotOf(zdo);
                Place(s, pos, rot, pose.x, pose.y, pose.z, pivot, out Vector3 p, out Vector3 e);
                if (Vector3.Distance(p, t.position) < 0.05f && Quaternion.Angle(Quaternion.Euler(e), t.rotation) < 1f) return;
                if (Explains(s, pos, rot, pivot)) return;   // posed again by an older SignLink (the kept pose is stale): the base holds
                rot = t.rotation * Quaternion.Inverse(Tip(pose.x) * Quaternion.Euler(0f, 0f, pose.y));
                pos = Unplace(s, t.position, rot, pose.x, pose.z, pivot);               // moved by something else since
                return;
            }
            Vector3 f = t.forward;
            if (Mathf.Abs(f.y) < 1e-4f)
            {
                pos = t.position;
                rot = Vector3.Dot(t.up, Vector3.up) > 0.9999f ? t.rotation : Quaternion.LookRotation(Level(f), Vector3.up);
                return;
            }
            rot = Quaternion.LookRotation(Mathf.Abs(f.y) < 0.999f ? Level(f) : Level(f.y > 0 ? -t.up : t.up), Vector3.up);
            pos = Unplace(s, t.position, rot, Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg, 0f, EdgePivot);
        }

        /// <summary>The stored base as SignMove.Write will leave it: moved and turned by what SignMove did to the stack
        /// but has not written yet (the signs stand moved and turned already, the ZDOs follow when the player applies).
        /// Without this, a flat sign turned about the up axis read as content rotated in its plane until the write
        /// (Explains cannot tell the two apart: a yaw is a turn in a flat sign's plane), and 3 Place took that reading
        /// for a new pose, dropping the tilt the player had just chosen (0.20.10-0.20.11).</summary>
        static void Pending(Sign root, ref Vector3 bp, ref Quaternion br)
        {
            bp += SignMove.Pending(root);
            float turn = SignMove.PendingTurn(root);
            if (Mathf.Abs(turn) < 1e-4f) return;
            Quaternion q = Quaternion.AngleAxis(turn, Vector3.up);
            Vector3 pivot = root.transform.position;                        // as MoveBase: the move first, then the turn
            bp = pivot + q * (bp - pivot);                                  // about the first sign where it stands now
            br = q * br;
        }

        /// <summary>Does some pose from this base put the sign where it is (tilt and turn read from its rotation, a shift
        /// along its normal)? Then the base still holds.</summary>
        static bool Explains(Sign s, Vector3 bp, Quaternion br, int pivot)
        {
            Transform t = s.transform;
            Quaternion q = Quaternion.Inverse(br) * t.rotation;
            Vector3 f = q * Vector3.forward;
            float tilt = Mathf.Clamp(Normalize(Mathf.Atan2(f.y, f.z) * Mathf.Rad2Deg), -90f, 90f);
            float turn = Normalize((Quaternion.Inverse(Tip(tilt)) * q).eulerAngles.z);
            Place(s, bp, br, tilt, turn, 0f, pivot, out Vector3 p0, out Vector3 e);
            Vector3 n = br * Tip(tilt) * Vector3.forward;
            Vector3 off = t.position - p0;
            return (off - Vector3.Dot(off, n) * n).magnitude < 0.05f && Quaternion.Angle(Quaternion.Euler(e), t.rotation) < 1f;
        }

        /// <summary>The directions of the sign as built, to move it in (SignMove): to the right of someone who faced it,
        /// and towards them, both level; up is up.</summary>
        public static void Frame(Sign s, out Vector3 right, out Vector3 towards)
        {
            Base(s, out _, out Quaternion br);
            towards = Level(br * Vector3.forward);
            right = Vector3.Cross(towards, Vector3.up);
        }

        /// <summary>Where the stack's first sign would stand in a pose (the ghost frame): Place from the base, nothing
        /// written; a move or turn not yet written (SignMove) is counted in by Base.</summary>
        public static void Preview(Sign root, float tilt, float turn, bool edge, out Vector3 pos, out Quaternion rot)
        {
            tilt = Mathf.Clamp(Normalize(tilt), -90f, 90f);
            turn = Normalize(turn);
            Base(root, out Vector3 bp, out Quaternion br);
            Place(root, bp, br, tilt, turn, 0f, PivotFor(root, tilt, turn, edge), out pos, out Vector3 euler);
            rot = Quaternion.Euler(euler);
        }

        /// <summary>The stored base moves by d and turns by turnDeg about the up axis through the first sign where it is
        /// now (SignMove.Write: the stack was moved and turned rigidly, so the pose read back stays what it was). A sign
        /// never posed has no stored base: its base is read from how it stands.</summary>
        public static void MoveBase(Sign root, Vector3 d, float turnDeg)
        {
            ZDO zdo = SignFinder.Zdo(root);
            if (zdo == null || !StoredBase(zdo, root.transform.position, out Vector3 bp, out Quaternion br, ahead: d)) return;
            bp += d;
            if (Mathf.Abs(turnDeg) > 1e-4f)
            {
                Quaternion q = Quaternion.AngleAxis(turnDeg, Vector3.up);
                Vector3 pivot = root.transform.position;
                bp = pivot + q * (bp - pivot);
                zdo.Set(BaseR, q * br);
            }
            zdo.Set(BaseP, bp);
        }

        /// <summary>Where the base is for a first sign at `here` in this pose: Place backwards.</summary>
        static Vector3 Unplace(Sign s, Vector3 here, Quaternion br, float tilt, float shift, int pivot)
        {
            Vector3 pos = here - br * Tip(tilt) * Vector3.forward * shift;
            if (Mathf.Abs(tilt) < 0.005f || pivot == BoardPivot) return pos;  // about the board's middle: the centre stays put
            return pos - br * Lift(s, tilt, pivot);
        }

        /// <summary>How far the sign's centre moves for a tilt, in its frame as built: over its bottom edge
        /// (FloorPlan.Tip), or about the middle of its text's plane (FloorPlan.FaceTip, 0.20.83).</summary>
        static Vector3 Lift(Sign s, float tilt, int pivot)
        {
            Measure(s, out float foot, out _, out float textZ);
            double forward, up;
            if (pivot == FacePivot) FloorPlan.FaceTip(tilt, textZ, out forward, out up);
            else FloorPlan.Tip(tilt, foot, textZ, out forward, out up);
            return new Vector3(0f, (float)up, (float)forward);
        }

        static readonly Dictionary<int, Vector3> measured = new Dictionary<int, Vector3>();

        /// <summary>The prefab's numbers, in the sign's own frame: how far its lowest and highest collider points lie under
        /// and over its centre (0.275 m each: placing rests it on a floor there) and its text in front of it (0.07 m).
        /// Measured once per prefab.</summary>
        static void Measure(Sign s, out float foot, out float head, out float textZ)
        {
            ZDO zdo = SignFinder.Zdo(s);
            int prefab = zdo != null ? zdo.GetPrefab() : 0;
            if (prefab != 0 && measured.TryGetValue(prefab, out Vector3 m))
            {
                foot = m.x;
                head = m.y;
                textZ = m.z;
                return;
            }
            Transform t = s.transform;
            textZ = t.InverseTransformPoint(s.m_textWidget.transform.position).z;
            foot = head = 0f;
            foreach (BoxCollider c in s.GetComponentsInChildren<BoxCollider>(true))
            {
                if (c.isTrigger) continue;
                Vector3 h = c.size / 2;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                    float y = t.InverseTransformPoint(c.transform.TransformPoint(c.center + corner)).y;
                    foot = Mathf.Max(foot, -y);
                    head = Mathf.Max(head, y);
                }
            }
            if (foot <= 0f) foot = 0.275f;
            if (head <= 0f) head = 0.275f;
            if (prefab != 0) measured[prefab] = new Vector3(foot, head, textZ);
        }

        /// <summary>Where the first sign goes for a pose: tipped over its bottom edge (FloorPlan.Tip; edge) or about its
        /// middle (the centre stays), moved `shift` metres along its normal, turned around its centre; its rotation as
        /// the Euler angles the world save keeps (Savable).</summary>
        static void Place(Sign s, Vector3 bp, Quaternion br, float tilt, float turn, float shift, int pivot, out Vector3 pos, out Vector3 euler)
        {
            Quaternion tipped = br * Tip(tilt);
            euler = Savable(tipped * Quaternion.Euler(0f, 0f, turn));
            pos = bp + tipped * Vector3.forward * shift;
            if (Mathf.Abs(tilt) < 0.005f || pivot == BoardPivot) return;
            pos += br * Lift(s, tilt, pivot);
        }

        /// <summary>A rotation as Euler angles the world save keeps exactly. ZDO.Save writes them with
        /// ZPackage.WriteSmallRotation, which truncates each to half a degree and drops x and z when both are within half
        /// a degree of 0 (network sync sends full floats, so the loss shows only after a reload). Rounded to that grid,
        /// plus 0.001° so the truncation lands on the same step: a stack keeps its pose, and its copies the very same one.
        /// Written to the ZDO as it is (Create, SignBuilder.Clone), never derived again from a quaternion: lying, the x
        /// angle is at ±90°, where that loses its last digits.</summary>
        public static Vector3 Savable(Quaternion q)
        {
            Vector3 e = q.eulerAngles;
            float x = Grid(e.x), y = Grid(e.y), z = Grid(e.z);
            if ((x <= 0.5f || x >= 359.5f) && (z <= 0.5f || z >= 359.5f)) x = z = 0f;
            return new Vector3(x + 0.001f, y + 0.001f, z + 0.001f);
        }

        static float Grid(float deg)
        {
            deg = Mathf.Round(deg * 2f) / 2f % 360f;
            return deg < 0f ? deg + 360f : deg;
        }

        /// <summary>Does this pose make the stack's signs again (they are not where and how it puts them: another pose, or
        /// placed by an older SignLink)? A pose SignLink made on the save's grid is compared exactly; one made before
        /// (0.12-0.16, rounded by the save since) within 0.75° and a centimetre. Throws, changing nothing, when it may not
        /// happen: a ward over the sign or over its new spot, the area still loading, over a multiplayer limit. Callers that
        /// write first (a saved animation) ask before.</summary>
        public static bool Check(Sign any, float tilt, float turn, float shift, bool edge = true)
        {
            tilt = Mathf.Clamp(Normalize(tilt), -90f, 90f);
            turn = Normalize(turn);
            Sign root = SignFinder.Root(any);
            if (!root || SignFinder.Zdo(root) == null) throw new InvalidOperationException("the sign is gone (unloaded or removed)");
            SignText.RequireEdit(root);
            Base(root, out Vector3 bp, out Quaternion br);
            Place(root, bp, br, tilt, turn, shift, PivotFor(root, tilt, turn, edge), out Vector3 pos, out Vector3 euler);
            Transform t = root.transform;
            bool kept = Kept(SignFinder.Zdo(root), out _);                    // made on the save's grid: compare exactly;
            float far = kept ? 0.002f : 0.01f, angle = kept ? 0.05f : 0.75f; // older poses were rounded by the save
            if (Vector3.Distance(t.position, pos) < far && Quaternion.Angle(t.rotation, Quaternion.Euler(euler)) < angle) return false;
            SignBuilder.Guard();                                            // the signs are made again
            SignFinder.RequireLoaded(root);
            if (!PrivateArea.CheckAccess(pos, 0f, false)) throw new InvalidOperationException("a ward you have no access to protects that spot");
            // a new pose is held up as a move is (0.20.130, review: a sign laid face up on a floor stood off it, and the
            // game broke it after its first half minute)
            List<Sign> stack = SignFinder.Stack(root);
            Quaternion to = Quaternion.Euler(euler);
            if (!SignMove.Holds(root, stack, pos - t.position, to * Quaternion.Inverse(t.rotation), out string why, remade: true))
                throw new InvalidOperationException(why);
            SignMove.RequireSpot(stack, pos, to);
            return true;
        }

        /// <summary>Gives the stack a pose (tilt, turn, shift along its normal in metres), and new texts if given (else it
        /// keeps its own) with their name: the signs are made again at their new places (see the summary) when the pose
        /// changes, else only the texts are written (SignBuilder.SetContent). The new signs are all made before the old
        /// ones go, so a failure leaves the stack as it was. Returns the stack, its first sign first.</summary>
        public static List<Sign> Set(Sign any, float tilt, float turn, float shift, bool edge = true, IList<string> texts = null, string name = null)
        {
            tilt = Mathf.Clamp(Normalize(tilt), -90f, 90f);
            turn = Normalize(turn);
            List<Sign> stack = SignFinder.Stack(any);
            Sign root = stack.Count > 0 ? stack[0] : any;
            if (!Check(root, tilt, turn, shift, edge))
            {
                List<Sign> kept = texts != null ? SignBuilder.SetContent(root, texts, name) : stack;
                Keep(kept.Count > 0 ? kept[0] : root, tilt, turn, shift, edge);   // the same angles on the grid: the asked
                return kept;                                                       // values still read back
            }

            // the signs are made again with these texts (or their own): stored anew, so the multiplayer limit applies
            SignText.RequireRoom(root, texts != null ? SaveCost.Bytes(texts) : Stored(root), texts != null ? "this content" : "this sign");
            if (texts != null && texts.Count * (Mathf.Abs(tilt) > 0.05f ? (float)FloorPlan.Step : SignBuilder.DefaultStep) >= SignFinder.StackDepth)
                throw new ArgumentException($"too many copies for one sign ({texts.Count})");
            int pivot = PivotFor(root, tilt, turn, edge);
            SignMove.WriteFor(root);                                        // allowed: moves not applied go into the data copied
            Base(root, out Vector3 bp, out Quaternion br);
            Place(root, bp, br, tilt, turn, shift, pivot, out Vector3 pos, out Vector3 euler);
            Quaternion rot = Quaternion.Euler(euler);
            float step = Mathf.Abs(tilt) > 0.05f ? (float)FloorPlan.Step : SignBuilder.DefaultStep;
            bool home = Mathf.Abs(tilt) < 0.05f && Mathf.Abs(turn) < 0.05f && Mathf.Abs(shift) < 0.0005f;

            // what every new sign carries: the old one's data (the first sign's for signs the new texts add)
            var data = new List<Data>();
            foreach (Sign s in stack) data.Add(new Data(SignFinder.Zdo(s)));
            int count = texts != null ? Math.Max(1, texts.Count) : data.Count;
            for (int i = data.Count; i < count; i++) data.Add(data[0].Copy());
            string author = null, shown = null;
            if (texts != null) SignText.Author(out author, out shown);
            for (int i = 0; i < count; i++)
            {
                if (texts != null)
                {
                    data[i].Strings[ZDOVars.s_text] = i < texts.Count ? texts[i] : "";
                    data[i].Bytes.Remove(Animation.StoredAnims.Key);       // new content: no saved animation over it
                    data[i].Strings[ZDOVars.s_author] = author;             // written by this player, as Sign.SetText says
                    if (shown != null) data[i].Strings[ZDOVars.s_authorDisplayName] = shown;
                }
                if (i == 0)
                {
                    data[i].Ints.Remove(SignBuilder.CopyKey);
                    data[i].Ints[SignFinder.CountKey] = count;                 // how many signs the stack has (SignFinder.IsLoaded)
                    continue;
                }
                data[i].Forget(BaseP, BaseR);                                // the first sign's alone
                data[i].Vecs.Remove(PoseKey);
                data[i].Ints.Remove(PivotKey);
                data[i].Vecs.Remove(SignMove.NudgeKey);
                data[i].Floats.Remove(SignMove.TurnKey);
                data[i].Ints.Remove(SignFinder.CountKey);
                data[i].Longs.Remove(SignMarks.MadeKey);
                data[i].Strings.Remove(SignMarks.NameKey);
                data[i].Strings.Remove(SignMarks.VersionKey);
                data[i].Strings.Remove(SignMarks.SourceKey);
                data[i].Vecs.Remove(SignFit.FitKey);
                data[i].Vecs.Remove(SignFit.CentreKey);
                data[i].Floats.Remove(SignFit.OutKey);
                data[i].Bytes.Remove(Animation.StoredAnims.Key);
                data[i].Longs.Remove(ZDOVars.s_creator);                    // copies belong to no one (SignBuilder.Clone):
                data[i].Ints.Remove(ZDOVars.s_creatorIndex);                // monsters leave them alone
                data[i].Strings.Remove(ZDOVars.s_creatorName);
                data[i].Ints[SignBuilder.CopyKey] = 1;
                data[i].Ints[SignBuilder.FreeKey] = 1;                       // made for nothing: drops nothing, even leading
            }
            if (home)
            {
                data[0].Forget(BaseP, BaseR);
                data[0].Vecs.Remove(PoseKey);
                data[0].Ints.Remove(PivotKey);
            }
            else
            {
                data[0].Vecs[BaseP] = bp;
                data[0].Quats[BaseR] = br;
                data[0].Vecs[PoseKey] = new Vector3(tilt, turn, shift);
                data[0].Ints[PivotKey] = pivot;
            }
            if (texts != null) data[0].Strings.Remove(SignMarks.SourceKey);  // new content: its writer says what it is
            data[0].Longs[SignMarks.MadeKey] = DateTime.UtcNow.Ticks;
            data[0].Strings[SignMarks.VersionKey] = Plugin.Version;
            if (texts != null) data[0].Strings[SignMarks.NameKey] = name ?? "";

            var made = new List<Sign>();
            try
            {
                for (int i = 0; i < count; i++) made.Add(Create(data[i].Prefab, pos + rot * Vector3.forward * (step * i), euler, data[i]));
            }
            catch
            {
                SignBuilder.Delete(made);                                    // the old stack stays as it was
                throw;
            }
            SignBuilder.Delete(stack);
            return made;
        }

        /// <summary>What a stack holds now, as SaveCost.Bytes counts it (its texts and its saved animation): what making
        /// its signs again stores anew.</summary>
        public static long Stored(Sign root)
        {
            long n = 0;
            foreach (Sign s in SignFinder.Stack(root)) n += SaveCost.PerSign + SignText.TextBytes(s);   // cached by revision
            byte[] blob = Animation.StoredAnims.Blob(root);
            return n + (blob != null && blob.Length > 0 ? blob.Length + 8 : 0);   // as SaveCost.Bytes, SignArea and the window count
        }

        /// <summary>Makes the stack's signs again exactly where and how they stand, with all their data (Apply on
        /// 3 Place › Changes, after moving it, since 0.20.72): every game places them anew at once, vanilla ones included (the game
        /// places a sign only when it loads; SignMove moves it in place). The new signs are made before the old ones go.
        /// Returns the new stack, its first sign first.</summary>
        public static List<Sign> Remake(Sign any)
        {
            List<Sign> stack = SignFinder.Stack(any);
            Sign root = stack.Count > 0 ? stack[0] : any;
            if (!root || SignFinder.Zdo(root) == null) throw new InvalidOperationException("the sign is gone (unloaded or removed)");
            SignText.RequireEdit(root);
            SignBuilder.Guard();                                            // the signs are made again
            SignFinder.RequireLoaded(root);
            SignText.RequireRoom(root, Stored(root), "this sign");          // stored anew: the multiplayer limits apply
            SignMove.WriteFor(root);                                        // allowed: moves not applied are made with them
            var made = new List<Sign>();
            try
            {
                foreach (Sign s in stack)
                {
                    ZDO z = SignFinder.Zdo(s);
                    if (z != null) made.Add(Create(z.GetPrefab(), s.transform.position, z.m_rotation, new Data(z)));
                }
            }
            catch
            {
                SignBuilder.Delete(made);                                    // the old stack stays as it was
                throw;
            }
            SignBuilder.Delete(stack);
            return made;
        }

        /// <summary>Writes the pose asked for on the first sign when it shows it but keeps other numbers (a turn of 10.2°
        /// lands on 10° of the grid, as 10° did). A sign SignLink never posed gets its base stored first (as it stands),
        /// so the asked pose reads back as asked (Read): a fit waiting for a 0.2° turn is not applied again every half
        /// second, each time moving the content a little further.</summary>
        static void Keep(Sign root, float tilt, float turn, float shift, bool edge)
        {
            ZDO zdo = SignFinder.Zdo(root);
            if (zdo == null) return;
            var now = new Vector3(tilt, turn, shift);
            bool stored = StoredBase(zdo, Vector3.zero, out _, out _);
            if (now.sqrMagnitude < 1e-6f)                                   // the home pose: as built, nothing kept
            {
                if (!stored) return;
                root.m_nview.ClaimOwnership();
                zdo.Set(BaseR, new Quaternion(0f, 0f, 0f, 0f));             // "none", as StoredBase and Kept read them
                zdo.Set(PoseKey, new Vector3(float.NaN, 0f, 0f));
                zdo.Set(PivotKey, 0);
                return;
            }
            int pivot = PivotFor(root, tilt, turn, edge);
            if (Kept(zdo, out Vector3 was) && (now - was).sqrMagnitude < 1e-6f && PivotOf(zdo) == pivot) return;
            root.m_nview.ClaimOwnership();
            if (!stored)
            {
                Base(root, out Vector3 bp, out Quaternion br);
                zdo.Set(BaseP, bp);
                zdo.Set(BaseR, br);
            }
            zdo.Set(PoseKey, now);
            zdo.Set(PivotKey, pivot);
        }

        /// <summary>A sign of this prefab made at pos with data and these Euler angles (Savable), through the game's
        /// initial-ZDO path (ZNetScene.CreateObject): its components read text, creator and health from the start. The
        /// ZDO's Persistent, Type and Distant flags come from the prefab, as the game sets them, and a ZDO connection (a
        /// portal or sync link, the eighth output of ZDOExtraData.GetData) is not copied: a sign has none. A failure
        /// leaves nothing behind: an orphan ZDO would become an extra sign a frame later.</summary>
        static Sign Create(int prefab, Vector3 pos, Vector3 euler, Data d)
        {
            GameObject go = ZNetScene.instance.GetPrefab(prefab) ?? throw new InvalidOperationException("prefab not found");
            ZNetView nv = go.GetComponent<ZNetView>();
            ZDO zdo = ZDOMan.instance.CreateNewZDO(pos, prefab);
            Sign s = null;
            try
            {
                zdo.Persistent = nv.m_persistent;
                zdo.Type = nv.m_type;
                zdo.Distant = nv.m_distant;
                zdo.SetPrefab(prefab);
                zdo.m_rotation = euler;                                     // as the save keeps it (not SetRotation)
                d.Write(zdo);
                ZNetView.m_useInitZDO = true;
                ZNetView.m_initZDO = zdo;
                try
                {
                    s = Object.Instantiate(go, pos, Quaternion.Euler(euler)).GetComponent<Sign>();
                }
                finally
                {
                    ZNetView.m_initZDO = null;
                    ZNetView.m_useInitZDO = false;
                }
                s.m_lastRevision = uint.MaxValue;
                try { s.UpdateText(); }
                catch (Exception e) { Plugin.Log.LogWarning($"a remade sign shows its text later: {e.Message}"); }   // the game's
                return s;                                                     // own view check throws for author "host"
            }
            catch
            {
                if (s) ZNetScene.instance.Destroy(s.gameObject);
                else ZDOMan.instance.DestroyZDO(zdo);                        // ours and a moment old: no extra sign later
                throw;
            }
        }

        /// <summary>Everything a ZDO holds (ZDOExtraData), to make it again elsewhere.</summary>
        sealed class Data
        {
            public int Prefab;
            public Dictionary<int, float> Floats = new Dictionary<int, float>();
            public Dictionary<int, Vector3> Vecs = new Dictionary<int, Vector3>();
            public Dictionary<int, Quaternion> Quats = new Dictionary<int, Quaternion>();
            public Dictionary<int, int> Ints = new Dictionary<int, int>();
            public Dictionary<int, long> Longs = new Dictionary<int, long>();
            public Dictionary<int, string> Strings = new Dictionary<int, string>();
            public Dictionary<int, byte[]> Bytes = new Dictionary<int, byte[]>();

            Data() { }

            public Data(ZDO z)
            {
                Prefab = z.GetPrefab();
                ZDOExtraData.GetData(z.m_uid, out List<KeyValuePair<int, float>> f, out List<KeyValuePair<int, Vector3>> v,
                                     out List<KeyValuePair<int, Quaternion>> q, out List<KeyValuePair<int, int>> n,
                                     out List<KeyValuePair<int, long>> l, out List<KeyValuePair<int, string>> s,
                                     out List<KeyValuePair<int, byte[]>> b, out _);
                if (f != null) foreach (var e in f) Floats[e.Key] = e.Value;
                if (v != null) foreach (var e in v) Vecs[e.Key] = e.Value;
                if (q != null) foreach (var e in q) Quats[e.Key] = e.Value;
                if (n != null) foreach (var e in n) Ints[e.Key] = e.Value;
                if (l != null) foreach (var e in l) Longs[e.Key] = e.Value;
                if (s != null) foreach (var e in s) Strings[e.Key] = e.Value;
                if (b != null) foreach (var e in b) Bytes[e.Key] = e.Value;
            }

            public Data Copy() => new Data
            {
                Prefab = Prefab, Floats = new Dictionary<int, float>(Floats), Vecs = new Dictionary<int, Vector3>(Vecs),
                Quats = new Dictionary<int, Quaternion>(Quats), Ints = new Dictionary<int, int>(Ints),
                Longs = new Dictionary<int, long>(Longs), Strings = new Dictionary<int, string>(Strings),
                Bytes = new Dictionary<int, byte[]>(Bytes),
            };

            public void Forget(int vec, int quat)
            {
                Vecs.Remove(vec);
                Quats.Remove(quat);
            }

            public void Write(ZDO z)
            {
                foreach (var e in Floats) z.Set(e.Key, e.Value);
                foreach (var e in Vecs) z.Set(e.Key, e.Value);
                foreach (var e in Quats) z.Set(e.Key, e.Value);
                foreach (var e in Ints) z.Set(e.Key, e.Value);
                foreach (var e in Longs) z.Set(e.Key, e.Value);
                foreach (var e in Strings) z.Set(e.Key, e.Value);
                foreach (var e in Bytes) z.Set(e.Key, e.Value);
            }
        }

        /// <summary>The flat sign whose spot the player looks at (within 1.5 m of where the crosshair meets the floor or
        /// the ceiling), or null: a flat sign's board is inside the floor or ceiling, out of the crosshair's reach.</summary>
        public static Sign Looked()
        {
            Transform eye = GameCamera.instance ? GameCamera.instance.transform : null;
            return eye ? Looked(eye.position, eye.forward) : null;
        }

        /// <summary>The same along any ray (the cursor's, when the window picks a sign).</summary>
        public static Sign Looked(Vector3 origin, Vector3 direction)
        {
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, 60f, Mask, QueryTriggerInteraction.Ignore)) return null;
            Sign best = null;
            float bestD = 1.5f;
            foreach (Sign s in SignFinder.Loaded())
            {
                if (!Flat(s) || SignFinder.Zdo(s) == null) continue;
                Vector3 d = s.transform.position - hit.point;
                if (Mathf.Abs(d.y) > 0.4f) continue;
                d.y = 0f;
                if (d.magnitude < bestD)
                {
                    bestD = d.magnitude;
                    best = s;
                }
            }
            if (!best) return null;
            List<Sign> stack = SignFinder.Stack(best);
            return stack.Count > 0 ? stack[0] : best;
        }
    }
}
