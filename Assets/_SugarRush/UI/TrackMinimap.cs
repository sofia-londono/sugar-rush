using System.Collections.Generic;
using UnityEngine;
using UIElements = UnityEngine.UIElements;

namespace SugarRush
{
    /// <summary>
    /// HUD mini-map: the circuit seen from above (drawn once with Painter2D from the racing
    /// line), the start line, Ralph's smashed stretches, and a dot per racer in their kart's
    /// colour; this player's dot is bigger with a white ring. Only the dots move each frame.
    /// </summary>
    public class TrackMinimap : UIElements.VisualElement
    {
        readonly Vector3[] points;
        readonly Vector2 min, size;
        readonly List<UIElements.VisualElement> dots = new();
        readonly List<UIElements.VisualElement> marks = new();

        public TrackMinimap(TrackPath path)
        {
            pickingMode = UIElements.PickingMode.Ignore;
            AddToClassList("minimap");
            points = path.points;
            Vector2 lo = new(float.MaxValue, float.MaxValue), hi = new(float.MinValue, float.MinValue);
            foreach (var p in points)
            {
                lo = Vector2.Min(lo, new Vector2(p.x, p.z));
                hi = Vector2.Max(hi, new Vector2(p.x, p.z));
            }
            min = lo;
            size = hi - lo;
            generateVisualContent += Draw;
            RegisterCallback<UIElements.GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        /// <summary>World position -> pixel inside the element (north up, keeping the aspect ratio).</summary>
        Vector2 ToMap(Vector3 world)
        {
            float w = resolvedStyle.width, h = resolvedStyle.height;
            const float pad = 18f;
            float scale = Mathf.Min((w - pad * 2f) / Mathf.Max(size.x, 1f), (h - pad * 2f) / Mathf.Max(size.y, 1f));
            float ox = (w - size.x * scale) * 0.5f, oy = (h - size.y * scale) * 0.5f;
            return new Vector2(ox + (world.x - min.x) * scale, h - (oy + (world.z - min.y) * scale));
        }

        void Draw(UIElements.MeshGenerationContext ctx)
        {
            float w = resolvedStyle.width, h = resolvedStyle.height;
            if (!(w >= 10f && h >= 10f) || points.Length < 2) return; // also rejects NaN before the first layout
            var p = ctx.painter2D;
            p.lineJoin = UIElements.LineJoin.Round;
            p.lineCap = UIElements.LineCap.Round;

            void Loop(float width, Color color)
            {
                p.strokeColor = color;
                p.lineWidth = width;
                p.BeginPath();
                p.MoveTo(ToMap(points[0]));
                for (int i = 1; i < points.Length; i++) p.LineTo(ToMap(points[i]));
                p.ClosePath();
                p.Stroke();
            }
            Loop(22f, new Color(0.36f, 0.18f, 0.38f, 0.6f)); // soft outline
            Loop(15f, Color.white);
            Loop(8f, new Color(1f, 0.56f, 0.78f));

            // Start / finish: a small checkered bar across the line.
            Vector2 a = ToMap(points[0]), b = ToMap(points[1]);
            Vector2 across = new Vector2(-(b - a).y, (b - a).x).normalized * 11f;
            p.strokeColor = new Color(0.25f, 0.15f, 0.25f);
            p.lineWidth = 5f;
            p.BeginPath();
            p.MoveTo(a - across);
            p.LineTo(a + across);
            p.Stroke();
        }

        /// <summary>Moves the racer dots (and the smashed-road marks) to their current positions.</summary>
        public void Refresh(IList<RaceProgress> racers, RaceProgress me, KartRoster roster, RalphChaos chaos)
        {
            if (!(resolvedStyle.width >= 10f)) return;
            while (dots.Count < racers.Count)
            {
                var dot = new UIElements.VisualElement { pickingMode = UIElements.PickingMode.Ignore };
                dot.AddToClassList("minimap__dot");
                Add(dot);
                dots.Add(dot);
            }
            for (int i = 0; i < dots.Count; i++)
            {
                var dot = dots[i];
                bool used = i < racers.Count && racers[i];
                dot.style.display = used ? UIElements.DisplayStyle.Flex : UIElements.DisplayStyle.None;
                if (!used) continue;
                var r = racers[i];
                bool mine = r == me;
                dot.EnableInClassList("minimap__dot--me", mine);
                dot.style.backgroundColor = roster ? roster.Get(r.kartIndex).color : Color.white;
                Vector2 m = ToMap(r.transform.position);
                float half = mine ? 16f : 11f;
                dot.style.left = m.x - half;
                dot.style.top = m.y - half;
            }
            // My dot on top of the others.
            int myIndex = me ? racers.IndexOf(me) : -1;
            if (myIndex >= 0 && myIndex < dots.Count) dots[myIndex].BringToFront();

            int zones = chaos && chaos.Active ? chaos.zones.Length : 0;
            while (marks.Count < zones)
            {
                var mark = new UIElements.VisualElement { pickingMode = UIElements.PickingMode.Ignore };
                mark.AddToClassList("minimap__rubble");
                Insert(0, mark);
                marks.Add(mark);
            }
            for (int z = 0; z < marks.Count; z++)
            {
                bool show = z < zones && chaos.IsBroken(z);
                marks[z].style.display = show ? UIElements.DisplayStyle.Flex : UIElements.DisplayStyle.None;
                if (!show) continue;
                Vector2 m = ToMap(chaos.zones[z].rubble.position);
                marks[z].style.left = m.x - 11f;
                marks[z].style.top = m.y - 11f;
            }
        }
    }
}
