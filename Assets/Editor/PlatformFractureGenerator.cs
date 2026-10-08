using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Builds the breakable version of the BreakingPlatform prefab:
//  - an intact copy of the pedestal mesh and a set of pre-fractured chunks (the source FBX is not modified)
//  - the Visual hierarchy inside the prefab (Visual/Intact + Visual/Fractured/Chunk_N)
// Every generated mesh stores its original object-space position in UV2 so the crack shader
// draws the same cracks on the intact mesh and on the chunks.
public static class PlatformFractureGenerator
{
    private const string PrefabPath = "Assets/Prefabs/BreakingPlatform.prefab";
    private const string MeshPath = "Assets/Meshes/Pedestal_001_Breakable.asset";
    private const string MaterialPath = "Assets/Materials/BreakingPlatform.mat";

    // Vertical cut planes: angle around Y (degrees) and offset from the centre (object units)
    private static readonly Vector2[] Cuts =
    {
        new Vector2(12f, 0.15f),
        new Vector2(58f, -0.35f),
        new Vector2(103f, 0.3f),
        new Vector2(147f, -0.1f),
    };

    private struct Vert
    {
        public Vector3 pos;
        public Vector3 normal;
        public Vector2 uv;
        public Vector3 orig;

        public static Vert Lerp(Vert a, Vert b, float t)
        {
            return new Vert
            {
                pos = Vector3.Lerp(a.pos, b.pos, t),
                normal = Vector3.Lerp(a.normal, b.normal, t).normalized,
                uv = Vector2.Lerp(a.uv, b.uv, t),
                orig = Vector3.Lerp(a.orig, b.orig, t),
            };
        }
    }

    [MenuItem("Tools/Platforms/Build Breaking Platform")]
    public static void Build()
    {
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Mesh source = FindSourceMesh(root);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            Mesh intact = BuildIntact(source);
            List<Mesh> chunks = BuildChunks(source, out List<Vector3> centres);
            SaveMeshes(intact, chunks);

            // Root keeps the collider and logic; all rendering lives under Visual
            var rootFilter = root.GetComponent<MeshFilter>();
            var rootRenderer = root.GetComponent<MeshRenderer>();
            if (rootRenderer != null) Object.DestroyImmediate(rootRenderer);
            if (rootFilter != null) Object.DestroyImmediate(rootFilter);

            Transform visual = root.transform.Find("Visual");
            if (visual != null) Object.DestroyImmediate(visual.gameObject);
            visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            var intactGo = new GameObject("Intact");
            intactGo.transform.SetParent(visual, false);
            intactGo.AddComponent<MeshFilter>().sharedMesh = intact;
            var intactRenderer = intactGo.AddComponent<MeshRenderer>();
            intactRenderer.sharedMaterial = material;

            var fractured = new GameObject("Fractured").transform;
            fractured.SetParent(visual, false);
            var chunkTransforms = new Transform[chunks.Count];
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = new GameObject("Chunk_" + i);
                chunk.transform.SetParent(fractured, false);
                chunk.transform.localPosition = centres[i];
                chunk.AddComponent<MeshFilter>().sharedMesh = chunks[i];
                chunk.AddComponent<MeshRenderer>().sharedMaterial = material;
                chunkTransforms[i] = chunk.transform;
            }
            fractured.gameObject.SetActive(false);

            var platform = root.GetComponent<BreakingPlatform>();
            var so = new SerializedObject(platform);
            so.FindProperty("visual").objectReferenceValue = visual;
            so.FindProperty("intact").objectReferenceValue = intactGo;
            so.FindProperty("fractured").objectReferenceValue = fractured.gameObject;
            var list = so.FindProperty("chunks");
            list.arraySize = chunkTransforms.Length;
            for (int i = 0; i < chunkTransforms.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = chunkTransforms[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"Breaking platform built: {chunks.Count} chunks from {source.name}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Mesh FindSourceMesh(GameObject root)
    {
        // The original external mesh is referenced by the root collider
        var collider = root.GetComponent<MeshCollider>();
        Mesh mesh = collider.sharedMesh;
        if (mesh.name.EndsWith("_Breakable"))
        {
            throw new System.InvalidOperationException("Collider should reference the original pedestal mesh.");
        }
        return mesh;
    }

    private static Mesh BuildIntact(Mesh source)
    {
        var mesh = new Mesh { name = source.name + "_Breakable" };
        Vector3[] vertices = source.vertices;
        mesh.vertices = vertices;
        mesh.normals = source.normals;
        mesh.uv = source.uv;
        mesh.SetUVs(2, new List<Vector3>(vertices));
        mesh.triangles = source.triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static List<Mesh> BuildChunks(Mesh source, out List<Vector3> centres)
    {
        Vector3[] v = source.vertices;
        Vector3[] n = source.normals;
        Vector2[] uv = source.uv;
        int[] t = source.triangles;

        var whole = new List<Vert>(t.Length);
        foreach (int index in t)
        {
            whole.Add(new Vert { pos = v[index], normal = n[index], uv = uv[index], orig = v[index] });
        }

        Vector3 centre = source.bounds.center;
        var pieces = new List<List<Vert>> { whole };
        foreach (Vector2 cut in Cuts)
        {
            float angle = cut.x * Mathf.Deg2Rad;
            Vector3 normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var plane = new Plane(normal, centre + normal * cut.y);

            var next = new List<List<Vert>>();
            foreach (var piece in pieces)
            {
                Split(piece, plane, out var front, out var back);
                if (front.Count > 0) next.Add(front);
                if (back.Count > 0) next.Add(back);
            }
            pieces = next;
        }

        var meshes = new List<Mesh>();
        centres = new List<Vector3>();
        foreach (var piece in pieces)
        {
            if (piece.Count < 3 * 4) continue; // drop slivers

            Vector3 c = Vector3.zero;
            float area = 0f;
            for (int i = 0; i < piece.Count; i += 3)
            {
                float a = Vector3.Cross(piece[i + 1].pos - piece[i].pos, piece[i + 2].pos - piece[i].pos).magnitude;
                c += (piece[i].pos + piece[i + 1].pos + piece[i + 2].pos) / 3f * a;
                area += a;
            }
            c /= Mathf.Max(area, 1e-5f);

            var positions = new Vector3[piece.Count];
            var normals = new Vector3[piece.Count];
            var uvs = new Vector2[piece.Count];
            var origs = new List<Vector3>(piece.Count);
            var tris = new int[piece.Count];
            for (int i = 0; i < piece.Count; i++)
            {
                positions[i] = piece[i].pos - c;
                normals[i] = piece[i].normal;
                uvs[i] = piece[i].uv;
                origs.Add(piece[i].orig);
                tris[i] = i;
            }

            var mesh = new Mesh { name = source.name + "_Chunk" + meshes.Count };
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.SetUVs(2, origs);
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            centres.Add(c);
        }
        return meshes;
    }

    // Splits a triangle soup by a plane, cutting triangles that cross it (winding preserved).
    private static void Split(List<Vert> tris, Plane plane, out List<Vert> front, out List<Vert> back)
    {
        front = new List<Vert>();
        back = new List<Vert>();
        var tri = new Vert[3];
        var d = new float[3];
        var side = new bool[3];

        for (int i = 0; i < tris.Count; i += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                tri[k] = tris[i + k];
                d[k] = plane.GetDistanceToPoint(tri[k].pos);
                side[k] = d[k] >= 0f;
            }

            if (side[0] && side[1] && side[2]) { front.Add(tri[0]); front.Add(tri[1]); front.Add(tri[2]); continue; }
            if (!side[0] && !side[1] && !side[2]) { back.Add(tri[0]); back.Add(tri[1]); back.Add(tri[2]); continue; }

            // The vertex alone on its side becomes v0; keep the cyclic order to preserve winding
            int lone = side[0] == side[1] ? 2 : (side[0] == side[2] ? 1 : 0);
            Vert v0 = tri[lone], v1 = tri[(lone + 1) % 3], v2 = tri[(lone + 2) % 3];
            float d0 = d[lone], d1 = d[(lone + 1) % 3], d2 = d[(lone + 2) % 3];
            Vert i1 = Vert.Lerp(v0, v1, d0 / (d0 - d1));
            Vert i2 = Vert.Lerp(v0, v2, d0 / (d0 - d2));

            List<Vert> loneSide = side[lone] ? front : back;
            List<Vert> otherSide = side[lone] ? back : front;
            loneSide.Add(v0); loneSide.Add(i1); loneSide.Add(i2);
            otherSide.Add(i1); otherSide.Add(v1); otherSide.Add(v2);
            otherSide.Add(i1); otherSide.Add(v2); otherSide.Add(i2);
        }
    }

    private static void SaveMeshes(Mesh intact, List<Mesh> chunks)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Meshes"))
        {
            AssetDatabase.CreateFolder("Assets", "Meshes");
        }
        if (AssetDatabase.LoadMainAssetAtPath(MeshPath) != null)
        {
            AssetDatabase.DeleteAsset(MeshPath);
        }
        AssetDatabase.CreateAsset(intact, MeshPath);
        foreach (Mesh chunk in chunks)
        {
            AssetDatabase.AddObjectToAsset(chunk, MeshPath);
        }
        AssetDatabase.SaveAssets();
    }
}
