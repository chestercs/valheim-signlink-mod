using System;
using SignLink.Pictures;
using UnityEngine;

namespace SignLink.UI
{
    /// <summary>The rows that say how a piece sits on its sign (Fit), in three parts: the sign's (DrawRotations: its
    /// tilt, with buttons for standing, face up and face down, and its rotation on its own face), the content's size
    /// (DrawSize) and where the content's middle goes (DrawContent: Right and Up along the sign's face as you read it,
    /// whatever the tilt; a sign is a flat thing,
    /// its content has no depth to move in, so there is no third direction (0.20.27: "Towards you" went; the sign
    /// itself moves on Move the sign). The Place tab draws the one panel for whatever the choice is; the rows show
    /// Session.Chosen.Fit and write it back (Layout only). The light is the content's: its row is on the source
    /// (2 Content, LightRow).</summary>
    internal sealed class FitPanel
    {
        public float Tilt, Turn, SizePct = 100f, RightM, AlongM;
        /// <summary>The half-degree grid a sign's rotation is saved on (SignPose.Savable: ZPackage.WriteSmallRotation): the Tilt
        /// and Rotate rows take it, so the texts are made for the very angle the sign keeps (0.20.191, the math review: Rotate
        /// 12.3 made texts for 12.3 on a sign saved at 12.5, a piece 4 m off the middle 1.4 cm off its spot).</summary>
        static readonly System.Func<float, float> HalfDegree = v => Mathf.Round(v * 2f) / 2f;
        /// <summary>The tilt tips the sign over its bottom edge (true) or about its middle.</summary>
        public bool Edge;
        /// <summary>The part off the sign's face a stored fit may carry (older fits: it cannot show): kept so a fit read
        /// back is the same fit (Fit.Same), not a change to apply.</summary>
        double normalWu;
        /// <summary>The piece's size as made, in metres (0: unknown): the size rows are then a width and a height, else a
        /// percentage. Set between frames (it changes which rows show).</summary>
        public Vector2 NaturalM;

        static readonly string[] Poses = { "Standing", "Face up", "Face down" };
        static readonly float[] PoseTilts = { 0f, 90f, -90f };
        readonly string key;

        /// <summary>key: what the number fields are called (one name per panel).</summary>
        public FitPanel(string key) => this.key = key;

        /// <summary>The rows as a fit: Up along the face and the part off it, back in the frame as built at the current
        /// tilt (Fit.OnSign the other way), so tilting the sign keeps the content where it sits on the face.</summary>
        public Fit Fit
        {
            get
            {
                double a = Tilt * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a), along = AlongM * 40.0;
                return new Fit { Scale = SizePct / 100.0, Right = RightM * 40.0, Up = along * c + normalWu * s, Out = -along * s + normalWu * c,
                                 Turn = Turn, Tilt = Tilt, Edge = Edge };
            }
            set                                              // unrounded: the rows round only what they show, so a fit
            {                                                // read back is the same fit (Fit.Same), not a change to apply
                SizePct = (float)(value.Scale * 100);
                RightM = (float)(value.Right / 40);
                Turn = (float)Fit.Angle(value.Turn);
                Tilt = (float)value.Tilt;
                Edge = value.Edge;
                value.OnSign(out _, out double up, out double normal);
                AlongM = (float)(up / 40);
                normalWu = normal;
            }
        }

        /// <summary>Every way the sign turns (3 Place › Sign rotations, 0.20.66, the author: a Sign rotations section that
        /// holds every rotation): the pose buttons (through Later: the button shown chosen changes), the tilt (about the
        /// middle, 0.20.78), then the Turn row (turnRow, MovePanel's), then Rotate, which spins the sign on its own face (the author:
        /// Rotate goes into the sign's positioning, since it rotates the sign after all ... Rotate goes under Turn).</summary>
        /// <param name="turnRow">the sign's Turn about the up axis, drawn under the tilt (the author: Turn (clockwise) goes
        /// right under the tilt)</param>
        public void DrawRotations(SignLinkWindow w, Action turnRow)
        {
            using (Widgets.Vertical(Theme.Section))
            {
                int posed = Array.IndexOf(PoseTilts, Mathf.Round(Tilt * 10f) / 10f), pick;
                using (Widgets.Horizontal())
                {
                    GUILayout.Label("The sign", Theme.Label, Widgets.W(Widgets.LabelW));
                    // small, as the pivot's chips were (0.20.78, the author: the Standing / Face up / Face down buttons as
                    // small as the middle / bottom edge buttons being removed now)
                    pick = Widgets.Toolbar(posed, Poses, Theme.Chip, Widgets.W(Mathf.Min(300f, Widgets.RestW)));
                    GUILayout.FlexibleSpace();
                }
                if (pick >= 0 && pick != posed)
                {
                    float to = PoseTilts[pick];
                    // a new tilt turns it about its middle; face up <-> face down keeps the content where it is in the
                    // world, its Up row changing sign (Fit.Laid, 0.20.107)
                    w.Later(() => Fit = Fit.Laid(to, false));
                }
                // the tilt turns the sign about its middle; the choice of pivot went (0.20.78, the author: tilt around
                // middle or bottom edge is legacy ... no bottom edge rotation needed, the fine-tuning is there for that:
                // Move the sign puts it where it should rest). A sign tilted over its bottom edge before keeps that pose
                // until its tilt changes.
                float tilt = Widgets.Number(key + "tilt", "Tilt", Tilt, -90f, 90f, 5f, "°", "0.#", snap: HalfDegree);
                if (Mathf.Abs(tilt - Tilt) > 1e-4f)
                {
                    tilt = HalfDegree(tilt);                                // as the save keeps it (SignPose.Savable)
                    // past a quarter turn (a typed -90 from face up) the face turns round, so it goes as the pose buttons go
                    // (Fit.Laid: the content stays where it is), not 10 m the other way; the part of the place off the face
                    // goes either way, so nothing hidden comes back on a later tilt (0.20.159, review)
                    if (Mathf.Abs(tilt - Tilt) > 90f) Fit = Fit.Laid(tilt, false);
                    else
                    {
                        Tilt = tilt;
                        Edge = false;
                        normalWu = 0;
                    }
                }
                Widgets.Space(4);
                turnRow?.Invoke();
                float turn = Widgets.Number(key + "turn", "Rotate (clockwise)", Turn, -180f, 180f, 5f, "°", "0.#", snap: HalfDegree);   // a piece that cannot move still turns
                if (Mathf.Abs(turn - Turn) > 1e-4f) Turn = HalfDegree(turn);   // a value the sign had already stays as it is
                Widgets.Hint("Turn swings the sign round where it stands, as the hammer turns a piece (Apply sign coord changes keeps it). " +
                             "Rotate spins it on its own face with what it shows. Tilt and Rotate show for you at once; the button " +
                             "at the bottom puts them on.");
            }
        }

        /// <summary>The size rows of what the sign holds (nothing picked): its scale (3 Place › Content size).</summary>
        public void DrawSize(bool movable)
        {
            bool was = GUI.enabled;
            using (Widgets.Vertical(Theme.Section))
            {
                GUI.enabled = was && movable;
                SizePct = Widgets.SizeRows(key, SizePct, NaturalM);
                GUI.enabled = was;
            }
        }

        /// <summary>The content's rows; movable: its place can change (else they show greyed).</summary>
        public void DrawContent(bool movable = true)
        {
            bool was = GUI.enabled;
            using (Widgets.Vertical(Theme.Section))
            {
                GUI.enabled = was && movable;
                RightM = Widgets.Number(key + "right", "Right", RightM, -25f, 25f, 0.25f, "m", typedLimit: 300f);
                AlongM = Widgets.Number(key + "up", "Up", AlongM, -25f, 25f, 0.25f, "m", typedLimit: 300f);
                GUI.enabled = was;
                // the same lines whatever the tilt, the tilt's own words on a line of their own: a hint that grew a line
                // as the Tilt slider below it crossed 45° moved the slider under the mouse (0.20.75)
                bool up = Tilt >= 45f, down = Tilt <= -45f;
                Widgets.Hint("Right and Up slide the content along the sign's face as you read it. The sign itself moves and turns " +
                             "under Sign, below.");
                Widgets.Hint(up ? "Face up: Up runs away from you." : down ? "Face down: Up runs towards you." : "Standing: Up runs up the sign.");
            }
        }
    }
}
