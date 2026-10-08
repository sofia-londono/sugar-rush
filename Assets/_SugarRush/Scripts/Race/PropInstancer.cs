using UnityEngine;
using UnityEngine.Rendering;

namespace SugarRush
{
    /// <summary>
    /// Draws a track's decoration with GPU instancing: each batch is one mesh submesh + material
    /// and the matrices of every copy within a 150 m chunk (so whole chunks are culled per camera).
    /// The scene stores only the matrices, not a mesh per copy, so the download stays small.
    /// Shadows only on "Quality" graphics in one-player races (like the rest of the track).
    /// </summary>
    [ExecuteAlways]
    public class PropInstancer : MonoBehaviour
    {
        [System.Serializable]
        public class Batch
        {
            public Mesh mesh;
            public int subMesh;
            public Material material;
            public Bounds bounds;
            public bool castShadows = true;
            public Matrix4x4[] matrices;
        }

        public Batch[] batches;

        void Update() => Draw(null);

        /// <summary>Queues every batch for this frame (all cameras, or just <paramref name="cam"/>).</summary>
        public void Draw(Camera cam)
        {
            if (batches == null) return;
            bool shadows = Application.isPlaying && GameSettings.Quality != 0 && !RaceSetup.IsSplitScreen;
            foreach (var b in batches)
            {
                if (!b.mesh || !b.material || b.matrices == null || b.matrices.Length == 0) continue;
                var rp = new RenderParams(b.material)
                {
                    worldBounds = b.bounds,
                    camera = cam,
                    layer = gameObject.layer,
                    receiveShadows = true,
                    shadowCastingMode = shadows && b.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                };
                Graphics.RenderMeshInstanced(rp, b.mesh, b.subMesh, b.matrices);
            }
        }
    }
}
