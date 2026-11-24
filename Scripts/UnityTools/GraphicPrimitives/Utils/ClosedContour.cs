using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    public enum ClosedShapeMode
    {
        Fill = 0,
        Edge = 2,
        EdgeAndFill = 3,
    }

    internal enum ContourGradient
    {
        Horizontal,
        Vertical,
        Radial,
    }

    internal readonly struct ContourPaint
    {
        private readonly Color _color;
        private readonly Gradient _gradient;
        private readonly ContourGradient _direction;

        internal ContourPaint(Color color, Gradient gradient, ContourGradient direction)
        {
            _color = color;
            _gradient = gradient;
            _direction = direction;
        }

        internal Color Sample(Vector2 position, Rect rect, float opacity = 1f)
        {
            var result = _color;
            if (_gradient != null)
            {
                float x = (position.x - rect.xMin) / rect.width;
                float y = (position.y - rect.yMin) / rect.height;
                float t = _direction switch
                {
                    ContourGradient.Vertical => y,
                    ContourGradient.Radial => new Vector2(2f * x - 1f, 2f * y - 1f).magnitude,
                    _ => x,
                };
                result = _gradient.Evaluate(Mathf.Clamp01(t));
            }

            result.a *= opacity;
            return result;
        }
    }

    internal static class ClosedContour
    {
        internal const int MaximumSamples = 768;
        private const float Epsilon = 0.00001f;

        private struct Vert
        {
            public int Outer;
            public int IncomingFringe;
            public int OutgoingFringe;
            public int Inner;
            public int Fill;
        }

        internal static bool Draw(VertexHelper mesh, ReadOnlySpan<Vector2> points, Rect rect, Vector4 uv,
            ClosedShapeMode mode, float width, float outerAa, float innerAa, in ContourPaint fill, in ContourPaint edge)
        {
            if (points.Length < 3 || points.Length > MaximumSamples || rect.width <= 0f || rect.height <= 0f)
            {
                return false;
            }

            Span<Vector2> contour = stackalloc Vector2[points.Length];
            contour = Clean(points, contour);
            if (contour.Length < 3 || !IsSimple(contour))
            {
                return false;
            }

            float area = SignedArea(contour);
            if (Mathf.Abs(area) <= Epsilon)
            {
                return false;
            }

            if (area < 0f)
            {
                contour.Reverse();
            }

            bool hasFill = mode != ClosedShapeMode.Edge;
            bool hasEdge = mode != ClosedShapeMode.Fill && width > Epsilon;
            if (!hasFill && !hasEdge)
            {
                return true;
            }

            Span<Vector2> normals = stackalloc Vector2[contour.Length];
            Span<Vector2> outer = stackalloc Vector2[contour.Length];
            Span<Vector2> inner = stackalloc Vector2[contour.Length];
            Span<Vector2> innerFringe = stackalloc Vector2[contour.Length];
            BuildNormals(contour, normals);
            float limit = Mathf.Min(rect.width, rect.height) * 0.5f;
            float outerWidth = FitBand(contour, normals, contour, outer, -Mathf.Min(Width(outerAa), limit));
            float edgeWidth = hasEdge ? FitBand(contour, normals, contour, inner, Mathf.Min(Width(width), limit)) : 0f;
            hasEdge = edgeWidth > Epsilon;
            if (!hasFill && !hasEdge)
            {
                return true;
            }

            float fringeDepth = hasEdge
                ? FitBand(contour, normals, inner, innerFringe, Mathf.Min(edgeWidth + Width(innerAa), limit), edgeWidth)
                : 0f;
            bool fadeInside = fringeDepth > edgeWidth + Epsilon;
            ReadOnlySpan<Vector2> fillContour = !hasEdge ? contour : fadeInside ? innerFringe : inner;

            Span<int> fillIndices = stackalloc int[(contour.Length - 2) * 3];
            int triangleIndexCount = hasFill ? Triangulate(fillContour, fillIndices) : 0;
            if (hasFill && triangleIndexCount == 0)
            {
                return false;
            }

            Span<Vert> rows = stackalloc Vert[contour.Length];
            var exteriorPaint = hasEdge ? edge : fill;
            for (int i = 0; i < rows.Length; i++)
            {
                Vector2 position = contour[i];
                int outline = AddVertex(mesh, position, rect, uv, exteriorPaint.Sample(position, rect));
                var fringe = outerWidth < -Epsilon
                    ? AddOuterFringe(mesh, contour, i, outline, outer[i], -outerWidth, rect, uv, exteriorPaint)
                    : (Incoming: outline, Outgoing: outline);
                int innerIndex = hasEdge
                    ? AddVertex(mesh, inner[i], rect, uv, edge.Sample(inner[i], rect))
                    : outline;
                int fillIndex = innerIndex;
                if (hasEdge && (hasFill || fadeInside))
                {
                    Vector2 fillPosition = fadeInside ? innerFringe[i] : inner[i];
                    Color fillColor = hasFill ? fill.Sample(fillPosition, rect) : edge.Sample(fillPosition, rect, 0f);
                    fillIndex = AddVertex(mesh, fillPosition, rect, uv, fillColor);
                }

                rows[i] = new Vert
                {
                    Outer = outline,
                    IncomingFringe = fringe.Incoming,
                    OutgoingFringe = fringe.Outgoing,
                    Inner = innerIndex,
                    Fill = fillIndex,
                };
            }

            for (int i = 0; i < rows.Length; i++)
            {
                Vert previous = rows[(i + rows.Length - 1) % rows.Length];
                Vert current = rows[i];
                GraphicMesh.AddStrip(mesh, previous.Outer, previous.OutgoingFringe, current.Outer, current.IncomingFringe);
                if (hasEdge)
                {
                    GraphicMesh.AddStrip(mesh, previous.Inner, previous.Outer, current.Inner, current.Outer);
                    if (fadeInside)
                    {
                        GraphicMesh.AddStrip(mesh, previous.Fill, previous.Inner, current.Fill, current.Inner);
                    }
                }
            }

            for (int i = 0; i < triangleIndexCount; i += 3)
            {
                mesh.AddTriangle(rows[fillIndices[i]].Fill, rows[fillIndices[i + 1]].Fill, rows[fillIndices[i + 2]].Fill);
            }

            return true;
        }

        private static (int Incoming, int Outgoing) AddOuterFringe(VertexHelper mesh, ReadOnlySpan<Vector2> contour,
            int index, int solid, Vector2 miter, float width, Rect rect, Vector4 uv, in ContourPaint paint)
        {
            Vector2 position = contour[index];
            Vector2 previous = contour[(index + contour.Length - 1) % contour.Length];
            Vector2 next = contour[(index + 1) % contour.Length];
            Vector2 incoming = -Vector2.Perpendicular((position - previous).normalized);
            Vector2 outgoing = -Vector2.Perpendicular((next - position).normalized);
            float angle = Mathf.Atan2(Cross(incoming, outgoing), Vector2.Dot(incoming, outgoing));
            Color transparent = paint.Sample(position, rect, 0f);
            if (angle <= Epsilon)
            {
                int shared = AddVertex(mesh, miter, rect, uv, transparent);
                return (shared, shared);
            }

            // Keep the solid vertex sharp. The fade follows an arc at constant distance from it.
            const float maximumStep = 10f * Mathf.Deg2Rad;
            int steps = Mathf.CeilToInt(angle / maximumStep);
            float firstAngle = Mathf.Atan2(incoming.y, incoming.x);
            int start = AddVertex(mesh, position + incoming * width, rect, uv, transparent);
            int last = start;
            for (int step = 1; step <= steps; step++)
            {
                float currentAngle = firstAngle + angle * step / steps;
                Vector2 direction = step == steps ? outgoing : new Vector2(Mathf.Cos(currentAngle), Mathf.Sin(currentAngle));
                int current = AddVertex(mesh, position + direction * width, rect, uv, transparent);
                GraphicMesh.AddFanTriangle(mesh, solid, last, current);
                last = current;
            }

            return (start, last);
        }

        internal static Vector2 EvaluateSymmetricCurve(AnimationCurve curve, Rect rect, float probe, bool fillAbove)
        {
            probe = Mathf.Clamp01(probe);
            float halfProbe = 1f - Mathf.Abs(2f * probe - 1f);
            float height = curve == null ? 0.5f : curve.Evaluate(halfProbe);
            height = float.IsFinite(height) ? height : 0.5f;
            float y = Mathf.LerpUnclamped(rect.yMin, rect.yMax, height);
            float gap = probe > 0f && probe < 1f ? Mathf.Min(0.01f, rect.height * 0.001f) : 0f;
            y = fillAbove ? Mathf.Min(y, rect.yMax - gap) : Mathf.Max(y, rect.yMin + gap);
            return new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, probe), y);
        }

        internal static int BuildSymmetricCurve(AnimationCurve curve, Rect rect, int quality, bool fillAbove, Span<Vector2> contour)
        {
            int curveCount = (quality - 1) * 2 + 1;
            float straightY = fillAbove ? rect.yMax : rect.yMin;
            for (int i = 0; i < curveCount; i++)
            {
                contour[i] = EvaluateSymmetricCurve(curve, rect, (float)i / (curveCount - 1), fillAbove);
            }

            contour[curveCount] = new Vector2(rect.xMax, straightY);
            contour[curveCount + 1] = new Vector2(rect.xMin, straightY);
            return curveCount + 2;
        }

        internal static Gradient WhiteGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        private static Span<Vector2> Clean(ReadOnlySpan<Vector2> source, Span<Vector2> output)
        {
            int count = 0;
            foreach (Vector2 point in source)
            {
                if (!float.IsFinite(point.x) || !float.IsFinite(point.y))
                {
                    return output[..0];
                }

                if (count == 0 || (output[count - 1] - point).sqrMagnitude > Epsilon * Epsilon)
                {
                    output[count++] = point;
                }
            }

            if (count > 1 && (output[0] - output[count - 1]).sqrMagnitude <= Epsilon * Epsilon)
            {
                count--;
            }

            for (int i = 0; i < count && count >= 3;)
            {
                Vector2 previous = output[(i + count - 1) % count];
                Vector2 current = output[i];
                Vector2 next = output[(i + 1) % count];
                if (Mathf.Abs(Cross(current - previous, next - current)) <= Epsilon &&
                    Vector2.Dot(current - previous, next - current) > 0f)
                {
                    output[(i + 1)..count].CopyTo(output[i..]);
                    count--;
                }
                else
                {
                    i++;
                }
            }

            return output[..count];
        }

        private static void BuildNormals(ReadOnlySpan<Vector2> contour, Span<Vector2> normals)
        {
            for (int i = 0; i < contour.Length; i++)
            {
                Vector2 previous = contour[(i + contour.Length - 1) % contour.Length];
                Vector2 current = contour[i];
                Vector2 next = contour[(i + 1) % contour.Length];
                Vector2 incoming = -Vector2.Perpendicular((current - previous).normalized);
                Vector2 outgoing = -Vector2.Perpendicular((next - current).normalized);
                Vector2 bisector = (incoming + outgoing).normalized;
                normals[i] = bisector == Vector2.zero ? outgoing : bisector / Mathf.Max(0.25f, Vector2.Dot(bisector, outgoing));
            }
        }

        private static float FitBand(ReadOnlySpan<Vector2> contour, ReadOnlySpan<Vector2> normals,
            ReadOnlySpan<Vector2> reference, Span<Vector2> candidate, float requested, float current = 0f)
        {
            WriteOffset(contour, normals, candidate, requested);
            bool expanding = requested < 0f;
            if (Mathf.Approximately(requested, current) || FitsBand(reference, candidate, expanding))
            {
                return requested;
            }

            float valid = current;
            float invalid = requested;
            for (int i = 0; i < 10; i++)
            {
                float middle = (valid + invalid) * 0.5f;
                WriteOffset(contour, normals, candidate, middle);
                if (FitsBand(reference, candidate, expanding))
                {
                    valid = middle;
                }
                else
                {
                    invalid = middle;
                }
            }

            WriteOffset(contour, normals, candidate, valid);
            return valid;
        }

        private static void WriteOffset(ReadOnlySpan<Vector2> contour, ReadOnlySpan<Vector2> normals, Span<Vector2> result, float depth)
        {
            for (int i = 0; i < contour.Length; i++)
            {
                result[i] = contour[i] - normals[i] * depth;
            }
        }

        private static bool FitsBand(ReadOnlySpan<Vector2> reference, ReadOnlySpan<Vector2> candidate, bool expanding)
        {
            ReadOnlySpan<Vector2> outer = expanding ? candidate : reference;
            ReadOnlySpan<Vector2> inner = expanding ? reference : candidate;
            for (int i = 0; i < inner.Length; i++)
            {
                int previous = (i + inner.Length - 1) % inner.Length;
                if (Cross(inner[i] - inner[previous], outer[previous] - inner[previous]) > Epsilon ||
                    Cross(outer[i] - inner[i], outer[previous] - inner[i]) > Epsilon)
                {
                    return false;
                }
            }

            return SignedArea(candidate) > Epsilon && IsSimple(candidate);
        }

        private static bool IsSimple(ReadOnlySpan<Vector2> points)
        {
            for (int i = 0; i < points.Length; i++)
            {
                int next = (i + 1) % points.Length;
                for (int j = i + 2; j < points.Length; j++)
                {
                    int otherNext = (j + 1) % points.Length;
                    if (otherNext == i)
                    {
                        continue;
                    }

                    if (Intersects(points[i], points[next], points[j], points[otherNext]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float first = Cross(b - a, c - a);
            float second = Cross(b - a, d - a);
            float third = Cross(d - c, a - c);
            float fourth = Cross(d - c, b - c);
            if (((first < -Epsilon && second > Epsilon) || (first > Epsilon && second < -Epsilon)) &&
                ((third < -Epsilon && fourth > Epsilon) || (third > Epsilon && fourth < -Epsilon)))
            {
                return true;
            }

            return (Mathf.Abs(first) <= Epsilon && Between(c, a, b)) ||
                   (Mathf.Abs(second) <= Epsilon && Between(d, a, b)) ||
                   (Mathf.Abs(third) <= Epsilon && Between(a, c, d)) ||
                   (Mathf.Abs(fourth) <= Epsilon && Between(b, c, d));
        }

        private static bool Between(Vector2 point, Vector2 a, Vector2 b)
        {
            return Vector2.Dot(point - a, point - b) <= Epsilon;
        }

        private static float SignedArea(ReadOnlySpan<Vector2> points)
        {
            Vector2 origin = points[0];
            float area = 0f;
            for (int i = 1; i < points.Length - 1; i++)
            {
                area += Cross(points[i] - origin, points[i + 1] - origin);
            }

            return area * 0.5f;
        }

        private static int Triangulate(ReadOnlySpan<Vector2> contour, Span<int> triangles)
        {
            Span<int> remaining = stackalloc int[contour.Length];
            for (int i = 0; i < contour.Length; i++)
            {
                remaining[i] = i;
            }

            int count = contour.Length;
            int index = 0;
            while (count >= 3)
            {
                bool clipped = false;
                for (int i = 0; i < count; i++)
                {
                    int a = remaining[(i + count - 1) % count];
                    int b = remaining[i];
                    int c = remaining[(i + 1) % count];
                    if (Cross(contour[b] - contour[a], contour[c] - contour[b]) <= Epsilon)
                    {
                        continue;
                    }

                    if (HasInteriorVertex(contour, remaining[..count], a, b, c))
                    {
                        continue;
                    }

                    triangles[index++] = a;
                    triangles[index++] = c;
                    triangles[index++] = b;
                    remaining[(i + 1)..count].CopyTo(remaining[i..]);
                    count--;
                    clipped = true;
                    break;
                }

                if (!clipped)
                {
                    return 0;
                }
            }

            return index;
        }

        private static bool HasInteriorVertex(ReadOnlySpan<Vector2> points, ReadOnlySpan<int> remaining, int a, int b, int c)
        {
            Vector2 start = points[a];
            Vector2 middle = points[b];
            Vector2 end = points[c];
            foreach (int index in remaining)
            {
                if (index != a && index != b && index != c)
                {
                    Vector2 point = points[index];
                    if (Cross(middle - start, point - start) >= -Epsilon &&
                        Cross(end - middle, point - middle) >= -Epsilon &&
                        Cross(start - end, point - end) >= -Epsilon)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int AddVertex(VertexHelper mesh, Vector2 position, Rect rect, Vector4 uv, Color color)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(position, color, GraphicMesh.MapUv(position, rect, uv));
            return index;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static float Width(float value)
        {
            return float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }
    }
}
