using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// Makes a forest track feel alive, all from the first local player's position:
    /// - darker stretches (tunnels): the sun dims and the candy props darken (_SR_Shade), smoothly;
    /// - ambience loops (birds, wind, bubbling chocolate) mixed by zone: wind on high ground and
    ///   in the open, bubbles near the chocolate, birds everywhere (louder deep in the forest);
    /// - butterflies and candy birds fluttering around the camera (instanced, a few dozen matrices);
    /// - sparkles floating in the air everywhere and coloured fireflies in the dark stretches.
    /// One component per track, built by the Sugar Rush builder.
    /// </summary>
    public class ForestAmbience : MonoBehaviour
    {
        [Header("Zones")]
        public TrackPath path;
        [Tooltip("Dark stretches (tunnels) as metres along the racing line: x = start, y = end.")]
        public Vector2[] darkZones;
        [Tooltip("Chocolate (lakes, river, fountains) for the bubbling: x, z, radius.")]
        public Vector3[] water;
        public Light sun;
        [Range(0f, 1f)] public float darkSunDim = 0.45f;
        [Range(0f, 1f)] public float darkPropShade = 0.3f;

        [Header("Sound")]
        public AudioClip birds, wind, bubbles;
        public float ambienceVolume = 0.55f;

        [Header("Critters")]
        public Mesh butterflyMesh, birdMesh;
        public Material critterMaterial;
        public int butterflies = 26, candyBirds = 9;

        [Header("Particles")]
        public ParticleSystem sparkles, fireflies;

        static readonly int ShadeId = Shader.PropertyToID("_SR_Shade");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        AudioSource birdSource, windSource, bubbleSource;
        float shade, sunBase, minPathY, maxPathY;

        struct Critter { public Vector3 pos, vel, target; public float phase, size; }
        Critter[] flutter, flock;
        Matrix4x4[] flutterMatrices, flockMatrices;
        MaterialPropertyBlock flutterColours, flockColours;

        void Start()
        {
            if (sun) sunBase = sun.intensity;
            minPathY = float.MaxValue; maxPathY = float.MinValue;
            if (path)
                foreach (var p in path.points) { minPathY = Mathf.Min(minPathY, p.y); maxPathY = Mathf.Max(maxPathY, p.y); }
            birdSource = Loop(birds);
            windSource = Loop(wind);
            bubbleSource = Loop(bubbles);

            var palette = new[]
            {
                new Color(1f, 0.55f, 0.8f), new Color(1f, 0.85f, 0.35f), new Color(0.55f, 0.85f, 1f),
                new Color(0.75f, 0.6f, 1f), new Color(0.55f, 0.95f, 0.75f), new Color(1f, 0.65f, 0.45f),
            };
            flutter = Spawn(butterflies, 0.6f, 1f);
            flock = Spawn(candyBirds, 1.4f, 2f);
            flutterMatrices = new Matrix4x4[flutter.Length];
            flockMatrices = new Matrix4x4[flock.Length];
            flutterColours = Colours(flutter.Length, palette);
            flockColours = Colours(flock.Length, palette);
            if (fireflies) { var e = fireflies.emission; e.enabled = false; }
        }

        AudioSource Loop(AudioClip clip)
        {
            if (!clip) return null;
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.playOnAwake = false;
            s.time = Random.Range(0f, clip.length * 0.9f);
            s.Play();
            return s;
        }

        Critter[] Spawn(int count, float sizeMin, float sizeMax)
        {
            var list = new Critter[count];
            for (int i = 0; i < count; i++)
                list[i] = new Critter { pos = Vector3.one * 1e5f, phase = Random.value * 10f, size = Random.Range(sizeMin, sizeMax) };
            return list;
        }

        static MaterialPropertyBlock Colours(int count, Color[] palette)
        {
            var block = new MaterialPropertyBlock();
            if (count == 0) return block;
            var colours = new Vector4[count];
            for (int i = 0; i < count; i++) colours[i] = palette[i % palette.Length];
            block.SetVectorArray(BaseColorId, colours);
            return block;
        }

        void OnDestroy() => Shader.SetGlobalFloat(ShadeId, 0f);

        void Update()
        {
            var race = RaceManager.Instance;
            var cam = race && race.kartCamera ? race.kartCamera.transform : (Camera.main ? Camera.main.transform : null);
            if (!cam) return;
            var player = race ? race.Player : null;
            float s = player ? player.PathDistance : (path ? path.ProjectDistance(cam.position, path.FindClosestSegment(cam.position)) : 0f);
            float dt = Time.deltaTime;

            // Darkness: 1 inside a dark stretch, fading over 15 m at its ends.
            float targetShade = 0f;
            if (darkZones != null && path)
                foreach (var z in darkZones)
                {
                    float into = Mathf.Min(Mathf.Repeat(s - z.x, path.Length), Mathf.Repeat(z.y - s, path.Length));
                    if (Mathf.Repeat(s - z.x, path.Length) <= Mathf.Repeat(z.y - z.x, path.Length))
                        targetShade = Mathf.Max(targetShade, Mathf.Clamp01(into / 15f));
                }
            shade = Mathf.MoveTowards(shade, targetShade, dt * 0.8f);
            if (sun) sun.intensity = sunBase * (1f - darkSunDim * shade);
            Shader.SetGlobalFloat(ShadeId, darkPropShade * shade);

            // Ambience mix.
            float bubbleNear = 0f;
            if (water != null)
                foreach (var w in water)
                {
                    float d = new Vector2(cam.position.x - w.x, cam.position.z - w.y).magnitude - w.z;
                    bubbleNear = Mathf.Max(bubbleNear, Mathf.InverseLerp(45f, 5f, d));
                }
            float height = maxPathY > minPathY ? Mathf.InverseLerp(minPathY, maxPathY, cam.position.y) : 0f;
            float volume = ambienceVolume * GameSettings.SfxVolume;
            Fade(birdSource, volume * (0.45f + 0.35f * shade), dt);
            Fade(windSource, volume * (0.2f + 0.55f * height) * (1f - 0.6f * shade), dt);
            Fade(bubbleSource, volume * 0.9f * bubbleNear, dt);

            // Particles follow the camera; fireflies only where it is dark.
            if (sparkles) sparkles.transform.position = cam.position + cam.forward * 12f;
            if (fireflies)
            {
                fireflies.transform.position = cam.position + cam.forward * 10f;
                var e = fireflies.emission;
                e.enabled = shade > 0.35f;
            }

            // Critters.
            float time = Time.time;
            UpdateCritters(flutter, flutterMatrices, cam, 8f, 45f, 1.2f, 5f, 3.2f, 14f, time, dt);
            UpdateCritters(flock, flockMatrices, cam, 20f, 70f, 10f, 22f, 9f, 9f, time, dt);
            Draw(butterflyMesh, flutterMatrices, flutterColours);
            Draw(birdMesh, flockMatrices, flockColours);
        }

        void Fade(AudioSource source, float target, float dt)
        {
            if (source) source.volume = Mathf.MoveTowards(source.volume, target, dt * 0.5f);
        }

        /// <summary>
        /// Each critter heads for a target point near the camera (ahead of it mostly) and picks a new
        /// one when it gets there; critters left far behind jump to a fresh spot ahead. Wings flap
        /// by squashing the mesh across.
        /// </summary>
        void UpdateCritters(Critter[] list, Matrix4x4[] matrices, Transform cam, float near, float far, float lowY, float highY,
            float speed, float flapRate, float time, float dt)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 Fresh()
            {
                Vector2 r = Random.insideUnitCircle * far;
                Vector3 p = cam.position + flatForward * Random.Range(near, far) + new Vector3(r.x, 0f, r.y) * 0.6f;
                p.y = cam.position.y - 2f + Random.Range(lowY, highY);
                return p;
            }
            for (int i = 0; i < list.Length; i++)
            {
                ref var c = ref list[i];
                if ((c.pos - cam.position).sqrMagnitude > far * far * 1.6f) { c.pos = Fresh(); c.target = Fresh(); c.vel = Vector3.zero; }
                if ((c.target - c.pos).sqrMagnitude < 4f) c.target = Fresh();
                Vector3 want = (c.target - c.pos).normalized * speed;
                want.y += Mathf.Sin(time * 1.7f + c.phase) * speed * 0.3f; // bobbing flight
                c.vel = Vector3.Lerp(c.vel, want, dt * 1.5f);
                c.pos += c.vel * dt;
                float flap = 0.25f + 0.75f * Mathf.Abs(Mathf.Sin(time * flapRate + c.phase));
                var rot = c.vel.sqrMagnitude > 0.01f ? Quaternion.LookRotation(c.vel.normalized, Vector3.up) : Quaternion.identity;
                matrices[i] = Matrix4x4.TRS(c.pos, rot, new Vector3(c.size * flap, c.size, c.size));
            }
        }

        void Draw(Mesh mesh, Matrix4x4[] matrices, MaterialPropertyBlock colours)
        {
            if (!mesh || !critterMaterial || matrices.Length == 0) return;
            var rp = new RenderParams(critterMaterial)
            {
                matProps = colours,
                worldBounds = new Bounds(transform.position, Vector3.one * 100000f),
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
            };
            Graphics.RenderMeshInstanced(rp, mesh, 0, matrices);
        }
    }
}
