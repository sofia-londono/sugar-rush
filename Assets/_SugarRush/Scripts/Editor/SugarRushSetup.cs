using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.UIElements;

namespace SugarRush.EditorTools
{
    /// <summary>
    /// One-click builders for the imported Sketchfab assets: kart materials, prefabs and roster,
    /// track import settings, the race scene and the main menu scene.
    /// </summary>
    public static class SugarRushSetup
    {
        const string Root = "Assets/_SugarRush";
        const string KartFbx = Root + "/Art/Karts/SRstorybookracers.fbx";
        const string KartTextures = Root + "/Art/Karts/Textures";
        const string TrackFbx = Root + "/Art/Track/map_tgsd.fbx";
        const string TrackTextures = Root + "/Art/Track/Textures";
        const string GeneratedDir = Root + "/Art/Generated";
        const string MaterialsDir = Root + "/Materials";
        const string PrefabsDir = Root + "/Prefabs";
        const string DataDir = Root + "/Data";
        const string UIDir = Root + "/UI";
        const string RosterPath = DataDir + "/KartRoster.asset";
        const string AIDir = DataDir + "/AI";
        const string DifficultyPath = AIDir + "/AIDifficulty.asset";
        const string AudioDir = Root + "/Audio";
        const string SoundLibraryPath = Root + "/Resources/SoundLibrary.asset";
        const string NetPrefabsDir = PrefabsDir + "/Net";
        const string NetResourcesDir = Root + "/Resources/Net";
        const string NetworkPrefabsListPath = DataDir + "/Net/NetworkPrefabs.asset";
        const string PanelSettingsPath = UIDir + "/PanelSettings.asset";
        const string RaceScenePath = Root + "/Scenes/" + SceneNames.Race + ".unity";
        const string MenuScenePath = Root + "/Scenes/" + SceneNames.MainMenu + ".unity";
        const string CharactersDir = Root + "/Art/Characters";
        const string RalphPrefabPath = CharactersDir + "/Ralph/Ralph.prefab";

        public const float TrackScale = 40f;
        const string RoadColliderMesh = "raod_tgsd_001";
        const string MiniMapMesh = "mini_map_road";
        const string SkyMesh = "sky_tgsd";
        const int IgnoreRaycastLayer = 2;
        const float RoadHalfWidth = 5f;
        const float PathSpacing = 4f;

        // Node suffix in the FBX -> character, texture prefix, FBX material names, stats (max speed, acceleration, turn rate)
        static readonly (string name, string fullName, string suffix, string tex, string body, string spoiler, string wheel,
            float speed, float accel, float turn, Color color)[] Karts =
        {
            ("Vanellope",   "Vanellope von Schweetz", "",     "vanellope",   "Material.015", "Material.014", "Material.013", 28.5f, 16.5f, 132f, new Color(0.55f, 0.85f, 0.75f)),
            ("Taffyta",     "Taffyta Muttonfudge",    ".001", "taffyta",     "Material.005", "Material.016", "Material.006", 30.0f, 14.0f, 115f, new Color(1.00f, 0.55f, 0.78f)),
            ("Adorabeezle", "Adorabeezle Winterpop",  ".002", "adorableeze", "Material.001", "Material.002", "Material.003", 27.5f, 17.5f, 138f, new Color(0.56f, 0.82f, 1.00f)),
            ("Rancis",      "Rancis Fluggerbutter",   ".003", "rancis",      "Material.007", "Material.009", "Material.008", 29.5f, 15.0f, 120f, new Color(1.00f, 0.85f, 0.45f)),
            ("Candlehead",  "Candlehead",             ".004", "candlehead",  "Material.010", "Material.011", "Material.012", 28.0f, 17.0f, 128f, new Color(0.76f, 0.64f, 1.00f)),
        };
        const float MinSpeed = 26f, MaxSpeed = 30.5f, MinAccel = 13f, MaxAccel = 18f, MinTurn = 108f, MaxTurn = 140f;

        // Racing line traced over a top-down render of the road meshes (world XZ at TrackScale = 40),
        // starting at the start line and following the floor arrows. Each point is snapped to the
        // centre of the minimap road strip, smoothed and resampled.
        static readonly Vector2[] Route =
        {
            new(15.2f, -75.1f), new(-2.1f, -74.2f), new(-9.0f, -70.8f), new(-14.1f, -65.0f), new(-20.1f, -56.2f), new(-26.3f, -55.7f),
            new(-34.4f, -60.4f), new(-43.6f, -64.5f), new(-52.9f, -66.1f), new(-61.0f, -62.7f), new(-65.6f, -54.6f), new(-63.3f, -45.4f),
            new(-56.3f, -35.0f), new(-47.1f, -25.7f), new(-39.0f, -20.0f), new(-36.3f, -13.0f), new(-41.3f, -8.4f), new(-50.6f, -8.0f),
            new(-62.1f, -11.4f), new(-73.7f, -15.3f), new(-85.2f, -20.0f), new(-95.6f, -25.7f), new(-106.0f, -31.5f), new(-117.5f, -31.5f),
            new(-126.8f, -23.4f), new(-130.2f, -13.0f), new(-125.6f, -0.3f), new(-115.2f, 6.6f), new(-106.0f, 7.8f), new(-100.2f, 13.1f),
            new(-92.1f, 18.1f), new(-80.6f, 24.6f), new(-66.7f, 33.1f), new(-54.0f, 41.2f), new(-41.3f, 48.2f), new(-26.3f, 51.6f),
            new(-14.8f, 56.9f), new(-2.1f, 57.9f), new(9.5f, 53.2f), new(21.0f, 48.2f), new(34.9f, 49.3f), new(44.1f, 48.6f),
            new(48.2f, 41.2f), new(47.6f, 30.8f), new(51.0f, 20.5f), new(62.6f, 15.8f), new(76.4f, 13.5f), new(88.0f, 7.8f),
            new(92.6f, 2.0f), new(88.0f, -4.9f), new(78.7f, -11.9f), new(68.3f, -20.0f), new(60.3f, -26.9f), new(57.9f, -36.1f),
            new(60.7f, -46.5f), new(62.1f, -55.7f), new(55.6f, -62.7f), new(42.9f, -67.3f), new(30.2f, -71.4f),
        };

        // Route points placed by hand instead of snapped to the minimap strip, which runs off-centre
        // there. 48: hairpin under the chocolate arch (measured from a top-down capture).
        static readonly Dictionary<int, Vector2> RouteOverrides = new()
        {
            [48] = new Vector2(93.4f, 3.4f),
        };

        static readonly Color Pink = new(1f, 0.56f, 0.78f), Cream = new(1f, 0.97f, 0.98f);

        [MenuItem("Sugar Rush/Build Everything")]
        public static string BuildAll()
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine(SetupKartMaterials());
            log.AppendLine(BuildKartPrefabs());
            log.AppendLine(SetupAI());
            log.AppendLine(SetupNetwork());
            log.AppendLine(SetupAudio());
            log.AppendLine(SetupTrackImport());
            log.AppendLine(SetupUIAssets());
            log.AppendLine(SetupCharacters());
            log.AppendLine(BuildRaceScene());
            log.AppendLine(BuildMenuScene());
            log.AppendLine(SetupBuildSettings());
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

        [MenuItem("Sugar Rush/2. Kart Prefabs + Roster")]
        public static string BuildKartPrefabs()
        {
            EnsureFolder(PrefabsDir);
            EnsureFolder(DataDir);
            var physicMat = GetKartPhysicsMaterial();
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(KartFbx);
            var log = new System.Text.StringBuilder("Kart prefabs:");
            var entries = new List<KartRoster.Entry>();

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
                kart.maxSpeed = k.speed;
                kart.acceleration = k.accel;
                kart.turnRate = k.turn;

                var visuals = root.AddComponent<KartVisuals>();
                visuals.kart = kart;
                visuals.model = model;
                visuals.wheels = visualWheels.ToArray();

                string path = $"{PrefabsDir}/Kart_{k.name}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);

                entries.Add(new KartRoster.Entry
                {
                    id = k.name,
                    displayName = k.fullName,
                    prefab = prefab,
                    color = k.color,
                    speed = Mathf.InverseLerp(MinSpeed, MaxSpeed, k.speed),
                    acceleration = Mathf.InverseLerp(MinAccel, MaxAccel, k.accel),
                    handling = Mathf.InverseLerp(MinTurn, MaxTurn, k.turn),
                });
                log.Append($" {k.name}(r={wheelRadius:0.00})");
            }

            var roster = AssetDatabase.LoadAssetAtPath<KartRoster>(RosterPath);
            if (!roster)
            {
                roster = ScriptableObject.CreateInstance<KartRoster>();
                AssetDatabase.CreateAsset(roster, RosterPath);
            }
            roster.karts = entries.ToArray();
            AssignPersonalities(roster);
            EditorUtility.SetDirty(roster);
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

        // ---------------------------------------------------------------- AI

        // Starting values for each character's driving style. Assets are only created when
        // missing, so tweaks made in the Inspector are never overwritten by the builder.
        static readonly (string name, float straight, float corner, float braking, float lane, float consistency,
            float aggression, float attack, float block, float mistakes, Vector2 mistakeTime)[] Personalities =
        {
            // Balanced all-rounder (drives when the player picks someone else).
            ("Vanellope",   1.02f, 1.03f, 1.00f, 1.5f, 0.60f, 0.25f,  9f, 12f, 0.5f, new Vector2(0.4f, 0.8f)),
            // Aggressive: rams karts alongside and blocks karts behind.
            ("Taffyta",     1.00f, 0.98f, 1.10f, 2.0f, 0.50f, 0.90f, 10f, 14f, 0.5f, new Vector2(0.4f, 0.8f)),
            // Rocket on straights, brakes late and is slow through corners.
            ("Rancis",      1.08f, 0.80f, 1.35f, 1.2f, 0.50f, 0.20f,  9f, 12f, 1.0f, new Vector2(0.4f, 0.8f)),
            // Clumsy: frequent wobbles, missed braking points and hesitations.
            ("Candlehead",  0.98f, 0.95f, 1.00f, 2.0f, 0.15f, 0.10f,  9f, 12f, 7.0f, new Vector2(0.5f, 1.2f)),
            // Steady and safe: brakes early, holds a tight line, never makes mistakes.
            ("Adorabeezle", 0.99f, 1.00f, 0.85f, 0.6f, 1.00f, 0.00f,  9f, 12f, 0.0f, new Vector2(0.4f, 0.8f)),
        };

        [MenuItem("Sugar Rush/AI Personalities + Difficulty")]
        public static string SetupAI()
        {
            EnsureFolder(AIDir);
            int created = 0;
            foreach (var p in Personalities)
            {
                string path = $"{AIDir}/AI_{p.name}.asset";
                if (AssetDatabase.LoadAssetAtPath<AIPersonality>(path)) continue;
                var asset = ScriptableObject.CreateInstance<AIPersonality>();
                asset.straightSpeed = p.straight;
                asset.cornerSpeed = p.corner;
                asset.lateBraking = p.braking;
                asset.laneWidth = p.lane;
                asset.consistency = p.consistency;
                asset.aggression = p.aggression;
                asset.attackRange = p.attack;
                asset.blockRange = p.block;
                asset.mistakesPerMinute = p.mistakes;
                asset.mistakeDuration = p.mistakeTime;
                AssetDatabase.CreateAsset(asset, path);
                created++;
            }
            if (!AssetDatabase.LoadAssetAtPath<AIDifficulty>(DifficultyPath))
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AIDifficulty>(), DifficultyPath);
                created++;
            }

            var roster = AssetDatabase.LoadAssetAtPath<KartRoster>(RosterPath);
            if (roster)
            {
                AssignPersonalities(roster);
                EditorUtility.SetDirty(roster);
            }
            AssetDatabase.SaveAssets();
            return $"AI: {created} assets created";
        }

        static void AssignPersonalities(KartRoster roster)
        {
            foreach (var entry in roster.karts)
                entry.personality = AssetDatabase.LoadAssetAtPath<AIPersonality>($"{AIDir}/AI_{entry.id}.asset");
        }

        // ---------------------------------------------------------------- Network

        /// <summary>
        /// Online assets: a prefab variant of every kart with NetworkObject + NetKart (variants
        /// follow any change to the single player prefabs), the room and race-state prefabs, the
        /// network prefab list, and a NetworkManager prefab (20 ticks/s, WebSockets for WSS relay).
        /// Single player never loads any of these.
        /// </summary>
        [MenuItem("Sugar Rush/Network Assets")]
        public static string SetupNetwork()
        {
            EnsureFolder(NetPrefabsDir);
            EnsureFolder(NetResourcesDir);
            EnsureFolder(DataDir + "/Net");
            var roster = AssetDatabase.LoadAssetAtPath<KartRoster>(RosterPath);
            var networked = new List<GameObject>();

            foreach (var entry in roster.karts)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
                var netObj = instance.AddComponent<NetworkObject>();
                netObj.DontDestroyWithOwner = true; // a player who leaves hands the kart to the AI
                netObj.AutoObjectParentSync = false;
                instance.AddComponent<NetKart>();
                var variant = PrefabUtility.SaveAsPrefabAsset(instance, $"{NetPrefabsDir}/Kart_{entry.id}_Net.prefab");
                Object.DestroyImmediate(instance);
                entry.netPrefab = variant;
                networked.Add(variant);
            }
            EditorUtility.SetDirty(roster);

            GameObject NetPrefab<T>(string name, System.Action<T> setup = null) where T : Component
            {
                var go = new GameObject(name);
                go.AddComponent<NetworkObject>();
                var component = go.AddComponent<T>();
                setup?.Invoke(component);
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{NetResourcesDir}/{name}.prefab");
                Object.DestroyImmediate(go);
                return prefab;
            }
            networked.Add(NetPrefab<NetLobby>("NetLobby", lobby => lobby.roster = roster));
            networked.Add(NetPrefab<NetRace>("NetRace"));

            AssetDatabase.DeleteAsset(NetworkPrefabsListPath);
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            foreach (var prefab in networked) list.Add(new NetworkPrefab { Prefab = prefab });
            AssetDatabase.CreateAsset(list, NetworkPrefabsListPath);

            var managerGo = new GameObject("NetworkManager");
            var transport = managerGo.AddComponent<UnityTransport>();
            transport.UseWebSockets = true;
            var manager = managerGo.AddComponent<NetworkManager>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.TickRate = 20;
            manager.NetworkConfig.EnableSceneManagement = true;
            manager.NetworkConfig.ConnectionApproval = false;
            manager.NetworkConfig.ForceSamePrefabs = true;
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            manager.RunInBackground = true;
            PrefabUtility.SaveAsPrefabAsset(managerGo, $"{NetResourcesDir}/NetworkManager.prefab");
            Object.DestroyImmediate(managerGo);

            AssetDatabase.SaveAssets();
            return $"Network: {roster.karts.Length} kart variants, {networked.Count} network prefabs, NetworkManager @20 ticks";
        }

        // ---------------------------------------------------------------- Audio

        /// <summary>
        /// Import settings tuned for a light game: music streamed from compressed OGG, short
        /// effects mono at 22 kHz decompressed once into memory, a hard cap on voices, and a
        /// bigger DSP buffer (lower audio CPU on a laptop).
        /// </summary>
        [MenuItem("Sugar Rush/Audio")]
        public static string SetupAudio()
        {
            var log = new System.Text.StringBuilder("Audio:");
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                bool isMusic = path.Contains("/Music/");
                var settings = importer.defaultSampleSettings;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                if (isMusic)
                {
                    settings.loadType = AudioClipLoadType.Streaming;
                    settings.quality = 0.45f;
                    settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                    importer.forceToMono = false;
                    importer.loadInBackground = true;
                }
                else
                {
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    settings.quality = 0.6f;
                    settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                    settings.sampleRateOverride = 22050;
                    importer.forceToMono = true;
                    importer.loadInBackground = false;
                }
                settings.preloadAudioData = !isMusic;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }

            EnsureFolder(Path.GetDirectoryName(SoundLibraryPath).Replace('\\', '/'));
            var lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(SoundLibraryPath);
            if (!lib)
            {
                lib = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(lib, SoundLibraryPath);
            }
            AudioClip Clip(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{file}");
            lib.menuMusic = Clip("Music/menu_music.ogg");
            lib.raceMusic = Clip("Music/race_music.ogg");
            lib.engineLoop = Clip("SFX/engine_loop.wav");
            lib.driftLoop = Clip("SFX/drift_loop.wav");
            lib.boost = Clip("SFX/boost.wav");
            lib.crashes = new[]
            {
                Clip("SFX/crash_impactSoft_heavy_000.ogg"),
                Clip("SFX/crash_impactSoft_heavy_002.ogg"),
                Clip("SFX/crash_impactPunch_medium_001.ogg"),
            };
            lib.countdownBeep = Clip("SFX/countdown_beep.wav");
            lib.countdownGo = Clip("SFX/countdown_go.wav");
            lib.lapChime = Clip("SFX/lap_chime.wav");
            lib.finishFanfare = Clip("SFX/finish_fanfare.wav");
            lib.coin = Clip("SFX/coin.wav");
            lib.ralphWarning = Clip("SFX/ralph_warning.wav");
            lib.ralphSmash = Clip("SFX/ralph_smash.wav");
            lib.hammerFix = Clip("SFX/hammer_fix.wav");
            lib.uiMove = Clip("SFX/ui_move.ogg");
            lib.uiClick = Clip("SFX/ui_click.ogg");
            lib.uiBack = Clip("SFX/ui_back.ogg");
            lib.uiConfirm = Clip("SFX/ui_confirm.ogg");
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();

            // Project audio: at most 24 real voices, and the "best performance" DSP buffer.
            var audioManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0];
            var so = new SerializedObject(audioManager);
            so.FindProperty("m_RealVoiceCount").intValue = 24;
            so.FindProperty("m_VirtualVoiceCount").intValue = 48;
            so.FindProperty("m_DSPBufferSize").intValue = 1024;
            so.ApplyModifiedPropertiesWithoutUndo();

            int missing = 0;
            foreach (var f in typeof(SoundLibrary).GetFields())
                if (f.FieldType == typeof(AudioClip) && !(AudioClip)f.GetValue(lib)) missing++;
            return log.Append($" library ok, missing clips={missing}, voices=24").ToString();
        }

        // ---------------------------------------------------------------- Track import

        [MenuItem("Sugar Rush/3. Track Import")]
        public static string SetupTrackImport()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(TrackFbx);
            importer.globalScale = TrackScale;
            importer.addCollider = false;

            // The sky dome should not receive lighting.
            var sky = GetMaterial("Track/Sky", "Universal Render Pipeline/Unlit", Color.white,
                AssetDatabase.LoadAssetAtPath<Texture2D>(TrackTextures + "/Sky_tgsd.png"));
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Sky_tgsd"), sky);

            importer.SaveAndReimport();
            return $"Track import: scale x{TrackScale}";
        }

        // ---------------------------------------------------------------- UI assets

        [MenuItem("Sugar Rush/4. UI Assets")]
        public static string SetupUIAssets()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (!settings)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UIDir + "/SugarRushTheme.tss");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.clearColor = false;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return "UI assets: theme=" + (settings.themeStyleSheet ? settings.themeStyleSheet.name : "MISSING");
        }

        // ---------------------------------------------------------------- Race scene

        [MenuItem("Sugar Rush/5. Race Scene")]
        public static string BuildRaceScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddLighting();

            var track = InstantiateTrack(withColliders: true, out var road, out int colliders);
            Physics.SyncTransforms();

            var path = BuildTrackPath(track);
            string wallInfo = BuildWalls(WeldedMesh.From(road), path);
            BuildFinishLine(path);
            string chaosInfo = BuildChaos(path);
            BuildPodium();

            var cam = AddCamera(3000f);
            var kartCamera = cam.gameObject.AddComponent<KartCamera>();

            var manager = new GameObject("RaceManager").AddComponent<RaceManager>();
            manager.roster = AssetDatabase.LoadAssetAtPath<KartRoster>(RosterPath);
            manager.aiDifficulty = AssetDatabase.LoadAssetAtPath<AIDifficulty>(DifficultyPath);
            manager.path = path;
            manager.kartCamera = kartCamera;

            // Place the camera behind the start line so the editor view matches the first frame.
            Vector3 start = path.Point(0), dir = path.Direction(0);
            cam.transform.position = start - dir * 40f + Vector3.up * 6f;
            cam.transform.LookAt(start);

            var ui = AddUIDocument("RaceUI");
            ui.gameObject.AddComponent<RaceUI>().document = ui;
            AddEventSystem();

            EnsureFolder(Path.GetDirectoryName(RaceScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, RaceScenePath);
            return $"Race scene: path={path.Count} pts / {path.Length:0} m | colliders={colliders} | {wallInfo} | {chaosInfo}";
        }

        static TrackPath BuildTrackPath(GameObject track)
        {
            // Minimap road strip centroids: the centre of the road in XZ.
            MeshFilter mini = null;
            foreach (var mf in track.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name == MiniMapMesh) mini = mf;
            var centroids = new List<Vector2>();
            var m = mini.transform.localToWorldMatrix;
            var verts = mini.sharedMesh.vertices;
            var tris = mini.sharedMesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                var c = (m.MultiplyPoint3x4(verts[tris[i]]) + m.MultiplyPoint3x4(verts[tris[i + 1]]) + m.MultiplyPoint3x4(verts[tris[i + 2]])) / 3f;
                centroids.Add(new Vector2(c.x, c.z));
            }

            var control = new List<Vector2>();
            for (int k = 0; k < Route.Length; k++)
            {
                var p = Route[k];
                if (RouteOverrides.TryGetValue(k, out var fixedPoint)) { control.Add(fixedPoint); continue; }
                Vector2 sum = Vector2.zero; int n = 0;
                foreach (var c in centroids) if ((c - p).sqrMagnitude < 49f) { sum += c; n++; }
                control.Add(n > 0 ? Vector2.Lerp(p, sum / n, 0.7f) : p);
            }

            // Closed Catmull-Rom spline, densely sampled, then resampled at even spacing.
            var dense = new List<Vector2>();
            int count = control.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 p0 = control[(i - 1 + count) % count], p1 = control[i], p2 = control[(i + 1) % count], p3 = control[(i + 2) % count];
                for (int s = 0; s < 16; s++)
                {
                    float t = s / 16f, t2 = t * t, t3 = t2 * t;
                    dense.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            var even = new List<Vector2> { dense[0] };
            float carry = 0f;
            for (int i = 0; i < dense.Count; i++)
            {
                Vector2 a = dense[i], b = dense[(i + 1) % dense.Count];
                float seg = Vector2.Distance(a, b);
                float d = PathSpacing - carry;
                while (d <= seg)
                {
                    even.Add(Vector2.Lerp(a, b, d / seg));
                    d += PathSpacing;
                }
                carry = seg - (d - PathSpacing);
            }
            if (Vector2.Distance(even[^1], even[0]) < PathSpacing * 0.5f) even.RemoveAt(even.Count - 1);

            // Heights: road surface under each point, following the previous height so bridges
            // and overhangs (ice arch) don't make the line jump.
            var points = new Vector3[even.Count];
            float prevY = 13f;
            for (int i = 0; i < even.Count; i++)
            {
                float y = prevY;
                float best = float.MaxValue;
                foreach (var h in Physics.RaycastAll(new Vector3(even[i].x, 400f, even[i].y), Vector3.down, 800f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (h.normal.y < 0.5f || h.collider.name == "TrackWalls") continue;
                    float dy = Mathf.Abs(h.point.y - prevY);
                    if (dy < best && dy < 8f) { best = dy; y = h.point.y; }
                }
                points[i] = new Vector3(even[i].x, y, even[i].y);
                prevY = y;
            }

            var go = new GameObject("TrackPath");
            var path = go.AddComponent<TrackPath>();
            path.points = points;
            path.roadHalfWidth = RoadHalfWidth;
            return path;
        }

        static void BuildFinishLine(TrackPath path)
        {
            var parent = new GameObject("FinishLine").transform;
            Vector3 start = path.Point(0);
            Vector3 dir = Vector3.ProjectOnPlane(path.Direction(0), Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            parent.SetPositionAndRotation(start, Quaternion.LookRotation(dir, Vector3.up));

            var checker = GetGeneratedTexture("checker", 64, (x, y) => ((x / 8 + y / 8) % 2 == 0) ? Color.white : new Color(0.35f, 0.2f, 0.3f));
            var stripes = GetGeneratedTexture("candy_stripes", 64, (x, y) => ((x + y) / 16 % 2 == 0) ? Pink : Cream);
            var lineMat = GetMaterial("Track/FinishLine", "Universal Render Pipeline/Unlit", Color.white, checker, new Vector2(10f, 2f));
            var bannerMat = GetMaterial("Track/FinishBanner", "Universal Render Pipeline/Lit", Color.white, checker, new Vector2(14f, 1.5f));
            var caneMat = GetMaterial("Track/CandyCane", "Universal Render Pipeline/Lit", Color.white, stripes, new Vector2(2f, 6f));

            float width = RoadHalfWidth * 2f + 1f;
            var line = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(line.GetComponent<Collider>());
            line.name = "CheckeredLine";
            line.transform.SetParent(parent, false);
            line.transform.position = start + Vector3.up * 0.06f;
            line.transform.rotation = Quaternion.LookRotation(Vector3.down, dir);
            line.transform.localScale = new Vector3(width, 2.4f, 1f);
            line.GetComponent<Renderer>().sharedMaterial = lineMat;

            float postOffset = RoadHalfWidth + 1.2f;
            foreach (int side in new[] { -1, 1 })
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(post.GetComponent<Collider>());
                post.name = side < 0 ? "CandyCaneLeft" : "CandyCaneRight";
                post.transform.SetParent(parent, false);
                post.transform.position = start + right * (postOffset * side) + Vector3.up * 3.5f;
                post.transform.localScale = new Vector3(0.7f, 3.5f, 0.7f);
                post.GetComponent<Renderer>().sharedMaterial = caneMat;
            }

            var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(banner.GetComponent<Collider>());
            banner.name = "Banner";
            banner.transform.SetParent(parent, false);
            banner.transform.position = start + Vector3.up * 6.6f;
            banner.transform.localScale = new Vector3(postOffset * 2f + 0.7f, 1.4f, 0.3f);
            banner.GetComponent<Renderer>().sharedMaterial = bannerMat;
        }

        /// <summary>
        /// Invisible guide walls (points on the road surface). End of the bridge:
        /// karts landing wide hit the front of a candy prop or wedge in the 1 m gap beside the
        /// rainbow ramp (seg 94); this diagonal steers them onto the ramp.
        /// </summary>
        static readonly Vector3[][] GuideWalls =
        {
            new[] { new Vector3(-36.5f, 20.1f, 59f), new Vector3(-28.6f, 20.1f, 55.1f), new Vector3(-25f, 19f, 55.9f), new Vector3(-21.5f, 18f, 56.6f) },
        };

        /// <summary>Racing-line points where karts leave the ground: ring exit and the plateau drop to the bridge.</summary>
        static readonly int[] JumpLips = { 74, 89 };

        /// <summary>True if any part of edge a-b passes within 5 m (flat) of a jump lip.</summary>
        static bool NearJumpLip(TrackPath path, Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            foreach (int lip in JumpLips)
            {
                Vector3 p = path.Point(lip);
                p.y = 0f;
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                if (Vector3.Distance(a + ab * t, p) < 5f) return true;
            }
            return false;
        }

        /// <summary>
        /// Invisible walls along open road edges that drop off (bridges, cliffs). Edges with any
        /// track surface just beyond them (holes in the road mesh, drivable shoulders) stay open,
        /// and so do edges that cross the racing line or sit on a jump lip (<see cref="JumpLips"/>).
        /// </summary>
        static string BuildWalls(WeldedMesh road, TrackPath path)
        {
            const float height = 2.5f, below = 1f, probe = 1.5f, maxDrop = 3f, crossingDistance = 3.5f, jumpLipClearance = 2.5f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int skipped = 0, crossings = 0;

            foreach (var (a, b, opposite) in road.BoundaryEdges())
            {
                Vector3 edge = b - a;

                int seg = path.FindClosestSegment((a + b) * 0.5f);
                path.DistanceToSegment((a + b) * 0.5f, seg, out float t);
                Vector3 onPath = Vector3.Lerp(path.Point(seg), path.Point(seg + 1), t);
                Vector3 flat = (a + b) * 0.5f - onPath;
                flat.y = 0f;
                Vector3 edgeFlat = Vector3.ProjectOnPlane(edge, Vector3.up).normalized;
                Vector3 pathFlat = Vector3.ProjectOnPlane(path.Direction(seg), Vector3.up).normalized;
                float alongPath = Mathf.Abs(Vector3.Dot(edgeFlat, pathFlat));
                bool acrossTheRoad = alongPath < 0.5f;
                if (flat.magnitude < crossingDistance && acrossTheRoad) { crossings++; continue; }
                // Jump lips: the road mesh breaks into little edges right where karts take off.
                // Any wall there that faces the kart or stands near the line stops it dead.
                if (NearJumpLip(path, a, b) && (alongPath < 0.75f || flat.magnitude < jumpLipClearance)) { crossings++; continue; }
                // Scraps of edge right by the line only snag karts.
                if (edge.magnitude < 0.5f && flat.magnitude < jumpLipClearance) { crossings++; continue; }

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

            // Hand-placed guides where the mesh leaves no edge to wall off (see GuideWalls).
            foreach (var line in GuideWalls)
                for (int g = 0; g + 1 < line.Length; g++)
                {
                    Vector3 a = line[g], b = line[g + 1];
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

            string assetPath = Root + "/Art/Track/TrackWalls.asset";
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(mesh, assetPath);

            var walls = new GameObject("TrackWalls");
            walls.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(walls, StaticEditorFlags.BatchingStatic);
            return $"walls={tris.Count / 6} seamsSkipped={skipped} crossingsSkipped={crossings}";
        }

        // ---------------------------------------------------------------- Results podium

        static Material GetStandeeMaterial()
        {
            var mat = GetMaterial("Characters/Standee", "Universal Render Pipeline/Unlit", Color.white);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A candy podium far below the track with its own (disabled) camera; see ResultsPodium.</summary>
        static void BuildPodium()
        {
            var root = new GameObject("ResultsPodium");
            root.transform.position = new Vector3(0f, -400f, 0f);
            var podium = root.AddComponent<ResultsPodium>();
            podium.roster = AssetDatabase.LoadAssetAtPath<KartRoster>(RosterPath);
            podium.standeeMaterial = GetStandeeMaterial();

            var lit = "Universal Render Pipeline/Lit";
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.name = "Floor";
            floor.transform.SetParent(root.transform, false);
            floor.transform.localScale = new Vector3(12f, 0.05f, 12f);
            floor.GetComponent<Renderer>().sharedMaterial = GetMaterial("UI/PodiumFloor", lit, new Color(1f, 0.82f, 0.9f));

            // 1st in the middle (tallest), 2nd on the left, 3rd on the right (as seen by the camera at +Z).
            var steps = new (float x, float height, Color color)[]
            {
                (0f, 0.8f, new Color(1f, 0.85f, 0.35f)),
                (2.0f, 0.55f, new Color(0.62f, 0.9f, 0.8f)),
                (-2.0f, 0.35f, new Color(1f, 0.6f, 0.8f)),
            };
            for (int i = 0; i < 3; i++)
            {
                var (x, h, col) = steps[i];
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(step.GetComponent<Collider>());
                step.name = $"Step{i + 1}";
                step.transform.SetParent(root.transform, false);
                step.transform.localPosition = new Vector3(x, h * 0.5f, 0f);
                step.transform.localScale = new Vector3(1.9f, h, 2.3f); // karts stand on them, nose to the camera
                step.GetComponent<Renderer>().sharedMaterial = GetMaterial($"UI/PodiumStep{i + 1}", lit, col);
                var spot = new GameObject($"Place{i + 1}").transform;
                spot.SetParent(root.transform, false);
                spot.localPosition = new Vector3(x, h, 0f);
                podium.spots[i] = spot; // characters face +Z, towards the camera
            }

            var lightGo = new GameObject("PodiumLight");
            lightGo.transform.SetParent(root.transform, false);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 12f;
            light.intensity = 2.5f;
            light.color = new Color(1f, 0.95f, 0.9f);
            lightGo.transform.localPosition = new Vector3(0f, 4f, 3f);
            lightGo.SetActive(true);

            var camGo = new GameObject("PodiumCamera");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(1f, 0.87f, 0.93f);
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 30f;
            camGo.transform.localPosition = new Vector3(0f, 2.3f, 8.2f);
            camGo.transform.LookAt(root.transform.position + new Vector3(0f, 1.05f, 0f));
            podium.podiumCamera = cam;
            camGo.SetActive(false);
        }

        // ---------------------------------------------------------------- Ralph's chaos

        /// <summary>Path segments of the stretches Ralph smashes: straight, flat and wide enough for a detour.</summary>
        static readonly int[] ChaosZoneSegments = { 13, 103, 126 };
        /// <summary>
        /// Pairs of coins: path segment and sideways offset (clamped to the road). Off the racing
        /// line on purpose, so collecting five is a choice and not an accident.
        /// </summary>
        static readonly (int seg, float lateral)[] CoinRows =
        {
            (6, -2.8f), (32, 2.8f), (46, -2.8f), (64, 2.8f), (82, -2.8f), (110, 2.8f), (140, -2.8f), (152, 2.8f),
        };
        const float ChaosZoneHalfLength = 7f;

        /// <summary>Racer characters downloaded as GLB (in ~/Downloads): roster id, file, triangle budget.</summary>
        static readonly (string id, string file, int maxTriangles)[] GlbCharacters =
        {
            ("Taffyta", "taffyta_muttonfudge.glb", 7500),
            ("Candlehead", "candlehead.glb", 8000), // below this her cupcake hat breaks up
            ("Rancis", "rancis_fluggerbutter.glb", 7500),
            ("Adorabeezle", "adorabeezle_winterpop.glb", 7500),
        };
        const string VanellopeFbx = CharactersDir + "/Vanellope/Vanellope.fbx";
        const float CharacterHeight = 1.35f;

        /// <summary>
        /// Characters for the menu, selection and podium (and Ralph for the chaos). GLB sources are
        /// read from Downloads only when their prefab is missing; Vanellope comes as an FBX in the
        /// project. Racers without a model (Adorabeezle) get a portrait drawn by code.
        /// </summary>
        [MenuItem("Sugar Rush/Characters")]
        public static string SetupCharacters()
        {
            var log = new System.Text.StringBuilder("Characters:");
            string downloads = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads");

            if (!File.Exists(RalphPrefabPath))
            {
                string glb = Path.Combine(downloads, "ralph_el_demoledor.glb");
                if (File.Exists(glb)) { var r = GlbImport.Import(glb, CharactersDir + "/Ralph", "Ralph", 6f); log.Append($" Ralph {r.Triangles} tris;"); }
                else log.Append(" Ralph missing (chaos works without him);");
            }

            foreach (var (id, file, maxTris) in GlbCharacters)
            {
                string prefab = $"{CharactersDir}/{id}/{id}.prefab";
                if (File.Exists(prefab)) continue;
                string glb = Path.Combine(downloads, file);
                if (!File.Exists(glb)) { log.Append($" {id} missing;"); continue; }
                var r = GlbImport.Import(glb, $"{CharactersDir}/{id}", id, CharacterHeight, 1024, maxTris);
                log.Append($" {id} {r.Triangles} tris;");
            }

            string vanPrefab = CharactersDir + "/Vanellope/Vanellope.prefab";
            if (!File.Exists(vanPrefab) && File.Exists(VanellopeFbx)) { BuildFbxCharacter(VanellopeFbx, CharactersDir + "/Vanellope/vanellope_diff.png", vanPrefab, "Vanellope"); log.Append(" Vanellope (FBX);"); }

            var roster = AssetDatabase.LoadAssetAtPath<KartRoster>(RosterPath);
            foreach (var entry in roster.karts)
            {
                entry.character = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharactersDir}/{entry.id}/{entry.id}.prefab");
                entry.portrait = entry.character ? null : BuildPortrait(entry.id, entry.color);
                log.Append($" {entry.id}={(entry.character ? "3D" : entry.portrait ? "portrait" : "none")}");
            }
            EditorUtility.SetDirty(roster);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        /// <summary>An FBX character: one URP material with its texture, scaled to CharacterHeight, standing on its origin.</summary>
        static void BuildFbxCharacter(string fbxPath, string texturePath, string prefabPath, string name)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            var texImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            texImporter.maxTextureSize = 1024;
            texImporter.SaveAndReimport();
            var mat = GetMaterial($"Characters/{name}", "Universal Render Pipeline/Lit", Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            mat.SetFloat("_Smoothness", 0.15f);

            var root = new GameObject(name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = name + "_Model";
            model.transform.SetParent(root.transform, false);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }

            var bounds = new Bounds();
            bool any = false;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                foreach (var v in baked.vertices)
                {
                    var w = smr.transform.TransformPoint(v);
                    if (!any) { bounds = new Bounds(w, Vector3.zero); any = true; } else bounds.Encapsulate(w);
                }
                Object.DestroyImmediate(baked);
            }
            float scale = bounds.size.y > 1e-4f ? CharacterHeight / bounds.size.y : 1f;
            model.transform.localScale *= scale;
            model.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Portrait for a racer without a 3D model, drawn by code: round badge in their colour, a
        /// winter hat with a pompom, fringe, big eyes, rosy cheeks, a smile and snowflakes.
        /// </summary>
        static Texture2D BuildPortrait(string id, Color color)
        {
            const int size = 512;
            Color hair = Color.Lerp(color, Color.white, 0.25f), badge = Color.Lerp(color, Color.white, 0.55f);
            Color hat = new(1f, 0.55f, 0.75f), skin = new(1f, 0.89f, 0.8f), ink = new(0.25f, 0.18f, 0.28f), cheek = new(1f, 0.62f, 0.68f);
            float px = 1.5f / size;
            float Fill(float sd) => Mathf.Clamp01(0.5f - sd / px); // signed distance -> coverage
            float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
            float Ellipse(Vector2 p, Vector2 c, Vector2 r) { var q = new Vector2((p.x - c.x) / r.x, (p.y - c.y) / r.y); return (q.magnitude - 1f) * Mathf.Min(r.x, r.y); }

            return GetGeneratedTexture($"Portrait_{id}", size, (x, y) =>
            {
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                var c = Color.clear;
                void Paint(Color col, float coverage) { if (coverage > 0f) c = Color.Lerp(c, new Color(col.r, col.g, col.b, 1f), coverage); }

                float badgeSd = Circle(p, new Vector2(0.5f, 0.5f), 0.47f);
                Paint(Color.white, Fill(badgeSd));
                Paint(badge, Fill(badgeSd + 0.03f));
                // Snowflake dots around the badge.
                for (int k = 0; k < 10; k++)
                {
                    float a = k * 0.628f + 0.3f;
                    Paint(Color.white, Fill(Circle(p, new Vector2(0.5f + Mathf.Cos(a) * 0.38f, 0.5f + Mathf.Sin(a) * 0.38f), 0.014f + (k % 3) * 0.004f)));
                }
                // Hair behind the face, then the face.
                Paint(hair, Fill(Circle(p, new Vector2(0.5f, 0.47f), 0.24f)));
                Paint(skin, Fill(Ellipse(p, new Vector2(0.5f, 0.42f), new Vector2(0.19f, 0.17f))));
                // Fringe: a band of hair across the forehead with a scalloped lower edge.
                float scallop = 0.5f + 0.012f * Mathf.Cos(p.x * 60f);
                if (p.y > scallop && Circle(p, new Vector2(0.5f, 0.47f), 0.24f) < 0f) Paint(hair, Fill(scallop - p.y));
                // Winter hat with a white band and pompom.
                float hatSd = Mathf.Max(Circle(p, new Vector2(0.5f, 0.6f), 0.25f), 0.61f - p.y);
                Paint(hat, Fill(hatSd));
                Paint(Color.white, Fill(Mathf.Max(Mathf.Abs(p.y - 0.615f) - 0.03f, Mathf.Abs(p.x - 0.5f) - 0.25f)));
                Paint(Color.white, Fill(Circle(p, new Vector2(0.5f, 0.87f), 0.05f)));
                // Eyes with a highlight, cheeks, smile.
                foreach (float ex in new[] { 0.43f, 0.57f })
                {
                    Paint(ink, Fill(Ellipse(p, new Vector2(ex, 0.43f), new Vector2(0.024f, 0.036f))));
                    Paint(Color.white, Fill(Circle(p, new Vector2(ex + 0.008f, 0.448f), 0.009f)));
                    Paint(cheek, 0.7f * Fill(Ellipse(p, new Vector2(ex + (ex < 0.5f ? -0.045f : 0.045f), 0.38f), new Vector2(0.03f, 0.02f))));
                }
                float smile = Mathf.Abs(Circle(p, new Vector2(0.5f, 0.375f), 0.04f)) - 0.006f;
                if (p.y < 0.37f) Paint(ink, Fill(smile));
                return c;
            }, transparent: true);
        }

        /// <summary>Drivable road to the left and right of a point on the racing line (metres).</summary>
        static (float left, float right) RoadExtents(TrackPath path, float distance)
        {
            Vector3 p = path.PositionAtDistance(distance);
            Vector3 right = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(path.DirectionAtDistance(distance), Vector3.up)).normalized;
            var ext = new float[2];
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1f : 1f;
                for (float o = 0.5f; o <= 12f; o += 0.5f)
                {
                    Vector3 q = p + right * sign * o;
                    if (!Physics.Raycast(q + Vector3.up * 3f, Vector3.down, out var hit, 6f) || hit.collider.name == "TrackWalls"
                        || Mathf.Abs(hit.point.y - p.y) > 0.7f || hit.normal.y < 0.85f) break;
                    if (Physics.Raycast(p + Vector3.up * 0.8f, right * sign, o)) break;
                    ext[side] = o;
                }
            }
            return (ext[0], ext[1]);
        }

        static RaycastHit GroundHit(Vector3 p)
        {
            Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (!hit.collider) hit.point = p;
            if (hit.normal == Vector3.zero) hit.normal = Vector3.up;
            return hit;
        }

        static string BuildChaos(TrackPath path)
        {
            var root = new GameObject("RalphChaos");
            var chaos = root.AddComponent<RalphChaos>();
            chaos.path = path;

            var lit = "Universal Render Pipeline/Lit";
            var chocolate = GetMaterial("Chaos/Chocolate", lit, new Color(0.32f, 0.17f, 0.1f));
            var crack = GetMaterial("Chaos/Crack", lit, new Color(0.13f, 0.06f, 0.04f));
            var candies = new[]
            {
                GetMaterial("Chaos/CandyPink", lit, new Color(1f, 0.5f, 0.74f)),
                GetMaterial("Chaos/CandyMint", lit, new Color(0.52f, 0.9f, 0.76f)),
                GetMaterial("Chaos/CandyLemon", lit, new Color(1f, 0.87f, 0.42f)),
                GetMaterial("Chaos/Cookie", lit, new Color(0.86f, 0.62f, 0.38f)),
                chocolate,
            };
            var gold = GetMaterial("Chaos/Gold", lit, new Color(1f, 0.76f, 0.2f));
            gold.SetFloat("_Metallic", 0.65f);
            gold.SetFloat("_Smoothness", 0.75f);
            gold.EnableKeyword("_EMISSION");
            gold.SetColor("_EmissionColor", new Color(0.45f, 0.3f, 0.04f));
            gold.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            gold.enableInstancing = true;
            EditorUtility.SetDirty(gold);

            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            var cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

            // Zones: rubble on the narrower side of the road, the wider side stays open.
            var zones = new List<ChaosZone>();
            for (int zi = 0; zi < ChaosZoneSegments.Length; zi++)
            {
                float d = path.DistanceAt(ChaosZoneSegments[zi]);
                float left = float.MaxValue, right = float.MaxValue;
                foreach (float off in new[] { -ChaosZoneHalfLength + 1f, 0f, ChaosZoneHalfLength - 1f })
                {
                    var (l, r) = RoadExtents(path, d + off);
                    left = Mathf.Min(left, l);
                    right = Mathf.Min(right, r);
                }
                bool blockRight = right <= left;
                float narrow = Mathf.Min(blockRight ? right : left, 5.5f);
                var zone = new ChaosZone
                {
                    distance = d,
                    halfLength = ChaosZoneHalfLength,
                    blockedMin = blockRight ? -1f : -narrow,
                    blockedMax = blockRight ? narrow : 1f,
                    roadMin = -left,
                    roadMax = right,
                };

                Vector3 rightVec = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(path.DirectionAtDistance(d), Vector3.up)).normalized;
                float mid = (zone.blockedMin + zone.blockedMax) * 0.5f;
                var hit = GroundHit(path.PositionAtDistance(d) + rightVec * mid);
                var rubbleGo = new GameObject($"Rubble_{zi}");
                rubbleGo.transform.SetParent(root.transform, false);
                rubbleGo.transform.SetPositionAndRotation(hit.point,
                    Quaternion.LookRotation(Vector3.ProjectOnPlane(path.DirectionAtDistance(d), hit.normal).normalized, hit.normal));
                BuildRubbleMesh(rubbleGo, zi, zone.blockedMax - zone.blockedMin, ChaosZoneHalfLength * 2f - 1f, cube, cylinder, chocolate, crack, candies);
                zone.rubble = rubbleGo.transform;
                // Ralph stands just past the outer edge of the rubble, facing the road, and pounds it.
                float standLateral = blockRight ? zone.blockedMax + 0.7f : zone.blockedMin - 0.7f;
                Vector3 standOnLine = path.PositionAtDistance(d) + rightVec * standLateral;
                var standHit = GroundHit(standOnLine);
                zone.ralphSpot = Mathf.Abs(standHit.point.y - standOnLine.y) < 1f ? standHit.point : standOnLine;
                zone.ralphSide = rightVec * (blockRight ? 1f : -1f);
                zones.Add(zone);
            }
            chaos.zones = zones.ToArray();

            // Coins: pairs along the road, beside the racing line.
            var coinList = new List<Transform>();
            foreach (var (seg, lateral) in CoinRows)
                for (int k = 0; k < 2; k++)
                {
                    float d = path.DistanceAt(seg) + k * 3.5f;
                    var (l, r) = RoadExtents(path, d);
                    float lat = Mathf.Clamp(lateral, -Mathf.Max(0f, l - 1f), Mathf.Max(0f, r - 1f));
                    Vector3 rightVec = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(path.DirectionAtDistance(d), Vector3.up)).normalized;
                    var hit = GroundHit(path.PositionAtDistance(d) + rightVec * lat);
                    var coin = new GameObject($"Coin_{coinList.Count:00}");
                    coin.transform.SetParent(root.transform, false);
                    coin.transform.position = hit.point + Vector3.up * 1f;
                    coin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // stand the disc up
                    coin.transform.localScale = new Vector3(0.9f, 0.05f, 0.9f);
                    coin.AddComponent<MeshFilter>().sharedMesh = cylinder;
                    var mr = coin.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = gold;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    coinList.Add(coin.transform);
                }
            chaos.coins = coinList.ToArray();

            // Ralph: one model, hidden until he jumps in.
            var ralphPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RalphPrefabPath);
            if (ralphPrefab)
            {
                var ralph = (GameObject)PrefabUtility.InstantiatePrefab(ralphPrefab);
                ralph.transform.SetParent(root.transform, false);
                chaos.ralph = ralph.AddComponent<RalphPuppet>();
                chaos.ralph.model = ralph.transform.GetChild(0);
                // A body for karts to bounce off (enabled only while he stands on the road).
                var capsule = ralph.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 2.2f, 0.3f);
                capsule.height = 4.4f;
                capsule.radius = 1.3f;
                capsule.enabled = false;
                var body = ralph.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                ralph.SetActive(false);
            }
            chaos.dust = BuildSugarDust(root.transform);
            return $"chaos: {zones.Count} zones, {coinList.Count} coins, ralph={(ralphPrefab ? "yes" : "missing")}";
        }

        /// <summary>Puffs of sugar dust for Ralph's punches: one small particle system, emitted on demand.</summary>
        static ParticleSystem BuildSugarDust(Transform parent)
        {
            var dot = GetGeneratedTexture("SoftDot", 64, (x, y) =>
            {
                float dx = (x + 0.5f) / 32f - 1f, dy = (y + 0.5f) / 32f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                return new Color(1f, 1f, 1f, a * a);
            }, transparent: true);
            var mat = GetMaterial("Chaos/SugarDust", "Universal Render Pipeline/Particles/Unlit", Color.white, dot);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);

            var go = new GameObject("SugarDust");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 80;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f), new Color(1f, 0.85f, 0.93f, 0.9f));
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.6f;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>
        /// Smashed road, built once and combined into a single mesh: chocolate craters, cracks
        /// and chunks of candy over a <paramref name="width"/> x <paramref name="length"/> patch.
        /// </summary>
        static void BuildRubbleMesh(GameObject go, int seed, float width, float length, Mesh cube, Mesh cylinder,
            Material chocolate, Material crack, Material[] candies)
        {
            var rng = new System.Random(1234 + seed * 77);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var groups = new Dictionary<Material, List<CombineInstance>>();
            void Piece(Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
            {
                if (!groups.TryGetValue(mat, out var list)) groups[mat] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, rot, scale) });
            }

            float hw = width * 0.5f, hl = length * 0.5f;
            for (int i = 0; i < 3; i++)
                Piece(cylinder, chocolate, new Vector3(R(-hw, hw) * 0.25f, 0.03f, -hl * 0.6f + i * hl * 0.6f),
                    Quaternion.Euler(0f, R(0f, 180f), 0f), new Vector3(width * R(0.55f, 0.75f), 0.03f, R(3f, 4.2f)));
            for (int i = 0; i < 7; i++)
                Piece(cube, crack, new Vector3(R(-hw, hw) * 0.8f, 0.07f, R(-hl, hl) * 0.85f),
                    Quaternion.Euler(0f, R(0f, 180f), 0f), new Vector3(0.18f, 0.04f, R(2f, 4f)));
            for (int i = 0; i < 14; i++)
            {
                float size = R(0.5f, 1.15f);
                Piece(cube, candies[rng.Next(candies.Length)], new Vector3(R(-hw, hw) * 0.85f, size * 0.22f, R(-hl, hl) * 0.9f),
                    Quaternion.Euler(R(-30f, 30f), R(0f, 360f), R(-30f, 30f)), new Vector3(size, size * R(0.6f, 1f), size * R(0.7f, 1.2f)));
            }

            var parts = new List<CombineInstance>();
            var mats = new List<Material>();
            foreach (var (mat, list) in groups)
            {
                var part = new Mesh();
                part.CombineMeshes(list.ToArray(), true, true);
                parts.Add(new CombineInstance { mesh = part, transform = Matrix4x4.identity });
                mats.Add(mat);
            }
            var mesh = new Mesh { name = $"Rubble_{seed}" };
            mesh.CombineMeshes(parts.ToArray(), false, false);
            foreach (var p in parts) Object.DestroyImmediate(p.mesh);
            mesh.RecalculateBounds();
            string meshPath = $"{GeneratedDir}/Rubble_{seed}.asset";
            EnsureFolder(GeneratedDir);
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
        }

        // ---------------------------------------------------------------- Main menu scene

        [MenuItem("Sugar Rush/6. Main Menu Scene")]
        public static string BuildMenuScene()
        {
            // Read the start line from the race scene's racing line so the turntable sits on the road.
            EditorSceneManager.OpenScene(RaceScenePath, OpenSceneMode.Single);
            var path = Object.FindFirstObjectByType<TrackPath>();
            Vector3 spot = path.Point(4);
            Vector3 dir = Vector3.ProjectOnPlane(path.Direction(4), Vector3.up).normalized;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddLighting();
            InstantiateTrack(withColliders: false, out _, out _);

            var showcaseRoot = new GameObject("KartShowcase");
            showcaseRoot.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(dir, Vector3.up));
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(platform.GetComponent<Collider>());
            platform.name = "Platform";
            platform.transform.SetParent(showcaseRoot.transform, false);
            platform.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            platform.transform.localScale = new Vector3(3f, 0.08f, 3f);
            platform.GetComponent<Renderer>().sharedMaterial = GetMaterial("UI/Platform", "Universal Render Pipeline/Lit", Pink);

            var turntable = new GameObject("Turntable").transform;
            turntable.SetParent(showcaseRoot.transform, false);
            turntable.localPosition = new Vector3(0f, 0.16f, 0f);
            turntable.localRotation = Quaternion.Euler(0f, 140f, 0f);

            var showcase = showcaseRoot.AddComponent<KartShowcase>();
            showcase.roster = AssetDatabase.LoadAssetAtPath<KartRoster>(RosterPath);
            showcase.turntable = turntable;
            showcase.standeeMaterial = GetStandeeMaterial();

            // Camera behind the turntable looking down the ice canyon, framed so the kart sits
            // right of centre (the menu is on the left) with its racer standing to its right.
            var cam = AddCamera(3000f);
            Vector3 target = spot + Vector3.up * 0.7f;
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            cam.transform.position = target - dir * 5.2f - right * 0.2f + Vector3.up * 1.1f;
            cam.transform.LookAt(target - right * 1.6f);
            cam.fieldOfView = 50f;

            // The racer stands beside the platform, turned towards the camera.
            var characterSpot = new GameObject("CharacterSpot").transform;
            characterSpot.SetParent(showcaseRoot.transform, false);
            characterSpot.position = spot - dir * 0.9f + right * 1.7f + Vector3.up * 0.16f;
            Vector3 toCamera = Vector3.ProjectOnPlane(cam.transform.position - characterSpot.position, Vector3.up);
            characterSpot.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            showcase.characterSpot = characterSpot;

            // Two-player page: the camera turns to look straight down the road; each player's kart
            // (with its racer in it) sits on its side, angled towards the camera.
            var duoCam = new GameObject("DuoCameraPose").transform;
            duoCam.SetParent(showcaseRoot.transform, false);
            // Tilted down so the karts sit in the upper part of the screen, above the players' panels.
            duoCam.position = spot - dir * 2f + Vector3.up * 1f;
            duoCam.LookAt(spot + dir * 6f - Vector3.up * 0.4f);
            showcase.duoCameraPose = duoCam;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var kartSpot = new GameObject($"DuoKart{i + 1}").transform;
                kartSpot.SetParent(showcaseRoot.transform, false);
                kartSpot.position = spot + dir * 4f + right * (1.7f * side) + Vector3.up * 0.05f;
                kartSpot.rotation = Quaternion.LookRotation(Quaternion.Euler(0f, 35f * -side, 0f) * -dir, Vector3.up);
                showcase.duoKartSpots[i] = kartSpot;

            }

            var ui = AddUIDocument("MainMenuUI");
            var menu = ui.gameObject.AddComponent<MainMenuUI>();
            menu.document = ui;
            menu.roster = showcase.roster;
            menu.showcase = showcase;
            AddEventSystem();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            return "Menu scene saved: " + MenuScenePath;
        }

        [MenuItem("Sugar Rush/7. Build Settings")]
        public static string SetupBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(RaceScenePath, true),
            };
            if (AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.DeleteAsset("Assets/Scenes");

            // Both quality levels (0 = Mobile/performance, 1 = PC/quality) on every platform, so the
            // in-game Graphics option works on desktop and web alike.
            var qualityAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var so = new SerializedObject(qualityAsset);
            var levels = so.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("excludedTargetPlatforms").ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();

            PlayerSettings.productName = "Sugar Rush";
            PlayerSettings.runInBackground = true;
            return "Build settings: MainMenu, Race | quality levels: " + string.Join(",", QualitySettings.names);
        }

        // ---------------------------------------------------------------- Scene helpers

        static void AddLighting()
        {
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
        }

        /// <summary>
        /// Instances the track. The bundled road mesh has holes (e.g. under the ice arch), so with
        /// colliders every visible piece gets one; only the sky dome and minimap are skipped.
        /// </summary>
        static GameObject InstantiateTrack(bool withColliders, out MeshFilter road, out int colliders)
        {
            var track = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TrackFbx));
            track.name = "Track";
            road = null;
            colliders = 0;
            foreach (var mf in track.GetComponentsInChildren<MeshFilter>())
            {
                GameObjectUtility.SetStaticEditorFlags(mf.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                if (mf.name == RoadColliderMesh) { road = mf; mf.GetComponent<Renderer>().enabled = false; }
                if (mf.name == MiniMapMesh) { mf.GetComponent<Renderer>().enabled = false; continue; }
                if (mf.name == SkyMesh || !withColliders) continue;
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                colliders++;
            }
            return track;
        }

        static Camera AddCamera(float farClip)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = farClip;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(1f, 0.8f, 0.9f);
            camGo.AddComponent<AudioListener>();
            return cam;
        }

        static UIDocument AddUIDocument(string name)
        {
            var doc = new GameObject(name).AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            return doc;
        }

        static void AddEventSystem() =>
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        // ---------------------------------------------------------------- Asset helpers

        static Material GetMaterial(string name, string shaderName, Color color, Texture tex = null, Vector2? tiling = null)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find(shaderName));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = Shader.Find(shaderName);
            mat.SetColor("_BaseColor", color);
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D GetGeneratedTexture(string name, int size, System.Func<int, int, Color> pixel, bool transparent = false)
        {
            EnsureFolder(GeneratedDir);
            string path = $"{GeneratedDir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, pixel(x, y));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = transparent ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = transparent;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

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
