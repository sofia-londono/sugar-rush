using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Computer driver: follows the TrackPath with a look-ahead target, slows for sharp
    /// corners, and unsticks itself (reverse, then back to track) when blocked.
    /// </summary>
    [RequireComponent(typeof(KartController), typeof(RaceProgress))]
    public class AIKartDriver : MonoBehaviour
    {
        [Range(0.5f, 1.1f)] public float skill = 0.95f;
        public float laneOffset;
        public float lookAheadBase = 7f;
        public float lookAheadPerSpeed = 0.45f;
        public float cornerLookAhead = 30f;
        public float minCornerSpeed = 9f;
        public float brakingDeceleration = 12f;
        public float whiskerLength = 4f;

        KartController kart;
        RaceProgress progress;
        float baseMaxSpeed;
        float reverseTimer;
        bool reversedThisStall;

        void Start()
        {
            kart = GetComponent<KartController>();
            progress = GetComponent<RaceProgress>();
            baseMaxSpeed = kart.maxSpeed;
            kart.maxSpeed = baseMaxSpeed * skill;
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

            // Steering towards a point ahead on the racing line, in this driver's lane.
            float lookAhead = lookAheadBase + kart.Speed * lookAheadPerSpeed;
            // Lanes only on gentle stretches; everyone takes the centre through tight corners.
            Vector3 dir = path.DirectionAtDistance(s + lookAhead);
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            float bend = Vector3.Angle(Vector3.ProjectOnPlane(path.DirectionAtDistance(s), Vector3.up), Vector3.ProjectOnPlane(dir, Vector3.up));
            float lane = laneOffset * Mathf.Clamp01(1f - bend / 45f);
            Vector3 target = path.PositionAtDistance(s + lookAhead) + right * lane;
            Vector3 local = transform.InverseTransformPoint(target);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(angle / 20f + AvoidObstacles(), -1f, 1f);

            float targetSpeed = CornerSpeed(path, s);
            float excess = kart.ForwardSpeed - targetSpeed;
            float throttle = excess < 0f ? 1f : (excess > 2f ? -Mathf.Clamp01(excess / 6f) : 0.1f);
            // Pointing well away from the line: ease off so the kart can turn in.
            if (Mathf.Abs(angle) > 50f && kart.Speed > minCornerSpeed) throttle = Mathf.Min(throttle, 0f);

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
        /// Fastest speed that still makes every corner in the next stretch, braking in time:
        /// each corner's allowed speed is raised by how much braking distance is left before it.
        /// </summary>
        float CornerSpeed(TrackPath path, float s)
        {
            Vector3 dirNow = Vector3.ProjectOnPlane(path.DirectionAtDistance(s), Vector3.up);
            float limit = kart.maxSpeed;
            for (float ahead = 6f; ahead <= cornerLookAhead + 12f; ahead += 6f)
            {
                // How sharp the corner at this distance is: heading change over the next 12 m.
                Vector3 a = Vector3.ProjectOnPlane(path.DirectionAtDistance(s + ahead), Vector3.up);
                Vector3 b = Vector3.ProjectOnPlane(path.DirectionAtDistance(s + ahead + 12f), Vector3.up);
                float sharpness = Mathf.Max(Vector3.Angle(a, b), Vector3.Angle(dirNow, a) * 0.5f);
                float cornerSpeed = Mathf.Lerp(kart.maxSpeed, minCornerSpeed, Mathf.InverseLerp(15f, 80f, sharpness));
                float reachable = Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * brakingDeceleration * ahead);
                limit = Mathf.Min(limit, reachable);
            }
            return limit;
        }
    }
}
