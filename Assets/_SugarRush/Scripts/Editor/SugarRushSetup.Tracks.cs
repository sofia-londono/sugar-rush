using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

namespace SugarRush.EditorTools
{
    /// <summary>
    /// Race tracks generated from a closed Unity Splines loop: road with candy curbs and rails,
    /// invisible walls, rolling ground that meets the road, themed decoration combined into a few
    /// meshes, the racing line, start/finish, Ralph's chaos and the race rig. Each track is a
    /// <see cref="SplineTrack"/> (knots + look) and gets its own race scene.
    /// </summary>
    public static partial class SugarRushSetup
    {
        /// <summary>Design of one spline track. Knot 0 is the start line; karts drive towards knot 1.</summary>
        class SplineTrack
        {
            public string Id, Folder, Scene;
            public Vector3[] Knots;
            /// <summary>Widest half width of the road (the nominal one; profiles are drawn for it).</summary>
            public float HalfWidth = 6f;
            /// <summary>Optional half width at each knot (narrow stretches), blended along the lap.</summary>
            public float[] Widths;
            /// <summary>Optional banking at each knot, in degrees: the outside of the turn is raised.</summary>
            public float[] Banks;
            /// <summary>Jump gaps: the road stops at the point nearest each XZ spot (the lip) for the given length (m).</summary>
            public (Vector2 lip, float length)[] Gaps;
            /// <summary>Caramel puddles and edge gummy bears, as fractions of the lap.</summary>
            public float[] Puddles, EdgeBears;
            /// <summary>Height of the open ground around the track (the road is at the knots' heights).</summary>
            public float GroundLevel = -1.5f;
            public int Seed = 1;
            public Color Sky = new(0.8f, 0.9f, 1f);
            /// <summary>The Japanese bridge carries the road at the centre-line point nearest this XZ spot (none if zero length).</summary>
            public Vector2 BridgeNear;
            public float BridgeLength, BridgeHeight = 5f;
            /// <summary>Chocolate lake: circles (x, z, radius) carved into the ground, filled at <see cref="LakeLevel"/>.</summary>
            public Vector3[] Lake;
            public float LakeLevel = -2.5f;
            public System.Func<TrackBuild, Materials> Materials;
            public System.Action<TrackBuild> Decorate;
        }

        class Materials
        {
            public Material Road, Curb, Ground;
        }

        const float GroundCell = 5f, WorldMargin = 150f;
        const float CurbWidth = 0.9f, RailWidth = 0.35f, RailHeight = 0.6f, ShoulderWidth = 4.5f, ShoulderDrop = 1.3f;

        /// <summary>Saves a generated mesh, updating an existing asset in place so scenes that use it keep their reference.</summary>
        static Mesh SaveMeshAsset(Mesh mesh, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ---------------------------------------------------------------- Gummy Forest

        /// <summary>
        /// "Bosque de gomitas" (~1.07 km): start straight, a fast banked right-hander, the climb to
        /// the hilltop and jump 1 off its lip, banked hairpin 1, a narrow chicane through the
        /// forest, the Japanese bridge over the chocolate lake, a kicker and jump 2 over a gap
        /// above a chocolate river (too slow and you fall in), banked hairpin 2 and back to the line.
        /// Caramel puddles and giant gummy bears on the edges make the line harder.
        /// </summary>
        static readonly SplineTrack GummyForest = new()
        {
            Id = "gummy_forest",
            Folder = "GummyForest",
            Scene = "GummyForest_Track",
            Seed = 7,
            HalfWidth = 6.5f,
            Knots = new[]
            {
                new Vector3(0f, 0f, 0f),        // 0 start / finish
                new Vector3(0f, 0f, 80f),
                new Vector3(25f, 1f, 135f),     // 2 fast banked right-hander
                new Vector3(80f, 3f, 160f),
                new Vector3(135f, 5f, 140f),
                new Vector3(165f, 8f, 100f),    // 5 climbing
                new Vector3(170f, 12f, 55f),    // 6 hilltop lip: jump 1
                new Vector3(168f, 8.5f, 40f),   // 7 steep drop
                new Vector3(160f, 6f, 0f),
                new Vector3(158f, 4f, -45f),
                new Vector3(159f, 3f, -75f),    // 10 hairpin 1: an arc of radius 19 around (140, -75)
                new Vector3(153.4f, 2.8f, -88.4f),
                new Vector3(140f, 2.6f, -94f),
                new Vector3(126.6f, 2.5f, -88.4f),
                new Vector3(121f, 2.5f, -75f),
                new Vector3(118f, 3f, -40f),    // 13 narrow chicane
                new Vector3(108f, 4f, -15f),
                new Vector3(118f, 5f, 10f),
                new Vector3(102f, 4f, 35f),
                new Vector3(80f, 2f, 55f),
                new Vector3(45f, 1f, 45f),
                new Vector3(48f, 0.5f, 10f),    // 19 Japanese bridge over the lake
                new Vector3(50f, 0.8f, -12f),   // 20 kicker
                new Vector3(52f, 3.2f, -27f),   // 21 crest: jump 2 over the river
                new Vector3(54f, -0.5f, -46f),  // 22 landing well below the lip (karts must never reach the far edge from underneath)
                new Vector3(45f, 0f, -92f),     // hairpin 2: an arc of radius 18 around (27, -92)
                new Vector3(39.7f, -0.3f, -104.7f),
                new Vector3(27f, -0.5f, -110f),
                new Vector3(14.3f, -0.3f, -104.7f),
                new Vector3(9f, 0f, -92f),
                new Vector3(2f, 0f, -45f),
            },
            Widths = new[]
            {
                6.5f, 6.5f, 6.5f, 6.5f, 6.5f, 6f, 6f, 6f, 6f, 6f, 6f, 6f, 6f, 6f, 6f,
                4.2f, 4f, 4f, 4.2f, 5.5f, 5.5f, 5.5f, 6f, 6f, 6f, 6f, 6f, 6f, 6f, 6f, 6.5f,
            },
            Banks = new[]
            {
                0f, 0f, 10f, 12f, 10f, 4f, 0f, 0f, 0f, 4f, 12f, 15f, 15f, 15f, 12f,
                0f, 6f, 6f, 4f, 6f, 6f, 0f, 0f, 0f, 0f, 10f, 14f, 14f, 14f, 10f, 4f,
            },
            Gaps = new[] { (new Vector2(51.9f, -26.5f), 8f) },
            Puddles = new[] { 0.12f, 0.33f, 0.5f, 0.66f, 0.9f },
            EdgeBears = new[] { 0.08f, 0.27f, 0.45f, 0.6f, 0.75f, 0.95f },
            BridgeNear = new Vector2(48f, 10f),
            BridgeLength = 30f,
            Lake = new[]
            {
                new Vector3(48f, 10f, 11f), new Vector3(75f, 15f, 20f),                         // lake under the bridge
                new Vector3(37f, -29f, 6f), new Vector3(52f, -29f, 6f), new Vector3(67f, -29f, 6f), // river under jump 2
            },
            Materials = GummyForestMaterials,
            Decorate = DecorateGummyForest,
        };

        [MenuItem("Sugar Rush/Tracks/Gummy Forest")]
        public static string BuildGummyForest() => BuildSplineTrack(GummyForest);

        // ---------------------------------------------------------------- Builder

        /// <summary>Working data while one spline track is generated.</summary>
        class TrackBuild
        {
            public SplineTrack Design;
            public Transform Root;
            public string AssetDir, MaterialDir;
            /// <summary>Centre line every metre (closed, index 0 = start), its flat right vectors and distances.</summary>
            public List<Vector3> Center, Right;
            public List<float> Dist;
            public float Length;
            /// <summary>Centre line every 2 m, for distance-to-road queries.</summary>
            public List<Vector3> Coarse;
            public Rect Bounds;
            public System.Random Rng;
            public Materials Mats;
            /// <summary>Centre-line samples where the road is not drawn (bridge, jump gaps).</summary>
            public bool[] Hidden;
            /// <summary>Centre-line samples with nothing to drive on (jump gaps).</summary>
            public bool[] NoGround;
            /// <summary>Half width and signed banking (radians, + = right side up) at each centre sample.</summary>
            public float[] Width, Bank;
            /// <summary>Distances (m) the chaos zones keep away from (bridge, gaps, hazards).</summary>
            public readonly List<float> Busy = new();

            /// <summary>The road's own right and up at a sample (tilted on banked turns).</summary>
            public void Frame(int i, out Vector3 right, out Vector3 up)
            {
                float a = Bank != null ? Bank[i] : 0f;
                right = Right[i] * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);
                up = Vector3.up * Mathf.Cos(a) - Right[i] * Mathf.Sin(a);
            }

            /// <summary>
            /// A cross-section point at sample i. Profiles are drawn for the nominal half width:
            /// the road part stretches to the local width, everything past the edge keeps its size.
            /// Up to the rail the point follows the banking; beyond it (shoulders) it is level.
            /// </summary>
            public Vector3 ProfilePoint(int i, Vector2 q)
            {
                float hw0 = Design.HalfWidth, w = Width != null ? Width[i] : hw0;
                float ax = Mathf.Abs(q.x), sign = q.x < 0f ? -1f : 1f;
                float x = ax <= hw0 ? q.x * w / hw0 : sign * (ax - hw0 + w);
                float edge = w + CurbWidth + RailWidth + 0.01f;
                Frame(i, out var right, out var up);
                if (Mathf.Abs(x) <= edge) return Center[i] + right * x + up * q.y;
                return Center[i] + right * (sign * edge) + Right[i] * (sign * (Mathf.Abs(x) - edge)) + Vector3.up * q.y;
            }

            /// <summary>Road point at a real sideways offset (metres from the centre line).</summary>
            public Vector3 RoadPoint(int i, float lateral, float height = 0f)
            {
                Frame(i, out var right, out var up);
                return Center[i] + right * lateral + up * height;
            }

            public bool BusyNear(float d, float range)
            {
                foreach (float b in Busy)
                {
                    float gap = Mathf.Abs(Mathf.Repeat(d - b + Length * 0.5f, Length) - Length * 0.5f);
                    if (gap < range) return true;
                }
                return false;
            }
            readonly List<(Vector3 pos, float radius)> occupied = new();
            readonly Dictionary<(int, int, Mesh, int, Material, bool), List<Matrix4x4>> props = new();

            public bool HiddenAtDistance(float d)
            {
                if (Hidden == null) return false;
                int i = Mathf.FloorToInt(Mathf.Repeat(d, Length) / Length * Hidden.Length) % Hidden.Length;
                return Hidden[i];
            }

            /// <summary>Busy for Ralph: bridge, gaps, hazards (±15 m).</summary>
            public bool ExcludedForChaos(float d) => HiddenAtDistance(d) || BusyNear(d, 15f);

            /// <summary>0 outside the chocolate lake, rising to 1 a few metres inside its shore.</summary>
            public float LakeFactor(float x, float z)
            {
                if (Design.Lake == null) return 0f;
                float f = 0f;
                foreach (var c in Design.Lake)
                {
                    float d = new Vector2(x - c.x, z - c.y).magnitude;
                    f = Mathf.Max(f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c.z + 6f, c.z - 6f, d)));
                }
                return f;
            }

            /// <summary>Queues an instanced prop (every submesh with its material); drawn by <see cref="PropInstancer"/>.</summary>
            public void AddProp(Mesh mesh, Material[] mats, Matrix4x4 matrix, bool shadows = true)
            {
                Vector3 p = matrix.GetColumn(3);
                int cx = Mathf.FloorToInt(p.x / 150f), cz = Mathf.FloorToInt(p.z / 150f);
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var key = (cx, cz, mesh, s, mats[Mathf.Min(s, mats.Length - 1)], shadows);
                    if (!props.TryGetValue(key, out var list)) props[key] = list = new List<Matrix4x4>();
                    list.Add(matrix);
                }
            }

            /// <summary>One PropInstancer under the track with a batch per chunk + mesh + submesh + material.</summary>
            public int FlushProps(out int instances)
            {
                var batches = new List<PropInstancer.Batch>();
                int triangles = 0;
                instances = 0;
                foreach (var (key, list) in props)
                {
                    var (_, _, mesh, sub, mat, shadows) = key;
                    for (int start = 0; start < list.Count; start += 500)
                    {
                        var matrices = list.GetRange(start, Mathf.Min(500, list.Count - start)).ToArray();
                        var bounds = new Bounds(matrices[0].MultiplyPoint3x4(mesh.bounds.center), Vector3.zero);
                        foreach (var m in matrices)
                        {
                            var mb = mesh.bounds;
                            for (int corner = 0; corner < 8; corner++)
                                bounds.Encapsulate(m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents,
                                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
                        }
                        batches.Add(new PropInstancer.Batch { mesh = mesh, subMesh = sub, material = mat, bounds = bounds, castShadows = shadows, matrices = matrices });
                        triangles += (int)mesh.GetIndexCount(sub) / 3 * matrices.Length;
                        if (sub == 0) instances += matrices.Length;
                    }
                }
                props.Clear();
                if (batches.Count == 0) return 0;
                var go = new GameObject("Props");
                go.transform.SetParent(Root, false);
                go.AddComponent<PropInstancer>().batches = batches.ToArray();
                return triangles;
            }
            readonly Dictionary<(int, int), Dictionary<Material, List<CombineInstance>>> decor = new();

            public float R(float a, float b) => a + (float)Rng.NextDouble() * (b - a);
            public T Pick<T>(T[] items) => items[Rng.Next(items.Length)];

            /// <summary>Flat distance from a point to the centre line, with the road height there.</summary>
            public float RoadDistance(Vector2 xz, out float roadY, out int index)
            {
                float best = float.MaxValue;
                roadY = 0f;
                index = 0;
                int n = Coarse.Count;
                for (int i = 0; i < n; i++)
                {
                    Vector3 a = Coarse[i], b = Coarse[(i + 1) % n];
                    Vector2 a2 = new(a.x, a.z), ab = new(b.x - a.x, b.z - a.z);
                    float t = Mathf.Clamp01(Vector2.Dot(xz - a2, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                    float d = (a2 + ab * t - xz).sqrMagnitude;
                    if (d < best) { best = d; roadY = Mathf.Lerp(a.y, b.y, t); index = i; }
                }
                return Mathf.Sqrt(best);
            }

            /// <summary>Open ground: gentle bumps, rising into a ring of hills at the edge of the world.</summary>
            public float Natural(float x, float z)
            {
                int s = Design.Seed;
                float n = (Mathf.PerlinNoise(x * 0.011f + s * 13.1f, z * 0.011f) - 0.5f) * 5f
                        + (Mathf.PerlinNoise(x * 0.04f, z * 0.04f + s * 7.7f) - 0.5f) * 1.2f;
                float edge = Mathf.Min(Mathf.Min(x - Bounds.xMin, Bounds.xMax - x), Mathf.Min(z - Bounds.yMin, Bounds.yMax - z));
                float rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(80f, 0f, edge)) * 35f;
                return Design.GroundLevel + n + rim;
            }

            /// <summary>Ground height: tucked under the road and its shoulders, blending out to the open ground.</summary>
            public float GroundHeight(float x, float z)
            {
                float d = RoadDistance(new Vector2(x, z), out float roadY, out _);
                float near = Design.HalfWidth + ShoulderWidth;
                float h;
                if (d <= near) h = roadY - 2.2f;
                else
                {
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - near) / 35f));
                    h = Mathf.Lerp(roadY - 1.8f, Natural(x, z), k);
                }
                // Chocolate lake: a shore just above the surface, then the basin.
                float lake = LakeFactor(x, z);
                if (lake > 0f) h = Mathf.Lerp(Mathf.Max(h, Design.LakeLevel + 0.6f), Design.LakeLevel - 3f, lake);
                return h;
            }

            /// <summary>
            /// A random free spot on the ground between minRoad and maxRoad metres from the road,
            /// more likely close to it. Marks it taken.
            /// </summary>
            public bool TryPlace(float minRoad, float maxRoad, float radius, out Vector3 pos)
            {
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    float x = R(Bounds.xMin + 60f, Bounds.xMax - 60f), z = R(Bounds.yMin + 60f, Bounds.yMax - 60f);
                    float d = RoadDistance(new Vector2(x, z), out _, out _);
                    if (d < minRoad + radius || d > maxRoad) continue;
                    if (R(0f, 1f) > Mathf.Lerp(1f, 0.25f, (d - minRoad) / Mathf.Max(1f, maxRoad - minRoad))) continue;
                    var p = new Vector3(x, 0f, z);
                    if (!IsFree(p, radius)) continue;
                    p.y = GroundHeight(x, z);
                    occupied.Add((p, radius));
                    pos = p;
                    return true;
                }
                pos = default;
                return false;
            }

            public bool IsFree(Vector3 p, float radius)
            {
                foreach (var (q, r) in occupied)
                {
                    float dx = p.x - q.x, dz = p.z - q.z;
                    if (dx * dx + dz * dz < (r + radius) * (r + radius)) return false;
                }
                return true;
            }

            public void Occupy(Vector3 p, float radius) => occupied.Add((p, radius));

            /// <summary>Queues a decoration piece; pieces are combined per 150 m chunk and material.</summary>
            public void Add(Mesh mesh, Material mat, Matrix4x4 matrix)
            {
                Vector3 p = matrix.GetColumn(3);
                var key = (Mathf.FloorToInt(p.x / 150f), Mathf.FloorToInt(p.z / 150f));
                if (!decor.TryGetValue(key, out var byMat)) decor[key] = byMat = new Dictionary<Material, List<CombineInstance>>();
                if (!byMat.TryGetValue(mat, out var list)) byMat[mat] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, transform = matrix });
            }

            /// <summary>One renderer per chunk, one submesh per material.</summary>
            public int FlushDecor()
            {
                int triangles = 0;
                foreach (var (key, byMat) in decor)
                {
                    var parts = new List<CombineInstance>();
                    var mats = new List<Material>();
                    foreach (var (mat, list) in byMat)
                    {
                        var part = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                        part.CombineMeshes(list.ToArray(), true, true);
                        parts.Add(new CombineInstance { mesh = part, transform = Matrix4x4.identity });
                        mats.Add(mat);
                    }
                    var mesh = new Mesh { name = $"Decor_{key.Item1}_{key.Item2}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                    mesh.CombineMeshes(parts.ToArray(), false, false);
                    foreach (var p in parts) Object.DestroyImmediate(p.mesh);
                    mesh.RecalculateBounds();
                    triangles += mesh.triangles.Length / 3;
                    mesh = SaveMeshAsset(mesh, $"{AssetDir}/{mesh.name}.asset");
                    var go = new GameObject(mesh.name);
                    go.transform.SetParent(Root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
                }
                decor.Clear();
                return triangles;
            }
        }

        static string BuildSplineTrack(SplineTrack design)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddLighting();

            var b = new TrackBuild
            {
                Design = design,
                AssetDir = $"{Root}/Art/Tracks/{design.Folder}",
                MaterialDir = $"Tracks/{design.Folder}",
                Rng = new System.Random(design.Seed * 7919),
            };
            // Generated meshes are rebuilt from scratch; only this track's scene uses them.
            AssetDatabase.DeleteAsset(b.AssetDir);
            EnsureFolder(b.AssetDir);

            // The design as a Unity spline (kept in the scene to look at and tweak knots).
            var spline = new Spline();
            foreach (var k in design.Knots) spline.Add(new BezierKnot(k));
            spline.Closed = true;
            spline.SetTangentMode(TangentMode.AutoSmooth);
            var splineGo = new GameObject("TrackSpline");
            splineGo.AddComponent<SplineContainer>().Spline = spline;

            // Centre line: dense samples of the spline, resampled evenly.
            float splineLength = spline.GetLength();
            int denseCount = Mathf.CeilToInt(splineLength / 0.25f);
            var dense = new List<Vector3>(denseCount);
            for (int i = 0; i < denseCount; i++) dense.Add(spline.EvaluatePosition(i / (float)denseCount));
            b.Center = ResampleClosed(dense, 1f);
            b.Coarse = ResampleClosed(dense, 2f);
            var pathPoints = ResampleClosed(dense, PathSpacing);
            b.Right = new List<Vector3>(b.Center.Count);
            b.Dist = new List<float>(b.Center.Count);
            int n = b.Center.Count;
            float minRadius = float.MaxValue;
            Vector3 minRadiusAt = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Vector3.ProjectOnPlane(b.Center[(i + 1) % n] - b.Center[(i - 1 + n) % n], Vector3.up).normalized;
                b.Right.Add(Vector3.Cross(Vector3.up, dir));
                b.Dist.Add(b.Length);
                b.Length += Vector3.Distance(b.Center[i], b.Center[(i + 1) % n]);
            }
            ApplyWidthsAndBanks(b);
            for (int i = 0; i < n; i++)
            {
                // Turning radius over 6 m: the shoulders fold over themselves below ShoulderWidth + road.
                float angle = Vector3.Angle(b.Right[(i - 3 + n) % n], b.Right[(i + 3) % n]) * Mathf.Deg2Rad;
                if (angle > 1e-4f && 6f / angle < minRadius) { minRadius = 6f / angle; minRadiusAt = b.Center[i]; }
            }

            Vector2 lo = new(float.MaxValue, float.MaxValue), hi = new(float.MinValue, float.MinValue);
            foreach (var p in b.Center) { lo = Vector2.Min(lo, new Vector2(p.x, p.z)); hi = Vector2.Max(hi, new Vector2(p.x, p.z)); }
            b.Bounds = Rect.MinMaxRect(lo.x - WorldMargin, lo.y - WorldMargin, hi.x + WorldMargin, hi.y + WorldMargin);

            var rootGo = new GameObject("Track"); // RaceManager turns off shadows under "Track" on low graphics
            b.Root = rootGo.transform;
            b.Mats = design.Materials(b);

            b.Hidden = new bool[n];
            b.NoGround = new bool[n];
            string bridgeInfo = PlaceBridge(b, pathPoints);
            string gapInfo = MarkGaps(b);
            int roadTris = BuildRoad(b);
            int groundTris = BuildGround(b);
            BuildLake(b);
            BuildTrackWalls(b);
            Physics.SyncTransforms();

            var pathGo = new GameObject("TrackPath");
            var path = pathGo.AddComponent<TrackPath>();
            path.points = pathPoints.ToArray();
            path.roadHalfWidth = design.HalfWidth;
            path.halfWidths = new float[pathPoints.Count];
            for (int k = 0; k < pathPoints.Count; k++) path.halfWidths[k] = b.Width[Mathf.RoundToInt(k * PathSpacing) % n];
            string hazardInfo = BuildHazards(b, path);

            design.Decorate(b);
            int decorTris = b.FlushDecor();
            int propTris = b.FlushProps(out int propCount);

            BuildFinishLine(path, b.Width[0]);
            var zones = PickChaosZones(path, 3, b.ExcludedForChaos);
            string chaosInfo = BuildChaos(path, zones, PickCoinRows(path, zones), $"Rubble_{design.Folder}_");
            AddRaceRig(path);
            var cam = Object.FindFirstObjectByType<KartCamera>().GetComponent<Camera>();
            cam.backgroundColor = design.Sky;

            string scenePath = $"{Root}/Scenes/{design.Scene}.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            SetupBuildSettings();
            return $"{design.Folder}: lap {path.Length:0} m, {path.Count} pts, min radius {minRadius:0.0} m at ({minRadiusAt.x:0},{minRadiusAt.z:0}) | tris road {roadTris}, ground {groundTris}, decor {decorTris}, props {propCount} instances / {propTris} tris | {bridgeInfo} | {gapInfo} | {hazardInfo} | zones {string.Join(",", zones)} | {chaosInfo}";
        }

        /// <summary>Evenly spaced points along a closed polyline (3D distance), starting at its first point.</summary>
        static List<Vector3> ResampleClosed(List<Vector3> dense, float spacing)
        {
            var result = new List<Vector3> { dense[0] };
            float carry = 0f;
            for (int i = 0; i < dense.Count; i++)
            {
                Vector3 a = dense[i], b = dense[(i + 1) % dense.Count];
                float seg = Vector3.Distance(a, b);
                float d = spacing - carry;
                while (d <= seg)
                {
                    result.Add(Vector3.Lerp(a, b, d / seg));
                    d += spacing;
                }
                carry = seg - (d - spacing);
            }
            if (Vector3.Distance(result[^1], result[0]) < spacing * 0.5f) result.RemoveAt(result.Count - 1);
            return result;
        }

        /// <summary>
        /// Sweeps a cross-section (lateral, height) along the centre line. Faces point to the left
        /// of each step through the profile, so a profile running left to right faces up.
        /// UV: u across the profile, v = metres along x <paramref name="vPerMetre"/>; with
        /// <paramref name="worldUV"/> the UV is world XZ / 10 (matches the ground texture).
        /// </summary>
        static void Extrude(TrackBuild b, Vector2[] profile, float vPerMetre, List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            int step = 1, bool worldUV = false, bool[] skip = null)
        {
            int n = b.Center.Count, cols = profile.Length, start = verts.Count;
            var u = new float[cols];
            for (int j = 1; j < cols; j++) u[j] = u[j - 1] + Vector2.Distance(profile[j], profile[j - 1]);
            for (int j = 1; j < cols; j++) u[j] /= Mathf.Max(u[cols - 1], 1e-4f);

            var rows = new List<int>();
            for (int i = 0; i < n; i += step) rows.Add(i);
            rows.Add(n); // closes the loop with its own UVs (no texture seam)
            foreach (int i in rows)
            {
                int k = i % n;
                float v = (i >= n ? b.Length : b.Dist[k]) * vPerMetre;
                for (int j = 0; j < cols; j++)
                {
                    Vector3 p = b.ProfilePoint(k, profile[j]);
                    verts.Add(p);
                    uvs.Add(worldUV ? new Vector2(p.x, p.z) / 10f : new Vector2(u[j], v));
                }
            }
            for (int row = 0; row + 1 < rows.Count; row++)
            {
                if (skip != null && (skip[rows[row] % n] || skip[rows[row + 1] % n])) continue;
                for (int j = 0; j + 1 < cols; j++)
                {
                    int a = start + row * cols + j, c = a + cols;
                    tris.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                }
            }
        }

        static Mesh NewMesh(string name, List<Vector3> verts, List<Vector2> uvs, params List<int>[] submeshes)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Road, candy curbs and grassy shoulders (one mesh, also the collider) plus the striped rails.</summary>
        static int BuildRoad(TrackBuild b)
        {
            float hw = b.Design.HalfWidth, curb = hw + CurbWidth, shoulder = hw + ShoulderWidth;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            List<int> road = new(), curbs = new(), grass = new();
            var roadProfile = new[] { new Vector2(-hw, 0f), new Vector2(hw, 0f) };
            var curbLeft = new[] { new Vector2(-curb, 0.1f), new Vector2(-hw, 0.02f) };
            var curbRight = new[] { new Vector2(hw, 0.02f), new Vector2(curb, 0.1f) };
            Extrude(b, roadProfile, 0.1f, verts, uvs, road, skip: b.Hidden);
            Extrude(b, curbLeft, 1f / 3f, verts, uvs, curbs, skip: b.Hidden);
            Extrude(b, curbRight, 1f / 3f, verts, uvs, curbs, skip: b.Hidden);
            // Shoulders, with a steep skirt that always reaches below the ground (banked turns lift one edge).
            float skirt = shoulder + 2.5f;
            Extrude(b, new[] { new Vector2(-skirt, -4.5f), new Vector2(-shoulder, -ShoulderDrop), new Vector2(-curb, 0.1f) }, 0f, verts, uvs, grass, worldUV: true, skip: b.Hidden);
            Extrude(b, new[] { new Vector2(curb, 0.1f), new Vector2(shoulder, -ShoulderDrop), new Vector2(skirt, -4.5f) }, 0f, verts, uvs, grass, worldUV: true, skip: b.Hidden);
            var mesh = SaveMeshAsset(NewMesh("Road", verts, uvs, road, curbs, grass), $"{b.AssetDir}/Road.asset");
            var go = new GameObject("Road");
            go.transform.SetParent(b.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { b.Mats.Road, b.Mats.Curb, b.Mats.Ground };
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

            // What karts drive on: road + curbs all the way round (also over the bridge, where only the bridge is drawn).
            var colVerts = new List<Vector3>();
            var colUvs = new List<Vector2>();
            var colTris = new List<int>();
            Extrude(b, roadProfile, 0f, colVerts, colUvs, colTris, skip: b.NoGround);
            Extrude(b, curbLeft, 0f, colVerts, colUvs, colTris, skip: b.NoGround);
            Extrude(b, curbRight, 0f, colVerts, colUvs, colTris, skip: b.NoGround);
            var colMesh = SaveMeshAsset(NewMesh("RoadCollider", colVerts, colUvs, colTris), $"{b.AssetDir}/RoadCollider.asset");
            var colGo = new GameObject("RoadCollider");
            colGo.transform.SetParent(b.Root, false);
            colGo.AddComponent<MeshCollider>().sharedMesh = colMesh;

            // Rails: a low striped bar on each curb; each face extruded on its own for crisp edges.
            float inner = curb, outer = curb + RailWidth, top = RailHeight;
            var railVerts = new List<Vector3>();
            var railUvs = new List<Vector2>();
            var railTris = new List<int>();
            var faces = new[]
            {
                new[] { new Vector2(inner, 0.05f), new Vector2(inner, top) },
                new[] { new Vector2(inner, top), new Vector2(outer, top) },
                new[] { new Vector2(outer, top), new Vector2(outer, 0.05f) },
                new[] { new Vector2(-outer, 0.05f), new Vector2(-outer, top) },
                new[] { new Vector2(-outer, top), new Vector2(-inner, top) },
                new[] { new Vector2(-inner, top), new Vector2(-inner, 0.05f) },
            };
            foreach (var face in faces) Extrude(b, face, 1f / 3f, railVerts, railUvs, railTris, skip: b.Hidden);
            var railMesh = SaveMeshAsset(NewMesh("Rails", railVerts, railUvs, railTris), $"{b.AssetDir}/Rails.asset");
            var rails = new GameObject("Rails");
            rails.transform.SetParent(b.Root, false);
            rails.AddComponent<MeshFilter>().sharedMesh = railMesh;
            rails.AddComponent<MeshRenderer>().sharedMaterial = b.Mats.Curb;
            GameObjectUtility.SetStaticEditorFlags(rails, StaticEditorFlags.BatchingStatic);
            return (road.Count + curbs.Count + grass.Count + railTris.Count) / 3;
        }

        /// <summary>
        /// Invisible walls just past the rails, all the way round and 7 m tall (karts can't leave
        /// the road, not even flying off a jump). Solid boxes 1.5 m thick, overlapping, instead of
        /// a thin mesh: a kart squeezed against one (e.g. by a gummy bear) is always pushed back
        /// onto the road, never out through it.
        /// </summary>
        static void BuildTrackWalls(TrackBuild b)
        {
            const float step = 4f, thickness = 1.5f, height = 7f;
            int n = b.Center.Count;
            var walls = new GameObject("TrackWalls");
            int count = 0;
            for (int i = 0; i < n; i += (int)step)
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = b.Width[i] + CurbWidth + 0.1f + thickness * 0.5f; // inner face just inside the rail
                    b.Frame(i, out var right, out var up);
                    Vector3 forward = Vector3.Cross(right, up);
                    var box = new GameObject("TrackWalls");
                    box.transform.SetParent(walls.transform, false);
                    box.transform.SetPositionAndRotation(b.Center[i] + right * (side * x) + up * (height * 0.5f - 1f), Quaternion.LookRotation(forward, up));
                    box.AddComponent<BoxCollider>().size = new Vector3(thickness, height, step + 1.2f);
                    count++;
                }
            GameObjectUtility.SetStaticEditorFlags(walls, StaticEditorFlags.BatchingStatic);
        }

        /// <summary>A height-field ground over the whole world, with a collider (in case a kart flies out).</summary>
        static int BuildGround(TrackBuild b)
        {
            int nx = Mathf.CeilToInt(b.Bounds.width / GroundCell) + 1, nz = Mathf.CeilToInt(b.Bounds.height / GroundCell) + 1;
            var verts = new List<Vector3>(nx * nz);
            var uvs = new List<Vector2>(nx * nz);
            var tris = new List<int>();
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    float px = b.Bounds.xMin + x * GroundCell, pz = b.Bounds.yMin + z * GroundCell;
                    verts.Add(new Vector3(px, b.GroundHeight(px, pz), pz));
                    uvs.Add(new Vector2(px, pz) / 10f);
                }
            for (int z = 0; z + 1 < nz; z++)
                for (int x = 0; x + 1 < nx; x++)
                {
                    int a = z * nx + x, c = a + nx;
                    tris.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                }
            var mesh = SaveMeshAsset(NewMesh("Ground", verts, uvs, tris), $"{b.AssetDir}/Ground.asset");
            var go = new GameObject("Ground");
            go.transform.SetParent(b.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = b.Mats.Ground;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return tris.Count / 3;
        }

        /// <summary>
        /// Ralph's stretches: the straightest, flattest spots, spread around the lap and away
        /// from the start grid.
        /// </summary>
        static int[] PickChaosZones(TrackPath path, int count, System.Func<float, bool> excluded = null)
        {
            Vector3 Flat(Vector3 v) => Vector3.ProjectOnPlane(v, Vector3.up);
            var candidates = new List<(int seg, float score)>();
            for (int i = 0; i < path.Count; i++)
            {
                float d = path.DistanceAt(i);
                if (d < 40f || d > path.Length - 60f) continue;
                if (excluded != null && (excluded(d - 12f) || excluded(d) || excluded(d + 12f))) continue;
                float bend = Vector3.Angle(Flat(path.Direction(i - 3)), Flat(path.Direction(i + 3)));
                float slope = Mathf.Abs(path.Point(i + 3).y - path.Point(i - 3).y);
                candidates.Add((i, bend + slope * 10f));
            }
            candidates.Sort((x, y) => x.score.CompareTo(y.score));
            var picked = new List<int>();
            float separation = path.Length / (count + 2);
            foreach (var (seg, _) in candidates)
            {
                if (picked.Count == count) break;
                bool farEnough = true;
                foreach (int other in picked)
                {
                    float gap = Mathf.Abs(path.DistanceAt(seg) - path.DistanceAt(other));
                    if (Mathf.Min(gap, path.Length - gap) < separation) farEnough = false;
                }
                if (farEnough) picked.Add(seg);
            }
            picked.Sort();
            return picked.ToArray();
        }

        /// <summary>Eight pairs of candies spread over the lap, sides alternating, clear of Ralph's stretches and the grid.</summary>
        static (int seg, float lateral)[] PickCoinRows(TrackPath path, int[] zones)
        {
            var rows = new List<(int, float)>();
            const int pairs = 8;
            for (int k = 0; k < pairs; k++)
            {
                float d = path.Length * (k + 0.5f) / pairs;
                foreach (int z in zones)
                    if (Mathf.Abs(d - path.DistanceAt(z)) < 25f) d = path.DistanceAt(z) + 30f;
                if (d > path.Length - 50f) d = path.Length - 50f;
                rows.Add((path.SegmentAtDistance(d), k % 2 == 0 ? -2.8f : 2.8f));
            }
            return rows.ToArray();
        }

        /// <summary>Centre sample nearest an XZ spot.</summary>
        static int NearestSample(TrackBuild b, Vector2 xz)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < b.Center.Count; i++)
            {
                float d = (new Vector2(b.Center[i].x, b.Center[i].z) - xz).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Width and banking per centre sample: knot values blended along the lap and smoothed;
        /// banking leans into the turn (outside edge up) and fades out on straights.
        /// </summary>
        static void ApplyWidthsAndBanks(TrackBuild b)
        {
            var d = b.Design;
            int n = b.Center.Count, knots = d.Knots.Length;
            var at = new int[knots];
            for (int k = 0; k < knots; k++) at[k] = NearestSample(b, new Vector2(d.Knots[k].x, d.Knots[k].z));
            float[] Blend(float[] values, float fallback)
            {
                var result = new float[n];
                for (int i = 0; i < n; i++) result[i] = fallback;
                if (values == null) return result;
                for (int k = 0; k < knots; k++)
                {
                    int a = at[k], len = ((at[(k + 1) % knots] - a) % n + n) % n;
                    if (len == 0) len = n;
                    for (int j = 0; j < len; j++)
                        result[(a + j) % n] = Mathf.Lerp(values[k], values[(k + 1) % knots], j / (float)len);
                }
                // Smooth over +-10 m.
                var smooth = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float sum = 0f;
                    for (int j = -10; j <= 10; j++) sum += result[(i + j + n) % n];
                    smooth[i] = sum / 21f;
                }
                return smooth;
            }
            b.Width = Blend(d.Widths, d.HalfWidth);
            var bankDeg = Blend(d.Banks, 0f);
            b.Bank = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 before = b.Center[i] - b.Center[(i - 8 + n) % n], after = b.Center[(i + 8) % n] - b.Center[i];
                float turn = Vector3.Cross(Vector3.ProjectOnPlane(before, Vector3.up).normalized, Vector3.ProjectOnPlane(after, Vector3.up).normalized).y;
                // Right turn (turn > 0): the right side is the inside, so it goes down.
                float lean = -Mathf.Sign(turn) * Mathf.Clamp01(Mathf.Abs(turn) / 0.17f);
                b.Bank[i] = bankDeg[i] * lean * Mathf.Deg2Rad;
            }
        }

        /// <summary>Jump gaps: from each lip the road (and what karts drive on) stops for the gap length.</summary>
        static string MarkGaps(TrackBuild b)
        {
            var d = b.Design;
            if (d.Gaps == null) return "gaps 0";
            int n = b.Center.Count;
            foreach (var (lip, length) in d.Gaps)
            {
                int start = NearestSample(b, lip);
                for (int k = 1; k <= Mathf.RoundToInt(length); k++)
                {
                    int i = (start + k) % n;
                    b.Hidden[i] = true;
                    b.NoGround[i] = true;
                }
                // The lip and the landing: no hazards where karts are in the air or just came down blind.
                b.Busy.Add(b.Dist[start]);
                b.Busy.Add(Mathf.Repeat(b.Dist[start] + length + 20f, b.Length));
            }
            return $"gaps {d.Gaps.Length}";
        }

        /// <summary>
        /// Sticky caramel puddles (one side of the road, the other stays free) and giant gummy
        /// bears sitting on the road edge with real colliders. Both go into a TrackHazards, which
        /// slows karts in caramel and tells the AI where to steer.
        /// </summary>
        static string BuildHazards(TrackBuild b, TrackPath path)
        {
            var d = b.Design;
            int n = b.Center.Count;
            var spots = new List<TrackHazards.Spot>();
            var root = new GameObject("TrackHazards");
            var hazards = root.AddComponent<TrackHazards>();
            hazards.path = path;

            // Slide a spot forward until it is clear of the start, the bridge, the gaps and other hazards.
            int Free(float fraction, float range)
            {
                int i = Mathf.FloorToInt(fraction * n) % n;
                for (int tries = 0; tries < 200; tries++, i = (i + 3) % n)
                {
                    float dist = b.Dist[i];
                    if (dist < 50f || dist > b.Length - 30f) continue;
                    if (b.HiddenAtDistance(dist) || b.BusyNear(dist, range)) continue;
                    return i;
                }
                return -1;
            }

            // Caramel puddles: irregular glossy blobs lying on the (maybe banked) road.
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int puddles = 0;
            if (d.Puddles != null)
                foreach (float f in d.Puddles)
                {
                    int i = Free(f, 22f);
                    if (i < 0) continue;
                    float side = puddles % 2 == 0 ? 1f : -1f;
                    float radius = b.R(2.1f, 2.6f);
                    float lateral = side * b.Width[i] * 0.35f;
                    Vector3 centre = b.RoadPoint(i, lateral, 0.035f);
                    b.Frame(i, out var right, out var up);
                    Vector3 forward = Vector3.Cross(right, up);
                    int c0 = verts.Count;
                    verts.Add(centre);
                    uvs.Add(Vector2.zero);
                    const int sides = 18;
                    float wobble = b.R(0f, 6f);
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = k / (float)sides * Mathf.PI * 2f;
                        float r = radius * (1f + 0.16f * Mathf.Sin(a * 3f + wobble) + 0.08f * Mathf.Sin(a * 5f + wobble * 2f));
                        verts.Add(centre + (right * Mathf.Cos(a) * 1.15f + forward * Mathf.Sin(a)) * r);
                        uvs.Add(Vector2.zero);
                    }
                    for (int k = 0; k < sides; k++) tris.AddRange(new[] { c0, c0 + 2 + k, c0 + 1 + k });
                    spots.Add(new TrackHazards.Spot { center = centre, radius = radius * 1.05f, distance = b.Dist[i], lateral = lateral, sticky = true });
                    b.Busy.Add(b.Dist[i]);
                    puddles++;
                }
            if (verts.Count > 0)
            {
                var mesh = SaveMeshAsset(NewMesh("Caramel", verts, uvs, tris), $"{b.AssetDir}/Caramel.asset");
                var caramel = GetMaterial($"{b.MaterialDir}/Caramel", "Universal Render Pipeline/Lit", new Color(1f, 0.68f, 0.3f));
                caramel.SetFloat("_Smoothness", 0.95f);
                caramel.EnableKeyword("_EMISSION");
                caramel.SetColor("_EmissionColor", new Color(0.35f, 0.18f, 0.04f));
                caramel.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                var go = new GameObject("CaramelPuddles");
                go.transform.SetParent(b.Root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = caramel;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Giant gummy bears sitting on the edge, a good chunk of them on the road.
            int bears = 0;
            var bear = CodeGummyBear(false);
            var eyes = PropMaterial(b, "BearEyes", new Color(0.25f, 0.12f, 0.15f), null, 0.9f);
            if (d.EdgeBears != null)
                foreach (float f in d.EdgeBears)
                {
                    int i = Free(f, 20f);
                    while (i >= 0 && b.Width[i] < 5f) i = Free((b.Dist[i] + 25f) / b.Length, 20f); // not in the narrow parts
                    if (i < 0) continue;
                    float side = bears % 2 == 0 ? -1f : 1f;
                    float height = 4.2f;
                    var size = bear.bounds.size * height;
                    float lateral = side * (b.Width[i] - 0.4f);
                    Vector3 pos = b.RoadPoint(i, lateral) - Vector3.up * 0.1f;
                    var rot = Quaternion.LookRotation(-b.Right[i] * side, Vector3.up) * Quaternion.Euler(0f, b.R(-25f, 25f), 0f);
                    var color = GummyColors[(bears * 2 + 1) % GummyColors.Length];
                    b.AddProp(bear, new[] { PropMaterial(b, $"EdgeBear{bears}", color, null, 0.85f, 0.18f), eyes }, Matrix4x4.TRS(pos, rot, Vector3.one * height));
                    // Collider lined up with the road and reaching past the wall: no gap where a kart
                    // could wedge itself between the bear and the wall (and get squeezed out of the track).
                    float inner = b.Width[i] - 1.5f, outer = b.Width[i] + CurbWidth + 0.6f;
                    var col = new GameObject($"GummyBearObstacle{bears}");
                    col.transform.SetParent(root.transform, false);
                    b.Frame(i, out var roadRight, out var roadUp);
                    col.transform.SetPositionAndRotation(b.RoadPoint(i, side * (inner + outer) * 0.5f),
                        Quaternion.LookRotation(Vector3.Cross(roadRight, roadUp), roadUp));
                    var box = col.AddComponent<BoxCollider>();
                    box.size = new Vector3(outer - inner, size.y * 0.9f, size.z * 0.8f);
                    box.center = new Vector3(0f, size.y * 0.45f, 0f);
                    spots.Add(new TrackHazards.Spot { center = pos, radius = Mathf.Max(size.x, size.z) * 0.45f, distance = b.Dist[i], lateral = lateral, sticky = false });
                    b.Busy.Add(b.Dist[i]);
                    bears++;
                }
            hazards.spots = spots.ToArray();
            return $"puddles {puddles}, edge bears {bears}";
        }

        /// <summary>
        /// The Japanese bridge carries the road: stretched to the road width and the design length,
        /// its deck heights (raycast through a temporary collider, smoothed) become the road heights
        /// there, and the road is not drawn under it (it stays as the collider).
        /// </summary>
        static string PlaceBridge(TrackBuild b, List<Vector3> pathPoints)
        {
            var d = b.Design;
            var mesh = PropImport.Load("JapaneseBridge");
            if (d.BridgeLength <= 0f || !mesh) return "bridge none";
            int n = b.Center.Count;
            int ic = 0;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float dd = (new Vector2(b.Center[i].x, b.Center[i].z) - d.BridgeNear).sqrMagnitude;
                if (dd < best) { best = dd; ic = i; }
            }
            int half = Mathf.RoundToInt(d.BridgeLength * 0.5f);
            Vector3 c = b.Center[ic];
            Vector3 forward = Vector3.Cross(b.Right[ic], Vector3.up);
            float baseY = Mathf.Min(b.Center[(ic - half + n) % n].y, b.Center[(ic + half) % n].y);
            // Railings just outside the curbs (the source is ~0.9 wide x 1 tall x 2.6 long).
            var size = mesh.bounds.size;
            float width = 2f * (b.Width[ic] + CurbWidth + 0.6f);
            var matrix = Matrix4x4.TRS(new Vector3(c.x, baseY - 0.15f, c.z), Quaternion.LookRotation(forward, Vector3.up),
                new Vector3(width / size.x, d.BridgeHeight, d.BridgeLength / size.z));

            var probe = new GameObject("BridgeProbe");
            probe.transform.SetPositionAndRotation(matrix.GetColumn(3), matrix.rotation);
            probe.transform.localScale = matrix.lossyScale;
            probe.AddComponent<MeshCollider>().sharedMesh = mesh;
            Physics.SyncTransforms();
            float Deck(Vector3 p, float fallback)
            {
                var hits = Physics.RaycastAll(p + Vector3.up * 30f, Vector3.down, 60f);
                float top = float.MinValue;
                foreach (var h in hits) if (h.collider.gameObject == probe) top = Mathf.Max(top, h.point.y);
                return top > float.MinValue ? top : fallback;
            }

            // Deck heights along the span, smoothed over the steps, blended in at both ends.
            var raw = new float[2 * half + 1];
            for (int k = -half; k <= half; k++) raw[k + half] = Deck(b.Center[(ic + k + n) % n], b.Center[(ic + k + n) % n].y);
            for (int k = -half; k <= half; k++)
            {
                float sum = 0f;
                int count = 0;
                for (int j = Mathf.Max(-half, k - 2); j <= Mathf.Min(half, k + 2); j++) { sum += raw[j + half]; count++; }
                int i = (ic + k + n) % n;
                float edge = Mathf.InverseLerp(half, half - 3, Mathf.Abs(k));
                var p = b.Center[i];
                p.y = Mathf.Lerp(p.y, Mathf.Max(p.y, sum / count + 0.03f), edge);
                b.Center[i] = p;
                b.Hidden[i] = Mathf.Abs(k) < half;
            }
            // The racing line follows (path points are every PathSpacing metres from the same start).
            for (int k = 0; k < pathPoints.Count; k++)
            {
                int i = Mathf.RoundToInt(k * PathSpacing) % n;
                int offset = Mathf.Abs(((i - ic) % n + n + n / 2) % n - n / 2);
                if (offset > half) continue;
                var p = pathPoints[k];
                p.y = b.Center[i].y;
                pathPoints[k] = p;
            }
            Object.DestroyImmediate(probe);

            var mats = new[]
            {
                PropMaterial(b, "BridgeKnobs", new Color(0.62f, 0.85f, 1f)),
                PropMaterial(b, "BridgeRails", new Color(1f, 0.66f, 0.8f)),
                PropMaterial(b, "BridgeDeck", new Color(0.98f, 0.86f, 0.66f)),
            };
            b.AddProp(mesh, mats, matrix);
            b.Busy.Add(b.Dist[ic]);
            float peak = float.MinValue;
            foreach (float r in raw) peak = Mathf.Max(peak, r);
            return $"bridge at {ic} m, deck rise {peak - baseY:0.0} m";
        }

        /// <summary>Flat chocolate discs at the lake level (the ground shore hides their edges).</summary>
        static void BuildLake(TrackBuild b)
        {
            var d = b.Design;
            if (d.Lake == null || d.Lake.Length == 0) return;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            const int sides = 40;
            foreach (var c in d.Lake)
            {
                int centre = verts.Count;
                verts.Add(new Vector3(c.x, d.LakeLevel, c.y));
                uvs.Add(Vector2.zero);
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    verts.Add(new Vector3(c.x + Mathf.Cos(a) * (c.z + 4f), d.LakeLevel, c.y + Mathf.Sin(a) * (c.z + 4f)));
                    uvs.Add(Vector2.zero);
                }
                for (int i = 0; i < sides; i++) tris.AddRange(new[] { centre, centre + 2 + i, centre + 1 + i });
            }
            var mesh = SaveMeshAsset(NewMesh("Lake", verts, uvs, tris), $"{b.AssetDir}/Lake.asset");
            var mat = GetMaterial($"{b.MaterialDir}/Chocolate", "Universal Render Pipeline/Lit", new Color(0.55f, 0.34f, 0.24f));
            mat.SetFloat("_Smoothness", 0.92f);
            var go = new GameObject("ChocolateLake");
            go.transform.SetParent(b.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Lit material for instanced props (pastel colour, optional texture).</summary>
        static Material PropMaterial(TrackBuild b, string name, Color color, Texture texture = null, float smoothness = 0.55f, float emission = 0f)
        {
            var m = GetMaterial($"{b.MaterialDir}/Props/{name}", "Universal Render Pipeline/Lit", color, texture);
            m.SetFloat("_Smoothness", smoothness);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>One material per submesh, in the flat pastel colours baked into the prop (ColorSlots).</summary>
        static Material[] SlotMaterials(TrackBuild b, string id, Mesh mesh, float smoothness = 0.55f)
        {
            var mats = new Material[mesh.subMeshCount];
            for (int s = 0; s < mats.Length; s++) mats[s] = PropMaterial(b, $"{id}_{s}", PropImport.SlotColor(mesh, s), null, smoothness);
            return mats;
        }

        // ---------------------------------------------------------------- Shapes for decoration

        /// <summary>UV sphere of radius 0.5.</summary>
        static Mesh LowSphere(int lon, int lat)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int j = 0; j <= lat; j++)
            {
                float th = j / (float)lat * Mathf.PI;
                for (int i = 0; i <= lon; i++)
                {
                    float ph = i / (float)lon * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * 0.5f);
                    uvs.Add(new Vector2(i / (float)lon, 1f - j / (float)lat));
                }
            }
            for (int j = 0; j < lat; j++)
                for (int i = 0; i < lon; i++)
                {
                    int a = j * (lon + 1) + i, c = a + lon + 1;
                    tris.AddRange(new[] { a, a + 1, c, a + 1, c + 1, c });
                }
            return NewMesh("Sphere", verts, uvs, tris);
        }

        /// <summary>Cylinder of radius 0.5 from y = 0 to 1, capped (caps have planar UVs for swirls).</summary>
        static Mesh LowCylinder(int sides)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, 1f, Mathf.Sin(a) * 0.5f));
                verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f));
                uvs.Add(new Vector2(i / (float)sides, 1f));
                uvs.Add(new Vector2(i / (float)sides, 0f));
            }
            for (int i = 0; i < sides; i++)
            {
                int t = i * 2, bt = t + 1;
                tris.AddRange(new[] { t, t + 2, bt, t + 2, bt + 2, bt });
            }
            foreach (float y in new[] { 1f, 0f })
            {
                int centre = verts.Count;
                verts.Add(new Vector3(0f, y, 0f));
                uvs.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, y, Mathf.Sin(a) * 0.5f));
                    uvs.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
                }
                for (int i = 0; i < sides; i++)
                {
                    int r = centre + 1 + i;
                    if (y > 0.5f) tris.AddRange(new[] { centre, r + 1, r });
                    else tris.AddRange(new[] { centre, r, r + 1 });
                }
            }
            return NewMesh("Cylinder", verts, uvs, tris);
        }

        /// <summary>A bent tube in the XY plane (radius R around the origin) from angle a0 to a1 — a gummy worm.</summary>
        static Mesh Tube(float radius, float thickness, float a0, float a1, int segments, int sides)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int s = 0; s <= segments; s++)
            {
                float a = Mathf.Lerp(a0, a1, s / (float)segments);
                Vector3 outward = new(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 c = outward * radius;
                for (int k = 0; k <= sides; k++)
                {
                    float beta = k / (float)sides * Mathf.PI * 2f;
                    verts.Add(c + (outward * Mathf.Cos(beta) + Vector3.forward * Mathf.Sin(beta)) * thickness);
                    uvs.Add(new Vector2(s / (float)segments, k / (float)sides));
                }
            }
            for (int s = 0; s < segments; s++)
                for (int k = 0; k < sides; k++)
                {
                    int a = s * (sides + 1) + k, c = a + sides + 1;
                    tris.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                }
            return NewMesh("Tube", verts, uvs, tris);
        }

        // ---------------------------------------------------------------- Gummy Forest look

        static readonly Color[] GummyColors =
        {
            new(1f, 0.27f, 0.38f), new(1f, 0.58f, 0.2f), new(1f, 0.86f, 0.28f), new(0.42f, 0.88f, 0.38f),
            new(0.32f, 0.68f, 1f), new(0.68f, 0.45f, 1f), new(1f, 0.5f, 0.8f),
        };

        static Material GummyMaterial(TrackBuild b, string name, Color color)
        {
            var m = GetMaterial($"{b.MaterialDir}/{name}", "Universal Render Pipeline/Lit", color);
            m.SetFloat("_Smoothness", 0.85f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 0.18f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Materials GummyForestMaterials(TrackBuild b)
        {
            const string lit = "Universal Render Pipeline/Lit";
            // Pink frosting road with a dashed white centre line (one texture repeat = 10 m).
            var roadTex = GetGeneratedTexture("GF_Road", 128, (x, y) =>
            {
                float u = (x + 0.5f) / 128f, v = (y + 0.5f) / 128f;
                float grain = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.06f + (((x * 73 + y * 151) % 17) == 0 ? 0.04f : 0f);
                var c = new Color(0.97f, 0.78f, 0.87f) * (0.96f + grain);
                if (u < 0.025f || u > 0.975f) c = new Color(0.9f, 0.62f, 0.78f);
                if (Mathf.Abs(u - 0.5f) < 0.016f && v < 0.5f) c = Color.white;
                c.a = 1f;
                return c;
            });
            var road = GetMaterial($"{b.MaterialDir}/Road", lit, Color.white, roadTex);
            road.SetFloat("_Smoothness", 0.3f);

            var curbTex = GetGeneratedTexture("GF_Curb", 32, (x, y) => y < 16 ? new Color(1f, 0.33f, 0.5f) : Color.white);
            var curb = GetMaterial($"{b.MaterialDir}/Curb", lit, Color.white, curbTex);
            curb.SetFloat("_Smoothness", 0.7f);

            // Mint sugar grass sprinkled with candy dots (wraps around for tiling).
            const int size = 256;
            var grassPixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.1f + Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.05f;
                    grassPixels[y * size + x] = new Color(0.52f + noise, 0.84f + noise * 0.5f, 0.55f + noise, 1f);
                }
            var dotRng = new System.Random(11);
            var dotColors = new[] { new Color(1f, 0.55f, 0.78f), new Color(1f, 0.9f, 0.45f), new Color(0.55f, 0.75f, 1f), Color.white, new Color(0.8f, 0.6f, 1f) };
            for (int d = 0; d < 170; d++)
            {
                int cx = dotRng.Next(size), cy = dotRng.Next(size);
                var col = dotColors[dotRng.Next(dotColors.Length)];
                bool horizontal = dotRng.Next(2) == 0;
                for (int k = 0; k < 4; k++)
                    for (int w = 0; w < 2; w++)
                    {
                        int px = (cx + (horizontal ? k : w)) % size, py = (cy + (horizontal ? w : k)) % size;
                        grassPixels[py * size + px] = col;
                    }
            }
            var grassTex = GetGeneratedTexture("GF_Grass", size, (x, y) => grassPixels[y * size + x]);
            var ground = GetMaterial($"{b.MaterialDir}/Grass", lit, Color.white, grassTex);
            ground.SetFloat("_Smoothness", 0.15f);

            return new Materials { Road = road, Curb = curb, Ground = ground };
        }

        /// <summary>
        /// The forest: Sugar Rush trees, gummy bears in every colour and candy canes fill it;
        /// half donuts mark the road edges here and there; giant gummy bears and chocolate bunnies
        /// watch the race; marshmallows and gummy rings float in the chocolate lake; cinnamon rolls lie around
        /// like boulders; gummy-worm arches and swirl lollipops for accents. Imported props are
        /// instanced (PropInstancer); the generated arches and lollipops are combined meshes.
        /// </summary>
        static void DecorateGummyForest(TrackBuild b)
        {
            var gummies = new Material[GummyColors.Length];
            for (int i = 0; i < gummies.Length; i++) gummies[i] = GummyMaterial(b, $"Gummy{i}", GummyColors[i]);
            var gummyProps = new Material[GummyColors.Length];
            for (int i = 0; i < gummyProps.Length; i++) gummyProps[i] = PropMaterial(b, $"GummyBear{i}", GummyColors[i], null, 0.85f, 0.18f);
            var stem = GetMaterial($"{b.MaterialDir}/Stem", "Universal Render Pipeline/Lit", new Color(1f, 0.96f, 0.9f));
            // Rainbow swirl for the giant lollipops.
            var swirlColors = new[] { new Color(1f, 0.45f, 0.7f), Color.white, new Color(1f, 0.85f, 0.35f), Color.white, new Color(0.45f, 0.75f, 1f), Color.white };
            var swirlTex = GetGeneratedTexture("GF_Swirl", 256, (x, y) =>
            {
                float dx = (x + 0.5f) / 256f - 0.5f, dy = (y + 0.5f) / 256f - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f);
                float band = Mathf.Repeat(a + r * 3.2f, 1f);
                return swirlColors[Mathf.Min((int)(band * swirlColors.Length), swirlColors.Length - 1)];
            });
            var swirl = GetMaterial($"{b.MaterialDir}/Swirl", "Universal Render Pipeline/Lit", Color.white, swirlTex);
            swirl.SetFloat("_Smoothness", 0.8f);

            var tree = PropImport.Load("SugarTree");
            var canes = PropImport.Load("CandyCanesLow");
            var bear = CodeGummyBear(false);
            var bearLow = CodeGummyBear(true);
            var bearEyes = PropMaterial(b, "BearEyes", new Color(0.25f, 0.12f, 0.15f), null, 0.9f);
            var bunny = PropImport.Load("ChocolateBunny");
            var cinnamon = PropImport.Load("CinnamonDelight");
            var donut = DonutArch("Code_DonutSmall", 1f, 0.55f, 11, 14, 8, 14, 6);
            // Candy-cane trees: their flat branches need both faces; four stripe tints for variety.
            var treeTints = new[] { Color.white, new Color(0.8f, 1f, 0.9f), new Color(1f, 0.93f, 0.75f), new Color(0.9f, 0.85f, 1f) };
            var treeMats = new Material[treeTints.Length][];
            for (int i = 0; i < treeTints.Length; i++)
            {
                var m = PropMaterial(b, $"SugarTree{i}", treeTints[i], PropImport.LoadTexture("SugarTree"), 0.6f);
                m.SetFloat("_Cull", 0f);
                m.doubleSidedGI = true;
                treeMats[i] = new[] { m };
            }
            var caneMat = new[] { PropMaterial(b, "CandyCanes", Color.white, PropImport.LoadTexture("CandyCanesLow"), 0.75f) };
            var bunnyMat = new[] { PropMaterial(b, "ChocolateBunny", new Color(0.8f, 0.58f, 0.45f), null, 0.7f) };
            var cinnamonMat = new[] { PropMaterial(b, "Cinnamon", Color.white, PropImport.LoadTexture("CinnamonDelight"), 0.35f) };


            var cylinder = LowCylinder(10);
            var disc = LowCylinder(18);
            float hw = b.Design.HalfWidth;
            int n = b.Center.Count;
            Quaternion Yaw() => Quaternion.Euler(0f, b.R(0f, 360f), 0f);
            Quaternion Facing(Vector3 from, Vector3 to) => Quaternion.LookRotation(Vector3.ProjectOnPlane(to - from, Vector3.up).normalized, Vector3.up);
            Matrix4x4 Place(Vector3 pos, Quaternion rot, float height) => Matrix4x4.TRS(pos, rot, Vector3.one * height);

            // Gummy-worm arches over the road (two-tone: one material per half).
            int arches = 0;
            foreach (float f in new[] { 0.13f, 0.44f, 0.71f })
            {
                int i = Mathf.FloorToInt(f * n) % n;
                if (b.Hidden != null && b.Hidden[i]) continue;
                Vector3 c = b.Center[i], right = b.Right[i];
                float radius = b.Width[i] + 3.5f;
                var halfA = Tube(radius, 0.9f, -0.15f, Mathf.PI * 0.5f, 14, 10);
                var halfB = Tube(radius, 0.9f, Mathf.PI * 0.5f, Mathf.PI + 0.15f, 14, 10);
                var m = Matrix4x4.TRS(c + Vector3.up * 0.2f, Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up), Vector3.one);
                b.Add(halfA, gummies[arches * 2 % gummies.Length], m);
                b.Add(halfB, gummies[(arches * 2 + 3) % gummies.Length], m);
                b.Occupy(c + right * radius, 3f);
                b.Occupy(c - right * radius, 3f);
                arches++;
            }

            // Marshmallows and gummy rings floating on the chocolate lake.
            int floaters = 0;
            var marshmallow = new[] { GetMaterial($"{b.MaterialDir}/Marshmallow", "Universal Render Pipeline/Lit", new Color(1f, 0.97f, 0.97f)),
                                      GetMaterial($"{b.MaterialDir}/MarshmallowPink", "Universal Render Pipeline/Lit", new Color(1f, 0.8f, 0.88f)) };
            var ring = Tube(1.1f, 0.45f, 0f, Mathf.PI * 2f, 20, 10);
            if (b.Design.Lake != null)
                foreach (var c in b.Design.Lake)
                {
                    if (c.z < 18f) continue; // the narrow arm under the bridge and the river stay clear
                    for (int k = 0; k < 9; k++)
                    {
                        float a = b.R(0f, Mathf.PI * 2f), r = b.R(0f, c.z * 0.75f);
                        var pos = new Vector3(c.x + Mathf.Cos(a) * r, b.Design.LakeLevel, c.y + Mathf.Sin(a) * r);
                        if (k % 3 == 2)
                            b.Add(ring, b.Pick(gummies), Matrix4x4.TRS(pos + Vector3.up * 0.1f, Quaternion.Euler(90f, b.R(0f, 360f), 0f), Vector3.one * b.R(1f, 1.6f)));
                        else
                        {
                            float size = b.R(1.4f, 2.4f);
                            b.Add(cylinder, b.Pick(marshmallow), Matrix4x4.TRS(pos - Vector3.up * size * 0.35f, Quaternion.Euler(b.R(-12f, 12f), b.R(0f, 360f), b.R(-12f, 12f)), new Vector3(size, size * 0.9f, size)));
                        }
                        floaters++;
                    }
                    b.Occupy(new Vector3(c.x, 0f, c.y), c.z + 2f);
                }

            // Half donuts standing on the shoulders every ~32 m, sides alternating (not at the start or over the bridge).
            int donutCount = 0;
            for (int i = 24; i < n - 30; i += 32)
            {
                if (b.Hidden != null && b.Hidden[i]) continue;
                float side = (i / 32) % 2 == 0 ? 1f : -1f;
                Vector3 pos = b.ProfilePoint(i, new Vector2(side * (hw + CurbWidth + RailWidth + 1.6f), -0.35f));
                // The arch stands along the road (its X axis along the driving direction).
                var rot = Quaternion.LookRotation(b.Right[i], Vector3.up) * Quaternion.Euler(0f, b.R(-8f, 8f), 0f);
                b.AddProp(donut, DonutMaterials(b, donutCount), Place(pos, rot, 1.1f), false);
                donutCount++;
            }

            // Landmarks: giant gummy bears and chocolate bunnies watching the race.
            int giants = 0;
            foreach (float f in new[] { 0.06f, 0.2f, 0.33f, 0.52f, 0.63f, 0.81f, 0.93f })
            {
                int i = Mathf.FloorToInt(f * n) % n;
                float side = giants % 2 == 0 ? 1f : -1f;
                Vector3 spot = b.Center[i] + b.Right[i] * side * b.R(24f, 32f);
                if (b.RoadDistance(new Vector2(spot.x, spot.z), out _, out _) < 18f || !b.IsFree(spot, 7f) || b.LakeFactor(spot.x, spot.z) > 0f) continue;
                spot.y = b.GroundHeight(spot.x, spot.z) - 0.2f;
                var rot = Facing(spot, b.Center[i]) * Quaternion.Euler(0f, b.R(-20f, 20f), 0f);
                if (giants % 3 == 2 && bunny) b.AddProp(bunny, bunnyMat, Place(spot, rot, b.R(11f, 14f)));
                else b.AddProp(bear, new[] { gummyProps[(giants * 3 + 1) % gummyProps.Length], bearEyes }, Place(spot, rot, b.R(9f, 12f)));
                b.Occupy(spot, 6f);
                giants++;
            }

            // Giant swirl lollipops facing the road.
            int lollipops = 0;
            for (int i = 0; i < 12; i++)
            {
                if (!b.TryPlace(hw + 10f, 70f, 2.5f, out var p)) continue;
                b.RoadDistance(new Vector2(p.x, p.z), out _, out int idx);
                Vector3 toRoad = Vector3.ProjectOnPlane(b.Coarse[idx] - p, Vector3.up).normalized;
                float h = b.R(5f, 8.5f), d = b.R(3f, 4.5f);
                b.Add(cylinder, stem, Matrix4x4.TRS(p - Vector3.up * 0.5f, Quaternion.identity, new Vector3(0.3f, h + 0.5f, 0.3f)));
                var face = Quaternion.LookRotation(toRoad, Vector3.up) * Quaternion.Euler(90f, 0f, 0f); // cylinder axis towards the road
                b.Add(disc, swirl, Matrix4x4.TRS(p + Vector3.up * h - toRoad * 0.25f, face, new Vector3(d, 0.5f, d)));
                lollipops++;
            }

            // The forest itself: Sugar Rush trees, gummy bears and candy canes, denser near the road.
            int trees = 0, bears = 0, caneCount = 0, rolls = 0;
            for (int i = 0; i < 190 && tree; i++)
            {
                if (!b.TryPlace(hw + 8f, 105f, 2.5f, out var p) || b.LakeFactor(p.x, p.z) > 0f) continue;
                float h = b.R(8f, 14f);
                b.AddProp(tree, b.Pick(treeMats), Matrix4x4.TRS(p - Vector3.up * 0.3f, Yaw(), new Vector3(h * 1.4f, h, h * 1.4f)));
                trees++;
            }
            for (int i = 0; i < 100; i++)
            {
                if (!b.TryPlace(hw + 6f, 80f, 1.4f, out var p) || b.LakeFactor(p.x, p.z) > 0f) continue;
                b.AddProp(bearLow, new[] { b.Pick(gummyProps), bearEyes }, Place(p - Vector3.up * 0.1f, Yaw(), b.R(1.6f, 3.6f)), false);
                bears++;
            }
            for (int i = 0; i < 70 && canes; i++)
            {
                if (!b.TryPlace(hw + 6f, 75f, 1.4f, out var p) || b.LakeFactor(p.x, p.z) > 0f) continue;
                b.AddProp(canes, caneMat, Place(p - Vector3.up * 0.2f, Yaw(), b.R(2.5f, 4.5f)), false);
                caneCount++;
            }
            for (int i = 0; i < 12 && cinnamon; i++)
            {
                if (!b.TryPlace(hw + 9f, 70f, 3f, out var p) || b.LakeFactor(p.x, p.z) > 0f) continue;
                b.AddProp(cinnamon, cinnamonMat, Place(p - Vector3.up * 0.4f, Yaw(), b.R(2f, 4.5f)));
                rolls++;
            }
            Debug.Log($"[SR] {b.Design.Folder} decor: arches {arches}, giants {giants}, lake floaters {floaters}, donuts {donutCount}, lollipops {lollipops}, " +
                      $"trees {trees}, gummy bears {bears}, canes {caneCount}, cinnamon rolls {rolls}");
        }
    }
}
