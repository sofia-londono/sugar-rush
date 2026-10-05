using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Racer characters, animated by code on their own skeletons (the downloaded models have
    /// bones but no animations): gentle breathing and head sway, a hop and a wave when picked,
    /// cheering with both arms up, or clapping. Seated mode puts them in their kart: thighs
    /// forward, shins down, hands on the wheel, hips pinned to the kart's seat. Works with the
    /// Blender-style ("Left_Arm", "Left_Leg") and 3ds Max-style ("Bip001_L_UpperArm",
    /// "Bip001_L_Thigh") rigs. Limbs are aimed in world space, so bone axes don't matter.
    /// </summary>
    public class CharacterPuppet : MonoBehaviour
    {
        public enum Mood { Idle, Cheer, Clap }

        public Mood mood;
        [Tooltip("Model under this object (scaled / grounded by the importer).")]
        public Transform model;
        [Tooltip("Sitting in a kart, hips pinned to this point.")]
        public Transform seatAnchor;

        class Limb
        {
            public Transform Upper, Lower, End;
            public Quaternion UpperBase, LowerBase;
        }

        Limb leftArm, rightArm, leftLeg, rightLeg;
        Transform head, spine, pelvis;
        Quaternion headBase, spineBase;
        Vector3 modelPosition, modelScale;
        float hopAt = -10f, waveUntil = -10f, phase;
        bool ready;

        public bool Seated => seatAnchor;

        void Awake() => EnsureReady();

        void EnsureReady()
        {
            if (ready) return;
            ready = true;
            if (!model && transform.childCount > 0) model = transform.GetChild(0);
            modelPosition = model ? model.localPosition : Vector3.zero;
            modelScale = model ? model.localScale : Vector3.one;
            phase = Random.Range(0f, 10f);

            leftArm = FindLimb(new[] { "left_arm", "l_upperarm" }, new[] { "left_elbow", "l_forearm" }, new[] { "left_hand", "l_hand" });
            rightArm = FindLimb(new[] { "right_arm", "r_upperarm" }, new[] { "right_elbow", "r_forearm" }, new[] { "right_hand", "r_hand" });
            leftLeg = FindLimb(new[] { "left_leg", "l_thigh" }, new[] { "left_knee", "l_calf" }, new[] { "left_foot", "l_foot" });
            rightLeg = FindLimb(new[] { "right_leg", "r_thigh" }, new[] { "right_knee", "r_calf" }, new[] { "right_foot", "r_foot" });
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!head && (n.StartsWith("head") || n.EndsWith("_head"))) head = t;
                if (!spine && (n.StartsWith("upper_spine") || n == "bip001_spine1")) spine = t;
                if (!pelvis && (n.StartsWith("pelvis") || n == "bip001_pelvis")) pelvis = t;
            }
            if (head) headBase = head.localRotation;
            if (spine) spineBase = spine.localRotation;
        }

        Limb FindLimb(string[] upper, string[] lower, string[] end)
        {
            static bool Match(string n, string[] keys) => n.StartsWith(keys[0]) || n.Contains(keys[1]);
            var limb = new Limb();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!limb.Upper && Match(n, upper)) limb.Upper = t;
                else if (!limb.Lower && Match(n, lower)) limb.Lower = t;
                else if (!limb.End && Match(n, end)) limb.End = t;
            }
            if (limb.Upper) limb.UpperBase = limb.Upper.localRotation;
            if (limb.Lower) limb.LowerBase = limb.Lower.localRotation;
            return limb;
        }

        /// <summary>A little jump and a wave, e.g. when this racer is picked.</summary>
        public void Hop()
        {
            EnsureReady();
            hopAt = Time.unscaledTime;
            waveUntil = Time.unscaledTime + 1.8f;
        }

        /// <summary>Applies the current pose right away (used before baking a driver into a static mesh).</summary>
        public void PoseNow()
        {
            EnsureReady();
            ApplyPose(0f, animate: false);
        }

        void LateUpdate() => ApplyPose(Time.unscaledTime + phase, animate: true);

        void ApplyPose(float time, bool animate)
        {
            Reset(leftArm);
            Reset(rightArm);
            Reset(leftLeg);
            Reset(rightLeg);
            if (head) head.localRotation = headBase;
            if (spine) spine.localRotation = spineBase;

            // Body: breathing, plus hops (looping while cheering). Seated racers bounce less.
            float hopT = Time.unscaledTime - hopAt;
            float hop = animate && hopT < 0.4f ? Mathf.Sin(hopT / 0.4f * Mathf.PI) : 0f;
            if (animate && mood == Mood.Cheer) hop = Mathf.Max(hop, Mathf.Abs(Mathf.Sin(time * 4.5f)) * 0.6f);
            float breathe = animate ? Mathf.Sin(time * 2.2f) * 0.012f : 0f;
            if (model)
            {
                model.localPosition = modelPosition + Vector3.up * ((Seated ? 0.08f : 0.22f) * hop);
                model.localScale = Vector3.Scale(modelScale, new Vector3(1f - breathe * 0.5f, 1f + breathe, 1f - breathe * 0.5f));
            }
            if (head && animate) head.rotation = Quaternion.AngleAxis(Mathf.Sin(time * 1.3f) * 6f, transform.up) * head.rotation;

            Vector3 up = transform.up, side = transform.right, fwd = transform.forward;

            if (Seated)
            {
                // Sitting: thighs forward along the seat, shins down to the pedals.
                AimLimb(leftLeg, (fwd * 0.95f - up * 0.15f - side * 0.12f).normalized, (-up * 0.75f + fwd * 0.4f).normalized);
                AimLimb(rightLeg, (fwd * 0.95f - up * 0.15f + side * 0.12f).normalized, (-up * 0.75f + fwd * 0.4f).normalized);
                if (pelvis) transform.position += seatAnchor.position - pelvis.position;
            }

            switch (mood)
            {
                case Mood.Idle when Seated:
                    // Hands on the wheel.
                    AimLimb(leftArm, (fwd * 0.6f - up * 0.55f - side * 0.3f).normalized, (fwd - up * 0.1f + side * 0.35f).normalized);
                    AimLimb(rightArm, (fwd * 0.6f - up * 0.55f + side * 0.3f).normalized, (fwd - up * 0.1f - side * 0.35f).normalized);
                    break;
                case Mood.Idle:
                    // Relaxed arms by the sides (some rigs rest with their arms out).
                    AimLimb(leftArm, (-up - side * 0.3f + fwd * 0.05f).normalized, (-up - side * 0.15f + fwd * 0.25f).normalized);
                    AimLimb(rightArm, (-up + side * 0.3f + fwd * 0.05f).normalized, (-up + side * 0.15f + fwd * 0.25f).normalized);
                    break;
                case Mood.Cheer:
                {
                    // Arms up in a V (straight up hides them behind the big chibi heads).
                    float pump = animate ? Mathf.Sin(time * 9f) * 0.12f : 0f;
                    AimLimb(leftArm, (up * 0.75f - side * (0.65f + pump)).normalized, (up * 0.9f - side * 0.35f).normalized);
                    AimLimb(rightArm, (up * 0.75f + side * (0.65f + pump)).normalized, (up * 0.9f + side * 0.35f).normalized);
                    break;
                }
                case Mood.Clap:
                {
                    float clap = animate ? (Mathf.Sin(time * 11f) + 1f) * 0.5f : 0f; // 0 = apart, 1 = together
                    Vector3 l = (fwd * 0.8f - up * 0.25f - side * 0.35f).normalized;
                    Vector3 r = (fwd * 0.8f - up * 0.25f + side * 0.35f).normalized;
                    AimLimb(leftArm, l, (fwd + side * Mathf.Lerp(0.1f, 0.7f, clap)).normalized);
                    AimLimb(rightArm, r, (fwd - side * Mathf.Lerp(0.1f, 0.7f, clap)).normalized);
                    break;
                }
            }

            // Wave with the right hand (also after being picked while idle).
            if (animate && Time.unscaledTime < waveUntil && mood == Mood.Idle)
            {
                float wave = Mathf.Sin(time * 12f) * 0.45f;
                AimLimb(rightArm, (up * 0.3f + side * 0.95f).normalized, (up + side * wave).normalized);
            }
        }

        static void Reset(Limb limb)
        {
            if (limb.Upper) limb.Upper.localRotation = limb.UpperBase;
            if (limb.Lower) limb.Lower.localRotation = limb.LowerBase;
        }

        static void AimLimb(Limb limb, Vector3 upperDir, Vector3 lowerDir)
        {
            Aim(limb.Upper, limb.Lower, upperDir);
            Aim(limb.Lower, limb.End, lowerDir);
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

        /// <summary>The racer sitting in their kart (animated). Null if the racer has no 3D model.</summary>
        public static CharacterPuppet CreateDriver(KartRoster.Entry entry, Transform kart)
        {
            if (!entry.character) return null;
            var seat = new GameObject("Seat").transform;
            seat.SetParent(kart, false);
            seat.localPosition = entry.seat;
            var go = Instantiate(entry.character, kart);
            go.name = entry.id + "_Driver";
            go.transform.localPosition = entry.seat;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * entry.driverScale;
            var puppet = go.AddComponent<CharacterPuppet>();
            puppet.seatAnchor = seat;
            puppet.PoseNow();
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return puppet;
        }

        /// <summary>
        /// The racer sitting in their kart for races: the seated pose is baked once into plain
        /// meshes, so nothing is skinned or animated per frame. Null if the racer has no 3D model.
        /// </summary>
        public static GameObject CreateStaticDriver(KartRoster.Entry entry, Transform kart)
        {
            var puppet = CreateDriver(entry, kart);
            if (!puppet) return null;
            var root = new GameObject(entry.id + "_Driver");
            root.transform.SetParent(kart, false);
            foreach (var smr in puppet.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh { name = smr.sharedMesh.name + "_Seated" };
                smr.BakeMesh(baked, true);
                var part = new GameObject(smr.name);
                part.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                part.transform.SetParent(root.transform, true);
                // BakeMesh leaves the vertices in the renderer's unscaled local space (FBX rigs are
                // often scaled 0.01-0.1), so the part takes the renderer's scale.
                part.transform.localScale = smr.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = baked;
                var mr = part.AddComponent<MeshRenderer>();
                mr.sharedMaterials = smr.sharedMaterials;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            puppet.gameObject.SetActive(false); // gone this frame already, not just at its end
            Destroy(puppet.seatAnchor.gameObject);
            Destroy(puppet.gameObject);
            return root;
        }
    }
}
