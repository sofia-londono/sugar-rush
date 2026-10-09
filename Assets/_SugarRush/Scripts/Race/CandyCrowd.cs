using UnityEngine;

namespace SugarRush
{
    /// <summary>
    /// The candy crowd on the stands: gummy bears that bounce and sway all race long and hop
    /// much higher when a kart races past. Drawn instanced (one batch, a colour per bear);
    /// only the matrices change each frame, and only while a camera is close enough to see it.
    /// </summary>
    public class CandyCrowd : MonoBehaviour
    {
        public Mesh mesh;
        public Material material;
        [Tooltip("Resting pose of every spectator (feet on the stand, facing the road).")]
        public Matrix4x4[] seats;
        public Vector4[] colours;
        public float hopHeight = 0.35f;
        public float excitedRange = 30f;
        public float visibleRange = 220f;

        Matrix4x4[] frame;
        float[] phase;
        MaterialPropertyBlock block;
        Bounds bounds;
        float excitement;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Start()
        {
            if (seats == null || seats.Length == 0) { enabled = false; return; }
            frame = new Matrix4x4[seats.Length];
            phase = new float[seats.Length];
            for (int i = 0; i < phase.Length; i++) phase[i] = Random.value * 6.3f;
            block = new MaterialPropertyBlock();
            if (colours != null && colours.Length == seats.Length) block.SetVectorArray(BaseColorId, colours);
            bounds = new Bounds(seats[0].GetColumn(3), Vector3.zero);
            foreach (var s in seats) bounds.Encapsulate(s.GetColumn(3));
            bounds.Expand(8f);
        }

        void Update()
        {
            var cam = Camera.main;
            if (!mesh || !material || !cam || bounds.SqrDistance(cam.transform.position) > visibleRange * visibleRange) return;

            // A kart close by gets everyone jumping.
            float target = 0f;
            var race = RaceManager.Instance;
            if (race)
                foreach (var r in race.Racers)
                    if (r && bounds.SqrDistance(r.transform.position) < excitedRange * excitedRange) { target = 1f; break; }
            excitement = Mathf.MoveTowards(excitement, target, Time.deltaTime * (target > excitement ? 3f : 0.6f));

            float time = Time.time;
            for (int i = 0; i < seats.Length; i++)
            {
                float rate = 3f + 4f * excitement;
                float hop = Mathf.Abs(Mathf.Sin(time * rate + phase[i])) * hopHeight * (0.3f + 1.7f * excitement);
                float sway = Mathf.Sin(time * 1.6f + phase[i]) * 8f;
                var lift = Matrix4x4.TRS(new Vector3(0f, hop, 0f), Quaternion.Euler(0f, 0f, sway), Vector3.one);
                frame[i] = seats[i] * lift;
            }
            var rp = new RenderParams(material) { matProps = block, worldBounds = bounds, receiveShadows = true };
            Graphics.RenderMeshInstanced(rp, mesh, 0, frame);
        }
    }
}
