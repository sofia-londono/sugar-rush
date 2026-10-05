using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SugarRush.EditorTools
{
    /// <summary>
    /// Minimal binary glTF (.glb) importer for the character models downloaded from Sketchfab:
    /// node hierarchy, meshes (static or skinned), the base colour texture of each material
    /// (metallic-roughness or the old spec-gloss extension), all saved as plain Unity assets
    /// (meshes, PNG textures, URP materials, prefab) so the game needs no glTF package.
    /// glTF is right-handed: X is mirrored and triangle winding flipped on the way in.
    /// </summary>
    public static class GlbImport
    {
        // ---------------------------------------------------------------- JSON model (JsonUtility)

        [Serializable] class Gltf
        {
            public int scene;
            public Scene[] scenes;
            public Node[] nodes;
            public MeshDef[] meshes;
            public Accessor[] accessors;
            public BufferView[] bufferViews;
            public MaterialDef[] materials;
            public TextureDef[] textures;
            public ImageDef[] images;
            public Skin[] skins;
        }
        [Serializable] class Scene { public int[] nodes; }
        [Serializable] class Node
        {
            public string name;
            public int[] children;
            public int mesh = -1;
            public int skin = -1;
            public float[] matrix, translation, rotation, scale;
        }
        [Serializable] class MeshDef { public string name; public Primitive[] primitives; }
        [Serializable] class Primitive { public Attributes attributes; public int indices = -1; public int material = -1; public int mode = 4; }
        [Serializable] class Attributes { public int POSITION = -1, NORMAL = -1, TEXCOORD_0 = -1, JOINTS_0 = -1, WEIGHTS_0 = -1, COLOR_0 = -1; }
        [Serializable] class Accessor { public int bufferView = -1; public int byteOffset; public int componentType; public bool normalized; public int count; public string type; }
        [Serializable] class BufferView { public int buffer; public int byteOffset; public int byteLength; public int byteStride; }
        [Serializable] class MaterialDef
        {
            public string name;
            public bool doubleSided;
            public string alphaMode;
            public Pbr pbrMetallicRoughness;
            public MaterialExtensions extensions;
        }
        [Serializable] class Pbr { public float[] baseColorFactor; public TexRef baseColorTexture; }
        [Serializable] class MaterialExtensions { public SpecGloss KHR_materials_pbrSpecularGlossiness; }
        [Serializable] class SpecGloss { public float[] diffuseFactor; public TexRef diffuseTexture; }
        [Serializable] class TexRef { public int index = -1; }
        [Serializable] class TextureDef { public int source = -1; }
        [Serializable] class ImageDef { public string name; public int bufferView = -1; public string mimeType; }
        [Serializable] class Skin { public int[] joints; public int inverseBindMatrices = -1; public int skeleton = -1; }

        // ---------------------------------------------------------------- Import

        public class Result
        {
            public GameObject Prefab;
            public int Triangles;
            public int Bones;
            public Vector3 Size;
        }

        /// <summary>
        /// Imports a .glb into <paramref name="outDir"/> and saves a prefab named <paramref name="name"/>,
        /// scaled so the model is <paramref name="height"/> metres tall and standing on its origin.
        /// </summary>
        public static Result Import(string glbPath, string outDir, string name, float height, int maxTextureSize = 1024, int maxTriangles = 0)
        {
            byte[] file = File.ReadAllBytes(glbPath);
            if (BitConverter.ToUInt32(file, 0) != 0x46546C67) throw new Exception("Not a .glb file: " + glbPath);
            int jsonLength = BitConverter.ToInt32(file, 12);
            var gltf = JsonUtility.FromJson<Gltf>(Encoding.UTF8.GetString(file, 20, jsonLength));
            int binStart = 20 + jsonLength + 8;
            var ctx = new Context { Gltf = gltf, File = file, BinStart = binStart };
            if (maxTriangles > 0)
            {
                int total = 0;
                foreach (var m in gltf.meshes)
                    foreach (var p in m.primitives)
                        total += p.indices >= 0 ? gltf.accessors[p.indices].count / 3 : gltf.accessors[p.attributes.POSITION].count / 3;
                ctx.Keep = Mathf.Min(1f, maxTriangles / (float)Mathf.Max(total, 1));
            }

            EnsureFolder(outDir);
            EnsureFolder(outDir + "/Meshes");
            EnsureFolder(outDir + "/Textures");
            EnsureFolder(outDir + "/Materials");

            var materials = BuildMaterials(ctx, outDir, name, maxTextureSize);

            // Node hierarchy (glTF -> Unity handedness).
            var root = new GameObject(name);
            var nodeObjects = new GameObject[gltf.nodes.Length];
            for (int i = 0; i < gltf.nodes.Length; i++)
                nodeObjects[i] = new GameObject(string.IsNullOrEmpty(gltf.nodes[i].name) ? "Node" + i : gltf.nodes[i].name);
            var hasParent = new bool[gltf.nodes.Length];
            for (int i = 0; i < gltf.nodes.Length; i++)
                if (gltf.nodes[i].children != null)
                    foreach (int c in gltf.nodes[i].children) { nodeObjects[c].transform.SetParent(nodeObjects[i].transform, false); hasParent[c] = true; }
            var sceneNodes = gltf.scenes != null && gltf.scenes.Length > 0 ? gltf.scenes[Mathf.Clamp(gltf.scene, 0, gltf.scenes.Length - 1)].nodes : null;
            for (int i = 0; i < gltf.nodes.Length; i++)
            {
                if (!hasParent[i]) nodeObjects[i].transform.SetParent(root.transform, false);
                ApplyTransform(gltf.nodes[i], nodeObjects[i].transform);
            }
            if (sceneNodes != null)
                for (int i = 0; i < gltf.nodes.Length; i++)
                    if (!hasParent[i] && Array.IndexOf(sceneNodes, i) < 0) UnityEngine.Object.DestroyImmediate(nodeObjects[i]);

            // Meshes.
            var result = new Result();
            for (int i = 0; i < gltf.nodes.Length; i++)
            {
                var node = gltf.nodes[i];
                if (node.mesh < 0 || !nodeObjects[i]) continue;
                var skin = node.skin >= 0 ? gltf.skins[node.skin] : null;
                var mesh = BuildMesh(ctx, gltf.meshes[node.mesh], skin, out int[] matIndices);
                mesh.name = $"{name}_{gltf.meshes[node.mesh].name ?? "mesh"}_{i}";
                AssetDatabase.CreateAsset(mesh, $"{outDir}/Meshes/{mesh.name}.asset");
                result.Triangles += mesh.triangles.Length / 3;

                var mats = new Material[matIndices.Length];
                for (int m = 0; m < mats.Length; m++) mats[m] = matIndices[m] >= 0 ? materials[matIndices[m]] : materials[^1];

                var go = nodeObjects[i];
                if (skin != null)
                {
                    var smr = go.AddComponent<SkinnedMeshRenderer>();
                    var bones = new Transform[skin.joints.Length];
                    for (int b = 0; b < bones.Length; b++) bones[b] = nodeObjects[skin.joints[b]].transform;
                    smr.bones = bones;
                    smr.rootBone = skin.skeleton >= 0 ? nodeObjects[skin.skeleton].transform : bones[0];
                    smr.sharedMesh = mesh;
                    smr.sharedMaterials = mats;
                    smr.updateWhenOffscreen = true; // only shown for a few seconds; avoids bounds guesswork
                    result.Bones = Mathf.Max(result.Bones, bones.Length);
                }
                else
                {
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterials = mats;
                }
            }

            // Size: bake the current pose into world-space bounds, then scale and ground the model.
            var bounds = WorldBounds(root);
            float scale = bounds.size.y > 1e-4f ? height / bounds.size.y : 1f;
            var holder = new GameObject(name + "_Model").transform;
            holder.SetParent(root.transform, false);
            foreach (Transform child in new List<Transform>(Children(root.transform)))
                if (child != holder) child.SetParent(holder, true);
            holder.localScale = Vector3.one * scale;
            holder.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            result.Size = bounds.size * scale;

            result.Prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{outDir}/{name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return result;
        }

        class Context
        {
            public Gltf Gltf;
            public byte[] File;
            public int BinStart;
            /// <summary>Fraction of triangles to keep (1 = no simplification).</summary>
            public float Keep = 1f;
        }

        static IEnumerable<Transform> Children(Transform t) { foreach (Transform c in t) yield return c; }

        static Bounds WorldBounds(GameObject root)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var m = smr.transform.localToWorldMatrix;
                foreach (var v in baked.vertices) { var w = m.MultiplyPoint3x4(v); if (!any) { b = new Bounds(w, Vector3.zero); any = true; } else b.Encapsulate(w); }
                UnityEngine.Object.DestroyImmediate(baked);
            }
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) { var w = m.MultiplyPoint3x4(v); if (!any) { b = new Bounds(w, Vector3.zero); any = true; } else b.Encapsulate(w); }
            }
            return b;
        }

        // ---------------------------------------------------------------- Transforms

        /// <summary>Mirror matrix S = diag(-1, 1, 1, 1): converts between glTF and Unity spaces as S·M·S.</summary>
        static Matrix4x4 Mirror(Matrix4x4 m)
        {
            var s = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));
            return s * m * s;
        }

        static void ApplyTransform(Node n, Transform t)
        {
            if (n.matrix != null && n.matrix.Length == 16)
            {
                var m = new Matrix4x4();
                for (int i = 0; i < 16; i++) m[i] = n.matrix[i]; // glTF is column-major like Matrix4x4's indexer
                m = Mirror(m);
                t.localPosition = m.GetColumn(3);
                t.localRotation = m.rotation;
                t.localScale = m.lossyScale;
                return;
            }
            if (n.translation != null && n.translation.Length == 3)
                t.localPosition = new Vector3(-n.translation[0], n.translation[1], n.translation[2]);
            if (n.rotation != null && n.rotation.Length == 4)
                t.localRotation = new Quaternion(n.rotation[0], -n.rotation[1], -n.rotation[2], n.rotation[3]);
            if (n.scale != null && n.scale.Length == 3)
                t.localScale = new Vector3(n.scale[0], n.scale[1], n.scale[2]);
        }

        // ---------------------------------------------------------------- Meshes

        static Mesh BuildMesh(Context ctx, MeshDef def, Skin skin, out int[] materialIndices)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var submeshes = new List<List<int>>();
            var mats = new List<int>();

            foreach (var p in def.primitives)
            {
                if (p.mode != 4) continue; // triangles only
                int baseVertex = positions.Count;
                var pos = ReadFloats(ctx, p.attributes.POSITION, 3);
                int count = pos.Length / 3;
                for (int i = 0; i < count; i++) positions.Add(new Vector3(-pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2]));

                if (p.attributes.NORMAL >= 0)
                {
                    var nrm = ReadFloats(ctx, p.attributes.NORMAL, 3);
                    for (int i = 0; i < count; i++) normals.Add(new Vector3(-nrm[i * 3], nrm[i * 3 + 1], nrm[i * 3 + 2]));
                }
                else for (int i = 0; i < count; i++) normals.Add(Vector3.up);

                if (p.attributes.TEXCOORD_0 >= 0)
                {
                    var uv = ReadFloats(ctx, p.attributes.TEXCOORD_0, 2);
                    for (int i = 0; i < count; i++) uvs.Add(new Vector2(uv[i * 2], 1f - uv[i * 2 + 1]));
                }
                else for (int i = 0; i < count; i++) uvs.Add(Vector2.zero);

                if (skin != null)
                {
                    var j = ReadFloats(ctx, p.attributes.JOINTS_0, 4);
                    var w = ReadFloats(ctx, p.attributes.WEIGHTS_0, 4);
                    for (int i = 0; i < count; i++) weights.Add(SortedWeight(j, w, i));
                }

                int[] indices;
                if (p.indices >= 0)
                {
                    var raw = ReadFloats(ctx, p.indices, 1);
                    indices = new int[raw.Length];
                    for (int i = 0; i < raw.Length; i++) indices[i] = (int)raw[i];
                }
                else
                {
                    indices = new int[count];
                    for (int i = 0; i < count; i++) indices[i] = i;
                }
                var tris = new List<int>(indices.Length);
                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    // Mirroring X flips handedness: swap two corners to keep faces pointing out.
                    tris.Add(baseVertex + indices[i]);
                    tris.Add(baseVertex + indices[i + 2]);
                    tris.Add(baseVertex + indices[i + 1]);
                }
                submeshes.Add(tris);
                mats.Add(p.material);
            }

            int triCount = 0;
            foreach (var l in submeshes) triCount += l.Count / 3;
            if (ctx.Keep < 0.999f)
            {
                var map = MeshDecimator.Simplify(positions, submeshes, Mathf.RoundToInt(triCount * ctx.Keep));
                positions = Compact(positions, map);
                normals = Compact(normals, map);
                uvs = Compact(uvs, map);
                if (skin != null) weights = Compact(weights, map);
            }

            var mesh = new Mesh { indexFormat = positions.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Count;
            for (int s = 0; s < submeshes.Count; s++) mesh.SetTriangles(submeshes[s], s);
            if (skin != null)
            {
                mesh.boneWeights = weights.ToArray();
                var ibm = ReadFloats(ctx, skin.inverseBindMatrices, 16);
                var binds = new Matrix4x4[skin.joints.Length];
                for (int b = 0; b < binds.Length; b++)
                {
                    var m = new Matrix4x4();
                    for (int k = 0; k < 16; k++) m[k] = ibm[b * 16 + k];
                    binds[b] = Mirror(m);
                }
                mesh.bindposes = binds;
            }
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            materialIndices = mats.ToArray();
            return mesh;
        }

        static List<T> Compact<T>(List<T> list, int[] map)
        {
            var result = new List<T>();
            for (int i = 0; i < list.Count; i++) if (map[i] >= 0) result.Add(list[i]);
            return result;
        }

        static BoneWeight SortedWeight(float[] joints, float[] w, int v)
        {
            var pairs = new (int j, float w)[4];
            float sum = 0f;
            for (int k = 0; k < 4; k++) { pairs[k] = ((int)joints[v * 4 + k], w[v * 4 + k]); sum += pairs[k].w; }
            Array.Sort(pairs, (a, b) => b.w.CompareTo(a.w));
            if (sum <= 1e-6f) sum = 1f;
            return new BoneWeight
            {
                boneIndex0 = pairs[0].j, weight0 = pairs[0].w / sum,
                boneIndex1 = pairs[1].j, weight1 = pairs[1].w / sum,
                boneIndex2 = pairs[2].j, weight2 = pairs[2].w / sum,
                boneIndex3 = pairs[3].j, weight3 = pairs[3].w / sum,
            };
        }

        /// <summary>Reads any accessor as floats (integers as values, normalized types scaled to 0..1).</summary>
        static float[] ReadFloats(Context ctx, int accessorIndex, int components)
        {
            var acc = ctx.Gltf.accessors[accessorIndex];
            var view = ctx.Gltf.bufferViews[acc.bufferView];
            int compSize = acc.componentType switch { 5120 or 5121 => 1, 5122 or 5123 => 2, _ => 4 };
            int stride = view.byteStride > 0 ? view.byteStride : compSize * components;
            int start = ctx.BinStart + view.byteOffset + acc.byteOffset;
            var result = new float[acc.count * components];
            for (int i = 0; i < acc.count; i++)
                for (int c = 0; c < components; c++)
                {
                    int o = start + i * stride + c * compSize;
                    float v = acc.componentType switch
                    {
                        5126 => BitConverter.ToSingle(ctx.File, o),
                        5125 => BitConverter.ToUInt32(ctx.File, o),
                        5123 => acc.normalized ? BitConverter.ToUInt16(ctx.File, o) / 65535f : BitConverter.ToUInt16(ctx.File, o),
                        5122 => acc.normalized ? Mathf.Max(BitConverter.ToInt16(ctx.File, o) / 32767f, -1f) : BitConverter.ToInt16(ctx.File, o),
                        5121 => acc.normalized ? ctx.File[o] / 255f : ctx.File[o],
                        5120 => acc.normalized ? Mathf.Max((sbyte)ctx.File[o] / 127f, -1f) : (sbyte)ctx.File[o],
                        _ => 0f,
                    };
                    result[i * components + c] = v;
                }
            return result;
        }

        // ---------------------------------------------------------------- Materials

        static List<Material> BuildMaterials(Context ctx, string outDir, string name, int maxTextureSize)
        {
            var gltf = ctx.Gltf;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var list = new List<Material>();
            var textureCache = new Dictionary<int, Texture2D>();
            var defs = gltf.materials ?? new MaterialDef[0];
            for (int i = 0; i <= defs.Length; i++)
            {
                var def = i < defs.Length ? defs[i] : null; // the extra one is for primitives without a material
                string path = $"{outDir}/Materials/{name}_{i}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;

                Color color = Color.white;
                int texIndex = -1;
                var sg = def?.extensions?.KHR_materials_pbrSpecularGlossiness;
                if (sg != null && (sg.diffuseTexture?.index ?? -1) >= 0) texIndex = sg.diffuseTexture.index;
                else if (def?.pbrMetallicRoughness?.baseColorTexture != null) texIndex = def.pbrMetallicRoughness.baseColorTexture.index;
                float[] factor = sg?.diffuseFactor is { Length: 4 } ? sg.diffuseFactor : def?.pbrMetallicRoughness?.baseColorFactor;
                if (factor is { Length: 4 }) color = new Color(factor[0], factor[1], factor[2], factor[3]);

                Texture2D tex = null;
                if (texIndex >= 0 && gltf.textures != null && texIndex < gltf.textures.Length)
                {
                    int image = gltf.textures[texIndex].source;
                    if (!textureCache.TryGetValue(image, out tex))
                    {
                        tex = ExtractImage(ctx, image, $"{outDir}/Textures/{name}_{image}", maxTextureSize);
                        textureCache[image] = tex;
                    }
                }
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", 0.15f);
                mat.SetFloat("_Metallic", 0f);
                bool doubleSided = def != null && def.doubleSided;
                mat.SetFloat("_Cull", doubleSided ? 0f : 2f);
                mat.doubleSidedGI = doubleSided;
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                list.Add(mat);
            }
            return list;
        }

        static Texture2D ExtractImage(Context ctx, int imageIndex, string pathNoExt, int maxSize)
        {
            var img = ctx.Gltf.images[imageIndex];
            if (img.bufferView < 0) return null;
            var view = ctx.Gltf.bufferViews[img.bufferView];
            string ext = img.mimeType == "image/jpeg" ? ".jpg" : ".png";
            string path = pathNoExt + ext;
            var bytes = new byte[view.byteLength];
            Buffer.BlockCopy(ctx.File, ctx.BinStart + view.byteOffset, bytes, 0, view.byteLength);
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.maxTextureSize = maxSize;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Compressed;
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
    }
}
