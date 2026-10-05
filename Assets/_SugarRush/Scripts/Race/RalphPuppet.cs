using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Ralph's smash, animated by code on the model's own skeleton (it comes in a T-pose with no
    /// animations). He leaps in from off the track and lands at the edge of the stretch he is
    /// about to wreck, facing the road; crouches (knees and back bent) and pounds the road with
    /// alternating fists; then stands up and leaps away. Bones are aimed in world space (upper
    /// limb towards the joint target, lower limb towards the hand / foot target), so the rig's
    /// own bone axes don't matter; after bending the legs the body is lowered until the feet
    /// touch the ground. While he stands there he has a body: karts that touch him bounce off.
    /// </summary>
    public class RalphPuppet : MonoBehaviour
    {
        [Tooltip("Model under this object (scaled / grounded by the importer).")]
        public Transform model;
        public float jumpHeight = 22f;
        public float flightTime = 1.1f;
        [Tooltip("How far in front of his feet the fists land (metres).")]
        public float reach = 2.6f;
        public float bounceSpeed = 9f;

        /// <summary>Fist hits, in seconds after landing (alternating left / right).</summary>
        public static readonly float[] Hits = { 0.55f, 1.0f, 1.45f, 1.9f, 2.35f, 2.8f };
        public const float LeaveAt = 3.4f;
        public const float EndAt = LeaveAt + 1.1f;

        Transform leftShoulder, leftElbow, leftHand, rightShoulder, rightElbow, rightHand;
        Transform leftThigh, leftKnee, leftFoot, rightThigh, rightKnee, rightFoot;
        Transform waist, chest, neck;
        Transform[] bones;
        Quaternion[] bindRotations;
        Vector3 modelScale;
        Renderer[] renderers;
        Collider body;

        Vector3 from, land, to;
        Quaternion facing;
        float clock = float.MaxValue; // seconds since landing (negative while flying in)
        bool ready;

        public bool Playing => clock < EndAt;
        public float Clock => clock;
        /// <summary>Standing on the road (has a body karts bounce off).</summary>
        public bool Standing => clock >= 0f && clock < LeaveAt;

        /// <summary>Finds the bones once (the object sits inactive in the scene until the first show).</summary>
        void EnsureReady()
        {
            if (ready) return;
            ready = true;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("LeftShoulder")) leftShoulder = t;
                else if (n.StartsWith("LeftElbow")) leftElbow = t;
                else if (n.StartsWith("LeftHand")) leftHand = t;
                else if (n.StartsWith("RightShoulder")) rightShoulder = t;
                else if (n.StartsWith("RightElbow")) rightElbow = t;
                else if (n.StartsWith("RightHand")) rightHand = t;
                else if (n.StartsWith("LeftThigh")) leftThigh = t;
                else if (n.StartsWith("LeftKnee")) leftKnee = t;
                else if (n.StartsWith("LeftFoot")) leftFoot = t;
                else if (n.StartsWith("RightThigh")) rightThigh = t;
                else if (n.StartsWith("RightKnee")) rightKnee = t;
                else if (n.StartsWith("RightFoot")) rightFoot = t;
                else if (n.StartsWith("Waist")) waist = t;
                else if (n.StartsWith("Chest")) chest = t;
                else if (n.StartsWith("Neck")) neck = t;
            }
            bones = new[] { leftShoulder, leftElbow, rightShoulder, rightElbow, leftThigh, leftKnee, rightThigh, rightKnee, waist, chest };
            bindRotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) if (bones[i]) bindRotations[i] = bones[i].localRotation;
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            modelScale = model ? model.localScale : Vector3.one;
            renderers = GetComponentsInChildren<Renderer>(true);
            body = GetComponent<Collider>();
        }

        /// <summary>
        /// Starts the show: Ralph lands at <paramref name="spot"/> in <paramref name="secondsToLanding"/>,
        /// facing <paramref name="face"/> (towards the road), coming from / leaving towards <paramref name="side"/>.
        /// </summary>
        public void Play(Vector3 spot, Vector3 face, Vector3 side, float secondsToLanding)
        {
            EnsureReady();
            land = spot;
            facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(face, Vector3.up).normalized, Vector3.up);
            Vector3 sideFlat = Vector3.ProjectOnPlane(side, Vector3.up).normalized;
            Vector3 along = facing * Vector3.right;
            from = spot + sideFlat * 30f + along * 8f + Vector3.up * 8f;
            to = spot + sideFlat * 30f - along * 8f + Vector3.up * 8f;
            clock = -secondsToLanding; // positive when this machine heard about it late
            gameObject.SetActive(true);
            Pose(clock);
        }

        /// <summary>Where fist <paramref name="hit"/> lands: in front of him, alternating sides.</summary>
        public Vector3 FistTarget(int hit)
        {
            Vector3 fwd = facing * Vector3.forward, right = facing * Vector3.right;
            return land + fwd * reach + right * ((hit % 2 == 0 ? -1f : 1f) * 1.1f) + fwd * (0.25f * (hit % 3));
        }

        /// <summary>Only drawn when a camera is near; the timing runs regardless.</summary>
        public void SetVisible(bool visible)
        {
            if (!ready) return;
            foreach (var r in renderers) if (r.enabled != visible) r.enabled = visible;
        }

        void Update()
        {
            if (!Playing) { if (gameObject.activeSelf) gameObject.SetActive(false); return; }
            clock += Time.deltaTime;
            Pose(clock);
            if (body) body.enabled = Standing;
        }

        void Pose(float t)
        {
            // Whole body: arc in, stand, arc out.
            Vector3 position;
            if (t < 0f)
            {
                float k = Mathf.Clamp01(1f + t / flightTime);
                position = Vector3.Lerp(from, land, k) + Vector3.up * (4f * jumpHeight * k * (1f - k));
                if (t < -flightTime) position = from + Vector3.up * 200f; // waiting, far out of sight
            }
            else if (t < LeaveAt) position = land;
            else
            {
                float k = Mathf.Clamp01((t - LeaveAt) / (EndAt - LeaveAt));
                position = Vector3.Lerp(land, to, k) + Vector3.up * (4f * jumpHeight * k * (1f - k));
            }
            transform.SetPositionAndRotation(position, facing);

            for (int i = 0; i < bones.Length; i++) if (bones[i]) bones[i].localRotation = bindRotations[i];

            Vector3 up = Vector3.up, fwd = transform.forward, right = transform.right;

            // Crouch: in over 0.35 s after landing, out before leaving. Landing squash on top.
            float crouch = t < 0f ? 0f : t < LeaveAt ? Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((LeaveAt - t) / 0.3f) : 0f;
            float squash = t >= 0f && t < 0.3f ? Mathf.Sin(t / 0.3f * Mathf.PI) : 0f;
            if (model) model.localScale = Vector3.Scale(modelScale, new Vector3(1f + 0.1f * squash, 1f - 0.14f * squash, 1f + 0.1f * squash));

            // Back bends over the road; each punch adds a little lunge.
            float lunge = 0f;
            int hitIndex = -1;
            float sinceHit = 99f;
            for (int i = 0; i < Hits.Length; i++)
            {
                float d = t - Hits[i];
                if (d > -0.3f && d < 0.3f && Mathf.Abs(d) < Mathf.Abs(sinceHit)) { hitIndex = i; sinceHit = d; }
            }
            if (hitIndex >= 0) lunge = Mathf.Clamp01(1f - Mathf.Abs(sinceHit) / 0.3f);
            float bend = crouch * (0.55f + 0.25f * lunge);
            Aim(waist, chest, Vector3.Slerp(up, fwd, 0.35f * bend));
            Aim(chest, neck, Vector3.Slerp(up, fwd, 0.55f * bend));

            // Legs: thighs forward, shins back (a squat), then drop the body so the feet stay on the ground.
            Vector3 thigh = Vector3.Slerp(-up, fwd, 0.55f * crouch), shin = Vector3.Slerp(-up, -fwd, 0.45f * crouch);
            Aim(leftThigh, leftKnee, (thigh - right * 0.25f).normalized);
            Aim(rightThigh, rightKnee, (thigh + right * 0.25f).normalized);
            Aim(leftKnee, leftFoot, (shin - right * 0.1f).normalized);
            Aim(rightKnee, rightFoot, (shin + right * 0.1f).normalized);
            if (t >= 0f && t < LeaveAt && leftFoot && rightFoot)
            {
                float footY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
                transform.position += Vector3.up * (land.y + 0.12f - footY);
            }

            // Arms: both fists up while flying / standing; while pounding, the hitting arm winds
            // up behind the head and slams onto its target, the other one is held ready.
            Vector3 raised = (up * 0.9f + fwd * 0.1f).normalized;
            Vector3 leftDir = (raised - right * 0.4f).normalized, rightDir = (raised + right * 0.4f).normalized;
            Vector3 leftLower = leftDir, rightLower = rightDir;
            if (t >= 0f && t < LeaveAt)
            {
                for (int i = 0; i < Hits.Length; i++)
                {
                    float d = t - Hits[i];
                    if (d < -0.35f || d > 0.25f) continue;
                    bool left = i % 2 == 0;
                    Transform shoulder = left ? leftShoulder : rightShoulder;
                    if (!shoulder) continue;
                    Vector3 toFist = (FistTarget(i) - shoulder.position).normalized;
                    Vector3 windUp = (up * 0.7f - fwd * 0.4f + (left ? -right : right) * 0.3f).normalized;
                    // -0.35..-0.08 s: wind up; -0.08..0 s: slam; 0..0.25 s: stay on the road, then lift.
                    float k = d < -0.08f ? 0f : d < 0f ? (d + 0.08f) / 0.08f : 1f - Mathf.Clamp01((d - 0.12f) / 0.13f);
                    Vector3 dir = Vector3.Slerp(windUp, toFist, k);
                    if (left) { leftDir = dir; leftLower = Vector3.Slerp(windUp, toFist, Mathf.Clamp01(k * 1.2f)); }
                    else { rightDir = dir; rightLower = Vector3.Slerp(windUp, toFist, Mathf.Clamp01(k * 1.2f)); }
                }
            }
            Aim(leftShoulder, leftElbow, leftDir);
            Aim(leftElbow, leftHand, leftLower);
            Aim(rightShoulder, rightElbow, rightDir);
            Aim(rightElbow, rightHand, rightLower);
        }

        static void Aim(Transform bone, Transform child, Vector3 worldDir)
        {
            if (!bone || !child) return;
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-6f) return;
            bone.rotation = Quaternion.FromToRotation(current, worldDir) * bone.rotation;
        }

        /// <summary>Karts that run into him bounce off (only karts simulated on this machine have physics contacts).</summary>
        void OnCollisionEnter(Collision collision)
        {
            var rb = collision.rigidbody;
            if (!rb || rb.isKinematic || !rb.GetComponent<KartController>()) return;
            Vector3 away = Vector3.ProjectOnPlane(rb.position - transform.position, Vector3.up).normalized;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            rb.linearVelocity = away * bounceSpeed + Vector3.up * 3f;
            var lib = SoundLibrary.Instance;
            if (lib && lib.crashes is { Length: > 0 }) AudioHub.PlayAt(lib.crashes[Random.Range(0, lib.crashes.Length)], rb.position, 1f);
        }
    }
}
