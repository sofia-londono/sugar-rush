using System.Collections.Generic;
using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Fixed features of a track: sticky caramel puddles (karts that drive through slow down and
    /// wobble, like Ralph's rubble), obstacles at the road edge (giant gummy bears: real
    /// colliders, placed by the builder), turbo pads (a short boost when driven over) and trick
    /// ramps (real wedges; this only tells the AI where they are). The AI steers around the
    /// hazards and onto the pads (and some drivers onto the ramps). Each machine only checks the
    /// karts it simulates, so it works the same offline and online.
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

        /// <summary>A rectangle on the road (turbo pad or trick ramp).</summary>
        [System.Serializable]
        public class Pad
        {
            public Vector3 center, forward;
            public float halfLength = 2.5f, halfWidth = 1.6f;
            public float distance, lateral;
        }

        public static TrackHazards Instance { get; private set; }

        [Header("Turbo pads and trick ramps")]
        public Pad[] boostPads;
        public Pad[] trickRamps;
        public float padBoost = 1.1f;
        public event System.Action<RaceProgress> PadTaken;

        public TrackPath path;
        public Spot[] spots;
        // Moving obstacles this step (owned by MovingObstacles, reused, first movingCount valid).
        List<Spot> moving;
        int movingCount;

        public void SetMovingSpots(List<Spot> list, int count) { moving = list; movingCount = count; }
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
                if (boostPads != null)
                    foreach (var pad in boostPads)
                    {
                        Vector3 local = p - pad.center;
                        Vector3 right = Vector3.Cross(Vector3.up, pad.forward);
                        if (Mathf.Abs(Vector3.Dot(local, pad.forward)) < pad.halfLength && Mathf.Abs(Vector3.Dot(local, right)) < pad.halfWidth + 0.4f
                            && Mathf.Abs(local.y) < 2f)
                        {
                            if (!r.Kart.IsBoosting) PadTaken?.Invoke(r);
                            r.Kart.Boost(padBoost);
                        }
                    }
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
        /// For the AI: the lane of the next turbo pad (or trick ramp, if it likes them) a little ahead,
        /// when it is within reach of the current lane.
        /// </summary>
        public bool PadHint(float s, float lane, bool ramps, out float hint)
        {
            hint = lane;
            if (!path) return false;
            float best = 40f;
            bool found = NearestPad(boostPads, s, lane, ref best, ref hint);
            if (ramps) found |= NearestPad(trickRamps, s, lane, ref best, ref hint);
            return found;
        }

        bool NearestPad(Pad[] pads, float s, float lane, ref float best, ref float hint)
        {
            bool found = false;
            if (pads == null) return false;
            foreach (var pad in pads)
            {
                float ahead = Mathf.Repeat(pad.distance - s, path.Length);
                if (ahead > path.Length - pad.halfLength) ahead = 0f; // on it right now
                if (ahead > best || Mathf.Abs(lane - pad.lateral) > 4.5f) continue;
                best = ahead;
                hint = pad.lateral;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// For the AI: if the next hazard within <see cref="aiLookAhead"/> sits in this lane, the
        /// lane that passes it on its open side.
        /// </summary>
        public bool LaneHint(float s, float lane, out float hint)
        {
            hint = lane;
            if (!path) return false;
            float best = aiLookAhead;
            bool found = false;
            if (spots != null)
                foreach (var spot in spots) found |= Avoid(spot, s, lane, ref best, ref hint);
            for (int i = 0; i < movingCount && moving != null; i++) found |= Avoid(moving[i], s, lane, ref best, ref hint);
            return found;
        }

        /// <summary>If this spot is the nearest one ahead in the lane, the lane that passes it on its open side.</summary>
        bool Avoid(Spot spot, float s, float lane, ref float best, ref float hint)
        {
            float ahead = Mathf.Repeat(spot.distance - s, path.Length);
            if (ahead > best && ahead < path.Length - 3f) return false;
            if (ahead >= path.Length - 3f) ahead = 0f; // just alongside it
            float clearance = spot.radius + 1.4f;
            if (Mathf.Abs(lane - spot.lateral) >= clearance) return false;
            float half = path.HalfWidthAt(spot.distance) - 1f;
            float left = spot.lateral - clearance, right = spot.lateral + clearance;
            // Take whichever side has room (and is closer to where the kart already is).
            bool leftFits = left >= -half, rightFits = right <= half;
            float pick = leftFits && (!rightFits || Mathf.Abs(lane - left) < Mathf.Abs(lane - right)) ? left : right;
            hint = Mathf.Clamp(pick, -half, half);
            best = ahead;
            return true;
        }
    }
}
