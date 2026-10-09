using System.Collections.Generic;
using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Obstacles that move: giant gum balls rolling from one side of the road to the other,
    /// candy-cane pendulums swinging across it, gummy bears walking across, and gummies falling
    /// from the trees (a shadow grows on the road first; whoever is under it when it lands is
    /// slowed, then the gummy sits there as a bump for a moment and melts away).
    /// Everything is a function of the race clock (RaceManager.RaceTime, the server's clock
    /// online), so every machine sees the same thing without sending anything. Moved as
    /// kinematic bodies, so karts bounce off them. Each frame they are also reported to
    /// TrackHazards as moving spots, so the AI steers around them.
    /// </summary>
    public class MovingObstacles : MonoBehaviour
    {
        [System.Serializable]
        public class Roller
        {
            public Rigidbody body;
            public Vector3 from, to;
            public float radius = 1.6f, period = 6f, phase;
        }

        [System.Serializable]
        public class Pendulum
        {
            public Rigidbody bob;
            public Transform arm;
            public Vector3 pivot, swingAxis;
            public float length = 7f, amplitude = 55f, period = 3.4f, phase;
        }

        [System.Serializable]
        public class Walker
        {
            public Rigidbody body;
            public Vector3 from, to;
            public float walkTime = 3.4f, waitTime = 2.2f, phase;
        }

        [System.Serializable]
        public class GummyRain
        {
            [Tooltip("Stretch of road (metres along the racing line).")]
            public float from, to;
            public float interval = 1.4f, warning = 1.6f, lying = 2.6f, dropHeight = 14f, size = 1.3f;
            public Rigidbody[] drops;
            public Transform[] shadows;
        }

        public TrackPath path;
        public Roller[] rollers;
        public Pendulum[] pendulums;
        public Walker[] walkers;
        public GummyRain rain;

        // Reused every physics step (no garbage): the moving spots reported to TrackHazards.
        readonly List<TrackHazards.Spot> spots = new();
        int spotCount;
        readonly Dictionary<Object, int> segmentHint = new();
        static readonly RaycastHit[] hits = new RaycastHit[8];

        float Clock
        {
            get
            {
                var race = RaceManager.Instance;
                return race ? race.RaceTime : Time.timeSinceLevelLoad;
            }
        }

        void FixedUpdate()
        {
            float t = Clock;
            spotCount = 0;

            if (rollers != null)
                foreach (var r in rollers)
                {
                    if (!r.body) continue;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(t / r.period * 2f + r.phase, 1f));
                    Vector3 pos = Vector3.Lerp(r.from, r.to, k);
                    Vector3 travel = r.to - r.from;
                    float rolled = k * travel.magnitude / Mathf.Max(0.1f, r.radius) * Mathf.Rad2Deg;
                    var axis = Vector3.Cross(Vector3.up, travel.normalized);
                    r.body.MovePosition(pos);
                    r.body.MoveRotation(Quaternion.AngleAxis(rolled, axis));
                    Report(r.body, pos, r.radius + 0.4f);
                }

            if (pendulums != null)
                foreach (var p in pendulums)
                {
                    if (!p.bob) continue;
                    float angle = p.amplitude * Mathf.Sin((t / p.period + p.phase) * Mathf.PI * 2f);
                    var rot = Quaternion.AngleAxis(angle, p.swingAxis);
                    Vector3 bob = p.pivot + rot * (Vector3.down * p.length);
                    p.bob.MovePosition(bob);
                    p.bob.MoveRotation(rot);
                    if (p.arm) p.arm.SetPositionAndRotation(p.pivot, rot);
                    Report(p.bob, bob, 1.6f);
                }

            if (walkers != null)
                foreach (var w in walkers)
                {
                    if (!w.body) continue;
                    // Walk across, wait at the edge, walk back, wait.
                    float cycle = 2f * (w.walkTime + w.waitTime);
                    float c = Mathf.Repeat(t + w.phase * cycle, cycle);
                    float k;
                    if (c < w.walkTime) k = c / w.walkTime;
                    else if (c < w.walkTime + w.waitTime) k = 1f;
                    else if (c < 2f * w.walkTime + w.waitTime) k = 1f - (c - w.walkTime - w.waitTime) / w.walkTime;
                    else k = 0f;
                    bool walking = k > 0f && k < 1f;
                    Vector3 pos = Vector3.Lerp(w.from, w.to, Mathf.SmoothStep(0f, 1f, k));
                    pos.y += walking ? Mathf.Abs(Mathf.Sin(t * 9f)) * 0.35f : 0f; // little hops
                    Vector3 heading = c < w.walkTime + w.waitTime ? w.to - w.from : w.from - w.to;
                    w.body.MovePosition(pos);
                    w.body.MoveRotation(Quaternion.LookRotation(Vector3.ProjectOnPlane(heading, Vector3.up).normalized, Vector3.up));
                    Report(w.body, pos, 1.5f);
                }

            UpdateRain(t);
            if (TrackHazards.Instance) TrackHazards.Instance.SetMovingSpots(spots, spotCount);
        }

        /// <summary>
        /// Gummies falling on a stretch of road. Drop number k falls at k * interval on a spot picked
        /// from k alone (same on every machine); its shadow grows during the warning time.
        /// </summary>
        void UpdateRain(float t)
        {
            var r = rain;
            if (r == null || r.drops == null || r.drops.Length == 0 || !path || r.to <= r.from) return;
            int pool = r.drops.Length;
            int newest = Mathf.FloorToInt((t + r.warning) / r.interval);
            for (int slot = 0; slot < pool; slot++)
            {
                // The most recent drop that uses this slot.
                int k = newest - Mathf.Abs(((newest - slot) % pool + pool) % pool);
                var body = r.drops[slot];
                var shadow = r.shadows != null && slot < r.shadows.Length ? r.shadows[slot] : null;
                float land = k * r.interval;
                float since = t - land; // < 0: falling or warning
                if (k < 1 || since > r.lying + 1f || since < -r.warning)
                {
                    Park(body, shadow);
                    continue;
                }
                float d = Mathf.Lerp(r.from, r.to, Hash(k, 1));
                float half = path.HalfWidthAt(d) - 1.2f;
                float lateral = Mathf.Lerp(-half, half, Hash(k, 2));
                Vector3 centre = path.PositionAtDistance(d);
                Vector3 right = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(path.DirectionAtDistance(d), Vector3.up)).normalized;
                Vector3 ground = Ground(centre + right * lateral);

                if (shadow)
                {
                    shadow.gameObject.SetActive(since < 0.2f);
                    float grow = Mathf.Clamp01(1f + since / r.warning);
                    shadow.position = ground + Vector3.up * 0.05f;
                    shadow.localScale = Vector3.one * Mathf.Lerp(0.3f, r.size * 1.6f, grow);
                }
                float fall = since < 0f ? Mathf.Clamp01(-since / 0.55f) : 0f; // the last 0.55 s of the warning: falling
                float melt = since > r.lying ? 1f - Mathf.Clamp01(since - r.lying) : 1f;
                body.gameObject.SetActive(since > -0.55f);
                body.transform.localScale = Vector3.one * r.size * Mathf.Max(0.01f, melt);
                body.MovePosition(ground + Vector3.up * (r.size * 0.4f + fall * fall * r.dropHeight));
                body.MoveRotation(Quaternion.Euler(0f, k * 47f, 0f));

                // Landing on someone: they are knocked about and slowed for a moment.
                if (since >= 0f && since < Time.fixedDeltaTime * 1.5f) Splat(ground, r.size * 1.3f);
                if (since > -r.warning) Report(body, ground, r.size + 0.6f);
            }
        }

        static void Park(Rigidbody body, Transform shadow)
        {
            if (body && body.gameObject.activeSelf) body.gameObject.SetActive(false);
            if (shadow && shadow.gameObject.activeSelf) shadow.gameObject.SetActive(false);
        }

        /// <summary>Same number for the same drop on every machine (0..1).</summary>
        static float Hash(int k, int salt)
        {
            float x = Mathf.Sin(k * 12.9898f + salt * 78.233f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        static Vector3 Ground(Vector3 p)
        {
            int count = Physics.RaycastNonAlloc(p + Vector3.up * 4f, Vector3.down, hits, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (hits[i].collider.name == "RoadCollider") return hits[i].point;
            return p;
        }

        static void Splat(Vector3 at, float radius)
        {
            var race = RaceManager.Instance;
            if (!race) return;
            foreach (var r in race.Racers)
            {
                if (!r || !r.hasAuthority) continue;
                if ((r.Kart.transform.position - at).sqrMagnitude > radius * radius) continue;
                r.Kart.SetRough(1.1f);
                var rb = r.Kart.GetComponent<Rigidbody>();
                if (rb && !rb.isKinematic) rb.AddForce(Vector3.up * 4f, ForceMode.VelocityChange);
            }
        }

        void Report(Object owner, Vector3 pos, float radius)
        {
            if (!path) return;
            segmentHint.TryGetValue(owner, out int hint);
            int seg = path.FindClosestSegment(pos, hint > 0 ? hint : -1);
            segmentHint[owner] = seg;
            float d = path.ProjectDistance(pos, seg);
            Vector3 right = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(path.Direction(seg), Vector3.up)).normalized;
            float lateral = Vector3.Dot(pos - path.PositionAtDistance(d), right);
            if (Mathf.Abs(lateral) > path.HalfWidthAt(d) + radius) return; // off the road: nothing to dodge
            if (spotCount == spots.Count) spots.Add(new TrackHazards.Spot());
            var spot = spots[spotCount++];
            spot.center = pos;
            spot.radius = radius;
            spot.distance = d;
            spot.lateral = lateral;
            spot.sticky = false;
        }
    }
}
