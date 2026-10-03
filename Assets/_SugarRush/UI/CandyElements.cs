using UnityEngine;
using UnityEngine.UIElements;

namespace SugarRush
{
    public enum CandyTone { Rainbow, Pink, Mint, Lavender, Lemon, Sky }

    /// <summary>Pastel colours shared by the procedural candy UI elements.</summary>
    public static class CandyPalette
    {
        public static readonly Color Pink = Hex("#FF8FC7"), PinkLight = Hex("#FFC2E0"), PinkDark = Hex("#D9579A");
        public static readonly Color Mint = Hex("#7EDDBF"), MintLight = Hex("#B5F0DD"), MintDark = Hex("#3FAE8A");
        public static readonly Color Lavender = Hex("#C3A3FF"), LavenderLight = Hex("#E0D0FF"), LavenderDark = Hex("#8E66DB");
        public static readonly Color Lemon = Hex("#FFE07A"), LemonLight = Hex("#FFF0B8"), LemonDark = Hex("#D9A93A");
        public static readonly Color Sky = Hex("#8FD3FF"), SkyLight = Hex("#C7EAFF"), SkyDark = Hex("#4E9FD6");
        public static readonly Color Cream = Hex("#FFF7FB"), Choco = Hex("#5A3248");

        public static readonly Color[] Sprinkles = { Pink, Mint, Lavender, Lemon, Sky, Hex("#FF6F91"), Color.white };
        static readonly string[] RainbowHex = { "#FF8FC7", "#FFE07A", "#7EDDBF", "#8FD3FF", "#C3A3FF" };

        public static Color Base(CandyTone t) => t switch
        {
            CandyTone.Mint => Mint, CandyTone.Lavender => Lavender, CandyTone.Lemon => Lemon, CandyTone.Sky => Sky, _ => Pink,
        };

        public static Color Light(CandyTone t) => t switch
        {
            CandyTone.Mint => MintLight, CandyTone.Lavender => LavenderLight, CandyTone.Lemon => LemonLight, CandyTone.Sky => SkyLight, _ => PinkLight,
        };

        /// <summary>Wraps each letter in a rich-text colour tag, cycling through the pastel palette.</summary>
        public static string RainbowText(string text)
        {
            var sb = new System.Text.StringBuilder();
            int k = 0;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) { sb.Append(c); continue; }
                sb.Append("<color=").Append(RainbowHex[k++ % RainbowHex.Length]).Append('>').Append(c).Append("</color>");
            }
            return sb.ToString();
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        public static void TraceRoundedRect(Painter2D p, float x, float y, float w, float h, float r)
        {
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
            p.MoveTo(new Vector2(x + r, y));
            p.LineTo(new Vector2(x + w - r, y));
            p.ArcTo(new Vector2(x + w, y), new Vector2(x + w, y + r), r);
            p.LineTo(new Vector2(x + w, y + h - r));
            p.ArcTo(new Vector2(x + w, y + h), new Vector2(x + w - r, y + h), r);
            p.LineTo(new Vector2(x + r, y + h));
            p.ArcTo(new Vector2(x, y + h), new Vector2(x, y + h - r), r);
            p.LineTo(new Vector2(x, y + r));
            p.ArcTo(new Vector2(x, y), new Vector2(x + r, y), r);
            p.ClosePath();
        }

        public static void Sprinkle(Painter2D p, Vector2 at, float angle, float length, Color color)
        {
            var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (length * 0.5f);
            p.strokeColor = color;
            p.BeginPath();
            p.MoveTo(at - d);
            p.LineTo(at + d);
            p.Stroke();
        }
    }

    /// <summary>
    /// Puffy candy lettering: a dark offset copy for depth, the coloured letters with a thick
    /// white outline, and a translucent white copy clipped to the top half as a sugar-glass shine.
    /// </summary>
    public class CandyTitle : VisualElement
    {
        readonly Label shadow, text, glossText;
        readonly VisualElement glossClip;
        string value;
        CandyTone tone;

        public CandyTitle(string value, CandyTone tone, string sizeClass)
        {
            AddToClassList("candy-title");
            AddToClassList(sizeClass);
            pickingMode = PickingMode.Ignore;

            shadow = new Label { pickingMode = PickingMode.Ignore };
            shadow.AddToClassList("candy-title__shadow");
            text = new Label { pickingMode = PickingMode.Ignore };
            text.AddToClassList("candy-title__text");
            glossClip = new VisualElement { pickingMode = PickingMode.Ignore };
            glossClip.AddToClassList("candy-title__gloss");
            glossText = new Label { pickingMode = PickingMode.Ignore };
            glossText.AddToClassList("candy-title__gloss-text");
            glossClip.Add(glossText);

            Add(shadow);
            Add(text);
            Add(glossClip);

            // The clipped shine copy must be exactly as tall as the real text to line up.
            text.RegisterCallback<GeometryChangedEvent>(_ => glossText.style.height = text.layout.height);

            Tone = tone;
            Text = value;
        }

        public string Text
        {
            get => value;
            set
            {
                if (this.value == value) return;
                this.value = value;
                Refresh();
            }
        }

        public CandyTone Tone
        {
            get => tone;
            set
            {
                RemoveFromClassList("candy-title--" + tone.ToString().ToLowerInvariant());
                tone = value;
                AddToClassList("candy-title--" + tone.ToString().ToLowerInvariant());
                Refresh();
            }
        }

        void Refresh()
        {
            if (value == null) return;
            // Luckiest Guy's lowercase glyphs look odd at title sizes, so titles are always caps.
            string caps = value.ToUpperInvariant();
            shadow.text = caps;
            glossText.text = caps;
            text.text = tone == CandyTone.Rainbow ? CandyPalette.RainbowText(caps) : caps;
        }
    }

    /// <summary>
    /// Cream panel with pink (or other) icing dripping over the top edge, colourful sprinkles,
    /// a soft drop shadow and a white candy border. Drips are seeded so they stay put.
    /// </summary>
    public class FrostingPanel : VisualElement
    {
        const float Radius = 44f;
        const float Border = 7f;
        readonly CandyTone icing;
        readonly int seed;

        public float IcingDepth { get; set; } = 44f;

        public FrostingPanel(CandyTone icing = CandyTone.Pink, int seed = 0)
        {
            this.icing = icing;
            this.seed = seed != 0 ? seed : Random.Range(1, int.MaxValue);
            AddToClassList("frosting-panel");
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (!(w >= 2f && h >= 2f)) return; // also rejects NaN before the first layout
            var p = ctx.painter2D;
            var rng = new System.Random(seed);
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // Drop shadow and body
            p.fillColor = new Color(0.35f, 0.15f, 0.27f, 0.22f);
            p.BeginPath();
            CandyPalette.TraceRoundedRect(p, 6f, 14f, w, h, Radius);
            p.Fill();

            p.fillColor = CandyPalette.Cream;
            p.BeginPath();
            CandyPalette.TraceRoundedRect(p, 0f, 0f, w, h, Radius);
            p.Fill();

            // Icing with drips along its lower edge (traced right to left)
            float d = Mathf.Min(IcingDepth, h * 0.45f);
            var dripGlints = new System.Collections.Generic.List<Vector2>();
            p.fillColor = CandyPalette.Base(icing);
            p.BeginPath();
            p.MoveTo(new Vector2(0f, Radius));
            p.ArcTo(new Vector2(0f, 0f), new Vector2(Radius, 0f), Radius);
            p.LineTo(new Vector2(w - Radius, 0f));
            p.ArcTo(new Vector2(w, 0f), new Vector2(w, Radius), Radius);
            p.LineTo(new Vector2(w, d));
            float x = w;
            while (x > 0f)
            {
                float nx = Mathf.Max(0f, x - Rand(55f, 120f));
                bool drip = nx > 30f && x < w - 30f && rng.NextDouble() < 0.65;
                if (drip)
                {
                    float dw = Rand(18f, 30f);
                    float bottom = d + Rand(14f, Mathf.Min(36f, h - d - 20f));
                    float cx = (x + nx) * 0.5f;
                    float right = cx + dw * 0.5f, left = cx - dw * 0.5f;
                    p.BezierCurveTo(new Vector2((x + right) * 0.5f, d), new Vector2(right, d), new Vector2(right, d + 6f));
                    p.LineTo(new Vector2(right, bottom - dw * 0.5f));
                    p.Arc(new Vector2(cx, bottom - dw * 0.5f), dw * 0.5f, Angle.Degrees(0f), Angle.Degrees(180f), ArcDirection.Clockwise);
                    p.LineTo(new Vector2(left, d + 6f));
                    p.BezierCurveTo(new Vector2(left, d), new Vector2((left + nx) * 0.5f, d), new Vector2(nx, d));
                    dripGlints.Add(new Vector2(cx - dw * 0.18f, bottom - dw * 0.55f));
                }
                else
                {
                    p.QuadraticCurveTo(new Vector2((x + nx) * 0.5f, d + Rand(6f, 14f)), new Vector2(nx, d));
                }
                x = nx;
            }
            p.LineTo(new Vector2(0f, Radius));
            p.ClosePath();
            p.Fill();

            // Shine along the icing and on each drip
            p.strokeColor = new Color(1f, 1f, 1f, 0.55f);
            p.lineWidth = 6f;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(Radius * 0.9f, 14f));
            p.LineTo(new Vector2(Mathf.Min(w * 0.45f, w - Radius), 14f));
            p.Stroke();
            p.fillColor = new Color(1f, 1f, 1f, 0.6f);
            foreach (var g in dripGlints)
            {
                p.BeginPath();
                p.Arc(g, 3.5f, Angle.Degrees(0f), Angle.Degrees(360f));
                p.Fill();
            }

            // Sprinkles on the icing
            p.lineWidth = 6f;
            int count = Mathf.RoundToInt(w / 24f);
            var tone = CandyPalette.Base(icing);
            for (int i = 0; i < count; i++)
            {
                var color = CandyPalette.Sprinkles[rng.Next(CandyPalette.Sprinkles.Length)];
                if (color == tone) color = Color.white;
                var at = new Vector2(Rand(Radius * 0.7f, w - Radius * 0.7f), Rand(12f, d - 8f));
                CandyPalette.Sprinkle(p, at, Rand(0f, Mathf.PI), 13f, color);
            }

            // White candy border on top of everything
            p.strokeColor = Color.white;
            p.lineWidth = Border;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            CandyPalette.TraceRoundedRect(p, Border * 0.5f, Border * 0.5f, w - Border, h - Border, Radius - Border * 0.5f);
            p.Stroke();
        }
    }

    /// <summary>Diagonal candy-cane stripes that can scroll; clipped by a rounded parent.</summary>
    public class CandyStripes : VisualElement
    {
        public float stripeWidth = 18f;
        public Color color = new(1f, 1f, 1f, 0.4f);
        float offset;
        IVisualElementScheduledItem tick;

        public CandyStripes()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        public bool Animate
        {
            set
            {
                if (value)
                {
                    tick ??= schedule.Execute(() => { offset += 1.6f; MarkDirtyRepaint(); }).Every(33);
                    tick.Resume();
                }
                else tick?.Pause();
            }
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (!(w >= 1f && h >= 1f)) return; // also rejects NaN before the first layout
            var p = ctx.painter2D;
            p.fillColor = color;
            float period = stripeWidth * 2f;
            for (float x = -period + offset % period; x < w + h; x += period)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(x, 0f));
                p.LineTo(new Vector2(x + stripeWidth, 0f));
                p.LineTo(new Vector2(x + stripeWidth - h, h));
                p.LineTo(new Vector2(x - h, h));
                p.ClosePath();
                p.Fill();
            }
        }
    }

    /// <summary>Round peppermint candy (alternating wedges, white rim, shine).</summary>
    public class PeppermintDisc : VisualElement
    {
        readonly Color stripe;

        public PeppermintDisc(Color stripe)
        {
            this.stripe = stripe;
            pickingMode = PickingMode.Ignore;
            AddToClassList("peppermint-disc");
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (!(w >= 2f && h >= 2f)) return; // also rejects NaN before the first layout
            var p = ctx.painter2D;
            var c = new Vector2(w * 0.5f, h * 0.5f);
            float r = Mathf.Min(w, h) * 0.5f - 3f;

            p.fillColor = Color.white;
            p.BeginPath();
            p.Arc(c, r, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Fill();

            p.fillColor = stripe;
            for (int i = 0; i < 8; i += 2)
            {
                float a0 = i * 45f, a1 = a0 + 45f;
                p.BeginPath();
                p.MoveTo(c);
                p.Arc(c, r - 4f, Angle.Degrees(a0), Angle.Degrees(a1));
                p.ClosePath();
                p.Fill();
            }

            p.strokeColor = Color.white;
            p.lineWidth = 6f;
            p.BeginPath();
            p.Arc(c, r - 1f, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Stroke();

            p.fillColor = new Color(1f, 1f, 1f, 0.55f);
            p.BeginPath();
            p.Arc(c + new Vector2(-r * 0.35f, -r * 0.4f), r * 0.2f, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Fill();
        }
    }

    /// <summary>Thick rounded chevron (white with a dark candy edge) pointing left or right.</summary>
    public class ChevronGlyph : VisualElement
    {
        readonly bool right;
        readonly Color edge;

        public ChevronGlyph(bool right, Color edge)
        {
            this.right = right;
            this.edge = edge;
            pickingMode = PickingMode.Ignore;
            AddToClassList("chevron-glyph");
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        void Draw(MeshGenerationContext ctx)
        {
            float w = layout.width, h = layout.height;
            if (!(w >= 2f && h >= 2f)) return; // also rejects NaN before the first layout
            var p = ctx.painter2D;
            float s = Mathf.Min(w, h);
            var c = new Vector2(w * 0.5f, h * 0.5f);
            float dx = s * 0.13f * (right ? 1f : -1f), dy = s * 0.24f;
            var top = c + new Vector2(-dx, -dy);
            var tip = c + new Vector2(dx, 0f);
            var bottom = c + new Vector2(-dx, dy);

            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            foreach (var (width, color) in new[] { (s * 0.24f, edge), (s * 0.13f, Color.white) })
            {
                p.lineWidth = width;
                p.strokeColor = color;
                p.BeginPath();
                p.MoveTo(top);
                p.LineTo(tip);
                p.LineTo(bottom);
                p.Stroke();
            }
        }
    }

    /// <summary>Full-screen layer of slowly falling, spinning sprinkles.</summary>
    public class SprinkleRain : VisualElement
    {
        struct Bit { public Vector2 pos; public float speed, angle, spin; public Color color; }

        readonly Bit[] bits;
        readonly System.Random rng = new();
        bool seeded;
        IVisualElementScheduledItem tick;

        public SprinkleRain(int count = 45)
        {
            bits = new Bit[count];
            pickingMode = PickingMode.Ignore;
            AddToClassList("sprinkle-rain");
            generateVisualContent += Draw;
            RegisterCallback<AttachToPanelEvent>(_ => (tick ??= schedule.Execute(Step).Every(33)).Resume());
            RegisterCallback<DetachFromPanelEvent>(_ => tick?.Pause());
        }

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void Step()
        {
            float w = layout.width, h = layout.height;
            if (!(w >= 1f && h >= 1f)) return; // also rejects NaN before the first layout
            for (int i = 0; i < bits.Length; i++)
            {
                ref var b = ref bits[i];
                if (!seeded || b.pos.y > h + 20f)
                {
                    b.pos = new Vector2(Rand(0f, w), seeded ? -20f : Rand(-h, h));
                    b.speed = Rand(40f, 110f);
                    b.angle = Rand(0f, Mathf.PI);
                    b.spin = Rand(-3f, 3f);
                    b.color = CandyPalette.Sprinkles[rng.Next(CandyPalette.Sprinkles.Length)];
                }
                b.pos.y += b.speed * 0.033f;
                b.angle += b.spin * 0.033f;
            }
            seeded = true;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            if (!seeded) return;
            var p = ctx.painter2D;
            p.lineWidth = 7f;
            p.lineCap = LineCap.Round;
            foreach (var b in bits) CandyPalette.Sprinkle(p, b.pos, b.angle, 16f, b.color);
        }
    }
}
