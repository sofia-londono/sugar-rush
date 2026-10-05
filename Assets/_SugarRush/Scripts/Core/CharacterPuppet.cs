using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Racer characters in the menu and on the podium, animated by code on their own skeletons
    /// (the downloaded models have bones but no animations): gentle breathing and head sway,
    /// a hop and a wave when picked, cheering with both arms up, or clapping. Works with the
    /// Blender-style ("Left_Arm", "Left_Elbow") and 3ds Max-style ("Bip001_L_UpperArm") rigs.
    /// Arms are aimed in world space, so it doesn't matter how each rig's bones are oriented.
    /// </summary>
    public class CharacterPuppet : MonoBehaviour
    {
        public enum Mood { Idle, Cheer, Clap }

        public Mood mood;
        [Tooltip("Model under this object (scaled / grounded by the importer).")]
        public Transform model;

        class Arm
        {
            public Transform Upper, Lower, Hand;
            public Quaternion UpperBase, LowerBase;
        }

        Arm left, right;
        Transform head, spine;
        Quaternion headBase, spineBase;
        Vector3 modelPosition, modelScale;
        float hopAt = -10f, waveUntil = -10f, phase;
        bool ready;

        void Awake() => EnsureReady();

        void EnsureReady()
        {
            if (ready) return;
            ready = true;
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            modelPosition = model ? model.localPosition : Vector3.zero;
            modelScale = model ? model.localScale : Vector3.one;
            phase = Random.Range(0f, 10f);

            left = FindArm("left_arm", "l_upperarm", "left_elbow", "l_forearm", "left_hand", "l_hand");
            right = FindArm("right_arm", "r_upperarm", "right_elbow", "r_forearm", "right_hand", "r_hand");
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!head && (n.StartsWith("head") || n.EndsWith("_head"))) head = t;
                if (!spine && (n.StartsWith("upper_spine") || n == "bip001_spine1")) spine = t;
            }
            if (head) headBase = head.localRotation;
            if (spine) spineBase = spine.localRotation;
        }

        Arm FindArm(string upperA, string upperB, string lowerA, string lowerB, string handA, string handB)
        {
            var arm = new Arm();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!arm.Upper && (n.StartsWith(upperA) || n.Contains(upperB))) arm.Upper = t;
                else if (!arm.Lower && (n.StartsWith(lowerA) || n.Contains(lowerB))) arm.Lower = t;
                else if (!arm.Hand && (n.StartsWith(handA) || n.Contains(handB))) arm.Hand = t;
            }
            if (arm.Upper) arm.UpperBase = arm.Upper.localRotation;
            if (arm.Lower) arm.LowerBase = arm.Lower.localRotation;
            return arm;
        }

        /// <summary>A little jump and a wave, e.g. when this racer is picked.</summary>
        public void Hop()
        {
            EnsureReady();
            hopAt = Time.unscaledTime;
            waveUntil = Time.unscaledTime + 1.8f;
        }

        void LateUpdate()
        {
            float time = Time.unscaledTime + phase;
            Reset(left);
            Reset(right);
            if (head) head.localRotation = headBase;
            if (spine) spine.localRotation = spineBase;

            // Body: breathing, plus hops (looping while cheering).
            float hopT = Time.unscaledTime - hopAt;
            float hop = hopT < 0.4f ? Mathf.Sin(hopT / 0.4f * Mathf.PI) : 0f;
            if (mood == Mood.Cheer) hop = Mathf.Max(hop, Mathf.Abs(Mathf.Sin(time * 4.5f)) * 0.6f);
            float breathe = Mathf.Sin(time * 2.2f) * 0.012f;
            if (model)
            {
                model.localPosition = modelPosition + Vector3.up * (0.22f * hop);
                model.localScale = Vector3.Scale(modelScale, new Vector3(1f - breathe * 0.5f, 1f + breathe, 1f - breathe * 0.5f));
            }
            if (head) head.rotation = Quaternion.AngleAxis(Mathf.Sin(time * 1.3f) * 6f, transform.up) * head.rotation;

            Vector3 up = transform.up, side = transform.right, fwd = transform.forward;
            switch (mood)
            {
                case Mood.Idle:
                    // Relaxed arms by the sides (some rigs rest with their arms out).
                    AimArm(left, (-up - side * 0.3f + fwd * 0.05f).normalized, (-up - side * 0.15f + fwd * 0.25f).normalized);
                    AimArm(right, (-up + side * 0.3f + fwd * 0.05f).normalized, (-up + side * 0.15f + fwd * 0.25f).normalized);
                    break;
                case Mood.Cheer:
                {
                    // Arms up in a V (straight up hides them behind the big chibi heads).
                    float pump = Mathf.Sin(time * 9f) * 0.12f;
                    AimArm(left, (up * 0.75f - side * (0.65f + pump)).normalized, (up * 0.9f - side * 0.35f).normalized);
                    AimArm(right, (up * 0.75f + side * (0.65f + pump)).normalized, (up * 0.9f + side * 0.35f).normalized);
                    break;
                }
                case Mood.Clap:
                {
                    float clap = (Mathf.Sin(time * 11f) + 1f) * 0.5f; // 0 = apart, 1 = together
                    Vector3 l = (fwd * 0.8f - up * 0.25f - side * 0.35f).normalized;
                    Vector3 r = (fwd * 0.8f - up * 0.25f + side * 0.35f).normalized;
                    AimArm(left, l, (fwd + side * Mathf.Lerp(0.1f, 0.7f, clap)).normalized);
                    AimArm(right, r, (fwd - side * Mathf.Lerp(0.1f, 0.7f, clap)).normalized);
                    break;
                }
            }

            // Wave with the right hand (also after being picked while idle).
            if (Time.unscaledTime < waveUntil && mood == Mood.Idle)
            {
                float wave = Mathf.Sin(time * 12f) * 0.45f;
                AimArm(right, (up * 0.3f + side * 0.95f).normalized, (up + side * wave).normalized);
            }
        }

        void Reset(Arm arm)
        {
            if (arm.Upper) arm.Upper.localRotation = arm.UpperBase;
            if (arm.Lower) arm.Lower.localRotation = arm.LowerBase;
        }

        static void AimArm(Arm arm, Vector3 upperDir, Vector3 lowerDir)
        {
            Aim(arm.Upper, arm.Lower, upperDir);
            Aim(arm.Lower, arm.Hand, lowerDir);
        }

        static void Aim(Transform bone, Transform child, Vector3 worldDir)
        {
            if (!bone || !child) return;
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(current, worldDir) * bone.rotation;
        }

        /// <summary>
        /// The character for a roster entry: its 3D model with a puppet, or (no model yet) a
        /// cut-out standee showing the 2D portrait. Null if neither exists.
        /// </summary>
        public static GameObject Create(KartRoster.Entry entry, Transform parent, Material standeeMaterial)
        {
            if (entry.character)
            {
                var go = Instantiate(entry.character, parent);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.AddComponent<CharacterPuppet>();
                return go;
            }
            if (!entry.portrait || !standeeMaterial) return null;
            var holder = new GameObject(entry.id + "_Standee");
            holder.transform.SetParent(parent, false);
            var standee = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(standee.GetComponent<Collider>());
            standee.name = "Portrait";
            standee.transform.SetParent(holder.transform, false);
            standee.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            standee.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // quads face -Z
            standee.transform.localScale = Vector3.one * 1.35f;
            var mat = new Material(standeeMaterial) { mainTexture = entry.portrait };
            mat.SetTexture("_BaseMap", entry.portrait);
            standee.GetComponent<Renderer>().sharedMaterial = mat;
            standee.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // No bones, but it still breathes, hops and bounces when cheering.
            holder.AddComponent<CharacterPuppet>().model = standee.transform;
            return holder;
        }
    }
}
