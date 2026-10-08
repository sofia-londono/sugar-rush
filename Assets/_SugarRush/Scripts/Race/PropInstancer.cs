using UnityEngine;
using UnityEngine.Rendering;

namespace SugarRush
{
    /// <summary>
    /// Draws a track's decoration with GPU instancing: each batch is one mesh submesh + material
    /// and the matrices of every copy within a chunk of the world (so whole chunks are culled per
    /// camera). Batches can have a lighter LOD mesh used past <see cref="Batch.lodDistance"/> and a
    /// cull distance past which they are not drawn at all (the billboard backdrop and the fog take
    /// over). The scene stores only the matrices, not a mesh per copy, so the download stays small.
    /// Batches can carry one colour per copy (see SugarRush/CandyInstanced), so trees of every
    /// colour share one draw call. Shadows only on "Quality" graphics in one-player races.
    /// </summary>
    [ExecuteAlways]
    public class PropInstancer : MonoBehaviour
    {
        [System.Serializable]
        public class Batch
        {
            public Mesh mesh;
            [Tooltip("Lighter mesh (same submeshes) drawn past lodDistance; none = always the main mesh.")]
            public Mesh lodMesh;
            public int subMesh;
            public Material material;
            public Bounds bounds;
            public bool castShadows = true;
            public float lodDistance = 60f;
            [Tooltip("Not drawn when the camera is farther than this from the batch (0 = always drawn).")]
            public float cullDistance;
            public Matrix4x4[] matrices;
            [Tooltip("Per-copy colour for shaders with an instanced _BaseColor (SugarRush/CandyInstanced); empty = the material colour.")]
            public Vector4[] colors;
            [System.NonSerialized] public MaterialPropertyBlock block;
        }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public Batch[] batches;

        static Camera[] cameras = new Camera[8];

        void Update()
        {
            if (!Application.isPlaying) { Draw(null); return; } // edit mode: scene view and game view alike
            // Per camera, so each one picks its own level of detail (split screen has two).
            if (Camera.allCamerasCount > cameras.Length) cameras = new Camera[Camera.allCamerasCount];
            int count = Camera.GetAllCameras(cameras);
            for (int i = 0; i < count; i++)
                if (cameras[i].cameraType == CameraType.Game) Draw(cameras[i]);
        }

        /// <summary>Queues every batch for this frame (for one camera, or all cameras with null).</summary>
        public void Draw(Camera cam)
        {
            if (batches == null) return;
            bool shadows = Application.isPlaying && GameSettings.Quality != 0 && !RaceSetup.IsSplitScreen;
            Vector3 eye = cam ? cam.transform.position : Vector3.zero;
            foreach (var b in batches)
            {
                if (!b.mesh || !b.material || b.matrices == null || b.matrices.Length == 0) continue;
                var mesh = b.mesh;
                if (cam)
                {
                    float d = Mathf.Sqrt(b.bounds.SqrDistance(eye));
                    if (b.cullDistance > 0f && d > b.cullDistance) continue;
                    if (b.lodMesh && d > b.lodDistance) mesh = b.lodMesh;
                }
                if (b.colors != null && b.colors.Length == b.matrices.Length && b.block == null)
                {
                    b.block = new MaterialPropertyBlock();
                    b.block.SetVectorArray(BaseColor, b.colors);
                }
                var rp = new RenderParams(b.material)
                {
                    matProps = b.block,
                    worldBounds = b.bounds,
                    camera = cam,
                    layer = gameObject.layer,
                    receiveShadows = true,
                    shadowCastingMode = shadows && b.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                };
                Graphics.RenderMeshInstanced(rp, mesh, b.subMesh, b.matrices);
            }
        }
    }
}
