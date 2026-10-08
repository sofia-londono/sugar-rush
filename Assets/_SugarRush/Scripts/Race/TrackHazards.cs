using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Fixed hazards on a track: sticky caramel puddles (karts that drive through slow down and
    /// wobble, like Ralph's rubble) and obstacles at the road edge (giant gummy bears: real
    /// colliders, placed by the builder). The AI steers around both. Each machine only checks
    /// the karts it simulates, so it works the same offline and online.
    /// </summary>
    public class TrackHazards : MonoBehaviour
    {
        [System.Serializable]
        public class Spot
        {
            public Vector3 center;
            public float radius;
            [Tooltip("Metres along the racing line.")]
            public float distance;
            [Tooltip("Sideways from the racing line (+ = right).")]
            public float lateral;
            [Tooltip("Caramel: slows karts inside it. Otherwise just something to steer around.")]
            public bool sticky;
        }

        public static TrackHazards Instance { get; private set; }

        public TrackPath path;
        public Spot[] spots;
        [Tooltip("How far ahead the AI looks for hazards in its lane (m).")]
        public float aiLookAhead = 32f;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void FixedUpdate()
        {
            var race = RaceManager.Instance;
            if (!race || spots == null) return;
            foreach (var r in race.Racers)
            {
                if (!r || !r.hasAuthority || !r.Kart.IsGrounded) continue;
                Vector3 p = r.Kart.transform.position;
                foreach (var s in spots)
                {
                    if (!s.sticky) continue;
                    float dx = p.x - s.center.x, dz = p.z - s.center.z;
                    if (dx * dx + dz * dz < s.radius * s.radius && Mathf.Abs(p.y - s.center.y) < 2f)
                    {
                        r.Kart.SetRough(0.2f);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// For the AI: if the next hazard within <see cref="aiLookAhead"/> sits in this lane, the
        /// lane that passes it on its open side.
        /// </summary>
        public bool LaneHint(float s, float lane, out float hint)
        {
            hint = lane;
            if (spots == null || !path) return false;
            float best = aiLookAhead;
            bool found = false;
            foreach (var spot in spots)
            {
                float ahead = Mathf.Repeat(spot.distance - s, path.Length);
                if (ahead > best && ahead < path.Length - 3f) continue;
                if (ahead >= path.Length - 3f) ahead = 0f; // just alongside it
                float clearance = spot.radius + 1.4f;
                if (Mathf.Abs(lane - spot.lateral) >= clearance) continue;
                float half = path.HalfWidthAt(spot.distance) - 1f;
                float left = spot.lateral - clearance, right = spot.lateral + clearance;
                // Take whichever side has room (and is closer to where the kart already is).
                bool leftFits = left >= -half, rightFits = right <= half;
                float pick = leftFits && (!rightFits || Mathf.Abs(lane - left) < Mathf.Abs(lane - right)) ? left : right;
                hint = Mathf.Clamp(pick, -half, half);
                best = ahead;
                found = true;
            }
            return found;
        }
    }
}
