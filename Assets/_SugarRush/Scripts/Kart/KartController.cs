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
        [Tooltip("Fraction of the turn rate left at top speed (1 = no reduction).")]
        [Range(0.3f, 1f)] public float highSpeedSteering = 0.6f;

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
        [Tooltip("Fraction of the normal turn rate available while airborne.")]
        [Range(0f, 1f)] public float airControl = 0.45f;

        [Header("Respawn")]
        public float killY = -50f;

        // Driver inputs
        [HideInInspector] public float Throttle;
        [HideInInspector] public float Steer;
        [HideInInspector] public bool DriftHeld;

        /// <summary>When set (e.g. by RaceProgress), Respawn() delegates to it instead of the safe-point history.</summary>
        public System.Action RespawnOverride;

        public float ForwardSpeed { get; private set; }
        public float Speed => IsProxy ? proxySpeed : (rb ? rb.linearVelocity.magnitude : 0f);
        public bool IsGrounded { get; private set; }
        public bool IsDrifting { get; private set; }
        public int DriftDirection { get; private set; }
        public int DriftLevel { get; private set; }
        public bool IsBoosting => IsProxy ? proxyBoosting : boostTimer > 0f;
        public Rigidbody Body => rb;

        /// <summary>Online: this kart is someone else's, shown from network snapshots (no physics here).</summary>
        public bool IsProxy { get; private set; }
        float proxySpeed;
        bool proxyBoosting;
        public float MaxSpeed => maxSpeed;

        Rigidbody rb;
        float springStrength;
        float springDamper;
        Vector3 groundNormal = Vector3.up;
        float driftTime;
        float boostTimer;
        float roughTimer;
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
            if (IsProxy) return;
            float dt = Time.fixedDeltaTime;
            ApplySuspension();

            Vector3 up = IsGrounded ? groundNormal : Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            Vector3 right = Vector3.Cross(up, forward);
            Vector3 velocity = rb.linearVelocity;
            ForwardSpeed = Vector3.Dot(velocity, forward);
            float sideSpeed = Vector3.Dot(velocity, right);

            UpdateDrift(dt);
            UpdateTricks(dt);
            if (boostTimer > 0f) boostTimer -= dt;
            if (roughTimer > 0f) roughTimer -= dt;

            if (IsGrounded)
            {
                // Engine / brake
                float topSpeed = maxSpeed + (IsBoosting ? boostSpeed : 0f);
                bool rough = roughTimer > 0f;
                if (rough) topSpeed = Mathf.Min(topSpeed, maxSpeed * RoughSpeedFactor);
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
                    rb.AddForce(-forward * (ForwardSpeed - topSpeed) * (rough ? 3.5f : 2f), ForceMode.Acceleration);
                // Rubble underneath: the kart shakes and wanders a little.
                if (rough) rb.AddTorque(up * Random.Range(-1f, 1f) * 1.5f, ForceMode.Acceleration);

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
                // Full turn rate at low speed, gentler at top speed so straights stay stable.
                float speed01 = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / Mathf.Max(1f, maxSpeed));
                float speedFactor = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / 6f) * Mathf.Lerp(1f, highSpeedSteering, speed01);
                float direction = ForwardSpeed >= -0.1f ? 1f : -1f;
                float targetYaw = steer * rate * Mathf.Deg2Rad * speedFactor * direction;

                Vector3 av = rb.angularVelocity;
                float currentYaw = Vector3.Dot(av, up);
                av += up * (targetYaw - currentYaw) * Mathf.Clamp01(dt * 12f);
                rb.angularVelocity = av;
            }
            else
            {
                // A little steering in the air so jumps can be lined up for the landing.
                Vector3 av = rb.angularVelocity;
                float targetYaw = Steer * turnRate * airControl * Mathf.Deg2Rad;
                av += Vector3.up * (targetYaw - Vector3.Dot(av, Vector3.up)) * Mathf.Clamp01(dt * 6f);
                rb.angularVelocity = av;

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

        /// <summary>
        /// Online: switch between simulating this kart and showing it as a kinematic proxy whose
        /// collider is a trigger (contacts with proxies become simple pushes, see NetKart).
        /// </summary>
        public void SetProxy(bool proxy)
        {
            if (!rb) rb = GetComponent<Rigidbody>();
            IsProxy = proxy;
            rb.collisionDetectionMode = proxy ? CollisionDetectionMode.Discrete : CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = proxy;
            rb.interpolation = proxy ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            foreach (var c in GetComponents<Collider>()) c.isTrigger = proxy;
            if (proxy) { Throttle = 0f; Steer = 0f; DriftHeld = false; IsDrifting = false; boostTimer = 0f; }
        }

        /// <summary>Online proxies: values for visuals and sound, taken from the latest snapshot.</summary>
        public void ApplyProxyState(float forwardSpeed, float speed, float steer, bool grounded, bool drifting, int driftDirection, bool boosting)
        {
            ForwardSpeed = forwardSpeed;
            proxySpeed = speed;
            Steer = steer;
            IsGrounded = grounded;
            IsDrifting = drifting;
            DriftDirection = driftDirection;
            proxyBoosting = boosting;
        }

        public void Boost(float duration) => boostTimer = Mathf.Max(boostTimer, duration);

        // ------------------------------------------------------------ Tricks
        [Header("Tricks")]
        [Tooltip("Turbo for landing a trick (drift pressed in the air).")]
        public float trickBoost = 1.3f;
        public float trickSpinTime = 0.45f;
        /// <summary>A trick was started in this jump; it pays off on landing.</summary>
        public bool TrickPending { get; private set; }
        /// <summary>0..1 through the trick spin (for KartVisuals).</summary>
        public float TrickProgress => TrickPending ? Mathf.Clamp01((Time.time - trickStart) / trickSpinTime) : 0f;
        public event System.Action TrickLanded;
        float airTime, trickStart;
        bool driftWasHeld;

        /// <summary>Pressing drift in the air (after a short hop) starts a spin; landing it gives a turbo.</summary>
        void UpdateTricks(float dt)
        {
            if (!IsGrounded)
            {
                airTime += dt;
                if (!TrickPending && airTime > 0.12f && DriftHeld && !driftWasHeld) { TrickPending = true; trickStart = Time.time; }
            }
            else
            {
                // Only a real jump counts (not a bump, nor being knocked up by a falling gummy).
                if (TrickPending)
                {
                    TrickPending = false;
                    if (airTime > 0.45f && roughTimer <= 0f)
                    {
                        Boost(trickBoost);
                        TrickLanded?.Invoke();
                    }
                }
                airTime = 0f;
            }
            driftWasHeld = DriftHeld;
        }

        /// <summary>Fraction of the top speed left while driving over rubble.</summary>
        public const float RoughSpeedFactor = 0.42f;
        public bool OnRoughGround => roughTimer > 0f;

        /// <summary>Over rubble for the next moment (call every frame while on it).</summary>
        public void SetRough(float duration = 0.15f) => roughTimer = Mathf.Max(roughTimer, duration);

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
            if (RespawnOverride != null) { RespawnOverride(); return; }
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
