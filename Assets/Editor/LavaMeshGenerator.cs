using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the meshes used by the lava shaders.
// Tools > Lava > Generate Lava Meshes regenerates both assets in place (references stay intact).
public static class LavaMeshGenerator
{
    public const string FloorMeshPath = "Assets/Meshes/LavaFloorChunk.asset";
    public const string WaveMeshPath = "Assets/Meshes/LavaWave.asset";

    // Wave size in world units (the Lava object is used at scale 1)
    public const float WaveWidth = 61.3f;
    public const float WaveLength = 39.2f;
    public const float WaveTop = 3.53f;          // local Y of the crest
    public const float WaveFloor = -1.6f;        // local Y where the lava floor is (object sits at y = 1.6)

    [MenuItem("Tools/Lava/Generate Lava Meshes")]
    public static void GenerateAll()
    {
        GenerateFloorMesh();
        GenerateWaveMesh(WaveWidth, WaveLength, WaveTop, WaveFloor);
        AssetDatabase.SaveAssets();
    }

    // Same footprint as Unity's built-in Plane (10 x 10, facing up) so the existing scale and
    // the WordGenerator chunk spacing keep working, but subdivided so the surface can swell.
    public static Mesh GenerateFloorMesh(int segments = 120)
    {
        int row = segments + 1;
        var vertices = new Vector3[row * row];
        var uvs = new Vector2[row * row];
        for (int z = 0; z < row; z++)
        {
            for (int x = 0; x < row; x++)
            {
                float u = (float)x / segments;
                float v = (float)z / segments;
                vertices[z * row + x] = new Vector3(5f - u * 10f, 0f, 5f - v * 10f);
                uvs[z * row + x] = new Vector2(u, v);
            }
        }

        var triangles = BuildGrid(segments, segments);
        Mesh mesh = CreateMesh("LavaFloorChunk", vertices, uvs, null, triangles, Vector3.up, 0);
        // Keep the X/Z size exactly 10 (chunk spacing uses bounds.size.z), pad Y for the swell
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(10f, 0.2f, 10f));
        return SaveMesh(mesh, FloorMeshPath);
    }

    // A rolling lava front: a gentle slope rising from the floor at the back, a rounded crest,
    // and a bulging front face that tucks under the floor. Extruded across X with tapered ends.
    public static Mesh GenerateWaveMesh(float width, float length, float top, float floor, int columns = 200, int profileSamples = 160)
    {
        float halfW = width * 0.5f;
        float zBack = -length * 0.5f;
        float zFront = length * 0.5f;
        float sunk = floor - 1.2f;
        float taper = 7f;

        int row = columns + 1;
        var vertices = new Vector3[row * profileSamples];
        var uvs = new Vector2[vertices.Length];
        var colors = new Color[vertices.Length];

        var dense = new List<ProfilePoint>(800);
        for (int c = 0; c < row; c++)
        {
            float x = Mathf.Lerp(-halfW, halfW, (float)c / columns);

            // Variation across the width so the front is not a straight line
            float crestOffset = 0.35f * Mathf.Sin(x * 0.21f + 1.3f) + 0.2f * Mathf.Sin(x * 0.53f + 0.4f);
            float frontOffset = 0.9f * Mathf.Sin(x * 0.16f + 2.1f) + 0.45f * Mathf.Sin(x * 0.41f);
            float edge = Mathf.Sin(Mathf.PI * 0.5f * Mathf.Clamp01((halfW - Mathf.Abs(x)) / taper));

            BuildProfile(dense, zBack, zFront + frontOffset, top + crestOffset, floor, sunk);

            // Resample: denser on the front face, coarser on the long back slope
            float totalWeighted = dense[dense.Count - 1].weighted;
            int k = 0;
            for (int s = 0; s < profileSamples; s++)
            {
                float target = totalWeighted * s / (profileSamples - 1);
                while (k < dense.Count - 2 && dense[k + 1].weighted < target) k++;
                ProfilePoint a = dense[k];
                ProfilePoint b = dense[k + 1];
                float t = Mathf.InverseLerp(a.weighted, b.weighted, target);

                float y = Mathf.Lerp(a.y, b.y, t);
                float z = Mathf.Lerp(a.z, b.z, t);
                float arc = Mathf.Lerp(a.arc, b.arc, t);
                float front = Mathf.Lerp(a.front, b.front, t);
                float lip = Mathf.Lerp(a.lip, b.lip, t);

                // Sink the shape into the floor towards the side ends
                y = Mathf.Lerp(sunk, y, edge);

                float aboveFloor = Mathf.Clamp01((y - sunk) / (floor + 0.4f - sunk));
                int index = c + s * row;
                vertices[index] = new Vector3(x, y, z);
                uvs[index] = new Vector2(x, arc);
                colors[index] = new Color(front, aboveFloor * edge, lip, 1f);
            }
        }

        var triangles = BuildGrid(columns, profileSamples - 1);
        Mesh mesh = CreateMesh("LavaWave", vertices, uvs, colors, triangles, Vector3.up, row * (profileSamples / 3) + row / 2);
        mesh.RecalculateTangents();
        Bounds bounds = mesh.bounds;
        bounds.Expand(4f);
        mesh.bounds = bounds;
        return SaveMesh(mesh, WaveMeshPath);
    }

    private struct ProfilePoint
    {
        public float z, y, arc, weighted, front, lip;
    }

    private static void BuildProfile(List<ProfilePoint> points, float zBack, float zFront, float top, float floor, float sunk)
    {
        points.Clear();
        float radiusZ = 4.8f;
        float radiusY = top - floor;
        float zCrest = zFront - radiusZ;

        // Back slope: rises out of the floor and flattens into the crest
        const int slopeSamples = 300;
        for (int i = 0; i <= slopeSamples; i++)
        {
            float t = (float)i / slopeSamples;
            float z = Mathf.Lerp(zBack, zCrest, t);
            float y = Mathf.Lerp(sunk, top, Mathf.SmoothStep(0f, 1f, Mathf.Pow(t, 0.6f)));
            Add(points, z, y, 0f, 0f, 0.35f);
        }

        // Rounded front (superellipse) leaning forward at the top, tucked back at the floor
        const int frontSamples = 400;
        const float exponent = 2.0f / 2.6f;
        for (int i = 1; i <= frontSamples; i++)
        {
            float a = (float)i / frontSamples * Mathf.PI * 0.5f;
            float sz = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(a)), exponent);
            float sy = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(a)), exponent);
            float h = sy;
            float z = zCrest + radiusZ * sz - 1.1f * (1f - h) * (1f - h);
            float y = floor + radiusY * sy;
            float progress = a / (Mathf.PI * 0.5f);
            float front = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.55f, progress));
            float lip = Mathf.Exp(-Mathf.Pow((progress - 0.3f) / 0.18f, 2f));
            Add(points, z, y, front, lip, 1f);
        }

        // Tuck under the floor
        ProfilePoint last = points[points.Count - 1];
        const int tuckSamples = 40;
        for (int i = 1; i <= tuckSamples; i++)
        {
            float t = (float)i / tuckSamples;
            Add(points, last.z - 0.9f * t * t, Mathf.Lerp(floor, sunk, t), 1f, 0f, 0.5f);
        }
    }

    private static void Add(List<ProfilePoint> points, float z, float y, float front, float lip, float weight)
    {
        var p = new ProfilePoint { z = z, y = y, front = front, lip = lip };
        if (points.Count > 0)
        {
            ProfilePoint prev = points[points.Count - 1];
            float d = new Vector2(z - prev.z, y - prev.y).magnitude;
            p.arc = prev.arc + d;
            p.weighted = prev.weighted + d * weight;
        }
        points.Add(p);
    }

    private static int[] BuildGrid(int columns, int rows)
    {
        int row = columns + 1;
        var triangles = new int[columns * rows * 6];
        int t = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                int i0 = c + r * row;
                int i1 = i0 + 1;
                int i2 = i0 + row;
                int i3 = i2 + 1;
                triangles[t++] = i0; triangles[t++] = i2; triangles[t++] = i1;
                triangles[t++] = i1; triangles[t++] = i2; triangles[t++] = i3;
            }
        }
        return triangles;
    }

    // Builds the mesh and flips the winding if the reference vertex does not face the expected way
    private static Mesh CreateMesh(string name, Vector3[] vertices, Vector2[] uvs, Color[] colors, int[] triangles, Vector3 expected, int referenceVertex)
    {
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        if (colors != null) mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        if (Vector3.Dot(mesh.normals[referenceVertex], expected) < 0f)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            }
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Meshes"))
        {
            AssetDatabase.CreateFolder("Assets", "Meshes");
        }

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            // Copy into the existing asset so prefab / scene references keep working
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }
}
