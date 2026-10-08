using System.Collections.Generic;
using UnityEngine;

namespace SugarRush.EditorTools
{
    /// <summary>
    /// Candy shapes made by code (no downloaded models, so no licences to track): a gummy bear in
    /// two levels of detail and a glazed half donut with smooth dripping glaze and sprinkles
    /// (small ones mark the road edges, big ones become the donut tunnel). Saved to Art/Props/Code_*.
    /// </summary>
    public static partial class SugarRushSetup
    {
        const string CodePropsDir = Root + "/Art/Props";

        /// <summary>
        /// Gummy bear, 1 tall, facing +Z, standing on y = 0. One submesh; vertex colours for
        /// SugarRush/CandyInstanced: the body takes the tint, eyes and nose stay dark.
        /// </summary>
        static Mesh CodeGummyBear(bool low)
        {
            string path = $"{CodePropsDir}/Code_GummyBear{(low ? "Low" : "")}.asset";
            var sphere = low ? LowSphere(6, 4) : LowSphere(10, 7);
            var small = low ? LowSphere(4, 3) : LowSphere(7, 5);
            var body = new List<CombineInstance>();
            var face = new List<CombineInstance>();
            void Part(List<CombineInstance> list, Mesh mesh, Vector3 pos, Vector3 scale, float roll = 0f) =>
                list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, Quaternion.Euler(0f, 0f, roll), scale) });
            Part(body, sphere, new Vector3(-0.17f, 0.13f, 0.02f), new Vector3(0.26f, 0.28f, 0.26f));
            Part(body, sphere, new Vector3(0.17f, 0.13f, 0.02f), new Vector3(0.26f, 0.28f, 0.26f));
            Part(body, sphere, new Vector3(0f, 0.38f, 0f), new Vector3(0.52f, 0.55f, 0.42f));
            Part(body, sphere, new Vector3(-0.27f, 0.48f, 0.05f), new Vector3(0.17f, 0.3f, 0.17f), -25f);
            Part(body, sphere, new Vector3(0.27f, 0.48f, 0.05f), new Vector3(0.17f, 0.3f, 0.17f), 25f);
            Part(body, sphere, new Vector3(0f, 0.78f, 0.02f), new Vector3(0.42f, 0.38f, 0.38f));
            Part(body, small, new Vector3(-0.16f, 0.95f, 0f), new Vector3(0.14f, 0.14f, 0.1f));
            Part(body, small, new Vector3(0.16f, 0.95f, 0f), new Vector3(0.14f, 0.14f, 0.1f));
            Part(body, small, new Vector3(0f, 0.74f, 0.19f), new Vector3(0.18f, 0.13f, 0.12f));
            Part(face, small, new Vector3(-0.08f, 0.83f, 0.18f), Vector3.one * 0.05f);
            Part(face, small, new Vector3(0.08f, 0.83f, 0.18f), Vector3.one * 0.05f);
            Part(face, small, new Vector3(0f, 0.77f, 0.25f), new Vector3(0.06f, 0.04f, 0.04f));
            var mesh = MergeColoured(low ? "Code_GummyBearLow" : "Code_GummyBear",
                (body, new Color(1f, 1f, 1f, 1f)), (face, new Color(0.18f, 0.1f, 0.12f, 1f)));
            mesh.RecalculateBounds();
            return SaveMeshAsset(mesh, path);
        }

        /// <summary>
        /// Half donut standing as an arch in local XY (X across, Z thickness), on y = 0: ring of
        /// radius <paramref name="major"/> and tube <paramref name="minor"/>. Submeshes: 0 dough,
        /// 1 glaze (a shell over the outside with smooth drips towards the hole), 2-4 sprinkles
        /// in three colours. A bit more than half a ring, so the feet sink into the ground.
        /// </summary>
        static Mesh DonutArch(string name, float major, float minor, int seed, int segments = 32, int sides = 14, int sprinkles = 40, int glazeAcross = 10, float glazeReach = 1.25f)
        {
            const float a0 = -0.14f, a1 = Mathf.PI + 0.14f;
            var verts = new List<Vector3>();
            var dough = new List<int>();
            var glaze = new List<int>();
            var sprinkle = new[] { new List<int>(), new List<int>(), new List<int>() };
            Vector3 Out(float a) => new(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 Surface(float a, float b, float r) => Out(a) * major + (Out(a) * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b)) * r;

            // Dough: the full tube.
            for (int s = 0; s <= segments; s++)
            {
                float a = Mathf.Lerp(a0, a1, s / (float)segments);
                for (int k = 0; k <= sides; k++) verts.Add(Surface(a, k / (float)sides * Mathf.PI * 2f, minor));
            }
            for (int s = 0; s < segments; s++)
                for (int k = 0; k < sides; k++)
                {
                    int i = s * (sides + 1) + k, c = i + sides + 1;
                    dough.AddRange(new[] { i, c, i + 1, i + 1, c, c + 1 });
                }

            // Glaze: a slightly bigger shell around the outside of the ring; how far it reaches
            // towards the hole changes smoothly along the ring (rounded drips).
            var rng = new System.Random(seed);
            var phases = new[] { (float)rng.NextDouble() * 6f, (float)rng.NextDouble() * 6f };
            float Reach(float t) => Mathf.Min(2.9f, glazeReach + 0.6f * Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 9f + phases[0])), 4f) + 0.2f * Mathf.Sin(t * 23f + phases[1]));
            int glazeSegs = segments * 2, across = glazeAcross, start = verts.Count;
            for (int s = 0; s <= glazeSegs; s++)
            {
                float t = s / (float)glazeSegs, a = Mathf.Lerp(a0 + 0.06f, a1 - 0.06f, t), reach = Reach(t);
                for (int k = 0; k <= across; k++) verts.Add(Surface(a, Mathf.Lerp(-reach, reach, k / (float)across), minor * 1.06f));
            }
            for (int s = 0; s < glazeSegs; s++)
                for (int k = 0; k < across; k++)
                {
                    int i = start + s * (across + 1) + k, c = i + across + 1;
                    glaze.AddRange(new[] { i, c, i + 1, i + 1, c, c + 1 });
                }

            // Sprinkles: little boxes lying on the glaze.
            for (int n = 0; n < sprinkles; n++)
            {
                float t = 0.04f + (float)rng.NextDouble() * 0.92f, a = Mathf.Lerp(a0, a1, t);
                float b = ((float)rng.NextDouble() * 2f - 1f) * Reach(t) * 0.75f;
                Vector3 normal = Out(a) * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                Vector3 centre = Surface(a, b, minor * 1.08f);
                var rot = Quaternion.LookRotation(normal) * Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 180f);
                float w = minor * 0.06f, l = minor * 0.24f;
                int s0 = verts.Count;
                for (int corner = 0; corner < 8; corner++)
                    verts.Add(centre + rot * new Vector3((corner & 1) == 0 ? -w : w, (corner & 2) == 0 ? -l : l, (corner & 4) == 0 ? -w * 0.6f : w * 0.6f));
                int[] box = { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5 };
                foreach (int idx in box) sprinkle[n % 3].Add(s0 + idx);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.subMeshCount = 5;
            mesh.SetTriangles(dough, 0);
            mesh.SetTriangles(glaze, 1);
            for (int i = 0; i < 3; i++) mesh.SetTriangles(sprinkle[i], 2 + i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMeshAsset(mesh, $"{CodePropsDir}/{name}.asset");
        }

        /// <summary>Combines parts into one submesh, each part painted with its vertex colour.</summary>
        static Mesh MergeColoured(string name, params (List<CombineInstance> parts, Color colour)[] groups)
        {
            var verts = new List<Vector3>();
            var colours = new List<Color>();
            var tris = new List<int>();
            foreach (var (parts, colour) in groups)
                foreach (var part in parts)
                {
                    int start = verts.Count;
                    foreach (var v in part.mesh.vertices) { verts.Add(part.transform.MultiplyPoint3x4(v)); colours.Add(colour); }
                    foreach (int t in part.mesh.triangles) tris.Add(start + t);
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetColors(colours);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Material for SugarRush/CandyInstanced props (colour per copy, wind sway, instancing).</summary>
        static Material CandyMaterial(TrackBuild b, string name, float wind, float gloss, float glow)
        {
            var m = GetMaterial($"{b.MaterialDir}/Props/{name}", "SugarRush/CandyInstanced", Color.white);
            m.SetColor("_TrunkColor", new Color(1f, 0.95f, 0.88f));
            m.SetFloat("_Wind", wind);
            m.SetFloat("_Gloss", gloss);
            m.SetFloat("_Glow", glow);
            m.enableInstancing = true;
            UnityEditor.EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Gummy tree, 1 tall, standing on y = 0: a slightly tapered cream trunk and a crown of
        /// soft squashed gummy drops (one submesh, see MergeColoured). <paramref name="lean"/> pushes the crown
        /// towards +X (tunnel trees lean over the road), the trunk bending with it.
        /// <paramref name="variant"/> changes the drop layout.
        /// </summary>
        static Mesh CodeGummyTree(int variant, float lean, bool low)
        {
            string name = $"Code_GummyTree{variant}{(lean > 0f ? "Lean" : "")}{(low ? "Low" : "")}";
            var rng = new System.Random(100 + variant * 17 + (lean > 0f ? 5 : 0));
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var drop = low ? LowSphere(5, 3) : LowSphere(7, 5);
            var trunkParts = new List<CombineInstance>();
            var crownParts = new List<CombineInstance>();

            // Trunk: a few stacked tapered pieces curving towards the crown.
            const float trunkTop = 0.62f;
            var piece = LowCylinder(low ? 5 : 6);
            int pieces = low ? 2 : 4;
            Vector3 Bend(float t) => new(lean * 0.55f * t * t, trunkTop * t, 0f);
            for (int k = 0; k < pieces; k++)
            {
                float t0 = k / (float)pieces, t1 = (k + 1) / (float)pieces;
                Vector3 a = Bend(t0), b = Bend(t1);
                float radius = Mathf.Lerp(0.034f, 0.022f, t0) * (lean > 0f ? 0.8f : 1f);
                var rot = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
                trunkParts.Add(new CombineInstance { mesh = piece, transform = Matrix4x4.TRS(a, rot, new Vector3(radius * 2f, (b - a).magnitude * 1.08f, radius * 2f)) });
            }

            // Crown: squashed drops around a centre above the trunk; leaning trees spread a wide, flat canopy.
            Vector3 centre = Bend(1f) + new Vector3(lean * 0.25f, 0.12f, 0f);
            int drops = low ? 5 : (lean > 0f ? 9 : 7);
            for (int k = 0; k < drops; k++)
            {
                float a = k / (float)drops * Mathf.PI * 2f + R(-0.3f, 0.3f);
                float r = k == 0 ? 0f : R(0.13f, 0.2f) * (1f + lean * 0.9f);
                var pos = centre + new Vector3(Mathf.Cos(a) * r * (1f + lean * 0.6f), k == 0 ? 0.06f : R(-0.06f, 0.08f), Mathf.Sin(a) * r);
                float size = (k == 0 ? 0.36f : R(0.22f, 0.3f)) * (1f + lean * 0.5f);
                crownParts.Add(new CombineInstance
                {
                    mesh = drop,
                    transform = Matrix4x4.TRS(pos, Quaternion.Euler(0f, R(0f, 360f), 0f), new Vector3(size * (1f + lean * 0.3f), size * 0.72f, size)),
                });
            }

            // One submesh: trunk (alpha 0 = trunk colour), crown (alpha 1 = takes the tint).
            var mesh = MergeColoured(name, (trunkParts, new Color(1f, 1f, 1f, 0f)), (crownParts, Color.white));
            // Normalise to height 1 on y = 0.
            var verts = mesh.vertices;
            float top = 0f;
            foreach (var v in verts) top = Mathf.Max(top, v.y);
            for (int i = 0; i < verts.Length; i++) verts[i] /= top;
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMeshAsset(mesh, $"{CodePropsDir}/{name}.asset");
        }

        static readonly (string name, Color glaze)[] DonutFlavours =
        {
            ("Strawberry", new Color(1f, 0.62f, 0.8f)),
            ("Chocolate", new Color(0.55f, 0.35f, 0.26f)),
            ("Mint", new Color(0.62f, 0.92f, 0.8f)),
            ("Vanilla", new Color(1f, 0.95f, 0.84f)),
        };

        /// <summary>Materials for a donut of the given flavour: dough, glaze, three sprinkle colours (instancing on).</summary>
        static Material[] DonutMaterials(TrackBuild b, int flavour)
        {
            var (name, colour) = DonutFlavours[flavour % DonutFlavours.Length];
            return new[]
            {
                PropMaterial(b, "DonutDough", new Color(0.93f, 0.7f, 0.45f), null, 0.25f),
                PropMaterial(b, $"DonutGlaze{name}", colour, null, 0.85f),
                PropMaterial(b, "SprinkleWhite", Color.white, null, 0.6f),
                PropMaterial(b, "SprinkleLemon", new Color(1f, 0.85f, 0.4f), null, 0.6f),
                PropMaterial(b, "SprinkleSky", new Color(0.55f, 0.75f, 1f), null, 0.6f),
            };
        }
    }
}
