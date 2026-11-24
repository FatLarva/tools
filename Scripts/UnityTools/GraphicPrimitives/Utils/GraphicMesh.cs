using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    internal static class GraphicMesh
    {
        internal const int MaximumVertices = 64_000;

        internal static void AddStrip(VertexHelper mesh, int previousInner, int previousOuter, int inner, int outer)
        {
            if (previousInner != previousOuter)
            {
                mesh.AddTriangle(previousInner, inner, previousOuter);
            }

            if (inner != outer)
            {
                mesh.AddTriangle(inner, outer, previousOuter);
            }
        }

        internal static void AddFanTriangle(VertexHelper mesh, int center, int previous, int current)
        {
            mesh.AddTriangle(center, current, previous);
        }

        internal static Vector4 GetSpriteUv(Sprite sprite)
        {
            return sprite != null ? DataUtility.GetOuterUV(sprite) : new Vector4(0f, 0f, 1f, 1f);
        }

        internal static Vector2 MapUv(Vector2 position, Rect rectangle, Vector4 uv)
        {
            float x = rectangle.width > 0f ? (position.x - rectangle.xMin) / rectangle.width : 0f;
            float y = rectangle.height > 0f ? (position.y - rectangle.yMin) / rectangle.height : 0f;
            if (uv.x != 0f || uv.y != 0f || uv.z != 1f || uv.w != 1f)
            {
                x = Mathf.Clamp01(x);
                y = Mathf.Clamp01(y);
            }

            return new Vector2(
                Mathf.LerpUnclamped(uv.x, uv.z, x),
                Mathf.LerpUnclamped(uv.y, uv.w, y));
        }
    }
}
