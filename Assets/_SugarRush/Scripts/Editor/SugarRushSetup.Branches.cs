using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace SugarRush.EditorTools
{
    /// <summary>
    /// Road that leaves the single loop: islands that split the road in two lanes for a while
    /// (the fork), and shortcuts — a separate narrow road through the forest that leaves the main
    /// road and joins it again further on (TrackPath.Branch: the AI and the lap counting know it).
    /// </summary>
    public static partial class SugarRushSetup
    {
        /// <summary>A shortcut being built: its own centre line (1 m apart), frames and where it joins the main road.</summary>
        class ShortcutBuild
        {
            public List<Vector3> Center = new(), Right = new();
            public List<float> Dist = new();
            public float Length, HalfWidth, AiChance;
            public int From, To; // main road samples where it leaves / joins
        }

        /// <summary>
        /// Shortcut centre lines (spline through the design knots, first and last knot snapped to the
        /// main road), openings in the main road's curbs/rails/walls where they join, and their lines
        /// added to the road-distance queries so the ground and the forest make room for them.
        /// </summary>
        static void PrepareShortcuts(TrackBuild b)
        {
            var d = b.Design;
            int n = b.Center.Count;
            b.OpenLeft = new bool[n];
            b.OpenRight = new bool[n];
            if (d.Shortcuts == null) return;
            foreach (var (knots, halfWidth, aiChance) in d.Shortcuts)
            {
                var sc = new ShortcutBuild { HalfWidth = halfWidth, AiChance = aiChance };
                sc.From = NearestSample(b, new Vector2(knots[0].x, knots[0].z));
                sc.To = NearestSample(b, new Vector2(knots[^1].x, knots[^1].z));
                var spline = new Spline();
                for (int k = 0; k < knots.Length; k++)
                {
                    Vector3 p = knots[k];
                    if (k == 0) p = b.Center[sc.From];
                    else if (k == knots.Length - 1) p = b.Center[sc.To];
                    else p.y = Mathf.Lerp(b.Center[sc.From].y, b.Center[sc.To].y, k / (float)(knots.Length - 1));
                    spline.Add(new BezierKnot(p));
                }
                spline.SetTangentMode(TangentMode.AutoSmooth);
                var dense = new List<Vector3>();
                int count = Mathf.CeilToInt(spline.GetLength() / 0.25f);
                for (int i = 0; i <= count; i++) dense.Add(spline.EvaluatePosition(i / (float)count));
                // Resample every metre (open line).
                sc.Center.Add(dense[0]);
                float carry = 0f;
                for (int i = 0; i + 1 < dense.Count; i++)
                {
                    float seg = Vector3.Distance(dense[i], dense[i + 1]);
                    float t = 1f - carry;
                    while (t <= seg) { sc.Center.Add(Vector3.Lerp(dense[i], dense[i + 1], t / seg)); t += 1f; }
                    carry = seg - (t - 1f);
                }
                sc.Center.Add(dense[^1]);
                for (int i = 0; i < sc.Center.Count; i++)
                {
                    Vector3 a = sc.Center[Mathf.Max(0, i - 1)], c = sc.Center[Mathf.Min(sc.Center.Count - 1, i + 1)];
                    sc.Right.Add(Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(c - a, Vector3.up).normalized));
                    sc.Dist.Add(sc.Length);
                    if (i + 1 < sc.Center.Count) sc.Length += Vector3.Distance(sc.Center[i], sc.Center[i + 1]);
                }
                b.Shortcuts.Add(sc);

                // Openings on the main road, on the side the shortcut leaves / joins (about 16 m each).
                void Open(int at, Vector3 towards, int from, int to)
                {
                    bool right = Vector3.Dot(towards - b.Center[at], b.Right[at]) > 0f;
                    for (int k = from; k <= to; k++)
                    {
                        int i = ((at + k) % n + n) % n;
                        if (right) b.OpenRight[i] = true; else b.OpenLeft[i] = true;
                    }
                }
                Open(sc.From, sc.Center[Mathf.Min(14, sc.Center.Count - 1)], -3, 16);
                Open(sc.To, sc.Center[Mathf.Max(0, sc.Center.Count - 15)], -16, 3);
                b.Busy.Add(b.Dist[sc.From]);
                b.Busy.Add(b.Dist[sc.To]);
                var coarse = new List<Vector3>();
                for (int i = 0; i < sc.Center.Count; i += 2) coarse.Add(sc.Center[i]);
                b.ExtraRoads.Add(coarse);
            }
        }

        /// <summary>Shortcut roads: surface + curbs (also the collider), rails and walls, all but where they meet the main road.</summary>
        static string BuildShortcuts(TrackBuild b, TrackPath path)
        {
            if (b.Shortcuts.Count == 0) return "shortcuts 0";
            var branches = new List<TrackPath.Branch>();
            int index = 0;
            foreach (var sc in b.Shortcuts)
            {
                int n = sc.Center.Count;
                float hw = sc.HalfWidth, curb = hw + CurbWidth;
                // Sweep a profile along the shortcut (open line). Rows within `trimEnds` metres of either
                // end are left out when `trim` is set (that part overlaps the main road).
                void Sweep(Vector2[] profile, float vPerMetre, List<Vector3> verts, List<Vector2> uvs, List<int> tris, bool trim, float lift = 0.015f)
                {
                    const float trimEnds = 12f;
                    int cols = profile.Length, start = verts.Count;
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < cols; j++)
                        {
                            Vector3 p = sc.Center[i] + sc.Right[i] * profile[j].x + Vector3.up * (profile[j].y + lift);
                            verts.Add(p);
                            uvs.Add(new Vector2(j / (float)Mathf.Max(1, cols - 1), sc.Dist[i] * vPerMetre));
                        }
                    for (int i = 0; i + 1 < n; i++)
                    {
                        if (trim && (sc.Dist[i] < trimEnds || sc.Dist[i + 1] > sc.Length - trimEnds)) continue;
                        for (int j = 0; j + 1 < cols; j++)
                        {
                            int a = start + i * cols + j, c = a + cols;
                            tris.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                        }
                    }
                }
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                List<int> road = new(), curbs = new();
                Sweep(new[] { new Vector2(-hw, 0f), new Vector2(hw, 0f) }, 0.1f, verts, uvs, road, false);
                Sweep(new[] { new Vector2(-curb, 0.1f), new Vector2(-hw, 0.02f) }, 1f / 3f, verts, uvs, curbs, true);
                Sweep(new[] { new Vector2(hw, 0.02f), new Vector2(curb, 0.1f) }, 1f / 3f, verts, uvs, curbs, true);
                var mesh = SaveMeshAsset(NewMesh($"Shortcut{index}", verts, uvs, road, curbs), $"{b.AssetDir}/Shortcut{index}.asset");
                var go = new GameObject($"Shortcut{index}");
                go.transform.SetParent(b.Root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { b.Mats.Road, b.Mats.Curb };
                // What karts drive on (named like the main one: falls and returns look for it).
                var col = new GameObject("RoadCollider");
                col.transform.SetParent(go.transform, false);
                col.AddComponent<MeshCollider>().sharedMesh = mesh;

                var railVerts = new List<Vector3>();
                var railUvs = new List<Vector2>();
                var railTris = new List<int>();
                float inner = curb, outer = curb + RailWidth;
                foreach (var face in new[]
                {
                    new[] { new Vector2(inner, 0.05f), new Vector2(inner, RailHeight) }, new[] { new Vector2(inner, RailHeight), new Vector2(outer, RailHeight) },
                    new[] { new Vector2(outer, RailHeight), new Vector2(outer, 0.05f) }, new[] { new Vector2(-outer, 0.05f), new Vector2(-outer, RailHeight) },
                    new[] { new Vector2(-outer, RailHeight), new Vector2(-inner, RailHeight) }, new[] { new Vector2(-inner, RailHeight), new Vector2(-inner, 0.05f) },
                })
                    Sweep(face, 1f / 3f, railVerts, railUvs, railTris, true);
                var rails = new GameObject($"Shortcut{index}Rails");
                rails.transform.SetParent(b.Root, false);
                rails.AddComponent<MeshFilter>().sharedMesh = SaveMeshAsset(NewMesh($"Shortcut{index}Rails", railVerts, railUvs, railTris), $"{b.AssetDir}/Shortcut{index}Rails.asset");
                rails.AddComponent<MeshRenderer>().sharedMaterial = b.Mats.Curb;

                // Walls (solid boxes) along both sides, except where the shortcut meets the main road.
                var walls = new GameObject($"Shortcut{index}Walls");
                for (int i = 12; i + 12 < n; i += 3)
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var box = new GameObject("TrackWalls");
                        box.transform.SetParent(walls.transform, false);
                        Vector3 forward = Vector3.Cross(sc.Right[i], Vector3.up);
                        box.transform.SetPositionAndRotation(sc.Center[i] + sc.Right[i] * side * (curb + 0.1f + 0.75f) + Vector3.up * 2.5f, Quaternion.LookRotation(forward, Vector3.up));
                        box.AddComponent<BoxCollider>().size = new Vector3(1.5f, 7f, 3.6f);
                    }

                var pts = new List<Vector3>();
                for (int i = 0; i < n; i += 2) pts.Add(sc.Center[i]);
                pts.Add(sc.Center[n - 1]);
                branches.Add(new TrackPath.Branch
                {
                    points = pts.ToArray(), halfWidth = hw, aiChance = sc.AiChance,
                    joinFrom = b.Dist[sc.From], joinTo = b.Dist[sc.To],
                });
                index++;
            }
            path.branches = branches.ToArray();
            return $"shortcuts {branches.Count} ({b.Shortcuts[0].Length:0} m vs {Mathf.Repeat(b.Dist[b.Shortcuts[0].To] - b.Dist[b.Shortcuts[0].From], b.Length):0} m on the road)";
        }

        /// <summary>
        /// Islands splitting the (widened) road in two lanes: a raised lens of candy curb and sugar
        /// grass with gummy trees on it, solid walls round its edge, hazard spots so the AI picks a
        /// lane, and TrackPath.islands so karts put back on the road land in a lane, not on the island.
        /// </summary>
        static string BuildIslands(TrackBuild b, TrackPath path, List<TrackHazards.Spot> spots, Transform root)
        {
            var d = b.Design;
            if (d.Forks == null) return "islands 0";
            int n = b.Center.Count;
            var islands = new List<TrackPath.Island>();
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            List<int> grass = new(), curbs = new();
            var tree = CodeGummyTree(1, 0f, false);
            var treeLow = CodeGummyTree(1, 0f, true);
            var treeMat = CandyMaterial(b, "CandyTree", 0.35f, 0.6f, 0.12f);
            foreach (var (start, length, half) in d.Forks)
            {
                int i0 = NearestSample(b, start);
                int len = Mathf.RoundToInt(length);
                float Half(int k) => half * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * k / len)), 0.6f); // Max: sin(pi) is a hair below 0
                // Surface: per row, an outer striped curb band each side (own UVs, like the road's curbs)
                // and the grass in between, raised 0.3 m.
                int rowStart = verts.Count;
                for (int k = 0; k <= len; k++)
                {
                    int i = (i0 + k) % n;
                    float h = Mathf.Max(0.05f, Half(k)), c = Mathf.Min(1f, h * 0.5f), v = k / 3f;
                    void Add(float x, float y, Vector2 uv) { verts.Add(b.RoadPoint(i, x, y)); uvs.Add(uv); }
                    Add(-h, 0.04f, new Vector2(0f, v));
                    Add(-h + c, 0.3f, new Vector2(1f, v));
                    Vector3 l = b.RoadPoint(i, -h + c, 0.3f), r = b.RoadPoint(i, h - c, 0.3f);
                    Add(-h + c, 0.3f, new Vector2(l.x, l.z) / 10f);
                    Add(h - c, 0.3f, new Vector2(r.x, r.z) / 10f);
                    Add(h - c, 0.3f, new Vector2(1f, v));
                    Add(h, 0.04f, new Vector2(0f, v));
                }
                for (int k = 0; k < len; k++)
                {
                    int a = rowStart + k * 6, c = a + 6;
                    curbs.AddRange(new[] { a, c, a + 1, a + 1, c, c + 1 });
                    grass.AddRange(new[] { a + 2, c + 2, a + 3, a + 3, c + 2, c + 3 });
                    curbs.AddRange(new[] { a + 4, c + 4, a + 5, a + 5, c + 4, c + 5 });
                }
                // Walls round the edge, trees down the middle, hazard spots for the AI.
                for (int k = 2; k < len - 1; k += 2)
                {
                    int i = (i0 + k) % n;
                    float h = Half(k);
                    if (h < 1.4f) continue; // the pointed tips have no wall: a kart that hits them glances off further on
                    b.Frame(i, out var right, out var up);
                    var forward = Vector3.Cross(right, up);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var box = new GameObject("IslandWall");
                        box.transform.SetParent(root, false);
                        box.transform.SetPositionAndRotation(b.RoadPoint(i, side * (h - 0.35f), 0.8f), Quaternion.LookRotation(forward, up));
                        box.AddComponent<BoxCollider>().size = new Vector3(0.7f, 1.6f, 2.6f);
                    }
                    if (k % 4 == 0) spots.Add(new TrackHazards.Spot { center = b.RoadPoint(i, 0f), radius = h + 0.6f, distance = b.Dist[i], lateral = 0f, sticky = false });
                    if (k % 8 == 4 && h > 2.2f)
                        b.AddTinted(tree, treeMat, Matrix4x4.TRS(b.RoadPoint(i, 0f, 0.1f), Quaternion.Euler(0f, k * 37f, 0f), Vector3.one * b.R(7f, 10f)),
                            b.Pick(GummyColors), false, treeLow, 35f, 300f);
                }
                islands.Add(new TrackPath.Island { from = b.Dist[i0], to = b.Dist[(i0 + len) % n], halfWidth = half });
                for (int k = 0; k <= len; k += 10) b.Busy.Add(b.Dist[(i0 + k) % n]);
            }
            var mesh = SaveMeshAsset(NewMesh("Islands", verts, uvs, curbs, grass), $"{b.AssetDir}/Islands.asset");
            var go = new GameObject("Islands");
            go.transform.SetParent(b.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { b.Mats.Curb, b.Mats.Ground };
            path.islands = islands.ToArray();
            return $"islands {islands.Count}";
        }
    }
}
