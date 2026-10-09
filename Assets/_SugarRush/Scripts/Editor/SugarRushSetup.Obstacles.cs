using System.Collections.Generic;
using UnityEngine;

namespace SugarRush.EditorTools
{
    /// <summary>
    /// Moving obstacles for spline tracks (see MovingObstacles): rolling gum balls, candy-cane
    /// pendulums on arches, gummy bears crossing the road and a stretch where gummies fall from
    /// the trees. Placed on free, fairly straight road (away from jumps, the bridge, hazards and
    /// tunnels' entrances), and marked busy so Ralph and the rest keep clear.
    /// </summary>
    public static partial class SugarRushSetup
    {
        static string BuildMovingObstacles(TrackBuild b, TrackPath path)
        {
            var d = b.Design;
            int n = b.Center.Count;
            var root = new GameObject("MovingObstacles");
            var mo = root.AddComponent<MovingObstacles>();
            mo.path = path;
            const string lit = "Universal Render Pipeline/Lit";

            Rigidbody Kinematic(GameObject go)
            {
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                return rb;
            }
            Material Glossy(string name, Color c)
            {
                var m = GetMaterial($"{b.MaterialDir}/Obstacles/{name}", lit, c);
                m.SetFloat("_Smoothness", 0.85f);
                UnityEditor.EditorUtility.SetDirty(m);
                return m;
            }
            // A free spot on fairly straight road, at least `range` m from anything busy.
            int Spot(float fraction, float range, float maxBend)
            {
                int i = Mathf.FloorToInt(fraction * n) % n;
                for (int tries = 0; tries < 120; tries++, i = (i + 4) % n)
                {
                    float dist = b.Dist[i];
                    if (dist < 60f || dist > b.Length - 30f || b.HiddenAtDistance(dist) || b.BusyNear(dist, range)) continue;
                    if (Vector3.Angle(b.Right[(i - 8 + n) % n], b.Right[(i + 8) % n]) > maxBend) continue;
                    return i;
                }
                return -1;
            }

            // Gum balls rolling from one edge to the other.
            var balls = new List<MovingObstacles.Roller>();
            var gumColours = new[] { new Color(1f, 0.55f, 0.78f), new Color(0.5f, 0.8f, 1f), new Color(0.7f, 0.95f, 0.6f) };
            if (d.GumBalls != null)
                foreach (float f in d.GumBalls)
                {
                    int i = Spot(f, 18f, 22f);
                    if (i < 0) continue;
                    const float radius = 1.6f;
                    var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = $"GumBall{balls.Count}";
                    go.transform.SetParent(root.transform, false);
                    go.transform.localScale = Vector3.one * radius * 2f;
                    go.GetComponent<Renderer>().sharedMaterial = Glossy($"GumBall{balls.Count % gumColours.Length}", gumColours[balls.Count % gumColours.Length]);
                    // Rolls in from the forest and out the other side, straight through the rails:
                    // it pushes karts across the road but never pins one against a wall.
                    float edge = b.Width[i] + CurbWidth + RailWidth + radius + 1.5f;
                    var r = new MovingObstacles.Roller
                    {
                        body = Kinematic(go), radius = radius, period = b.R(5.5f, 7f), phase = b.R(0f, 1f),
                        from = b.RoadPoint(i, -edge, radius), to = b.RoadPoint(i, edge, radius),
                    };
                    go.transform.position = r.from;
                    balls.Add(r);
                    b.Busy.Add(b.Dist[i]);
                }
            mo.rollers = balls.ToArray();

            // Candy-cane pendulums hanging from an arch over the road, swinging across it.
            var pendulums = new List<MovingObstacles.Pendulum>();
            var caneMat = GetMaterial("Track/CandyCane", lit, Color.white,
                UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{GeneratedDir}/candy_stripes.png"), new Vector2(2f, 6f));
            if (d.Pendulums != null)
                foreach (float f in d.Pendulums)
                {
                    int i = Spot(f, 16f, 25f);
                    if (i < 0) continue;
                    b.Frame(i, out var right, out var up);
                    Vector3 forward = Vector3.Cross(right, up);
                    float half = b.Width[i] + CurbWidth + RailWidth + 0.8f, top = 10.5f;
                    var arch = new GameObject($"PendulumArch{pendulums.Count}");
                    arch.transform.SetParent(root.transform, false);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        Object.DestroyImmediate(post.GetComponent<Collider>());
                        post.transform.SetParent(arch.transform, false);
                        post.transform.position = b.Center[i] + right * side * half + Vector3.up * (top * 0.5f - 0.5f);
                        post.transform.localScale = new Vector3(0.6f, top * 0.5f + 0.5f, 0.6f);
                        post.GetComponent<Renderer>().sharedMaterial = caneMat;
                    }
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.DestroyImmediate(bar.GetComponent<Collider>());
                    bar.transform.SetParent(arch.transform, false);
                    bar.transform.position = b.Center[i] + Vector3.up * top;
                    bar.transform.rotation = Quaternion.FromToRotation(Vector3.up, right);
                    bar.transform.localScale = new Vector3(0.5f, half, 0.5f);
                    bar.GetComponent<Renderer>().sharedMaterial = caneMat;

                    const float length = 9.4f, bobRadius = 1.15f;
                    var pivot = b.Center[i] + Vector3.up * top;
                    var arm = new GameObject($"PendulumArm{pendulums.Count}").transform;
                    arm.SetParent(root.transform, false);
                    arm.position = pivot;
                    var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.DestroyImmediate(stick.GetComponent<Collider>());
                    stick.transform.SetParent(arm, false);
                    stick.transform.localPosition = Vector3.down * length * 0.5f;
                    stick.transform.localScale = new Vector3(0.28f, length * 0.5f, 0.28f);
                    stick.GetComponent<Renderer>().sharedMaterial = caneMat;
                    var bob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bob.name = $"PendulumCandy{pendulums.Count}";
                    bob.transform.SetParent(root.transform, false);
                    bob.transform.position = pivot + Vector3.down * length;
                    bob.transform.localScale = Vector3.one * bobRadius * 2f;
                    bob.GetComponent<Renderer>().sharedMaterial = Glossy("PendulumCandy", new Color(1f, 0.45f, 0.6f));
                    pendulums.Add(new MovingObstacles.Pendulum
                    {
                        bob = Kinematic(bob), arm = arm, pivot = pivot, swingAxis = forward, length = length,
                        amplitude = 52f, period = b.R(3.2f, 3.9f), phase = b.R(0f, 1f),
                    });
                    b.Busy.Add(b.Dist[i]);
                }
            mo.pendulums = pendulums.ToArray();

            // Gummy bears walking across the road (they wait on the curb in between).
            var walkers = new List<MovingObstacles.Walker>();
            var bearMesh = CodeGummyBear(false);
            var bearMat = CandyMaterial(b, "CandyBear", 0f, 0.85f, 0.18f);
            if (d.CrossingBears != null)
                foreach (float f in d.CrossingBears)
                {
                    int i = Spot(f, 15f, 35f);
                    if (i < 0) continue;
                    const float size = 2.4f;
                    var go = new GameObject($"CrossingBear{walkers.Count}");
                    go.transform.SetParent(root.transform, false);
                    var model = new GameObject("Model");
                    model.transform.SetParent(go.transform, false);
                    model.transform.localScale = Vector3.one * size;
                    model.AddComponent<MeshFilter>().sharedMesh = bearMesh;
                    var mr = model.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = bearMat;
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", GummyColors[(walkers.Count * 3 + 2) % GummyColors.Length]);
                    mr.SetPropertyBlock(block);
                    var box = go.AddComponent<BoxCollider>();
                    box.size = new Vector3(size * 0.5f, size * 0.95f, size * 0.45f);
                    box.center = new Vector3(0f, size * 0.47f, 0f);
                    float edge = b.Width[i] - 0.4f;
                    var w = new MovingObstacles.Walker
                    {
                        body = Kinematic(go), walkTime = b.R(3f, 3.6f), waitTime = b.R(1.8f, 2.6f), phase = b.R(0f, 1f),
                        from = b.RoadPoint(i, -edge), to = b.RoadPoint(i, edge),
                    };
                    go.transform.position = w.from;
                    walkers.Add(w);
                    b.Busy.Add(b.Dist[i]);
                }
            // The shortcut has its own: a gummy bear crossing it and a gum ball rolling across.
            foreach (var sc in b.Shortcuts)
            {
                int mid = sc.Center.Count / 2, late = sc.Center.Count * 3 / 4;
                const float size = 2.2f;
                var go = new GameObject($"CrossingBear{walkers.Count}");
                go.transform.SetParent(root.transform, false);
                var model = new GameObject("Model");
                model.transform.SetParent(go.transform, false);
                model.transform.localScale = Vector3.one * size;
                model.AddComponent<MeshFilter>().sharedMesh = bearMesh;
                var mr = model.AddComponent<MeshRenderer>();
                mr.sharedMaterial = bearMat;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", GummyColors[(walkers.Count * 3 + 2) % GummyColors.Length]);
                mr.SetPropertyBlock(block);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(size * 0.5f, size * 0.95f, size * 0.45f);
                box.center = new Vector3(0f, size * 0.47f, 0f);
                var w = new MovingObstacles.Walker
                {
                    body = Kinematic(go), walkTime = 2.6f, waitTime = 1.6f, phase = b.R(0f, 1f),
                    from = sc.Center[mid] - sc.Right[mid] * (sc.HalfWidth - 0.4f), to = sc.Center[mid] + sc.Right[mid] * (sc.HalfWidth - 0.4f),
                };
                go.transform.position = w.from;
                walkers.Add(w);

                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.name = $"GumBall{balls.Count}";
                ball.transform.SetParent(root.transform, false);
                ball.transform.localScale = Vector3.one * 2.4f;
                ball.GetComponent<Renderer>().sharedMaterial = Glossy("GumBall0", gumColours[0]);
                var roller = new MovingObstacles.Roller
                {
                    body = Kinematic(ball), radius = 1.2f, period = 4.6f, phase = b.R(0f, 1f),
                    from = sc.Center[late] - sc.Right[late] * (sc.HalfWidth + CurbWidth + RailWidth + 2.7f) + Vector3.up * 1.2f,
                    to = sc.Center[late] + sc.Right[late] * (sc.HalfWidth + CurbWidth + RailWidth + 2.7f) + Vector3.up * 1.2f,
                };
                ball.transform.position = roller.from;
                balls.Add(roller);
            }
            mo.rollers = balls.ToArray();
            mo.walkers = walkers.ToArray();

            // Gummies falling from the trees on a stretch, a shadow warning where each one lands.
            if (d.GummyRain.length > 0f)
            {
                int start = NearestSample(b, d.GummyRain.start);
                var rain = new MovingObstacles.GummyRain { from = b.Dist[start], to = b.Dist[start] + d.GummyRain.length };
                var dot = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{GeneratedDir}/SoftDot.png");
                var shadowMat = GetMaterial($"{b.MaterialDir}/Obstacles/DropShadow", "Universal Render Pipeline/Particles/Unlit", new Color(0.25f, 0.1f, 0.2f, 0.55f), dot);
                shadowMat.SetFloat("_Surface", 1f);
                shadowMat.SetOverrideTag("RenderType", "Transparent");
                shadowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                shadowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                shadowMat.SetInt("_ZWrite", 0);
                shadowMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                shadowMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                var drop = LowSphere(10, 7);
                var shadowQuad = new Mesh { name = "DropShadow" };
                shadowQuad.SetVertices(new List<Vector3> { new(-0.5f, 0f, -0.5f), new(0.5f, 0f, -0.5f), new(-0.5f, 0f, 0.5f), new(0.5f, 0f, 0.5f) });
                shadowQuad.SetUVs(0, new List<Vector2> { new(0f, 0f), new(1f, 0f), new(0f, 1f), new(1f, 1f) });
                shadowQuad.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
                shadowQuad.RecalculateNormals();
                shadowQuad = SaveMeshAsset(shadowQuad, $"{b.AssetDir}/DropShadow.asset");
                drop = SaveMeshAsset(drop, $"{b.AssetDir}/GummyDrop.asset");
                const int pool = 6;
                rain.drops = new Rigidbody[pool];
                rain.shadows = new Transform[pool];
                for (int k = 0; k < pool; k++)
                {
                    var go = new GameObject($"FallingGummy{k}");
                    go.transform.SetParent(root.transform, false);
                    var model = new GameObject("Model");
                    model.transform.SetParent(go.transform, false);
                    model.transform.localScale = new Vector3(1f, 0.85f, 1f);
                    model.AddComponent<MeshFilter>().sharedMesh = drop;
                    model.AddComponent<MeshRenderer>().sharedMaterial = GummyMaterial(b, $"Gummy{k % GummyColors.Length}", GummyColors[k % GummyColors.Length]);
                    go.AddComponent<SphereCollider>().radius = 0.45f;
                    rain.drops[k] = Kinematic(go);
                    go.SetActive(false);
                    var shadow = new GameObject($"FallingGummyShadow{k}");
                    shadow.transform.SetParent(root.transform, false);
                    shadow.AddComponent<MeshFilter>().sharedMesh = shadowQuad;
                    var smr = shadow.AddComponent<MeshRenderer>();
                    smr.sharedMaterial = shadowMat;
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rain.shadows[k] = shadow.transform;
                    shadow.SetActive(false);
                }
                mo.rain = rain;
                for (float x = rain.from; x <= rain.to; x += 10f) b.Busy.Add(Mathf.Repeat(x, b.Length));
            }
            return $"gum balls {balls.Count}, pendulums {pendulums.Count}, crossing bears {walkers.Count}, gummy rain {(d.GummyRain.length > 0f ? d.GummyRain.length : 0f):0} m";
        }
    }
}
