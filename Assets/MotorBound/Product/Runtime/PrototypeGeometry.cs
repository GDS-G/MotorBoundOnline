using UnityEngine;

namespace MotorBound.Product
{
    /// <summary>Primitive inspection geometry with dimensions baked into vertices; transforms stay at unit scale.</summary>
    public static class PrototypeGeometry
    {
        public static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 dimensions, Material material, bool collides)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            var filter = box.GetComponent<MeshFilter>();
            var mesh = Object.Instantiate(filter.sharedMesh);
            mesh.name = name + " measured mesh";
            var vertices = mesh.vertices;
            for (var i = 0; i < vertices.Length; i++) vertices[i] = Vector3.Scale(vertices[i], dimensions);
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            box.GetComponent<Renderer>().sharedMaterial = material;
            var collider = box.GetComponent<BoxCollider>();
            collider.size = dimensions;
            collider.enabled = collides;
            return box;
        }
    }
}
