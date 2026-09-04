using UnityEngine;

namespace VortexKarts.Utils
{
    /// <summary>
    /// Builds simple primitive-based geometry for karts, pilots, decorations and greybox props.
    /// Colliders are stripped by default; callers that need collision add their own.
    /// </summary>
    public static class PrimitiveFactory
    {
        private static Mesh cubeMesh;
        private static Mesh sphereMesh;
        private static Mesh cylinderMesh;
        private static Mesh capsuleMesh;
        private static Mesh quadMesh;

        private static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            switch (type)
            {
                case PrimitiveType.Cube: return cubeMesh != null ? cubeMesh : (cubeMesh = ExtractMesh(type));
                case PrimitiveType.Sphere: return sphereMesh != null ? sphereMesh : (sphereMesh = ExtractMesh(type));
                case PrimitiveType.Cylinder: return cylinderMesh != null ? cylinderMesh : (cylinderMesh = ExtractMesh(type));
                case PrimitiveType.Capsule: return capsuleMesh != null ? capsuleMesh : (capsuleMesh = ExtractMesh(type));
                case PrimitiveType.Quad: return quadMesh != null ? quadMesh : (quadMesh = ExtractMesh(type));
                default: return ExtractMesh(type);
            }
        }

        private static Mesh ExtractMesh(PrimitiveType type)
        {
            var temp = GameObject.CreatePrimitive(type);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(temp);
            return mesh;
        }

        public static GameObject Create(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Vector3 localScale, Material material, bool withCollider = false, Quaternion? localRotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = GetPrimitiveMesh(type);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            if (withCollider)
            {
                switch (type)
                {
                    case PrimitiveType.Cube: go.AddComponent<BoxCollider>(); break;
                    case PrimitiveType.Sphere: go.AddComponent<SphereCollider>(); break;
                    case PrimitiveType.Capsule: go.AddComponent<CapsuleCollider>(); break;
                    case PrimitiveType.Cylinder:
                        var cap = go.AddComponent<CapsuleCollider>();
                        cap.height = 2f;
                        cap.radius = 0.5f;
                        break;
                    case PrimitiveType.Quad:
                        var bc = go.AddComponent<BoxCollider>();
                        bc.size = new Vector3(1f, 1f, 0.01f);
                        break;
                }
            }
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material,
            bool withCollider = false, Quaternion? localRotation = null)
        {
            return Create(PrimitiveType.Cube, name, parent, localPosition, size, material, withCollider, localRotation);
        }

        public static GameObject Sphere(string name, Transform parent, Vector3 localPosition, float diameter, Material material,
            bool withCollider = false)
        {
            return Create(PrimitiveType.Sphere, name, parent, localPosition, Vector3.one * diameter, material, withCollider);
        }

        public static GameObject Cylinder(string name, Transform parent, Vector3 localPosition, float diameter, float height,
            Material material, bool withCollider = false, Quaternion? localRotation = null)
        {
            // Unity's cylinder primitive is 2 units tall.
            return Create(PrimitiveType.Cylinder, name, parent, localPosition, new Vector3(diameter, height * 0.5f, diameter),
                material, withCollider, localRotation);
        }

        public static GameObject Capsule(string name, Transform parent, Vector3 localPosition, float diameter, float height,
            Material material, Quaternion? localRotation = null)
        {
            // Unity's capsule primitive is 2 units tall at scale 1.
            return Create(PrimitiveType.Capsule, name, parent, localPosition, new Vector3(diameter, height * 0.5f, diameter),
                material, false, localRotation);
        }

        public static GameObject Quad(string name, Transform parent, Vector3 localPosition, Vector2 size, Material material,
            Quaternion? localRotation = null)
        {
            return Create(PrimitiveType.Quad, name, parent, localPosition, new Vector3(size.x, size.y, 1f), material, false,
                localRotation);
        }

        public static void SetShadowCasting(GameObject go, bool cast, bool receive)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = cast
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i].receiveShadows = receive;
            }
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
            }
        }

        public static GameObject Empty(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }
    }
}
