using UnityEngine;

namespace UnityTools
{
    public static class VectorExtensions
    {
        public static Vector3 WithX(this Vector3 vector, float x) => new(x, vector.y, vector.z);
        public static Vector3 WithY(this Vector3 vector, float y) => new(vector.x, y, vector.z);
        public static Vector3 WithZ(this Vector3 vector, float z) => new(vector.x, vector.y, z);

        public static Vector2 WithX(this Vector2 vector, float x) => new(x, vector.y);
        public static Vector2 WithY(this Vector2 vector, float y) => new(vector.x, y);

        public static Vector3 ToXZ(this Vector2 vector) => new(vector.x, 0f, vector.y);
        public static Vector2 ToXZ(this Vector3 vector) => new(vector.x, vector.z);
    }
}