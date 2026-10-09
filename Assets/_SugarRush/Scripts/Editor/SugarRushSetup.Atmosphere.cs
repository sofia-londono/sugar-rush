using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SugarRush.EditorTools
{
    /// <summary>
    /// Life for the forest tracks: sun beams in the clearings, crowd stands with bouncing gummy
    /// bears at a few curves, curve-warning signs at the hairpins, bunting across the road,
    /// chocolate fountains, and the ForestAmbience (dark tunnels, ambience sounds, butterflies,
    /// candy birds, sparkles, fireflies).
    /// </summary>
    public static partial class SugarRushSetup
    {
        const string AmbienceDir = Root + "/Audio/Ambience";

        static string BuildForestAtmosphere(TrackBuild b, Vector2[] crowdCurves, float[] bunting, Vector2[] fountains)
        {
            var log = new System.Text.StringBuilder("atmosphere:");
            int n = b.Center.Count;
            var inTunnel = new bool[n];
            var dark = new List<Vector2>();
            if (b.Design.CrownTunnels != null)
                foreach (var t in b.Design.CrownTunnels)
                {
                    var samples = new List<int>(TunnelSamples(b, t));
                    foreach (int i in samples) inTunnel[i] = true;
                    if (samples.Count > 0) dark.Add(new Vector2(b.Dist[samples[0]], b.Dist[samples[^1]]));
                }
            foreach (int i in TunnelSamples(b, b.Design.DonutTunnel)) inTunnel[i] = true;

            // ---- Sun beams: tall soft additive planes slanting through the open stretches.
            var rayTex = GetGeneratedTexture("GF_SunRay", 64, (x, y) =>
            {
                float u = (x + 0.5f) / 64f, v = (y + 0.5f) / 64f;
                float across = Mathf.Pow(Mathf.Sin(u * Mathf.PI), 2f);
                float along = Mathf.SmoothStep(0f, 1f, v) * Mathf.SmoothStep(1f, 0.75f, v);
                float a = across * along;
                return new Color(1f, 0.96f, 0.85f, a);
            }, transparent: true);
            var rayMat = AdditiveMaterial("Atmosphere/SunRay", rayTex, new Color(1f, 0.93f, 0.8f, 0.45f));
            var quad = BackdropQuadMesh(b);
            int rays = 0;
            for (int i = 30; i < n; i += 55)
            {
                if (inTunnel[i] || b.Hidden[i]) continue;
                float side = rays % 2 == 0 ? 1f : -1f;
                Vector3 pos = b.Center[i] + b.Right[i] * side * (b.Width[i] + b.R(1f, 6f));
                pos.y = b.Center[i].y - 1f;
                var face = Quaternion.LookRotation(b.Right[i] * side, Vector3.up) * Quaternion.Euler(0f, 0f, 24f * side);
                b.Add(quad, rayMat, Matrix4x4.TRS(pos, face, new Vector3(b.R(5f, 8f), b.R(24f, 32f), 1f)));
                rays++;
            }
            log.Append($" rays {rays}");

            // ---- Crowd stands on the outside of a few curves.
            var bear = CodeGummyBear(true);
            var bearMat = CandyMaterial(b, "CandyBear", 0f, 0.85f, 0.18f);
            var standMats = new[]
            {
                GetMaterial($"{b.MaterialDir}/StandPink", "Universal Render Pipeline/Lit", new Color(1f, 0.72f, 0.85f)),
                GetMaterial($"{b.MaterialDir}/StandCream", "Universal Render Pipeline/Lit", new Color(1f, 0.96f, 0.9f)),
            };
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            int stands = 0, fans = 0;
            if (crowdCurves != null)
                foreach (var spot in crowdCurves)
                {
                    int i = NearestSample(b, spot);
                    Vector3 before = b.Center[(i - 10 + n) % n], after = b.Center[(i + 10) % n];
                    float turn = Vector3.Cross(Vector3.ProjectOnPlane(b.Center[i] - before, Vector3.up), Vector3.ProjectOnPlane(after - b.Center[i], Vector3.up)).y;
                    float side = turn > 0f ? -1f : 1f; // outside of the curve
                    Vector3 forward = Vector3.Cross(b.Right[i], Vector3.up);
                    var rot = Quaternion.LookRotation(forward, Vector3.up);
                    float baseLateral = b.Width[i] + CurbWidth + RailWidth + 4f;
                    var seats = new List<Matrix4x4>();
                    var colours = new List<Vector4>();
                    for (int row = 0; row < 3; row++)
                    {
                        float lateral = baseLateral + row * 1.8f, height = 0.6f + row * 0.7f;
                        Vector3 centre = b.Center[i] + b.Right[i] * side * lateral;
                        centre.y = b.Center[i].y - 0.8f;
                        b.Add(cube, standMats[row % 2], Matrix4x4.TRS(centre + Vector3.up * height * 0.5f, rot, new Vector3(1.8f, height + 1.6f, 18f)));
                        for (int k = 0; k < 8; k++)
                        {
                            Vector3 seat = centre + forward * (-7.5f + k * 2.15f + b.R(-0.3f, 0.3f)) + Vector3.up * (height + 0.8f);
                            var faceRoad = Quaternion.LookRotation(-b.Right[i] * side, Vector3.up) * Quaternion.Euler(0f, b.R(-20f, 20f), 0f);
                            seats.Add(Matrix4x4.TRS(seat, faceRoad, Vector3.one * b.R(1.3f, 1.8f)));
                            colours.Add(b.Pick(GummyColors));
                        }
                    }
                    var crowd = new GameObject($"CandyCrowd{stands}").AddComponent<CandyCrowd>();
                    crowd.transform.SetParent(b.Root, false);
                    crowd.mesh = bear;
                    crowd.material = bearMat;
                    crowd.seats = seats.ToArray();
                    crowd.colours = colours.ToArray();
                    b.Occupy(b.Center[i] + b.Right[i] * side * (baseLateral + 2f), 11f);
                    b.Busy.Add(b.Dist[i]);
                    fans += seats.Count;
                    stands++;
                }
            log.Append($" stands {stands} ({fans} fans)");

            // ---- Curve-warning signs (pink chevrons) on the outside of the hairpins.
            var chevron = GetGeneratedTexture("GF_Chevron", 128, (x, y) =>
            {
                float u = (x + 0.5f) / 128f, v = (y + 0.5f) / 128f;
                if (u < 0.06f || u > 0.94f || v < 0.08f || v > 0.92f) return new Color(1f, 0.45f, 0.65f);
                float stripe = Mathf.Repeat(u * 3f + Mathf.Abs(v - 0.5f) * 2.2f, 1f);
                return stripe < 0.45f ? new Color(1f, 0.45f, 0.65f) : Color.white;
            });
            var signMat = GetMaterial($"{b.MaterialDir}/ChevronSign", "Universal Render Pipeline/Lit", Color.white, chevron);
            var caneMat = GetMaterial("Track/CandyCane", "Universal Render Pipeline/Lit", Color.white,
                AssetDatabase.LoadAssetAtPath<Texture2D>($"{GeneratedDir}/candy_stripes.png"), new Vector2(2f, 6f));
            var post = LowCylinder(8);
            int signs = 0;
            for (int i = 0; i < n; i += 6)
            {
                Vector3 a = Vector3.ProjectOnPlane(b.Center[(i + 6) % n] - b.Center[i], Vector3.up).normalized;
                Vector3 c = Vector3.ProjectOnPlane(b.Center[(i + 18) % n] - b.Center[(i + 12) % n], Vector3.up).normalized;
                float bend = Vector3.SignedAngle(a, c, Vector3.up);
                if (Mathf.Abs(bend) < 32f || b.Hidden[i] || inTunnel[i]) continue; // only real hairpins
                float side = bend > 0f ? -1f : 1f; // outside
                int j = (i + 12) % n;
                Vector3 pos = b.Center[j] + b.Right[j] * side * (b.Width[j] + CurbWidth + RailWidth + 1.4f);
                pos.y = b.Center[j].y;
                Vector3 toward = -Vector3.Cross(b.Right[i], Vector3.up); // faces the drivers coming in
                var rot = Quaternion.LookRotation(toward, Vector3.up);
                float mirror = bend > 0f ? 1f : -1f;
                b.Add(cube, signMat, Matrix4x4.TRS(pos + Vector3.up * 2.1f, rot, new Vector3(2.6f * mirror, 1.5f, 0.12f)));
                foreach (float o in new[] { -0.9f, 0.9f })
                    b.Add(post, caneMat, Matrix4x4.TRS(pos + rot * new Vector3(o, -0.3f, 0.1f), Quaternion.identity, new Vector3(0.16f, 2.4f, 0.16f)));
                signs++;
                i += 12;
            }
            log.Append($" signs {signs}");

            // ---- Bunting: candy-cane poles on both sides with a sagging string of pennants across.
            var pennant = PennantMesh();
            var rope = LowCylinder(5);
            var ropeMat = GetMaterial($"{b.MaterialDir}/Rope", "Universal Render Pipeline/Lit", Color.white);
            var flagMats = new Material[GummyColors.Length];
            for (int k = 0; k < flagMats.Length; k++)
            {
                flagMats[k] = GetMaterial($"{b.MaterialDir}/Pennant{k}", "Universal Render Pipeline/Lit", Color.Lerp(GummyColors[k], Color.white, 0.15f));
                flagMats[k].SetFloat("_Cull", 0f);
            }
            int strings = 0;
            if (bunting != null)
                foreach (float f in bunting)
                {
                    int i = Mathf.FloorToInt(f * n) % n;
                    if (b.Hidden[i] || inTunnel[i]) continue;
                    float half = b.Width[i] + CurbWidth + RailWidth + 1.5f, top = 7.5f;
                    Vector3 l = b.Center[i] - b.Right[i] * half, r = b.Center[i] + b.Right[i] * half;
                    foreach (var p in new[] { l, r })
                        b.Add(post, caneMat, Matrix4x4.TRS(p - Vector3.up * 0.5f, Quaternion.identity, new Vector3(0.3f, top + 0.6f, 0.3f)));
                    const int flags = 16;
                    Vector3 Rope(float t) => Vector3.Lerp(l, r, t) + Vector3.up * (top - 1.2f * Mathf.Sin(t * Mathf.PI));
                    for (int k = 0; k < flags; k++)
                    {
                        float t0 = k / (float)flags, t1 = (k + 1) / (float)flags;
                        Vector3 p0 = Rope(t0), p1 = Rope(t1);
                        b.Add(rope, ropeMat, Matrix4x4.TRS(p0, Quaternion.FromToRotation(Vector3.up, p1 - p0), new Vector3(0.05f, (p1 - p0).magnitude, 0.05f)));
                        Vector3 mid = (p0 + p1) * 0.5f;
                        var flagRot = Quaternion.LookRotation(Vector3.Cross(p1 - p0, Vector3.up).normalized, Vector3.up);
                        b.Add(pennant, flagMats[k % flagMats.Length], Matrix4x4.TRS(mid, flagRot, new Vector3((p1 - p0).magnitude * 0.9f, 0.8f, 1f)));
                    }
                    strings++;
                }
            log.Append($" bunting {strings}");

            // ---- Chocolate fountains: stacked basins with chocolate pouring over every rim.
            var chocolate = GetMaterial($"{b.MaterialDir}/Chocolate", "Universal Render Pipeline/Lit", new Color(0.55f, 0.34f, 0.24f));
            var basinMat = GetMaterial($"{b.MaterialDir}/FountainBasin", "Universal Render Pipeline/Lit", new Color(1f, 0.92f, 0.95f));
            var cylinder = LowCylinder(16);
            var waterSpots = new List<Vector3>();
            if (b.Design.Lake != null) foreach (var c in b.Design.Lake) waterSpots.Add(c);
            int fountainCount = 0;
            if (fountains != null)
                foreach (var f in fountains)
                {
                    var p = new Vector3(f.x, b.GroundHeight(f.x, f.y), f.y);
                    float[] radii = { 4.2f, 2.8f, 1.6f };
                    float y = 0f;
                    for (int k = 0; k < radii.Length; k++)
                    {
                        float r = radii[k], h = k == 0 ? 1.1f : 0.6f;
                        b.Add(cylinder, basinMat, Matrix4x4.TRS(p + Vector3.up * y, Quaternion.identity, new Vector3(r * 2f, h, r * 2f)));
                        b.Add(cylinder, chocolate, Matrix4x4.TRS(p + Vector3.up * (y + h - 0.05f), Quaternion.identity, new Vector3(r * 1.8f, 0.12f, r * 1.8f)));
                        // Curtains of chocolate pouring over the rim to the tier below.
                        float fall = k == 0 ? y + h : 1.8f;
                        b.Add(cylinder, chocolate, Matrix4x4.TRS(p + Vector3.up * (y + h - fall), Quaternion.identity, new Vector3(r * 2.02f, fall, r * 2.02f)));
                        y += h + 1.8f;
                        b.Add(cylinder, basinMat, Matrix4x4.TRS(p + Vector3.up * (y - 1.8f), Quaternion.identity, new Vector3(0.5f, 1.8f, 0.5f)));
                    }
                    b.Occupy(p, 6f);
                    waterSpots.Add(new Vector3(f.x, f.y, 5f));
                    fountainCount++;
                }
            log.Append($" fountains {fountainCount}");

            // ---- Ambience: zones, sounds, critters, particles.
            var amb = new GameObject("ForestAmbience").AddComponent<ForestAmbience>();
            amb.path = b.Path;
            amb.darkZones = dark.ToArray();
            amb.water = waterSpots.ToArray();
            amb.sun = Object.FindFirstObjectByType<Light>();
            amb.birds = AmbienceClip("forest_birds.wav");
            amb.wind = AmbienceClip("forest_wind.wav");
            amb.bubbles = AmbienceClip("choco_bubbles.wav");
            amb.butterflyMesh = ButterflyMesh();
            amb.birdMesh = CandyBirdMesh();
            amb.critterMaterial = CandyMaterial(b, "Critters", 0f, 0.7f, 0.25f);
            var dot = GetGeneratedTexture("SoftDot", 64, (x, y) =>
            {
                float dx = (x + 0.5f) / 32f - 1f, dy = (y + 0.5f) / 32f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                return new Color(1f, 1f, 1f, a * a);
            }, transparent: true);
            var glow = AdditiveMaterial("Atmosphere/Glow", dot, Color.white);
            amb.sparkles = AirParticles(amb.transform, "Sparkles", glow, 60, 9f, new Vector2(4f, 6f), new Vector2(0.06f, 0.14f), new Vector3(36f, 10f, 36f),
                new Color(1f, 1f, 1f, 0.9f), new Color(1f, 0.85f, 0.95f, 0.9f), 0.15f);
            amb.fireflies = AirParticles(amb.transform, "Fireflies", glow, 50, 14f, new Vector2(3f, 5f), new Vector2(0.18f, 0.32f), new Vector3(22f, 5f, 22f),
                new Color(1f, 0.85f, 0.3f, 1f), new Color(0.6f, 1f, 0.7f, 1f), 0.8f);
            log.Append($" dark zones {dark.Count}");
            return log.ToString();
        }

        /// <summary>Additive, unlit, double-sided particle material (sun beams, sparkles, fireflies).</summary>
        static Material AdditiveMaterial(string name, Texture texture, Color colour)
        {
            var mat = GetMaterial(name, "Universal Render Pipeline/Particles/Unlit", colour, texture);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.SetFloat("_Cull", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ADD");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static AudioClip AmbienceClip(string file)
        {
            string path = $"{AmbienceDir}/{file}";
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.4f;
                settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                settings.sampleRateOverride = 22050;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.loadInBackground = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>A small looping cloud of glowing specks that follows the camera (ForestAmbience moves it).</summary>
        static ParticleSystem AirParticles(Transform parent, string name, Material mat, int max, float rate, Vector2 life, Vector2 size, Vector3 box,
            Color a, Color c, float wander)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = max;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(a, c);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.01f;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var noise = ps.noise;
            noise.enabled = wander > 0f;
            noise.strength = wander;
            noise.frequency = 0.4f;
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colour.color = fade;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>The 1 x 1 quad standing on its bottom edge (shared with the forest backdrop).</summary>
        static Mesh BackdropQuadMesh(TrackBuild b)
        {
            var quad = AssetDatabase.LoadAssetAtPath<Mesh>($"{b.AssetDir}/BackdropQuad.asset");
            if (quad) return quad;
            quad = new Mesh { name = "BackdropQuad" };
            quad.SetVertices(new List<Vector3> { new(-0.5f, 0f, 0f), new(0.5f, 0f, 0f), new(-0.5f, 1f, 0f), new(0.5f, 1f, 0f) });
            quad.SetUVs(0, new List<Vector2> { new(0f, 0f), new(1f, 0f), new(0f, 1f), new(1f, 1f) });
            quad.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            return SaveMeshAsset(quad, $"{b.AssetDir}/BackdropQuad.asset");
        }

        /// <summary>A pennant: a downward triangle 1 wide hanging from y = 0.</summary>
        static Mesh PennantMesh()
        {
            var mesh = new Mesh { name = "Pennant" };
            mesh.SetVertices(new List<Vector3> { new(-0.5f, 0f, 0f), new(0.5f, 0f, 0f), new(0f, -1f, 0f) });
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMeshAsset(mesh, $"{CodePropsDir}/Code_Pennant.asset");
        }

        /// <summary>Butterfly, 1 across, flying along +Z: two pairs of rounded wings (tinted) on a dark body.</summary>
        static Mesh ButterflyMesh()
        {
            var wings = new List<CombineInstance>();
            var body = new List<CombineInstance>();
            var disc = LowCylinder(10);
            foreach (float side in new[] { -1f, 1f })
            {
                wings.Add(new CombineInstance { mesh = disc, transform = Matrix4x4.TRS(new Vector3(side * 0.26f, 0f, 0.08f), Quaternion.Euler(0f, side * 20f, 0f), new Vector3(0.5f, 0.02f, 0.42f)) });
                wings.Add(new CombineInstance { mesh = disc, transform = Matrix4x4.TRS(new Vector3(side * 0.2f, 0f, -0.17f), Quaternion.Euler(0f, -side * 25f, 0f), new Vector3(0.34f, 0.02f, 0.3f)) });
            }
            body.Add(new CombineInstance { mesh = disc, transform = Matrix4x4.TRS(new Vector3(0f, 0.01f, -0.25f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.06f, 0.5f, 0.06f)) });
            var mesh = MergeColoured("Code_Butterfly", (wings, Color.white), (body, new Color(0.25f, 0.15f, 0.2f, 1f)));
            return SaveMeshAsset(mesh, $"{CodePropsDir}/Code_Butterfly.asset");
        }

        /// <summary>Candy bird, about 1 long, flying along +Z: round tinted body and wings, a little cream beak.</summary>
        static Mesh CandyBirdMesh()
        {
            var tinted = new List<CombineInstance>();
            var beak = new List<CombineInstance>();
            var sphere = LowSphere(8, 6);
            tinted.Add(new CombineInstance { mesh = sphere, transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(0.32f, 0.3f, 0.6f)) });
            tinted.Add(new CombineInstance { mesh = sphere, transform = Matrix4x4.TRS(new Vector3(0f, 0.12f, 0.28f), Quaternion.identity, Vector3.one * 0.26f) });
            foreach (float side in new[] { -1f, 1f })
                tinted.Add(new CombineInstance { mesh = sphere, transform = Matrix4x4.TRS(new Vector3(side * 0.42f, 0.05f, 0f), Quaternion.Euler(0f, 0f, side * 10f), new Vector3(0.6f, 0.05f, 0.3f)) });
            beak.Add(new CombineInstance { mesh = sphere, transform = Matrix4x4.TRS(new Vector3(0f, 0.1f, 0.44f), Quaternion.identity, new Vector3(0.08f, 0.07f, 0.14f)) });
            var mesh = MergeColoured("Code_CandyBird", (tinted, Color.white), (beak, new Color(1f, 0.85f, 0.4f, 0f)));
            return SaveMeshAsset(mesh, $"{CodePropsDir}/Code_CandyBird.asset");
        }
    }
}
