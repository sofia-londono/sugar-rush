using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Arcade kart physics: raycast suspension, acceleration, lateral grip, drift and boost.
    /// Inputs (Throttle, Steer, DriftHeld) are written by a driver component (player or AI).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KartController : MonoBehaviour
    {
        [Header("Wheels (local space, set by the prefab builder)")]
        public Vector3[] wheelAnchors = new Vector3[4];
        public float wheelRadius = 0.2f;

        [Header("Suspension")]
        public float suspensionTravel = 0.15f;
        public float dampingRatio = 0.35f;

        [Header("Engine")]
        public float maxSpeed = 28f;
        public float maxReverseSpeed = 8f;
        public float acceleration = 16f;
        public float brakeDeceleration = 30f;
        public float rollingDrag = 0.6f;

        [Header("Handling")]
        public float turnRate = 120f;
        public float grip = 12f;

        [Header("Drift & Boost")]
        public float driftGrip = 1.8f;
        public float driftTurnMultiplier = 1.3f;
        public float driftMinSpeed = 8f;
        public float[] driftBoostThresholds = { 0.9f, 1.8f };
        public float[] driftBoostDurations = { 0.8f, 1.5f };
        public float boostSpeed = 8f;
        public float boostAcceleration = 30f;

        [Header("Air")]
        public float extraGravity = 1.5f;
        public float airLevelingTorque = 6f;

        [Header("Respawn")]
        public float killY = -50f;

        // Driver inputs
        [HideInInspector] public float Throttle;
        [HideInInspector] public float Steer;
        [HideInInspector] public bool DriftHeld;

        public float ForwardSpeed { get; private set; }
        public float Speed => rb ? rb.linearVelocity.magnitude : 0f;
        public bool IsGrounded { get; private set; }
        public bool IsDrifting { get; private set; }
        public int DriftDirection { get; private set; }
        public int DriftLevel { get; private set; }
        public bool IsBoosting => boostTimer > 0f;
        public float MaxSpeed => maxSpeed;

        Rigidbody rb;
        float springStrength;
        float springDamper;
        Vector3 groundNormal = Vector3.up;
        float driftTime;
        float boostTimer;
        int groundedWheels;
        const int SafePointHistory = 6;
        readonly System.Collections.Generic.Queue<(Vector3, Quaternion)> safePoints = new();
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        float safeTimer;
        float upsideDownTimer;

        const float RayStartHeight = 0.3f;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.centerOfMass = new Vector3(0f, wheelRadius * 0.5f, 0f);
            rb.maxAngularVelocity = 12f;

            // Sized so the kart rests at half of its suspension travel.
            float g = Physics.gravity.magnitude;
            springStrength = rb.mass * g / (wheelAnchors.Length * suspensionTravel * 0.5f);
            springDamper = dampingRatio * 2f * Mathf.Sqrt(springStrength * rb.mass / wheelAnchors.Length);

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            ApplySuspension();

            Vector3 up = IsGrounded ? groundNormal : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            Vector3 right = Vector3.Cross(up, forward);
            Vector3 velocity = rb.linearVelocity;
            ForwardSpeed = Vector3.Dot(velocity, forward);
            float sideSpeed = Vector3.Dot(velocity, right);

            UpdateDrift(dt);
            if (boostTimer > 0f) boostTimer -= dt;

            if (IsGrounded)
            {
                // Engine / brake
                float topSpeed = maxSpeed + (IsBoosting ? boostSpeed : 0f);
                float accel = 0f;
                if (IsBoosting)
                    accel = ForwardSpeed < topSpeed ? boostAcceleration : 0f;
                else if (Throttle > 0.01f)
                    accel = ForwardSpeed < -0.5f ? brakeDeceleration * Throttle
                          : (ForwardSpeed < topSpeed ? acceleration * Throttle : 0f);
                else if (Throttle < -0.01f)
                    accel = ForwardSpeed > 0.5f ? brakeDeceleration * Throttle
                          : (ForwardSpeed > -maxReverseSpeed ? acceleration * 0.6f * Throttle : 0f);
                else
                    accel = -Mathf.Sign(ForwardSpeed) * Mathf.Min(Mathf.Abs(ForwardSpeed) / dt, rollingDrag);

                rb.AddForce(forward * accel, ForceMode.Acceleration);

                // Over-speed bleed (e.g. after a boost ends)
                if (ForwardSpeed > topSpeed)
                    rb.AddForce(-forward * (ForwardSpeed - topSpeed) * 2f, ForceMode.Acceleration);

                // Lateral grip
                float g = IsDrifting ? driftGrip : grip;
                float gripFactor = 1f - Mathf.Exp(-g * dt);
                rb.linearVelocity -= right * sideSpeed * gripFactor;

                // Steering (yaw around the ground normal)
                float steer = Steer;
                float rate = turnRate;
                if (IsDrifting)
                {
                    // Always turn into the drift; steering widens or tightens it.
                    steer = DriftDirection * Mathf.Lerp(0.35f, 1f, (Steer * DriftDirection + 1f) * 0.5f);
                    rate *= driftTurnMultiplier;
                }
                float speedFactor = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / 6f);
                float direction = ForwardSpeed >= -0.1f ? 1f : -1f;
                float targetYaw = steer * rate * Mathf.Deg2Rad * speedFactor * direction;

                Vector3 av = rb.angularVelocity;
                float currentYaw = Vector3.Dot(av, up);
                av += up * (targetYaw - currentYaw) * Mathf.Clamp01(dt * 12f);
                rb.angularVelocity = av;
            }
            else
            {
                rb.AddForce(Physics.gravity * extraGravity, ForceMode.Acceleration);
                // Keep the kart upright while airborne
                Vector3 axis = Vector3.Cross(transform.up, Vector3.up);
                rb.AddTorque(axis * airLevelingTorque, ForceMode.Acceleration);
            }

            TrackSafePosition(dt);
        }

        void ApplySuspension()
        {
            int hits = 0;
            Vector3 normalSum = Vector3.zero;
            float rayLength = RayStartHeight + wheelRadius + suspensionTravel;

            foreach (var anchor in wheelAnchors)
            {
                Vector3 origin = transform.TransformPoint(anchor) + transform.up * RayStartHeight;
                // Karts live on the "Ignore Raycast" layer so these rays never hit the kart itself.
                if (Physics.Raycast(origin, -transform.up, out RaycastHit hit, rayLength,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    float compression = rayLength - hit.distance;
                    float pointVelocity = Vector3.Dot(rb.GetPointVelocity(origin), transform.up);
                    float force = compression * springStrength - pointVelocity * springDamper;
                    rb.AddForceAtPosition(transform.up * Mathf.Max(0f, force), origin);
                    normalSum += hit.normal;
                    hits++;
                }
            }

            groundedWheels = hits;
            IsGrounded = hits > 0;
            groundNormal = hits > 0 ? normalSum.normalized : Vector3.up;
        }

        void UpdateDrift(float dt)
        {
            bool canDrift = IsGrounded && ForwardSpeed > driftMinSpeed;

            if (!IsDrifting)
            {
                if (DriftHeld && canDrift && Mathf.Abs(Steer) > 0.3f)
                {
                    IsDrifting = true;
                    DriftDirection = Steer > 0f ? 1 : -1;
                    driftTime = 0f;
                    DriftLevel = 0;
                }
                return;
            }

            if (DriftHeld && ForwardSpeed > driftMinSpeed * 0.5f)
            {
                if (IsGrounded) driftTime += dt;
                DriftLevel = 0;
                for (int i = 0; i < driftBoostThresholds.Length; i++)
                    if (driftTime >= driftBoostThresholds[i]) DriftLevel = i + 1;
                return;
            }

            // Drift released: reward with a boost
            if (DriftLevel > 0)
                boostTimer = Mathf.Max(boostTimer, driftBoostDurations[DriftLevel - 1]);
            IsDrifting = false;
            DriftLevel = 0;
            driftTime = 0f;
        }

        public void Boost(float duration) => boostTimer = Mathf.Max(boostTimer, duration);

        void TrackSafePosition(float dt)
        {
            bool upright = Vector3.Dot(transform.up, Vector3.up) > 0.7f;
            if (groundedWheels == wheelAnchors.Length && upright)
            {
                safeTimer += dt;
                if (safeTimer > 0.5f)
                {
                    safeTimer = 0f;
                    if (safePoints.Count == SafePointHistory) safePoints.Dequeue();
                    safePoints.Enqueue((transform.position,
                        Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up), Vector3.up)));
                }
            }

            upsideDownTimer = upright ? 0f : upsideDownTimer + dt;
            if (transform.position.y < killY || upsideDownTimer > 2f)
                Respawn();
        }

        /// <summary>
        /// Returns to the oldest remembered safe spot (a few seconds back), so a kart that
        /// fell off an edge isn't dropped right back at that edge.
        /// </summary>
        public void Respawn()
        {
            var (position, rotation) = safePoints.Count > 0 ? safePoints.Peek() : (spawnPosition, spawnRotation);
            safePoints.Clear();
            safePoints.Enqueue((position, rotation));
            Teleport(position + Vector3.up * 0.5f, rotation);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = position;
            rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            IsDrifting = false;
            boostTimer = 0f;
            upsideDownTimer = 0f;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            foreach (var a in wheelAnchors)
                Gizmos.DrawWireSphere(transform.TransformPoint(a), wheelRadius);
        }
    }
}
