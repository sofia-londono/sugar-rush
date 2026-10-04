using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Computer driver: follows the TrackPath with a look-ahead target, plans its speed for corners
    /// and jumps, avoids obstacles, and unsticks itself. Its style comes from an AIPersonality
    /// (pace, line, aggression, mistakes) scaled by the chosen AIDifficulty, with gentle rubber
    /// banding towards the player.
    /// </summary>
    [RequireComponent(typeof(KartController), typeof(RaceProgress))]
    public class AIKartDriver : MonoBehaviour
    {
        public AIPersonality personality;
        public AIDifficulty.Level difficulty;
        /// <summary>Base lane, picked once at spawn so racers spread over the road.</summary>
        public float laneOffset;

        [Header("Driving")]
        public float lookAheadBase = 7f;
        public float lookAheadPerSpeed = 0.45f;
        public float cornerLookAhead = 30f;
        public float minCornerSpeed = 9f;
        public float brakingDeceleration = 12f;
        public float whiskerLength = 4f;
        [Tooltip("Speed (m/s) to take a big drop at, so the kart can still turn when it lands.")]
        public float jumpSpeed = 12f;
        [Tooltip("Height loss (m) within 8 m of road that counts as a jump.")]
        public float jumpDrop = 4f;

        KartController kart;
        RaceProgress progress;
        float baseMaxSpeed;
        float reverseTimer;
        bool reversedThisStall;
        float rubberBand = 1f;
        float noiseSeed;
        float mistakeTimer;
        int mistakeKind;
        float mistakeSign;
        float planTimer, plannedSpeed;
        const float PlanInterval = 0.1f;

        AIPersonality P => personality ? personality : AIPersonality.Neutral;

        void Start()
        {
            kart = GetComponent<KartController>();
            progress = GetComponent<RaceProgress>();
            baseMaxSpeed = kart.maxSpeed;
            // The softer high-speed steering is a feel setting for human players; the AI plans its
            // corners assuming full turning ability.
            kart.highSpeedSteering = 1f;
            difficulty ??= new AIDifficulty.Level();
            noiseSeed = Random.Range(0f, 100f);
        }

        void OnDestroy()
        {
            if (kart) kart.maxSpeed = baseMaxSpeed;
        }

        void FixedUpdate()
        {
            var manager = RaceManager.Instance;
            var path = progress.path;
            bool canDrive = manager == null || manager.CurrentState != RaceManager.State.Countdown;
            if (!canDrive || !path)
            {
                kart.Throttle = 0f; kart.Steer = 0f; kart.DriftHeld = false;
                return;
            }

            float dt = Time.fixedDeltaTime;
            float s = progress.PathDistance;
            var p = P;
            UpdateRubberBand(manager, dt);
            UpdateMistakes(p, dt);

            // Pace: personality x difficulty x rubber band, with a little wander for inconsistent drivers.
            float wander = (Mathf.PerlinNoise(noiseSeed, Time.time * 0.15f) - 0.5f) * 0.06f * (1f - p.consistency);
            kart.maxSpeed = baseMaxSpeed * p.straightSpeed * difficulty.speedScale * rubberBand * (1f + wander);

            // Steering towards a point ahead on the racing line, in this driver's lane.
            float lookAhead = lookAheadBase + kart.Speed * lookAheadPerSpeed;
            Vector3 dir = path.DirectionAtDistance(s + lookAhead);
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            float bend = Vector3.Angle(Vector3.ProjectOnPlane(path.DirectionAtDistance(s), Vector3.up), Vector3.ProjectOnPlane(dir, Vector3.up));
            float lane = ChooseLane(manager, path, s, p) * Mathf.Clamp01(1f - bend / 45f);
            Vector3 target = path.PositionAtDistance(s + lookAhead) + right * lane;
            Vector3 local = transform.InverseTransformPoint(target);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(angle / 20f + AvoidObstacles(), -1f, 1f);

            // Corner/jump planning samples ~15 points ahead; 10 times a second is plenty.
            planTimer -= dt;
            if (planTimer <= 0f)
            {
                planTimer = PlanInterval;
                plannedSpeed = PlannedSpeed(path, s, p);
            }
            float targetSpeed = plannedSpeed;
            if (mistakeTimer > 0f && mistakeKind == 1) targetSpeed = kart.maxSpeed; // missed the braking point
            float excess = kart.ForwardSpeed - targetSpeed;
            float throttle = excess < 0f ? 1f : (excess > 2f ? -Mathf.Clamp01(excess / 6f) : 0.1f);
            // Pointing well away from the line: ease off so the kart can turn in.
            if (Mathf.Abs(angle) > 50f && kart.Speed > minCornerSpeed) throttle = Mathf.Min(throttle, 0f);

            if (mistakeTimer > 0f)
            {
                if (mistakeKind == 0) steer = Mathf.Clamp(steer + mistakeSign * 0.55f, -1f, 1f); // wobble
                else if (mistakeKind == 2) throttle *= 0.2f; // hesitates
            }

            // Unstick: back up once when progress stalls; RaceProgress returns the kart to the
            // track if it is still stuck a few seconds later.
            if (reverseTimer > 0f)
            {
                reverseTimer -= dt;
                throttle = -1f;
                steer = -steer;
            }
            else if (progress.StuckTime > 1.5f && !reversedThisStall)
            {
                reverseTimer = 1f;
                reversedThisStall = true;
            }
            if (progress.StuckTime <= 0f) reversedThisStall = false;

            if (progress.WrongWay && reverseTimer <= 0f) steer = Mathf.Sign(angle == 0f ? 1f : angle);

            kart.Throttle = throttle;
            kart.Steer = steer;
            kart.DriftHeld = false;
        }

        /// <summary>Gentle rubber band: a little faster when far behind the player, a little slower when far ahead.</summary>
        void UpdateRubberBand(RaceManager manager, float dt)
        {
            float target = 1f;
            if (manager && manager.Player && manager.Player != progress && !manager.Player.Finished)
            {
                float gap = manager.Player.RaceDistance - progress.RaceDistance; // > 0: this racer is behind
                float range = Mathf.Max(1f, difficulty.rubberBandDistance);
                target = 1f + difficulty.catchUpBoost * Mathf.Clamp01(gap / range)
                            - difficulty.leadSlowdown * Mathf.Clamp01(-gap / range);
            }
            rubberBand = Mathf.MoveTowards(rubberBand, target, dt * 0.05f);
        }

        void UpdateMistakes(AIPersonality p, float dt)
        {
            if (mistakeTimer > 0f) { mistakeTimer -= dt; return; }
            float chance = p.mistakesPerMinute * difficulty.mistakeScale / 60f * dt;
            if (Random.value >= chance) return;
            mistakeTimer = Random.Range(p.mistakeDuration.x, p.mistakeDuration.y);
            mistakeKind = Random.Range(0, 3);
            mistakeSign = Random.value < 0.5f ? -1f : 1f;
        }

        /// <summary>
        /// Lateral offset to drive at. Aggressive drivers steer into a kart alongside or just
        /// ahead (to bump it) and move across to block a kart close behind.
        /// </summary>
        float ChooseLane(RaceManager manager, TrackPath path, float s, AIPersonality p)
        {
            float lane = Mathf.Clamp(laneOffset, -p.laneWidth, p.laneWidth);
            lane += (Mathf.PerlinNoise(noiseSeed + 7f, Time.time * 0.2f) - 0.5f) * 2f * p.laneWidth * (1f - p.consistency);

            float aggression = p.aggression * difficulty.aggressionScale;
            if (aggression > 0.01f && manager)
            {
                Vector3 centre = path.PositionAtDistance(s);
                Vector3 right = Vector3.Cross(Vector3.up, path.DirectionAtDistance(s)).normalized;
                float bestAttack = p.attackRange, bestBlock = p.blockRange;
                float? attackLane = null, blockLane = null;
                foreach (var other in manager.Racers)
                {
                    if (other == progress) continue;
                    Vector3 to = other.transform.position - transform.position;
                    float ahead = Vector3.Dot(to, transform.forward);
                    float side = Mathf.Abs(Vector3.Dot(to, transform.right));
                    if (side > 5f) continue;
                    float otherLane = Vector3.Dot(other.transform.position - centre, right);
                    if (ahead > -2f && ahead < bestAttack) { bestAttack = ahead; attackLane = otherLane; }
                    else if (ahead < 0f && -ahead < bestBlock && other.Kart.Speed > kart.Speed * 0.9f) { bestBlock = -ahead; blockLane = otherLane; }
                }
                if (attackLane.HasValue) lane = Mathf.Lerp(lane, attackLane.Value, aggression);
                else if (blockLane.HasValue) lane = Mathf.Lerp(lane, blockLane.Value, aggression * 0.7f);
            }

            float limit = Mathf.Max(0f, path.roadHalfWidth - 1.5f);
            return Mathf.Clamp(lane, -limit, limit);
        }

        static readonly float[] WhiskerAngles = { -30f, -12f, 0f, 12f, 30f };

        /// <summary>
        /// Steering correction from short "whisker" rays at bumper height: anything steep
        /// (arch legs, decorations) pushes the kart towards the more open side.
        /// </summary>
        float AvoidObstacles()
        {
            float range = whiskerLength + kart.Speed * 0.25f;
            Vector3 origin = transform.position + transform.up * 0.5f;
            float push = 0f;
            foreach (float a in WhiskerAngles)
            {
                Vector3 ray = Quaternion.AngleAxis(a, transform.up) * transform.forward;
                if (!Physics.Raycast(origin, ray, out RaycastHit hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y > 0.6f) continue; // ramps and road are fine
                float weight = 1f - hit.distance / range;
                // Side rays steer away from their own side; the centre ray follows the surface normal.
                float side = a < 0f ? 1f : a > 0f ? -1f : Mathf.Sign(Vector3.Dot(hit.normal, transform.right) + 0.001f);
                push += side * weight * (a == 0f ? 1.5f : 1f);
            }
            return Mathf.Clamp(push, -1.2f, 1.2f);
        }

        /// <summary>
        /// Fastest speed that still makes every corner and jump in the next stretch, braking in
        /// time: each spot's allowed speed is raised by how much braking distance is left before it.
        /// </summary>
        float PlannedSpeed(TrackPath path, float s, AIPersonality p)
        {
            Vector3 dirNow = Vector3.ProjectOnPlane(path.DirectionAtDistance(s), Vector3.up);
            float cornerFactor = p.cornerSpeed * difficulty.cornerScale;
            float decel = brakingDeceleration * p.lateBraking;
            float limit = kart.maxSpeed;
            // Start at the kart itself: a jump right under it must still count.
            for (float ahead = 0f; ahead <= cornerLookAhead + 12f; ahead += 3f)
            {
                // How sharp the corner at this distance is: heading change over the next 12 m.
                Vector3 a = Vector3.ProjectOnPlane(path.DirectionAtDistance(s + ahead), Vector3.up);
                Vector3 b = Vector3.ProjectOnPlane(path.DirectionAtDistance(s + ahead + 12f), Vector3.up);
                float sharpness = Mathf.Max(Vector3.Angle(a, b), Vector3.Angle(dirNow, a) * 0.5f);
                float spotSpeed = Mathf.Lerp(kart.maxSpeed, minCornerSpeed, Mathf.InverseLerp(15f, 80f, sharpness)) * cornerFactor;

                float reachable = Mathf.Sqrt(spotSpeed * spotSpeed + 2f * decel * ahead);
                limit = Mathf.Min(limit, reachable);

                // Big drop ahead (e.g. leaving the plateau): arrive slowly enough to steer on landing.
                // A safety limit for everyone, so it ignores the personality's late braking.
                float drop = path.PositionAtDistance(s + ahead).y - path.PositionAtDistance(s + ahead + 10f).y;
                if (drop > jumpDrop)
                    limit = Mathf.Min(limit, Mathf.Sqrt(jumpSpeed * jumpSpeed + 2f * brakingDeceleration * ahead));
            }
            return limit;
        }
    }
}
