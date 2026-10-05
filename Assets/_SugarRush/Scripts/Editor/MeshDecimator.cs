using System.Collections.Generic;
using UnityEngine;

namespace SugarRush.EditorTools
{
    /// <summary>
    /// Small edge-collapse simplifier for the character models (quadric error, half-edge
    /// collapses so every kept vertex keeps its own UV and bone weights). UV seams are respected:
    /// a vertex split by a seam only collapses if each of its copies can follow an edge to a
    /// copy of the target. Open borders are locked, and collapses that would flip a triangle
    /// are refused. Works in passes of non-overlapping collapses until the target is reached.
    /// </summary>
    public static class MeshDecimator
    {
        /// <summary>
        /// Reduces <paramref name="triangles"/> (per submesh, indices into the vertex arrays) to
        /// about <paramref name="targetTriangles"/> in total. Returns the old-to-new vertex map
        /// (-1 = dropped) so the caller can compact its vertex attributes.
        /// </summary>
        public static int[] Simplify(IList<Vector3> positions, List<List<int>> triangles, int targetTriangles)
        {
            int n = positions.Count;
            var remap = new int[n];
            for (int i = 0; i < n; i++) remap[i] = i;

            // Same position = same "point" (vertices split by UV seams or hard normals).
            var pointOf = new int[n];
            var pointPos = new List<Vector3>();
            var lookup = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < n; i++)
            {
                var key = Vector3Int.RoundToInt(positions[i] * 100000f);
                if (!lookup.TryGetValue(key, out int p)) { p = pointPos.Count; lookup[key] = p; pointPos.Add(positions[i]); }
                pointOf[i] = p;
            }
            int points = pointPos.Count;

            // Error quadrics from the ORIGINAL surface; a collapse hands its quadric to the
            // surviving point, so the error is always measured against the original shape.
            var quadric = new double[points * 10];
            foreach (var list in triangles)
                for (int t = 0; t + 2 < list.Count; t += 3)
                {
                    int pa = pointOf[list[t]], pb = pointOf[list[t + 1]], pc = pointOf[list[t + 2]];
                    Vector3 A = pointPos[pa], B = pointPos[pb], C = pointPos[pc];
                    Vector3 cross = Vector3.Cross(B - A, C - A);
                    float area = cross.magnitude;
                    if (area <= 1e-12f) continue;
                    Vector3 nrm = cross / area;
                    double d = -Vector3.Dot(nrm, A);
                    foreach (int p in new[] { pa, pb, pc }) AddPlane(quadric, p, nrm, d, 1f); // equal weight: small details count as much as big flat areas
                }

            for (int pass = 0; pass < 40; pass++)
            {
                // Live triangles (submesh, a, b, c) after the collapses so far.
                var tris = new List<(int sub, int a, int b, int c)>();
                for (int s = 0; s < triangles.Count; s++)
                {
                    var list = triangles[s];
                    for (int t = 0; t + 2 < list.Count; t += 3)
                    {
                        int a = Find(remap, list[t]), b = Find(remap, list[t + 1]), c = Find(remap, list[t + 2]);
                        if (pointOf[a] == pointOf[b] || pointOf[b] == pointOf[c] || pointOf[a] == pointOf[c]) continue;
                        tris.Add((s, a, b, c));
                    }
                }
                // Write back the compacted lists so the next pass starts clean.
                foreach (var l in triangles) l.Clear();
                foreach (var (s, a, b, c) in tris) { triangles[s].Add(a); triangles[s].Add(b); triangles[s].Add(c); }
                if (tris.Count <= targetTriangles) break;

                // Adjacency, attribute edges and border detection.
                var pointTris = new List<int>[points];
                var edgeCount = new Dictionary<long, int>();
                var attrEdges = new Dictionary<long, List<(int u, int v)>>();
                for (int t = 0; t < tris.Count; t++)
                {
                    var (_, a, b, c) = tris[t];
                    int pa = pointOf[a], pb = pointOf[b], pc = pointOf[c];
                    foreach (int p in new[] { pa, pb, pc }) (pointTris[p] ??= new List<int>()).Add(t);
                    foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
                    {
                        int pu = pointOf[u], pv = pointOf[v];
                        long key = Key(Mathf.Min(pu, pv), Mathf.Max(pu, pv));
                        edgeCount[key] = edgeCount.TryGetValue(key, out int k) ? k + 1 : 1;
                        AddAttrEdge(attrEdges, Key(pu, pv), u, v);
                        AddAttrEdge(attrEdges, Key(pv, pu), v, u);
                    }
                }
                var locked = new bool[points];
                foreach (var (key, count) in edgeCount)
                    if (count == 1) { locked[(int)(key >> 32)] = true; locked[(int)(key & 0xFFFFFFFF)] = true; }

                // Candidate collapses pu -> pv, cheapest first.
                var candidates = new List<(double cost, int pu, int pv)>();
                foreach (var key in attrEdges.Keys)
                {
                    int pu = (int)(key >> 32), pv = (int)(key & 0xFFFFFFFF);
                    if (locked[pu]) continue;
                    double cost = Evaluate(quadric, pu, pointPos[pv]) + 1e-6 * (pointPos[pu] - pointPos[pv]).sqrMagnitude;
                    candidates.Add((cost, pu, pv));
                }
                candidates.Sort((x, y) => x.cost.CompareTo(y.cost));

                var touched = new bool[points];
                int removed = 0, wanted = tris.Count - targetTriangles;
                // Do at most a quarter of the remaining work per pass, so costs stay fresh.
                int passBudget = Mathf.Max(wanted / 4, 1);
                foreach (var (_, pu, pv) in candidates)
                {
                    if (removed >= passBudget) break;
                    if (touched[pu] || touched[pv]) continue;

                    // Every copy of pu must have an edge to a copy of pv to follow.
                    var moves = new List<(int u, int v)>();
                    bool ok = true;
                    var copies = new HashSet<int>();
                    foreach (int t in pointTris[pu])
                    {
                        var (_, a, b, c) = tris[t];
                        foreach (int x in new[] { a, b, c }) if (pointOf[x] == pu) copies.Add(x);
                    }
                    var edges = attrEdges[Key(pu, pv)];
                    foreach (int u in copies)
                    {
                        int target = -1;
                        foreach (var (eu, ev) in edges) if (eu == u) { target = ev; break; }
                        if (target < 0) { ok = false; break; }
                        moves.Add((u, target));
                    }
                    if (!ok) continue;

                    // No flipped or collapsed-to-a-sliver triangles.
                    int dying = 0;
                    foreach (int t in pointTris[pu])
                    {
                        var (_, a, b, c) = tris[t];
                        int qa = pointOf[a], qb = pointOf[b], qc = pointOf[c];
                        if (qa == pv || qb == pv || qc == pv) { dying++; continue; }
                        Vector3 A = pointPos[qa], B = pointPos[qb], C = pointPos[qc];
                        Vector3 before = Vector3.Cross(B - A, C - A);
                        if (qa == pu) A = pointPos[pv];
                        if (qb == pu) B = pointPos[pv];
                        if (qc == pu) C = pointPos[pv];
                        Vector3 after = Vector3.Cross(B - A, C - A);
                        if (after.sqrMagnitude < 1e-14f || Vector3.Dot(before.normalized, after.normalized) < 0.2f) { ok = false; break; }
                    }
                    if (!ok || dying == 0) continue;

                    foreach (var (u, v) in moves) remap[u] = v;
                    for (int k = 0; k < 10; k++) quadric[pv * 10 + k] += quadric[pu * 10 + k];
                    removed += dying;
                    touched[pu] = touched[pv] = true;
                    foreach (int t in pointTris[pu])
                    {
                        var (_, a, b, c) = tris[t];
                        touched[pointOf[a]] = touched[pointOf[b]] = touched[pointOf[c]] = true;
                    }
                }
                if (removed == 0) break;
            }

            // Old -> new vertex indices for the vertices still used.
            var used = new bool[n];
            foreach (var l in triangles) foreach (int i in l) used[i] = true;
            var map = new int[n];
            int next = 0;
            for (int i = 0; i < n; i++) map[i] = used[i] ? next++ : -1;
            foreach (var l in triangles) for (int i = 0; i < l.Count; i++) l[i] = map[l[i]];
            return map;
        }

        static int Find(int[] remap, int i)
        {
            while (remap[i] != i) i = remap[i] = remap[remap[i]];
            return i;
        }

        static long Key(int a, int b) => ((long)a << 32) | (uint)b;

        static void AddAttrEdge(Dictionary<long, List<(int, int)>> map, long key, int u, int v)
        {
            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<(int, int)>();
            if (!list.Contains((u, v))) list.Add((u, v));
        }

        // Symmetric 4x4 quadric stored as 10 doubles.
        static void AddPlane(double[] q, int p, Vector3 n, double d, float weight)
        {
            double a = n.x, b = n.y, c = n.z;
            int o = p * 10;
            q[o] += weight * a * a; q[o + 1] += weight * a * b; q[o + 2] += weight * a * c; q[o + 3] += weight * a * d;
            q[o + 4] += weight * b * b; q[o + 5] += weight * b * c; q[o + 6] += weight * b * d;
            q[o + 7] += weight * c * c; q[o + 8] += weight * c * d; q[o + 9] += weight * d * d;
        }

        static double Evaluate(double[] q, int p, Vector3 v)
        {
            int o = p * 10;
            double x = v.x, y = v.y, z = v.z;
            return q[o] * x * x + 2 * q[o + 1] * x * y + 2 * q[o + 2] * x * z + 2 * q[o + 3] * x
                 + q[o + 4] * y * y + 2 * q[o + 5] * y * z + 2 * q[o + 6] * y
                 + q[o + 7] * z * z + 2 * q[o + 8] * z + q[o + 9];
        }
    }
}
