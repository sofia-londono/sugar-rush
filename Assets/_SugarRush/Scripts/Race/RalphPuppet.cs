using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Ralph's smash, animated by code on the model's own skeleton (it comes in a T-pose with no
    /// animations): he leaps in from off the track with his fists up, lands fists-first, pounds
    /// the road twice more and leaps away. Arms are aimed by turning the shoulder bones so the
    /// upper arm points where we want; the body squashes on every hit.
    /// </summary>
    public class RalphPuppet : MonoBehaviour
    {
        [Tooltip("Model under this object (scaled / grounded by the importer).")]
        public Transform model;
        public float jumpHeight = 22f;
        public float flightTime = 1.1f;

        /// <summary>Hit times relative to landing: the first one breaks the road.</summary>
        public static readonly float[] Hits = { 0f, 0.65f, 1.3f };
        public const float LeaveAt = 1.9f;
        public const float EndAt = LeaveAt + 1.1f;

        Transform leftShoulder, leftElbow, rightShoulder, rightElbow, chest;
        Quaternion leftBase, rightBase, chestBase;
        Vector3 modelScale;
        Renderer[] renderers;

        Vector3 from, land, to;
        Quaternion facing;
        float clock = float.MaxValue; // seconds since landing (negative while flying in)

        public bool Playing => clock < EndAt;
        public float Clock => clock;

        bool ready;

        /// <summary>Finds the bones once (the object sits inactive in the scene until the first show).</summary>
        void EnsureReady()
        {
            if (ready) return;
            ready = true;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("LeftShoulder")) leftShoulder = t;
                else if (t.name.StartsWith("LeftElbow")) leftElbow = t;
                else if (t.name.StartsWith("RightShoulder")) rightShoulder = t;
                else if (t.name.StartsWith("RightElbow")) rightElbow = t;
                else if (t.name.StartsWith("Chest")) chest = t;
            }
            if (leftShoulder) leftBase = leftShoulder.localRotation;
            if (rightShoulder) rightBase = rightShoulder.localRotation;
            if (chest) chestBase = chest.localRotation;
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            modelScale = model ? model.localScale : Vector3.one;
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>
        /// Starts the show: Ralph lands at <paramref name="spot"/> in <paramref name="secondsToLanding"/>,
        /// facing <paramref name="face"/>, coming from / leaving towards <paramref name="side"/>.
        /// </summary>
        public void Play(Vector3 spot, Vector3 face, Vector3 side, float secondsToLanding)
        {
            EnsureReady();
            land = spot;
            facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(face, Vector3.up).normalized, Vector3.up);
            Vector3 sideFlat = Vector3.ProjectOnPlane(side, Vector3.up).normalized;
            from = spot + sideFlat * 30f + Vector3.up * 8f;
            to = spot - sideFlat * 30f + Vector3.up * 8f;
            clock = -secondsToLanding; // positive when this machine heard about it late
            gameObject.SetActive(true);
            Pose(0f);
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
        }

        void Pose(float t)
        {
            // Position: arc in, stand, arc out.
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

            // Fists: up while airborne and between hits, slammed down on each hit.
            float down = 0f, squash = 0f;
            if (t >= 0f && t < LeaveAt)
                foreach (float hit in Hits)
                {
                    float d = t - hit;
                    // Wind up 0.25 s before, slam in 0.08 s, hold briefly, lift.
                    if (d > -0.25f && d < 0f) down = Mathf.Max(down, -0.25f * Mathf.InverseLerp(-0.25f, 0f, d));
                    if (d >= 0f && d < 0.4f) down = Mathf.Max(down, d < 0.08f ? d / 0.08f : 1f - Mathf.InverseLerp(0.18f, 0.4f, d));
                    if (d >= 0f && d < 0.3f) squash = Mathf.Max(squash, Mathf.Sin(d / 0.3f * Mathf.PI) * (hit == 0f ? 1f : 0.6f));
                }
            if (model)
                model.localScale = Vector3.Scale(modelScale, new Vector3(1f + 0.12f * squash, 1f - 0.16f * squash, 1f + 0.12f * squash));

            if (chest) chest.localRotation = chestBase;
            if (leftShoulder) leftShoulder.localRotation = leftBase;
            if (rightShoulder) rightShoulder.localRotation = rightBase;
            if (chest)
            {
                // Lean into the punch.
                Vector3 axis = transform.right;
                chest.rotation = Quaternion.AngleAxis(25f * Mathf.Max(0f, down), axis) * chest.rotation;
            }

            Vector3 up = (Vector3.up * 0.9f + transform.forward * 0.15f).normalized;
            Vector3 slam = (transform.forward * 0.8f - Vector3.up * 0.6f).normalized;
            float k01 = Mathf.Clamp01(down);
            Vector3 dir = Vector3.Slerp(up, slam, k01);
            float spread = Mathf.Lerp(0.35f, 0.18f, k01);
            Aim(leftShoulder, leftElbow, (dir - transform.right * spread).normalized);
            Aim(rightShoulder, rightElbow, (dir + transform.right * spread).normalized);
        }

        static void Aim(Transform shoulder, Transform elbow, Vector3 worldDir)
        {
            if (!shoulder || !elbow) return;
            Vector3 current = elbow.position - shoulder.position;
            if (current.sqrMagnitude < 1e-6f) return;
            shoulder.rotation = Quaternion.FromToRotation(current, worldDir) * shoulder.rotation;
        }
    }
}
