using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

namespace SugarRush.EditorTools
{
    /// <summary>
    /// Decoration props downloaded from Sketchfab (GLB, or FBX inside a ZIP, in ~/Downloads):
    /// every renderer is baked into ONE light mesh (one submesh per source material), simplified
    /// to a triangle budget, stood on its origin with height 1 (long side along +Z) and saved to
    /// Art/Props/&lt;Id&gt;.asset. Textures are dropped (the track paints the slots in pastel
    /// colours) or kept small and pastel-tinted when they carry the look (stripes, glaze).
    /// Props are only rebuilt when their mesh is missing: the results are in the repo, the
    /// downloads are not (like the characters).
    /// </summary>
    public static class PropImport
    {
        public const string PropsDir = "Assets/_SugarRush/Art/Props";
        const string TempDir = "Assets/_PropImport";

        class Def
        {
            public string Id;
            /// <summary>GLB file, or ZIP file in Downloads.</summary>
            public string Source;
            /// <summary>FBX path inside the ZIP.</summary>
            public string ZipModel;
            /// <summary>Texture path inside the ZIP (null = no texture, or the GLB's own).</summary>
            public string ZipTexture;
            public int MaxTriangles;
            /// <summary>Keep UVs and a small pastel copy of the texture.</summary>
            public bool KeepTexture;
            /// <summary>
            /// Instead of a texture: each triangle takes the texture colour under it, the colours
            /// are grouped into up to <see cref="MaxColors"/> flat pastel slots (stored as vertex
            /// colours) and the UVs go, so seams no longer stop the simplifier.
            /// </summary>
            public bool ColorSlots;
            public int MaxColors = 5;
            /// <summary>Loose pieces smaller than this fraction of the model (sprinkles) become a 12-triangle box.</summary>
            public float SmallPart;
            public int TextureSize = 128;
            /// <summary>Only renderers whose name / material starts with this (null = all).</summary>
            public string RendererPrefix, MaterialName;
            /// <summary>Source material left out (e.g. a ground plate under the model).</summary>
            public string ExcludeMaterial;
        }

        static readonly Def[] Defs =
        {
            new() { Id = "SugarTree", Source = "sugar_rush_tree.glb", MaxTriangles = 0, KeepTexture = true, TextureSize = 64 },
            new() { Id = "CandyCanes", Source = "simple_candy_canes.glb", MaxTriangles = 1100, KeepTexture = true },
            new() { Id = "GummyBear", Source = "red-gummy-bear.zip", ZipModel = "source/GummyBear_V2.fbx", MaxTriangles = 1100 },
            // Light copies for the forest filler (hundreds of them; the detailed ones are for close-ups and giants).
            new() { Id = "GummyBearLow", Source = "red-gummy-bear.zip", ZipModel = "source/GummyBear_V2.fbx", MaxTriangles = 280 },
            new() { Id = "CandyCanesLow", Source = "simple_candy_canes.glb", MaxTriangles = 360, KeepTexture = true },
            new() { Id = "HalfDonutPink", Source = "half-donuts.zip", ZipModel = "source/donut.fbx", ZipTexture = "textures/donut 1 af.png", MaterialName = "lambert2", MaxTriangles = 900, ColorSlots = true, SmallPart = 0.12f },
            new() { Id = "HalfDonutChoco", Source = "half-donuts.zip", ZipModel = "source/donut.fbx", ZipTexture = "textures/donut 2 af.png", MaterialName = "la2", MaxTriangles = 900, ColorSlots = true, SmallPart = 0.12f },
            new() { Id = "HalfDonutBlue", Source = "half-donuts.zip", ZipModel = "source/donut.fbx", ZipTexture = "textures/donut 3 af.png", MaterialName = "lambert4", MaxTriangles = 900, ColorSlots = true, SmallPart = 0.12f },
            new() { Id = "CroissantDolphin", Source = "croissant-dolphins.zip", ZipModel = "source/All.fbx", ZipTexture = "textures/DolphSnack_Low_polySurface1_BaseColor.png", RendererPrefix = "Dolph", MaxTriangles = 1400, ColorSlots = true, MaxColors = 1 },
            new() { Id = "JapaneseBridge", Source = "japanese_bridge.glb", MaxTriangles = 6000 },
            new() { Id = "ChocolateBunny", Source = "chocolate_easter_bunny.glb", MaxTriangles = 4000 },
            new() { Id = "CinnamonDelight", Source = "cinnamon_delight.glb", MaxTriangles = 900, KeepTexture = true },
        };

        public static Mesh Load(string id) => AssetDatabase.LoadAssetAtPath<Mesh>($"{PropsDir}/{id}.asset");
        public static Texture2D LoadTexture(string id) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{PropsDir}/{id}_tex.png");

        [MenuItem("Sugar Rush/Props (from Downloads)")]
        public static string ImportAll() => Import(false);

        /// <summary>Imports every prop whose mesh is missing (or all of them with <paramref name="force"/>; or just <paramref name="only"/>).</summary>
        public static string Import(bool force, string only = null)
        {
            var log = new System.Text.StringBuilder("Props:");
            string downloads = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads");
            EnsureFolder(PropsDir);
            foreach (var def in Defs)
            {
                if (only != null && def.Id != only) continue;
                if (!force && Load(def.Id)) continue;
                string source = Path.Combine(downloads, def.Source);
                if (!File.Exists(source)) { log.Append($" {def.Id} missing;"); continue; }
                try { log.Append(" " + ImportOne(def, source) + ";"); }
                catch (System.Exception e) { log.Append($" {def.Id} FAILED {e.Message};"); Debug.LogException(e); }
                finally { AssetDatabase.DeleteAsset(TempDir); }
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static string ImportOne(Def def, string source)
        {
            EnsureFolder(TempDir);
            GameObject prefab;
            Texture sourceTexture = null;
            byte[] zipTexture = null;
            if (source.EndsWith(".glb"))
            {
                GlbImport.Import(source, TempDir, def.Id, 1f, 256);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TempDir}/{def.Id}.prefab");
                foreach (var rend in prefab.GetComponentsInChildren<Renderer>(true))
                    if (rend.sharedMaterial && rend.sharedMaterial.mainTexture) { sourceTexture = rend.sharedMaterial.mainTexture; break; }
            }
            else
            {
                using var zip = ZipFile.OpenRead(source);
                string fbxPath = $"{TempDir}/{def.Id}.fbx";
                zip.GetEntry(def.ZipModel).ExtractToFile(fbxPath, true);
                if (def.ZipTexture != null)
                {
                    using var s = zip.GetEntry(def.ZipTexture).Open();
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    zipTexture = ms.ToArray();
                }
                AssetDatabase.ImportAsset(fbxPath);
                var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.importAnimation = false;
                importer.SaveAndReimport();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            }

            // Bake every chosen renderer into world space, one triangle list per source material.
            var instance = Object.Instantiate(prefab);
            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            var slots = new List<string>();
            var triangles = new List<List<int>>();
            foreach (var rend in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (def.RendererPrefix != null && !rend.name.StartsWith(def.RendererPrefix)) continue;
                Mesh mesh;
                Matrix4x4 matrix;
                bool baked = false;
                if (rend is SkinnedMeshRenderer smr)
                {
                    mesh = new Mesh();
                    smr.BakeMesh(mesh, true);
                    matrix = smr.transform.localToWorldMatrix;
                    baked = true;
                }
                else
                {
                    var mf = rend.GetComponent<MeshFilter>();
                    if (!mf || !mf.sharedMesh) continue;
                    mesh = mf.sharedMesh;
                    matrix = rend.transform.localToWorldMatrix;
                }
                var mats = rend.sharedMaterials;
                var verts = mesh.vertices;
                var meshUv = mesh.uv;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    string matName = sub < mats.Length && mats[sub] ? mats[sub].name : "default";
                    if (def.MaterialName != null && matName != def.MaterialName) continue;
                    if (def.ExcludeMaterial != null && matName == def.ExcludeMaterial) continue;
                    int slot = slots.IndexOf(matName);
                    if (slot < 0) { slot = slots.Count; slots.Add(matName); triangles.Add(new List<int>()); }
                    int start = positions.Count;
                    foreach (var v in verts) positions.Add(matrix.MultiplyPoint3x4(v));
                    for (int i = 0; i < verts.Length; i++) uvs.Add(i < meshUv.Length ? meshUv[i] : Vector2.zero);
                    foreach (int t in mesh.GetTriangles(sub)) triangles[slot].Add(start + t);
                }
                if (baked) Object.DestroyImmediate(mesh);
            }
            Object.DestroyImmediate(instance);
            if (positions.Count == 0) throw new System.Exception("no geometry");

            // Unused vertices (other submeshes / filtered renderers) out first, then simplify.
            var used = new bool[positions.Count];
            foreach (var list in triangles) foreach (int t in list) used[t] = true;
            Compact(positions, uvs, triangles, Remap(used));
            int before = 0;
            foreach (var list in triangles) before += list.Count / 3;
            // To about 1 m first: the simplifier and the welding work in metres (some sources are a few cm).
            var raw = new Bounds(positions[0], Vector3.zero);
            foreach (var p in positions) raw.Encapsulate(p);
            float unit = 1f / Mathf.Max(raw.size.y, 1e-6f);
            for (int i = 0; i < positions.Count; i++) positions[i] = (positions[i] - raw.center) * unit;
            List<Color> slotColors = null;
            if (def.ColorSlots && zipTexture != null)
            {
                var readable = new Texture2D(2, 2);
                readable.LoadImage(zipTexture);
                slotColors = SplitByColor(positions, uvs, triangles, readable, def.MaxColors);
                Object.DestroyImmediate(readable);
                slots = slotColors.ConvertAll(c => "#" + ColorUtility.ToHtmlStringRGB(c));
            }
            if (def.SmallPart > 0f) ReplaceSmallParts(positions, uvs, triangles, def.SmallPart);
            if (def.MaxTriangles > 0 && before > def.MaxTriangles)
            {
                if (!def.KeepTexture && slotColors == null) WeldPositions(positions, uvs, triangles);
                // The simplifier rewrites the triangles itself; the map only compacts the vertices.
                var map = MeshDecimator.Simplify(positions, triangles, def.MaxTriangles);
                Compact(positions, uvs, null, map);
            }

            // Stand on the origin, long side along +Z, height 1.
            var bounds = new Bounds(positions[0], Vector3.zero);
            foreach (var p in positions) bounds.Encapsulate(p);
            var turn = bounds.size.x > bounds.size.z * 1.15f ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            float scale = 1f / Mathf.Max(bounds.size.y, 1e-4f);
            for (int i = 0; i < positions.Count; i++)
                positions[i] = turn * (new Vector3(positions[i].x - bounds.center.x, positions[i].y - bounds.min.y, positions[i].z - bounds.center.z) * scale);

            var result = new Mesh { name = def.Id };
            if (positions.Count > 65000) result.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            result.SetVertices(positions);
            if (def.KeepTexture) result.SetUVs(0, uvs);
            result.subMeshCount = triangles.Count;
            for (int s = 0; s < triangles.Count; s++) result.SetTriangles(triangles[s], s);
            if (slotColors != null)
            {
                var colors = new Color[positions.Count];
                for (int s = 0; s < triangles.Count; s++)
                    foreach (int t in triangles[s]) colors[t] = slotColors[s];
                result.colors = colors;
            }
            result.RecalculateNormals();
            result.RecalculateBounds();
            result.RecalculateTangents();
            string meshPath = $"{PropsDir}/{def.Id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing) { EditorUtility.CopySerialized(result, existing); Object.DestroyImmediate(result); result = existing; }
            else AssetDatabase.CreateAsset(result, meshPath);

            string texInfo = "";
            if (def.KeepTexture && (sourceTexture || zipTexture != null))
            {
                Texture tex = sourceTexture;
                Texture2D loaded = null;
                if (zipTexture != null) { loaded = new Texture2D(2, 2); loaded.LoadImage(zipTexture); tex = loaded; }
                SavePastelTexture(tex, $"{PropsDir}/{def.Id}_tex.png", def.TextureSize);
                if (loaded) Object.DestroyImmediate(loaded);
                texInfo = $" tex{def.TextureSize}";
            }
            int after = 0;
            foreach (var list in triangles) after += list.Count / 3;
            var size = bounds.size * scale;
            return $"{def.Id} {before}->{after} tris, slots [{string.Join(",", slots)}], size {(turn == Quaternion.identity ? size : new Vector3(size.z, size.y, size.x))}{texInfo}";
        }

        /// <summary>Saturation down, brightness up: the pastel look of the game.</summary>
        public static Color Pastel(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB(h, s * 0.72f, Mathf.Lerp(v, 1f, 0.3f));
        }

        /// <summary>The flat colour stored for a submesh by <see cref="Def.ColorSlots"/> (white if none).</summary>
        public static Color SlotColor(Mesh mesh, int submesh)
        {
            var colors = mesh.colors;
            if (colors.Length == 0) return Color.white;
            var tris = mesh.GetTriangles(submesh);
            return tris.Length > 0 ? colors[tris[0]] : Color.white;
        }

        /// <summary>
        /// Regroups triangles by the texture colour at their centre (k-means, up to maxColors
        /// groups) into new slots, one pastel colour each, rebuilding the vertices per slot
        /// (merged by position inside a slot). UVs are dropped.
        /// </summary>
        static List<Color> SplitByColor(List<Vector3> positions, List<Vector2> uvs, List<List<int>> triangles, Texture2D tex, int maxColors)
        {
            var all = new List<int>();
            foreach (var list in triangles) all.AddRange(list);
            int count = all.Count / 3;
            var samples = new Color[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 uv = (uvs[all[i * 3]] + uvs[all[i * 3 + 1]] + uvs[all[i * 3 + 2]]) / 3f;
                samples[i] = tex.GetPixelBilinear(uv.x, uv.y);
            }
            float Dist(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

            // Farthest-point seeds, then a few k-means rounds.
            var centres = new List<Color> { samples[0] };
            while (centres.Count < maxColors)
            {
                int far = 0;
                float farDist = -1f;
                for (int i = 0; i < count; i++)
                {
                    float d = float.MaxValue;
                    foreach (var c in centres) d = Mathf.Min(d, Dist(samples[i], c));
                    if (d > farDist) { farDist = d; far = i; }
                }
                if (farDist < 0.15f) break;
                centres.Add(samples[far]);
            }
            var label = new int[count];
            for (int round = 0; round < 8; round++)
            {
                for (int i = 0; i < count; i++)
                {
                    int best = 0;
                    float bestD = float.MaxValue;
                    for (int c = 0; c < centres.Count; c++) { float d = Dist(samples[i], centres[c]); if (d < bestD) { bestD = d; best = c; } }
                    label[i] = best;
                }
                var sum = new Color[centres.Count];
                var n = new int[centres.Count];
                for (int i = 0; i < count; i++) { sum[label[i]] += samples[i]; n[label[i]]++; }
                for (int c = 0; c < centres.Count; c++) if (n[c] > 0) centres[c] = sum[c] / n[c];
            }

            // Rebuild: one vertex per (position, slot); empty groups dropped.
            var used = new List<int>();
            for (int c = 0; c < centres.Count; c++) if (System.Array.IndexOf(label, c) >= 0) used.Add(c);
            var newPos = new List<Vector3>();
            var lookup = new Dictionary<(Vector3Int, int), int>();
            var newTris = new List<List<int>>();
            foreach (int c in used) newTris.Add(new List<int>());
            for (int i = 0; i < count; i++)
            {
                int slot = used.IndexOf(label[i]);
                for (int k = 0; k < 3; k++)
                {
                    var p = positions[all[i * 3 + k]];
                    var key = (Vector3Int.RoundToInt(p * 10000f), slot);
                    if (!lookup.TryGetValue(key, out int idx)) { idx = newPos.Count; lookup[key] = idx; newPos.Add(p); }
                    newTris[slot].Add(idx);
                }
            }
            foreach (var list in newTris)
                for (int k = list.Count - 3; k >= 0; k -= 3)
                    if (list[k] == list[k + 1] || list[k + 1] == list[k + 2] || list[k] == list[k + 2]) list.RemoveRange(k, 3);
            positions.Clear(); positions.AddRange(newPos);
            uvs.Clear();
            for (int i = 0; i < newPos.Count; i++) uvs.Add(Vector2.zero);
            triangles.Clear(); triangles.AddRange(newTris);
            return used.ConvertAll(c => Pastel(centres[c]));
        }

        /// <summary>
        /// Every connected piece (per slot) smaller than <paramref name="fraction"/> of the whole
        /// model is replaced by a box along its longest direction: a sprinkle needs 12 triangles, not 760.
        /// </summary>
        static void ReplaceSmallParts(List<Vector3> positions, List<Vector2> uvs, List<List<int>> triangles, float fraction)
        {
            var whole = new Bounds(positions[0], Vector3.zero);
            foreach (var p in positions) whole.Encapsulate(p);
            float limit = whole.size.magnitude * fraction;

            // Pieces = vertices joined by triangles (union-find), positions merged.
            var parent = new int[positions.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            var byPos = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < positions.Count; i++)
            {
                var key = Vector3Int.RoundToInt(positions[i] * 10000f);
                if (byPos.TryGetValue(key, out int other)) parent[Find(i)] = Find(other); else byPos[key] = i;
            }
            foreach (var list in triangles)
                for (int k = 0; k < list.Count; k += 3)
                {
                    parent[Find(list[k + 1])] = Find(list[k]);
                    parent[Find(list[k + 2])] = Find(list[k]);
                }

            for (int s = 0; s < triangles.Count; s++)
            {
                var list = triangles[s];
                var pieces = new Dictionary<int, List<int>>();
                for (int k = 0; k < list.Count; k += 3)
                {
                    int root = Find(list[k]);
                    if (!pieces.TryGetValue(root, out var tris)) pieces[root] = tris = new List<int>();
                    tris.Add(k);
                }
                var kept = new List<int>();
                foreach (var tris in pieces.Values)
                {
                    var b = new Bounds(positions[list[tris[0]]], Vector3.zero);
                    foreach (int k in tris) for (int j = 0; j < 3; j++) b.Encapsulate(positions[list[k + j]]);
                    if (tris.Count <= 12 || b.size.magnitude > limit)
                    {
                        foreach (int k in tris) kept.AddRange(new[] { list[k], list[k + 1], list[k + 2] });
                        continue;
                    }
                    // Longest direction: farthest pair of vertices from the centre.
                    Vector3 c = b.center, axis = Vector3.forward;
                    float far = 0f;
                    foreach (int k in tris)
                        for (int j = 0; j < 3; j++)
                        {
                            var d = positions[list[k + j]] - c;
                            if (d.sqrMagnitude > far) { far = d.sqrMagnitude; axis = d; }
                        }
                    axis.Normalize();
                    float length = 0f, radius = 0f;
                    foreach (int k in tris)
                        for (int j = 0; j < 3; j++)
                        {
                            var d = positions[list[k + j]] - c;
                            float along = Vector3.Dot(d, axis);
                            length = Mathf.Max(length, Mathf.Abs(along));
                            radius = Mathf.Max(radius, (d - axis * along).magnitude);
                        }
                    var rot = Quaternion.LookRotation(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right);
                    int start = positions.Count;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var local = new Vector3((corner & 1) == 0 ? -radius : radius, (corner & 2) == 0 ? -radius : radius, (corner & 4) == 0 ? -length : length);
                        positions.Add(c + rot * local);
                        uvs.Add(Vector2.zero);
                    }
                    int[] box = { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5 };
                    foreach (int idx in box) kept.Add(start + idx);
                }
                triangles[s] = kept;
            }

            // Drop the vertices nothing uses any more.
            var used = new bool[positions.Count];
            foreach (var list in triangles) foreach (int t in list) used[t] = true;
            Compact(positions, uvs, triangles, Remap(used));
        }

        /// <summary>Shrinks a texture and lifts it towards white (pastel), saved as PNG.</summary>
        static void SavePastelTexture(Texture source, string path, int size)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = Pastel(px[i]);
            tex.SetPixels(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.maxTextureSize = size;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        /// <summary>Without UVs, vertices at the same spot can merge (cleaner simplification).</summary>
        static void WeldPositions(List<Vector3> positions, List<Vector2> uvs, List<List<int>> triangles)
        {
            var lookup = new Dictionary<Vector3Int, int>();
            var map = new int[positions.Count];
            int next = 0;
            var first = new List<int>();
            for (int i = 0; i < positions.Count; i++)
            {
                var key = Vector3Int.RoundToInt(positions[i] * 10000f);
                if (!lookup.TryGetValue(key, out int idx)) { idx = next++; lookup[key] = idx; first.Add(i); }
                map[i] = idx;
            }
            var newPos = new List<Vector3>(next);
            var newUv = new List<Vector2>(next);
            foreach (int i in first) { newPos.Add(positions[i]); newUv.Add(uvs[i]); }
            foreach (var list in triangles)
                for (int k = 0; k < list.Count; k++) list[k] = map[list[k]];
            // Drop triangles that collapsed into lines.
            foreach (var list in triangles)
                for (int k = list.Count - 3; k >= 0; k -= 3)
                    if (list[k] == list[k + 1] || list[k + 1] == list[k + 2] || list[k] == list[k + 2]) list.RemoveRange(k, 3);
            positions.Clear(); positions.AddRange(newPos);
            uvs.Clear(); uvs.AddRange(newUv);
        }

        static int[] Remap(bool[] keep)
        {
            var map = new int[keep.Length];
            int n = 0;
            for (int i = 0; i < keep.Length; i++) map[i] = keep[i] ? n++ : -1;
            return map;
        }

        /// <summary>Applies an old-to-new vertex map (-1 = dropped) to the vertices and, if given, the triangles.</summary>
        static void Compact(List<Vector3> positions, List<Vector2> uvs, List<List<int>> triangles, int[] map)
        {
            int count = 0;
            foreach (int m in map) count = Mathf.Max(count, m + 1);
            var newPos = new Vector3[count];
            var newUv = new Vector2[count];
            for (int i = 0; i < map.Length; i++)
                if (map[i] >= 0) { newPos[map[i]] = positions[i]; newUv[map[i]] = uvs[i]; }
            if (triangles != null)
                foreach (var list in triangles)
                    for (int k = 0; k < list.Count; k++) list[k] = map[list[k]];
            positions.Clear(); positions.AddRange(newPos);
            uvs.Clear(); uvs.AddRange(newUv);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
