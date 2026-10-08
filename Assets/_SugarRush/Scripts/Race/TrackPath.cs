using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Closed racing line around the circuit (evenly spaced points, index 0 = start/finish line).
    /// Used for lap counting, race positions, AI steering and putting karts back on the track.
    /// </summary>
    public class TrackPath : MonoBehaviour
    {
        public Vector3[] points;
        public float roadHalfWidth = 5f;
        [Tooltip("Optional half width at each point (tracks with narrow stretches); empty = roadHalfWidth everywhere.")]
        public float[] halfWidths;

        float[] cumulative;
        float length;

        public int Count => points.Length;
        public float Length { get { EnsureCache(); return length; } }

        void Awake() => EnsureCache();

        void EnsureCache()
        {
            if (cumulative != null && cumulative.Length == points.Length) return;
            cumulative = new float[points.Length];
            float d = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                cumulative[i] = d;
                d += Vector3.Distance(points[i], points[(i + 1) % points.Length]);
            }
            length = d;
        }

        public int Wrap(int i) => ((i % Count) + Count) % Count;
        public Vector3 Point(int i) => points[Wrap(i)];
        public Vector3 Direction(int i) => (Point(i + 1) - Point(i)).normalized;
        public float DistanceAt(int i) { EnsureCache(); return cumulative[Wrap(i)]; }

        public float WrapDistance(float d) => Mathf.Repeat(d, Length);

        /// <summary>Segment index containing the given distance along the path.</summary>
        public int SegmentAtDistance(float d)
        {
            EnsureCache();
            d = WrapDistance(d);
            int lo = 0, hi = Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (cumulative[mid] <= d) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        public Vector3 PositionAtDistance(float d)
        {
            d = WrapDistance(d);
            int i = SegmentAtDistance(d);
            float segLength = Vector3.Distance(Point(i), Point(i + 1));
            float t = segLength > 0f ? (d - cumulative[i]) / segLength : 0f;
            return Vector3.Lerp(Point(i), Point(i + 1), t);
        }

        public Vector3 DirectionAtDistance(float d) => Direction(SegmentAtDistance(d));

        /// <summary>Drivable half width of the road at a distance along the path.</summary>
        public float HalfWidthAt(float d) =>
            halfWidths != null && halfWidths.Length == Count ? halfWidths[SegmentAtDistance(d)] : roadHalfWidth;

        /// <summary>
        /// Closest segment to a position. With a hint, only nearby segments are searched so
        /// overlapping parts of the track (bridges) don't confuse it.
        /// </summary>
        public int FindClosestSegment(Vector3 position, int hint = -1, int back = 4, int ahead = 12)
        {
            if (hint >= 0)
            {
                int best = SearchRange(position, hint - back, hint + ahead, out float bestDist);
                if (bestDist < 30f) return best;
            }
            return SearchRange(position, 0, Count - 1, out _);
        }

        int SearchRange(Vector3 position, int from, int to, out float bestDist)
        {
            int best = Wrap(from);
            bestDist = float.MaxValue;
            for (int k = from; k <= to; k++)
            {
                int i = Wrap(k);
                float d = DistanceToSegment(position, i, out _);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        /// <summary>Distance to a segment, with height differences weighted double (separates stacked roads).</summary>
        public float DistanceToSegment(Vector3 position, int i, out float t)
        {
            Vector3 a = Point(i), b = Point(i + 1);
            Vector3 ab = b - a;
            t = Mathf.Clamp01(Vector3.Dot(position - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
            Vector3 delta = position - (a + ab * t);
            delta.y *= 2f;
            return delta.magnitude;
        }

        /// <summary>Distance along the path of the position projected onto segment i.</summary>
        public float ProjectDistance(Vector3 position, int i)
        {
            DistanceToSegment(position, i, out float t);
            return DistanceAt(i) + t * Vector3.Distance(Point(i), Point(i + 1));
        }

        void OnDrawGizmos()
        {
            if (points == null || points.Length < 2) return;
            Gizmos.color = new Color(1f, 0.4f, 0.8f);
            for (int i = 0; i < points.Length; i++)
                Gizmos.DrawLine(points[i] + Vector3.up * 0.5f, points[(i + 1) % points.Length] + Vector3.up * 0.5f);
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(points[0], 2f);
        }
    }
}
