using System.Collections.Generic;
using UnityEngine;

namespace VortexKarts.Utils
{
    /// <summary>
    /// Accumulates quads/triangles and produces a Mesh. Used for the procedural road, walls, tunnels and props.
    /// </summary>
    public class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private bool useColors;

        public int VertexCount => vertices.Count;

        public void Clear()
        {
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
        }

        public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            vertices.Add(position);
            normals.Add(normal);
            uvs.Add(uv);
            if (useColors) colors.Add(Color.white);
            return vertices.Count - 1;
        }

        public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv, Color color)
        {
            useColors = true;
            while (colors.Count < vertices.Count) colors.Add(Color.white);
            vertices.Add(position);
            normals.Add(normal);
            uvs.Add(uv);
            colors.Add(color);
            return vertices.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        /// <summary>
        /// Adds a quad with corners given in clockwise order when viewed from the side the normal points to.
        /// a = bottom-left, b = bottom-right, c = top-right, d = top-left.
        /// </summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
        {
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            if (n.sqrMagnitude < 0.0001f) n = Vector3.up;
            int ia = AddVertex(a, n, uvA);
            int ib = AddVertex(b, n, uvB);
            int ic = AddVertex(c, n, uvC);
            int id = AddVertex(d, n, uvD);
            AddTriangle(ia, ib, id);
            AddTriangle(ib, ic, id);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddQuad(a, b, c, d, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        }

        /// <summary>Adds a strip segment between two cross-sections (left/right of previous and current).</summary>
        public void AddStripSegment(Vector3 prevLeft, Vector3 prevRight, Vector3 curLeft, Vector3 curRight,
            float vPrev, float vCur, Vector3 normalHint)
        {
            Vector3 n = Vector3.Cross(prevRight - prevLeft, curLeft - prevLeft).normalized;
            if (Vector3.Dot(n, normalHint) < 0f) n = -n;
            if (n.sqrMagnitude < 0.0001f) n = normalHint;
            int a = AddVertex(prevLeft, n, new Vector2(0f, vPrev));
            int b = AddVertex(prevRight, n, new Vector2(1f, vPrev));
            int c = AddVertex(curRight, n, new Vector2(1f, vCur));
            int d = AddVertex(curLeft, n, new Vector2(0f, vCur));
            // Unity's front face normal for (v0, v1, v2) is Cross(v1 - v0, v2 - v0); pick the winding that matches n.
            if (Vector3.Dot(Vector3.Cross(prevRight - prevLeft, curLeft - prevLeft), n) >= 0f)
            {
                AddTriangle(a, b, d);
                AddTriangle(b, c, d);
            }
            else
            {
                AddTriangle(a, d, b);
                AddTriangle(b, d, c);
            }
        }

        /// <summary>Adds an axis-aligned box given centre and size (6 faces, outward normals).</summary>
        public void AddBox(Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
            Vector3 p100 = center + new Vector3(h.x, -h.y, -h.z);
            Vector3 p110 = center + new Vector3(h.x, h.y, -h.z);
            Vector3 p010 = center + new Vector3(-h.x, h.y, -h.z);
            Vector3 p001 = center + new Vector3(-h.x, -h.y, h.z);
            Vector3 p101 = center + new Vector3(h.x, -h.y, h.z);
            Vector3 p111 = center + new Vector3(h.x, h.y, h.z);
            Vector3 p011 = center + new Vector3(-h.x, h.y, h.z);

            AddFace(p100, p000, p010, p110, Vector3.back);   // -Z
            AddFace(p001, p101, p111, p011, Vector3.forward); // +Z
            AddFace(p000, p001, p011, p010, Vector3.left);    // -X
            AddFace(p101, p100, p110, p111, Vector3.right);   // +X
            AddFace(p010, p011, p111, p110, Vector3.up);      // +Y
            AddFace(p001, p000, p100, p101, Vector3.down);    // -Y
        }

        /// <summary>Adds a face with an explicit normal; corners in counter-clockwise order seen from the normal side.</summary>
        public void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int ia = AddVertex(a, normal, new Vector2(0f, 0f));
            int ib = AddVertex(b, normal, new Vector2(1f, 0f));
            int ic = AddVertex(c, normal, new Vector2(1f, 1f));
            int id = AddVertex(d, normal, new Vector2(0f, 1f));
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f)
            {
                AddTriangle(ia, ib, ic);
                AddTriangle(ia, ic, id);
            }
            else
            {
                AddTriangle(ia, ic, ib);
                AddTriangle(ia, id, ic);
            }
        }

        public Mesh Build(string name, bool recalculateNormals = false)
        {
            var mesh = new Mesh();
            mesh.name = name;
            if (vertices.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            if (useColors && colors.Count == vertices.Count) mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            if (recalculateNormals) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
