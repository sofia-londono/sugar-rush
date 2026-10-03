using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SugarRush.EditorTools
{
    /// <summary>
    /// One-click builders for the imported Sketchfab assets: kart materials and prefabs,
    /// track import settings, and the playable track scene.
    /// </summary>
    public static class SugarRushSetup
    {
        const string Root = "Assets/_SugarRush";
        const string KartFbx = Root + "/Art/Karts/SRstorybookracers.fbx";
        const string KartTextures = Root + "/Art/Karts/Textures";
        const string TrackFbx = Root + "/Art/Track/map_tgsd.fbx";
        const string TrackTextures = Root + "/Art/Track/Textures";
        const string MaterialsDir = Root + "/Materials";
        const string PrefabsDir = Root + "/Prefabs";
        const string ScenePath = Root + "/Scenes/SugarRush_Track.unity";

        public const float TrackScale = 40f;
        const string RoadColliderMesh = "raod_tgsd_001";
        const string MiniMapMesh = "mini_map_road";
        const string SkyMesh = "sky_tgsd";
        const int IgnoreRaycastLayer = 2;

        // Node suffix in the FBX -> character, texture prefix, FBX material names (body, spoiler, wheel)
        static readonly (string name, string suffix, string tex, string body, string spoiler, string wheel)[] Karts =
        {
            ("Vanellope",   "",     "vanellope",   "Material.015", "Material.014", "Material.013"),
            ("Taffyta",     ".001", "taffyta",     "Material.005", "Material.016", "Material.006"),
            ("Adorabeezle", ".002", "adorableeze", "Material.001", "Material.002", "Material.003"),
            ("Rancis",      ".003", "rancis",      "Material.007", "Material.009", "Material.008"),
            ("Candlehead",  ".004", "candlehead",  "Material.010", "Material.011", "Material.012"),
        };

        [MenuItem("Sugar Rush/Build Everything")]
        public static string BuildAll()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine(SetupKartMaterials());
            log.AppendLine(BuildKartPrefabs());
            log.AppendLine(SetupTrackImport());
            log.AppendLine(BuildTrackScene());
            return log.ToString();
        }

        // ---------------------------------------------------------------- Karts

        [MenuItem("Sugar Rush/1. Kart Materials")]
        public static string SetupKartMaterials()
        {
            EnsureFolder(MaterialsDir + "/Karts");
            var importer = (ModelImporter)AssetImporter.GetAtPath(KartFbx);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            int count = 0;

            foreach (var k in Karts)
            {
                foreach (var (part, fbxMat) in new[] { ("body", k.body), ("spoiler", k.spoiler), ("wheel", k.wheel) })
                {
                    string matPath = $"{MaterialsDir}/Karts/{k.name}_{part}.mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (!mat)
                    {
                        mat = new Material(shader);
                        AssetDatabase.CreateAsset(mat, matPath);
                    }

                    var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KartTextures}/{k.tex}_{part}_diffusespec.png");
                    mat.SetTexture("_BaseMap", albedo);
                    mat.SetColor("_BaseColor", Color.white);
                    mat.SetFloat("_Smoothness", 0.45f);
                    mat.SetFloat("_Metallic", 0f);

                    string normalPath = $"{KartTextures}/{k.tex}_{part}_normal.png";
                    if (File.Exists(normalPath))
                    {
                        var ti = (TextureImporter)AssetImporter.GetAtPath(normalPath);
                        if (ti.textureType != TextureImporterType.NormalMap)
                        {
                            ti.textureType = TextureImporterType.NormalMap;
                            ti.SaveAndReimport();
                        }
                        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                        mat.EnableKeyword("_NORMALMAP");
                    }
                    EditorUtility.SetDirty(mat);

                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), fbxMat), mat);
                    count++;
                }
            }

            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            return $"Kart materials: {count} remapped";
        }

        [MenuItem("Sugar Rush/2. Kart Prefabs")]
        public static string BuildKartPrefabs()
        {
            EnsureFolder(PrefabsDir);
            var physicMat = GetKartPhysicsMaterial();
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(KartFbx);
            var log = new System.Text.StringBuilder("Kart prefabs:");

            foreach (var k in Karts)
            {
                var temp = Object.Instantiate(fbx);
                Transform Part(string n) => temp.transform.Find(n + k.suffix);

                var body = Part("Base");
                var spoiler = Part("Spoiler");
                var wheelParts = new[] { ("FL", true), ("FR", true), ("BL", false), ("BR", false) };

                float bottom = float.MaxValue, radiusSum = 0f;
                foreach (var (w, _) in wheelParts)
                {
                    var b = Part(w).GetComponent<Renderer>().bounds;
                    bottom = Mathf.Min(bottom, b.min.y);
                    radiusSum += Mathf.Max(b.extents.y, b.extents.z);
                }
                float wheelRadius = radiusSum / wheelParts.Length;
                var bodyBounds = body.GetComponent<Renderer>().bounds;

                var root = new GameObject("Kart_" + k.name);
                root.transform.position = new Vector3(bodyBounds.center.x, bottom, bodyBounds.center.z);
                var model = new GameObject("Model").transform;
                model.SetParent(root.transform, false);
                body.SetParent(model, true);
                body.name = "Body";
                if (spoiler) { spoiler.SetParent(model, true); spoiler.name = "Spoiler"; }

                var visualWheels = new List<KartVisuals.Wheel>();
                var anchors = new List<Vector3>();
                foreach (var (w, front) in wheelParts)
                {
                    var mesh = Part(w);
                    var center = mesh.GetComponent<Renderer>().bounds.center;
                    var pivot = new GameObject("Wheel_" + w).transform;
                    pivot.position = center;
                    pivot.SetParent(model, true);
                    var spin = new GameObject("Spin").transform;
                    spin.SetParent(pivot, false);
                    mesh.SetParent(spin, true);
                    mesh.name = "Mesh";

                    visualWheels.Add(new KartVisuals.Wheel { steerPivot = pivot, spin = spin, isFront = front });
                    var local = root.transform.InverseTransformPoint(center);
                    anchors.Add(new Vector3(local.x, wheelRadius, local.z));
                }

                // Everything else (other karts, the FBX camera) is discarded with the temp instance.
                root.transform.position = Vector3.zero;
                Object.DestroyImmediate(temp);
                root.layer = IgnoreRaycastLayer;

                var rb = root.AddComponent<Rigidbody>();
                rb.mass = 180f;
                rb.linearDamping = 0f;
                rb.angularDamping = 1.5f;

                var col = root.AddComponent<BoxCollider>();
                var bb = body.GetComponent<Renderer>().bounds;
                float colBottom = wheelRadius * 0.6f;
                float colTop = Mathf.Max(bb.max.y, colBottom + 0.3f);
                col.center = new Vector3(bb.center.x, (colBottom + colTop) * 0.5f, bb.center.z);
                col.size = new Vector3(bb.size.x, colTop - colBottom, bb.size.z);
                col.sharedMaterial = physicMat;

                var kart = root.AddComponent<KartController>();
                kart.wheelAnchors = anchors.ToArray();
                kart.wheelRadius = wheelRadius;

                var visuals = root.AddComponent<KartVisuals>();
                visuals.kart = kart;
                visuals.model = model;
                visuals.wheels = visualWheels.ToArray();

                string path = $"{PrefabsDir}/Kart_{k.name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                log.Append($" {k.name}(r={wheelRadius:0.00}, size={bb.size.x:0.0}x{bb.size.z:0.0})");
            }

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static PhysicsMaterial GetKartPhysicsMaterial()
        {
            string path = MaterialsDir + "/KartPhysics.physicsMaterial";
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (mat) return mat;
            mat = new PhysicsMaterial("KartPhysics")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0.1f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Average,
            };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------------------------------------------------------------- Track

        [MenuItem("Sugar Rush/3. Track Import")]
        public static string SetupTrackImport()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(TrackFbx);
            importer.globalScale = TrackScale;
            importer.addCollider = false;

            // The sky dome should not receive lighting.
            EnsureFolder(MaterialsDir + "/Track");
            string skyPath = MaterialsDir + "/Track/Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (!sky)
            {
                sky = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TrackTextures + "/Sky_tgsd.png"));
            sky.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(sky);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Sky_tgsd"), sky);

            importer.SaveAndReimport();
            return $"Track import: scale x{TrackScale}";
        }

        [MenuItem("Sugar Rush/4. Track Scene")]
        public static string BuildTrackScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.95f, 0.85f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.8f, 0.75f, 0.85f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.4f, 0.45f);

            // Track
            var track = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TrackFbx));
            track.name = "Track";
            // The bundled road mesh has holes (e.g. under the ice arch), so every visible
            // piece of the track gets a collider too; only the sky dome and minimap are skipped.
            MeshFilter road = null;
            int colliders = 0;
            foreach (var mf in track.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.name == RoadColliderMesh) road = mf;
                if (mf.name == MiniMapMesh) mf.GetComponent<Renderer>().enabled = false;
                GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                if (mf.name == MiniMapMesh || mf.name == SkyMesh) continue;
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                colliders++;
            }
            if (!road) return "ERROR: road collider mesh not found";

            road.GetComponent<Renderer>().enabled = false;
            Physics.SyncTransforms();

            var roadData = WeldedMesh.From(road);
            string wallInfo = BuildWalls(road.GetComponent<MeshCollider>(), roadData);
            float killY = roadData.MinY - 30f;

            // Player kart at a start position on the road
            var (startPos, startRot) = FindStart(road.GetComponent<MeshCollider>(), roadData);
            var kartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/Kart_Vanellope.prefab");
            var kartGo = (GameObject)PrefabUtility.InstantiatePrefab(kartPrefab);
            kartGo.transform.SetPositionAndRotation(startPos + Vector3.up * 0.3f, startRot);
            kartGo.AddComponent<PlayerKartInput>();
            var kart = kartGo.GetComponent<KartController>();
            kart.killY = killY;

            // Camera
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 3000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(1f, 0.8f, 0.9f);
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<KartCamera>();
            follow.target = kart;
            follow.SnapToTarget();

            var hud = new GameObject("DebugHUD").AddComponent<DebugSpeedometer>();
            hud.kart = kart;

            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return $"Scene saved: {ScenePath} | start={startPos} | colliders={colliders} | {wallInfo} | killY={killY:0}";
        }

        /// <summary>
        /// Invisible walls along open road edges that drop off (bridges, cliffs). Edges with any
        /// track surface just beyond them (holes in the road mesh, drivable shoulders) stay open.
        /// </summary>
        static string BuildWalls(MeshCollider roadCollider, WeldedMesh road)
        {
            const float height = 2.5f, below = 1f, probe = 1.5f, maxDrop = 3f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int skipped = 0;

            foreach (var (a, b, opposite) in road.BoundaryEdges())
            {
                Vector3 edge = b - a;
                Vector3 outward = Vector3.Cross(Vector3.up, edge).normalized;
                Vector3 mid = (a + b) * 0.5f;
                if (Vector3.Dot(outward, opposite - mid) > 0f) outward = -outward;

                // Ground continues past the edge? Then no wall.
                var ray = new Ray(mid + outward * probe + Vector3.up * 2f, Vector3.down);
                if (Physics.Raycast(ray, 2f + maxDrop, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) { skipped++; continue; }

                int i = verts.Count;
                verts.Add(a - Vector3.up * below);
                verts.Add(b - Vector3.up * below);
                verts.Add(a + Vector3.up * height);
                verts.Add(b + Vector3.up * height);
                tris.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 });
            }

            var mesh = new Mesh { name = "TrackWalls", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string path = Root + "/Art/Track/TrackWalls.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var walls = new GameObject("TrackWalls");
            walls.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(walls, StaticEditorFlags.BatchingStatic);
            return $"walls={tris.Count / 6} seamsSkipped={skipped}";
        }

        /// <summary>Picks a straight-ish spot on the road and faces along it.</summary>
        static (Vector3, Quaternion) FindStart(MeshCollider roadCollider, WeldedMesh road)
        {
            // Seed: southernmost road vertex, then move to the local road centre.
            Vector3 seed = road.Vertices[0];
            foreach (var v in road.Vertices) if (v.z < seed.z) seed = v;

            Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
            Vector3 centre = Vector3.zero; int n = 0;
            foreach (var v in road.Vertices)
                if ((Flat(v) - Flat(seed)).magnitude < 25f) { centre += v; n++; }
            centre /= Mathf.Max(1, n);

            // Principal horizontal direction of nearby road vertices.
            float sxx = 0, sxz = 0, szz = 0;
            foreach (var v in road.Vertices)
            {
                var d = Flat(v) - Flat(centre);
                if (d.magnitude > 40f) continue;
                sxx += d.x * d.x; sxz += d.x * d.z; szz += d.z * d.z;
            }
            float angle = 0.5f * Mathf.Atan2(2f * sxz, sxx - szz);
            var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            var ray = new Ray(new Vector3(centre.x, road.MaxY + 50f, centre.z), Vector3.down);
            Vector3 pos = roadCollider.Raycast(ray, out var hit, 500f) ? hit.point : centre;
            // The road arrows at this spot point the opposite way to the principal axis sign.
            return (pos, Quaternion.LookRotation(-dir, Vector3.up));
        }

        // ---------------------------------------------------------------- Helpers

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>World-space mesh with coincident vertices merged, for edge analysis.</summary>
        class WeldedMesh
        {
            public readonly List<Vector3> Vertices = new();
            public readonly List<int> Triangles = new();
            public float MinY = float.MaxValue, MaxY = float.MinValue;

            public static WeldedMesh From(MeshFilter mf)
            {
                var w = new WeldedMesh();
                var map = new Dictionary<Vector3Int, int>();
                var src = mf.sharedMesh.vertices;
                var remap = new int[src.Length];
                var m = mf.transform.localToWorldMatrix;
                for (int i = 0; i < src.Length; i++)
                {
                    var p = m.MultiplyPoint3x4(src[i]);
                    var key = Vector3Int.RoundToInt(p * 100f);
                    if (!map.TryGetValue(key, out int idx))
                    {
                        idx = w.Vertices.Count;
                        w.Vertices.Add(p);
                        map[key] = idx;
                        w.MinY = Mathf.Min(w.MinY, p.y);
                        w.MaxY = Mathf.Max(w.MaxY, p.y);
                    }
                    remap[i] = idx;
                }
                foreach (var t in mf.sharedMesh.triangles) w.Triangles.Add(remap[t]);
                return w;
            }

            public IEnumerable<(Vector3 a, Vector3 b, Vector3 opposite)> BoundaryEdges()
            {
                var count = new Dictionary<(int, int), int>();
                var third = new Dictionary<(int, int), int>();
                for (int i = 0; i < Triangles.Count; i += 3)
                {
                    for (int e = 0; e < 3; e++)
                    {
                        int p = Triangles[i + e], q = Triangles[i + (e + 1) % 3], r = Triangles[i + (e + 2) % 3];
                        var key = p < q ? (p, q) : (q, p);
                        count[key] = count.TryGetValue(key, out int c) ? c + 1 : 1;
                        third[key] = r;
                    }
                }
                foreach (var kv in count)
                    if (kv.Value == 1)
                        yield return (Vertices[kv.Key.Item1], Vertices[kv.Key.Item2], Vertices[third[kv.Key]]);
            }
        }
    }
}
