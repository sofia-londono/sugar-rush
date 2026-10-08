using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Tracks one racer along the TrackPath: distance raced, laps, wrong-way and off-track
    /// detection, and putting the kart back on the road ("back to track").
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class RaceProgress : MonoBehaviour
    {
        public TrackPath path;
        public string racerName;
        [Tooltip("Index in the kart roster (who this is, for the podium).")]
        public int kartIndex;
        public bool isPlayer;
        [Tooltip("Driven by a person (this machine's player or, online, someone else's).")]
        public bool isHuman;
        [Tooltip("Online: only the machine that simulates this kart may teleport it back to the track.")]
        public bool hasAuthority = true;

        [Header("Off track")]
        public float offTrackMargin = 7f;
        public float fallenDepth = 5f;
        public float autoReturnDelay = 6f;
        public float fallenReturnDelay = 1.5f;

        [Header("Stuck")]
        public float minProgressPerSecond = 2f;
        public float playerStuckReturnDelay = 8f;
        public float aiStuckReturnDelay = 4f;

        public KartController Kart { get; private set; }
        /// <summary>Total distance raced along the path (negative while behind the start line).</summary>
        public float RaceDistance { get; private set; }
        public int Segment { get; private set; }
        public float PathDistance { get; private set; }
        public int CompletedLaps { get; private set; }
        public int Position { get; set; }
        public bool Finished { get; set; }
        public float FinishTime { get; set; }
        public float BestLap { get; private set; }
        /// <summary>Ralph's chaos, offline: gold coins carried (online the count lives in NetKart.Coins).</summary>
        public int Coins { get; set; }
        public float LastLap { get; private set; }
        public bool WrongWay { get; private set; }
        public bool OffTrack { get; private set; }
        public float OffTrackTime { get; private set; }
        /// <summary>Seconds spent pressing the throttle without making progress along the track.</summary>
        public float StuckTime { get; private set; }
        /// <summary>Show the "lost?" prompt: off the road, or stuck against something.</summary>
        public bool NeedsHelp => (OffTrack && OffTrackTime > 1f) || StuckTime > 2.5f;
        public float AutoReturnIn => OffTrack
            ? (fallen ? fallenReturnDelay : autoReturnDelay) - OffTrackTime
            : StuckReturnDelay - StuckTime;
        float StuckReturnDelay => isPlayer ? playerStuckReturnDelay : aiStuckReturnDelay;
        /// <summary>How many times this racer was put back on the track.</summary>
        public int TrackReturns { get; private set; }
        /// <summary>Diagnostics: "segment:reason" for each return to the track.</summary>
        public readonly System.Collections.Generic.List<string> ReturnLog = new();

        public event System.Action<RaceProgress> LapCompleted;

        float lastPathDistance;
        float wrongWayTimer;
        float lapStartTime;
        bool fallen;
        float progressWindow, progressWindowStart;

        void Awake()
        {
            Kart = GetComponent<KartController>();
            Kart.RespawnOverride = ReturnToTrack;
        }

        void Start() => ResetTracking();

        /// <summary>Re-locates the kart on the path from scratch (start of race, after teleports).</summary>
        public void ResetTracking()
        {
            Segment = path.FindClosestSegment(transform.position);
            PathDistance = path.ProjectDistance(transform.position, Segment);
            lastPathDistance = PathDistance;
            RaceDistance = PathDistance > path.Length * 0.5f ? PathDistance - path.Length : PathDistance;
            CompletedLaps = 0;
            lapStartTime = 0f;
        }

        public void MarkLapStart(float raceTime) => lapStartTime = raceTime;

        void FixedUpdate()
        {
            if (!path) return;
            float dt = Time.fixedDeltaTime;

            Segment = path.FindClosestSegment(transform.position, Segment);
            PathDistance = path.ProjectDistance(transform.position, Segment);

            float delta = PathDistance - lastPathDistance;
            if (delta > path.Length * 0.5f) delta -= path.Length;
            else if (delta < -path.Length * 0.5f) delta += path.Length;
            if (Mathf.Abs(delta) < 30f) RaceDistance += delta; // ignore teleports and shortcuts
            lastPathDistance = PathDistance;

            int laps = Mathf.FloorToInt(RaceDistance / path.Length);
            if (laps > CompletedLaps)
            {
                CompletedLaps = laps;
                float now = RaceManager.Instance ? RaceManager.Instance.RaceTime : Time.time;
                LastLap = now - lapStartTime;
                if (BestLap <= 0f || LastLap < BestLap) BestLap = LastLap;
                lapStartTime = now;
                LapCompleted?.Invoke(this);
            }

            UpdateWrongWay(dt);
            UpdateOffTrack(dt);
            UpdateStuck(dt);
        }

        void UpdateStuck(float dt)
        {
            bool racing = !RaceManager.Instance || RaceManager.Instance.CurrentState != RaceManager.State.Countdown;
            if (!racing || Mathf.Abs(Kart.Throttle) < 0.3f || OffTrack)
            {
                StuckTime = 0f;
                progressWindow = 0f;
                progressWindowStart = RaceDistance;
                return;
            }

            progressWindow += dt;
            if (progressWindow < 1f) return;
            bool noProgress = RaceDistance - progressWindowStart < minProgressPerSecond * progressWindow;
            StuckTime = noProgress ? StuckTime + progressWindow : 0f;
            progressWindow = 0f;
            progressWindowStart = RaceDistance;

            if (StuckTime >= StuckReturnDelay) ReturnToTrack();
        }

        void UpdateWrongWay(float dt)
        {
            Vector3 dir = path.Direction(Segment);
            bool goingBackwards = Vector3.Dot(Kart.GetComponent<Rigidbody>().linearVelocity, dir) < -3f
                                  || (Vector3.Dot(transform.forward, dir) < -0.5f && Kart.Speed > 3f);
            wrongWayTimer = goingBackwards ? wrongWayTimer + dt : 0f;
            WrongWay = wrongWayTimer > 1f;
        }

        void UpdateOffTrack(float dt)
        {
            path.DistanceToSegment(transform.position, Segment, out float t);
            Vector3 onPath = Vector3.Lerp(path.Point(Segment), path.Point(Segment + 1), t);
            Vector3 flat = transform.position - onPath;
            float height = flat.y;
            flat.y = 0f;

            // Fell off, dropped into a jump's gap (no road under the racing line here), or slipped under the road.
            fallen = height < -fallenDepth || (height < -1.5f && !HasRoadBelow(onPath)) || (height < -1.2f && UnderRoad());
            bool far = flat.magnitude > path.HalfWidthAt(PathDistance) + offTrackMargin;
            OffTrack = (fallen || far) && !Finished;
            OffTrackTime = OffTrack ? OffTrackTime + dt : 0f;

            if (OffTrack && OffTrackTime >= (fallen ? fallenReturnDelay : autoReturnDelay))
                ReturnToTrack();
        }

        bool UnderRoad()
        {
            // From above: raycasts don't see the underside of a mesh collider.
            float y = transform.position.y;
            foreach (var h in Physics.RaycastAll(transform.position + Vector3.up * 8f, Vector3.down, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (h.collider.name == "RoadCollider" && h.point.y > y + 0.8f) return true;
            return false;
        }

        static bool HasRoadBelow(Vector3 point)
        {
            foreach (var h in Physics.RaycastAll(point + Vector3.up * 3f, Vector3.down, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (h.normal.y >= 0.5f && Mathf.Abs(h.point.y - point.y) < 1.5f) return true;
            return false;
        }

        bool IsSpotFree(Vector3 spot)
        {
            var manager = RaceManager.Instance;
            if (!manager) return true;
            foreach (var other in manager.Racers)
                if (other != this && (other.transform.position - spot).sqrMagnitude < 2.5f * 2.5f) return false;
            return true;
        }

        /// <summary>Puts the kart back on the racing line at its current checkpoint, facing forward.</summary>
        public void ReturnToTrack()
        {
            if (!hasAuthority) return;
            // Back to the nearest point with road under it (not over a jump's gap). A kart that
            // fell into a gap just behind it goes back before the ramp, to take the jump again.
            int seg = Segment;
            bool crossedGap = false;
            if (fallen)
                for (int back = 0; back < 3; back++)
                {
                    if (!HasRoadBelow(path.Point(seg))) { crossedGap = true; break; }
                    seg = path.Wrap(seg - 1);
                }
            if (!crossedGap) seg = Segment;
            for (int back = 0; back < 8 && !HasRoadBelow(path.Point(seg)); back++) seg = path.Wrap(seg - 1);
            if (crossedGap) seg = path.Wrap(seg - 6); // a run-up for the ramp
            Vector3 point = path.Point(seg);
            Vector3 dir = path.Direction(seg);

            // Don't drop two karts on the same spot: shift sideways if someone is already there.
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            foreach (float offset in new[] { 0f, -2.5f, 2.5f })
            {
                if (IsSpotFree(point + side * offset)) { point += side * offset; break; }
            }

            Vector3 position = point;
            var hits = Physics.RaycastAll(point + Vector3.up * 3f, Vector3.down, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float bestDy = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.normal.y < 0.5f) continue;
                float dy = Mathf.Abs(h.point.y - point.y);
                if (dy < bestDy) { bestDy = dy; position = h.point; }
            }

            ReturnLog.Add($"{seg}:{(OffTrack ? (fallen ? "fell" : "far") : StuckTime > 0f ? "stuck" : "manual")}@{Kart.transform.position.x:0},{Kart.transform.position.y:0},{Kart.transform.position.z:0}");
            Kart.Teleport(position + Vector3.up * 0.6f, Quaternion.LookRotation(Vector3.ProjectOnPlane(dir, Vector3.up), Vector3.up));
            TrackReturns++;
            OffTrackTime = 0f;
            StuckTime = 0f;
            progressWindow = 0f;
            OffTrack = false;
            wrongWayTimer = 0f;
            WrongWay = false;

            // Keep the raced distance continuous (the checkpoint is at or slightly behind the kart).
            Segment = seg;
            float newDistance = path.DistanceAt(seg);
            float delta = newDistance - lastPathDistance;
            if (delta > path.Length * 0.5f) delta -= path.Length;
            else if (delta < -path.Length * 0.5f) delta += path.Length;
            RaceDistance += delta;
            lastPathDistance = newDistance;
            PathDistance = newDistance;
            progressWindowStart = RaceDistance;
        }
    }
}
