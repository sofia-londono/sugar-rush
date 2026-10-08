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
            public float HalfWidth = 6f;
            /// <summary>Height of the open ground around the track (the road is at the knots' heights).</summary>
            public float GroundLevel = -1.5f;
            public int Seed = 1;
            public Color Sky = new(0.8f, 0.9f, 1f);
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
        /// "Bosque de gomitas": start straight, a wide right-hander into a climb, a jump off the
        /// hilltop, a fast descent into a hairpin, a wiggly southern stretch and back to the line.
        /// Gummy trees, gumdrops, mushrooms, gummy bears and gummy-worm arches.
        /// </summary>
        static readonly SplineTrack GummyForest = new()
        {
            Id = "gummy_forest",
            Folder = "GummyForest",
            Scene = "GummyForest_Track",
            Seed = 7,
            Knots = new[]
            {
                new Vector3(0f, 0f, 0f),        // start / finish
                new Vector3(0f, 0f, 70f),
                new Vector3(20f, 0.5f, 115f),
                new Vector3(65f, 2f, 130f),
                new Vector3(105f, 5f, 105f),    // climbing
                new Vector3(120f, 9f, 62f),
                new Vector3(113f, 12f, 24f),    // hilltop lip
                new Vector3(111f, 8.5f, 9f),    // steep drop: karts fly off the lip
                new Vector3(125f, 5f, -25f),
                new Vector3(160f, 2f, -50f),
                new Vector3(170f, 1f, -100f),   // hairpin
                new Vector3(140f, 1f, -135f),
                new Vector3(100f, 2f, -120f),
                new Vector3(70f, 3f, -145f),
                new Vector3(30f, 2f, -150f),
                new Vector3(-5f, 1f, -125f),
                new Vector3(-15f, 0f, -80f),
                new Vector3(-5f, 0f, -35f),
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
            readonly List<(Vector3 pos, float radius)> occupied = new();
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
                if (d <= near) return roadY - 1.5f;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - near) / 35f));
                return Mathf.Lerp(roadY - 1.2f, Natural(x, z), k);
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
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Vector3.ProjectOnPlane(b.Center[(i + 1) % n] - b.Center[(i - 1 + n) % n], Vector3.up).normalized;
                b.Right.Add(Vector3.Cross(Vector3.up, dir));
                b.Dist.Add(b.Length);
                b.Length += Vector3.Distance(b.Center[i], b.Center[(i + 1) % n]);
            }
            for (int i = 0; i < n; i++)
            {
                // Turning radius over 6 m: the shoulders fold over themselves below ShoulderWidth + road.
                float angle = Vector3.Angle(b.Right[(i - 3 + n) % n], b.Right[(i + 3) % n]) * Mathf.Deg2Rad;
                if (angle > 1e-4f) minRadius = Mathf.Min(minRadius, 6f / angle);
            }

            Vector2 lo = new(float.MaxValue, float.MaxValue), hi = new(float.MinValue, float.MinValue);
            foreach (var p in b.Center) { lo = Vector2.Min(lo, new Vector2(p.x, p.z)); hi = Vector2.Max(hi, new Vector2(p.x, p.z)); }
            b.Bounds = Rect.MinMaxRect(lo.x - WorldMargin, lo.y - WorldMargin, hi.x + WorldMargin, hi.y + WorldMargin);

            var rootGo = new GameObject("Track"); // RaceManager turns off shadows under "Track" on low graphics
            b.Root = rootGo.transform;
            b.Mats = design.Materials(b);

            int roadTris = BuildRoad(b);
            int groundTris = BuildGround(b);
            BuildTrackWalls(b);
            Physics.SyncTransforms();

            var pathGo = new GameObject("TrackPath");
            var path = pathGo.AddComponent<TrackPath>();
            path.points = pathPoints.ToArray();
            path.roadHalfWidth = design.HalfWidth;

            design.Decorate(b);
            int decorTris = b.FlushDecor();

            BuildFinishLine(path, design.HalfWidth);
            var zones = PickChaosZones(path, 3);
            string chaosInfo = BuildChaos(path, zones, PickCoinRows(path, zones), $"Rubble_{design.Folder}_");
            AddRaceRig(path);
            var cam = Object.FindFirstObjectByType<KartCamera>().GetComponent<Camera>();
            cam.backgroundColor = design.Sky;

            string scenePath = $"{Root}/Scenes/{design.Scene}.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            SetupBuildSettings();
            return $"{design.Folder}: lap {path.Length:0} m, {path.Count} pts, min radius {minRadius:0.0} m | tris road {roadTris}, ground {groundTris}, decor {decorTris} | zones {string.Join(",", zones)} | {chaosInfo}";
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
            int step = 1, bool worldUV = false)
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
                Vector3 c = b.Center[k], r = b.Right[k];
                float v = (i >= n ? b.Length : b.Dist[k]) * vPerMetre;
                for (int j = 0; j < cols; j++)
                {
                    Vector3 p = c + r * profile[j].x + Vector3.up * profile[j].y;
                    verts.Add(p);
                    uvs.Add(worldUV ? new Vector2(p.x, p.z) / 10f : new Vector2(u[j], v));
                }
            }
            for (int row = 0; row + 1 < rows.Count; row++)
                for (int j = 0; j + 1 < cols; j++)
                {
                    int a = start + row * cols + j, c = a + cols;
                    tris.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
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
            Extrude(b, new[] { new Vector2(-hw, 0f), new Vector2(hw, 0f) }, 0.1f, verts, uvs, road);
            Extrude(b, new[] { new Vector2(-curb, 0.1f), new Vector2(-hw, 0.02f) }, 1f / 3f, verts, uvs, curbs);
            Extrude(b, new[] { new Vector2(hw, 0.02f), new Vector2(curb, 0.1f) }, 1f / 3f, verts, uvs, curbs);
            Extrude(b, new[] { new Vector2(-shoulder, -ShoulderDrop), new Vector2(-curb, 0.1f) }, 0f, verts, uvs, grass, worldUV: true);
            Extrude(b, new[] { new Vector2(curb, 0.1f), new Vector2(shoulder, -ShoulderDrop) }, 0f, verts, uvs, grass, worldUV: true);
            var mesh = SaveMeshAsset(NewMesh("Road", verts, uvs, road, curbs, grass), $"{b.AssetDir}/Road.asset");
            var go = new GameObject("Road");
            go.transform.SetParent(b.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { b.Mats.Road, b.Mats.Curb, b.Mats.Ground };
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

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
            foreach (var face in faces) Extrude(b, face, 1f / 3f, railVerts, railUvs, railTris);
            var railMesh = SaveMeshAsset(NewMesh("Rails", railVerts, railUvs, railTris), $"{b.AssetDir}/Rails.asset");
            var rails = new GameObject("Rails");
            rails.transform.SetParent(b.Root, false);
            rails.AddComponent<MeshFilter>().sharedMesh = railMesh;
            rails.AddComponent<MeshRenderer>().sharedMaterial = b.Mats.Curb;
            GameObjectUtility.SetStaticEditorFlags(rails, StaticEditorFlags.BatchingStatic);
            return (road.Count + curbs.Count + grass.Count + railTris.Count) / 3;
        }

        /// <summary>Invisible walls just past the rails, all the way round (karts can't leave the road).</summary>
        static void BuildTrackWalls(TrackBuild b)
        {
            float x = b.Design.HalfWidth + CurbWidth + 0.1f; // just inside the rail
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            Extrude(b, new[] { new Vector2(x, -1f), new Vector2(x, 2.5f) }, 0f, verts, uvs, tris, 2);
            Extrude(b, new[] { new Vector2(-x, 2.5f), new Vector2(-x, -1f) }, 0f, verts, uvs, tris, 2);
            var mesh = SaveMeshAsset(NewMesh("TrackWalls", verts, uvs, tris), $"{b.AssetDir}/TrackWalls.asset");
            var walls = new GameObject("TrackWalls");
            walls.AddComponent<MeshCollider>().sharedMesh = mesh;
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
        static int[] PickChaosZones(TrackPath path, int count)
        {
            Vector3 Flat(Vector3 v) => Vector3.ProjectOnPlane(v, Vector3.up);
            var candidates = new List<(int seg, float score)>();
            for (int i = 0; i < path.Count; i++)
            {
                float d = path.DistanceAt(i);
                if (d < 40f || d > path.Length - 60f) continue;
                float bend = Vector3.Angle(Flat(path.Direction(i - 3)), Flat(path.Direction(i + 3)));
                float slope = Mathf.Abs(path.Point(i + 3).y - path.Point(i - 3).y);
                candidates.Add((i, bend + slope * 10f));
            }
            candidates.Sort((x, y) => x.score.CompareTo(y.score));
            var picked = new List<int>();
            float separation = path.Length / (count + 1);
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

        static void DecorateGummyForest(TrackBuild b)
        {
            var gummies = new Material[GummyColors.Length];
            for (int i = 0; i < gummies.Length; i++) gummies[i] = GummyMaterial(b, $"Gummy{i}", GummyColors[i]);
            var stem = GetMaterial($"{b.MaterialDir}/Stem", "Universal Render Pipeline/Lit", new Color(1f, 0.96f, 0.9f));
            var eyes = GetMaterial($"{b.MaterialDir}/Eyes", "Universal Render Pipeline/Lit", new Color(0.25f, 0.12f, 0.15f));
            eyes.SetFloat("_Smoothness", 0.9f);
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

            var sphere = LowSphere(10, 7);
            var smallSphere = LowSphere(8, 5);
            var cylinder = LowCylinder(10);
            var disc = LowCylinder(18);
            float hw = b.Design.HalfWidth;
            Quaternion Yaw() => Quaternion.Euler(0f, b.R(0f, 360f), 0f);

            // Lookout points for the big landmarks: keep them clear of the forest.
            Vector3 RoadPoint(float fraction, out Vector3 right)
            {
                int i = Mathf.FloorToInt(fraction * b.Center.Count) % b.Center.Count;
                right = b.Right[i];
                return b.Center[i];
            }

            // Gummy-worm arches over the road (two-tone: one material per half).
            int arches = 0;
            foreach (float f in new[] { 0.13f, 0.44f, 0.71f })
            {
                Vector3 c = RoadPoint(f, out var right);
                float radius = hw + 3.5f;
                var halfA = Tube(radius, 0.9f, -0.15f, Mathf.PI * 0.5f, 14, 10);
                var halfB = Tube(radius, 0.9f, Mathf.PI * 0.5f, Mathf.PI + 0.15f, 14, 10);
                // Tube lies in local XY with +X = right of the road.
                var m = Matrix4x4.TRS(c + Vector3.up * 0.2f, Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up), Vector3.one);
                int colour = arches * 2;
                b.Add(halfA, gummies[colour % gummies.Length], m);
                b.Add(halfB, gummies[(colour + 3) % gummies.Length], m);
                b.Occupy(c + right * radius, 3f);
                b.Occupy(c - right * radius, 3f);
                arches++;
            }

            // Giant gummy bears watching the race.
            int bears = 0;
            foreach (float f in new[] { 0.06f, 0.2f, 0.33f, 0.52f, 0.63f, 0.81f, 0.93f })
            {
                Vector3 c = RoadPoint(f, out var right);
                float side = bears % 2 == 0 ? 1f : -1f;
                Vector3 spot = c + right * side * b.R(24f, 32f);
                float d = b.RoadDistance(new Vector2(spot.x, spot.z), out _, out _);
                if (d < 18f || !b.IsFree(spot, 7f)) continue;
                spot.y = b.GroundHeight(spot.x, spot.z) - 0.3f;
                float size = b.R(8f, 12f);
                var facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(c - spot, Vector3.up).normalized, Vector3.up);
                var body = Matrix4x4.TRS(spot, facing * Quaternion.Euler(0f, b.R(-20f, 20f), 0f), Vector3.one * size);
                var mat = gummies[(bears * 3 + 1) % gummies.Length];
                void Part(Mesh mesh, Material material, Vector3 pos, Vector3 scale, float roll = 0f) =>
                    b.Add(mesh, material, body * Matrix4x4.TRS(pos, Quaternion.Euler(0f, 0f, roll), scale));
                Part(sphere, mat, new Vector3(-0.17f, 0.12f, 0.02f), new Vector3(0.26f, 0.3f, 0.26f));
                Part(sphere, mat, new Vector3(0.17f, 0.12f, 0.02f), new Vector3(0.26f, 0.3f, 0.26f));
                Part(sphere, mat, new Vector3(0f, 0.38f, 0f), new Vector3(0.52f, 0.55f, 0.42f));
                Part(sphere, mat, new Vector3(-0.27f, 0.48f, 0.05f), new Vector3(0.17f, 0.3f, 0.17f), -25f);
                Part(sphere, mat, new Vector3(0.27f, 0.48f, 0.05f), new Vector3(0.17f, 0.3f, 0.17f), 25f);
                Part(sphere, mat, new Vector3(0f, 0.78f, 0.02f), new Vector3(0.42f, 0.38f, 0.38f));
                Part(smallSphere, mat, new Vector3(-0.16f, 0.95f, 0f), new Vector3(0.14f, 0.14f, 0.1f));
                Part(smallSphere, mat, new Vector3(0.16f, 0.95f, 0f), new Vector3(0.14f, 0.14f, 0.1f));
                Part(smallSphere, mat, new Vector3(0f, 0.74f, 0.19f), new Vector3(0.18f, 0.13f, 0.12f));
                Part(smallSphere, eyes, new Vector3(-0.08f, 0.83f, 0.18f), Vector3.one * 0.05f);
                Part(smallSphere, eyes, new Vector3(0.08f, 0.83f, 0.18f), Vector3.one * 0.05f);
                Part(smallSphere, eyes, new Vector3(0f, 0.77f, 0.25f), new Vector3(0.06f, 0.04f, 0.04f));
                b.Occupy(spot, size * 0.45f);
                bears++;
            }

            // Giant swirl lollipops facing the road.
            int lollipops = 0;
            for (int i = 0; i < 26; i++)
            {
                if (!b.TryPlace(hw + 9f, 70f, 2.5f, out var p)) continue;
                b.RoadDistance(new Vector2(p.x, p.z), out _, out int idx);
                Vector3 toRoad = Vector3.ProjectOnPlane(b.Coarse[idx] - p, Vector3.up).normalized;
                float h = b.R(5f, 8.5f), d = b.R(3f, 4.5f);
                b.Add(cylinder, stem, Matrix4x4.TRS(p - Vector3.up * 0.5f, Quaternion.identity, new Vector3(0.3f, h + 0.5f, 0.3f)));
                var face = Quaternion.LookRotation(toRoad, Vector3.up) * Quaternion.Euler(90f, 0f, 0f); // cylinder axis towards the road
                b.Add(disc, swirl, Matrix4x4.TRS(p + Vector3.up * h - toRoad * 0.25f, face, new Vector3(d, 0.5f, d)));
                lollipops++;
            }

            // Gummy trees: a round gummy crown (sometimes two) on a cream stick.
            int trees = 0;
            for (int i = 0; i < 150; i++)
            {
                if (!b.TryPlace(hw + 7f, 95f, 2.2f, out var p)) continue;
                float h = b.R(4f, 9f), crown = b.R(3f, 5.5f);
                var mat = b.Pick(gummies);
                b.Add(cylinder, stem, Matrix4x4.TRS(p - Vector3.up * 0.5f, Yaw(), new Vector3(0.45f, h + 0.5f, 0.45f)));
                b.Add(sphere, mat, Matrix4x4.TRS(p + Vector3.up * (h + crown * 0.3f), Yaw(), new Vector3(crown, crown * 0.9f, crown)));
                if (b.R(0f, 1f) < 0.25f)
                    b.Add(sphere, b.Pick(gummies), Matrix4x4.TRS(p + Vector3.up * (h + crown * 0.95f), Yaw(), Vector3.one * crown * 0.6f));
                trees++;
            }

            // Gummy mushrooms in little clusters.
            int mushrooms = 0;
            for (int i = 0; i < 18; i++)
            {
                if (!b.TryPlace(hw + 6f, 60f, 3f, out var centre)) continue;
                int count = b.Rng.Next(2, 4);
                var capMat = b.R(0f, 1f) < 0.6f ? gummies[0] : gummies[6];
                for (int k = 0; k < count; k++)
                {
                    Vector3 p = centre + new Vector3(b.R(-2.2f, 2.2f), 0f, b.R(-2.2f, 2.2f));
                    p.y = b.GroundHeight(p.x, p.z);
                    float s = b.R(1.2f, 2.6f);
                    b.Add(cylinder, stem, Matrix4x4.TRS(p - Vector3.up * 0.3f, Quaternion.identity, new Vector3(s * 0.45f, s + 0.3f, s * 0.45f)));
                    b.Add(sphere, capMat, Matrix4x4.TRS(p + Vector3.up * s, Yaw(), new Vector3(s * 1.5f, s * 0.8f, s * 1.5f)));
                    mushrooms++;
                }
            }

            // Gumdrops scattered close to the road.
            int gumdrops = 0;
            for (int i = 0; i < 110; i++)
            {
                if (!b.TryPlace(hw + 5.5f, 55f, 1.4f, out var p)) continue;
                float s = b.R(1.4f, 3.2f);
                b.Add(smallSphere, b.Pick(gummies), Matrix4x4.TRS(p + Vector3.up * s * 0.2f, Yaw(), new Vector3(s, s * 0.9f, s)));
                gumdrops++;
            }
            Debug.Log($"[SR] {b.Design.Folder} decor: arches {arches}, bears {bears}, lollipops {lollipops}, trees {trees}, mushrooms {mushrooms}, gumdrops {gumdrops}");
        }
    }
}
