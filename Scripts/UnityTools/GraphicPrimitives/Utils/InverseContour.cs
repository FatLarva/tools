using System;
using System.Buffers;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    internal static class InverseContour
    {
        internal const int MaximumEllipseDetail = 1024;
        internal const int MaximumCornerDetail = 255;
        private const double FullTurn = Math.PI * 2d;

        private readonly struct Point
        {
            public readonly Vector2 Position;
            public readonly double Angle;

            public Point(Vector2 position)
            {
                Position = position;
                Angle = PolarAngle(position.x, position.y);
            }
        }

        private readonly struct Ray
        {
            public readonly double Angle;
            public readonly double X;
            public readonly double Y;

            public Ray(double angle)
            {
                Angle = angle;
                X = Math.Cos(angle);
                Y = Math.Sin(angle);
            }

            public Vector2 Position(Vector2 center, double radius)
            {
                return new Vector2((float)(center.x + X * radius), (float)(center.y + Y * radius));
            }
        }

        private struct Vert
        {
            public int HoleFringe;
            public int Hole;
            public int Stroke;
            public int Transition;
            public int Fill;
            public int Panel;
        }

        private readonly ref struct Ring
        {
            private readonly ReadOnlySpan<Point> _points;
            private readonly int _first;

            public Ring(ReadOnlySpan<Point> points)
            {
                _points = points;
                _first = 0;
                for (int i = 1; i < points.Length; i++)
                {
                    if (points[i].Angle < points[_first].Angle)
                    {
                        _first = i;
                    }
                }
            }

            public double Radius(Ray ray)
            {
                int count = _points.Length;
                if (count == 0)
                {
                    return 0f;
                }

                int first = 0;
                int last = count;
                while (first < last)
                {
                    int middle = first + (last - first) / 2;
                    if (_points[(_first + middle) % count].Angle <= ray.Angle)
                    {
                        first = middle + 1;
                    }
                    else
                    {
                        last = middle;
                    }
                }

                Vector2 a = _points[(_first + first + count - 1) % count].Position;
                Vector2 b = _points[(_first + first) % count].Position;
                double denominator = ray.X * ((double)b.y - a.y) - ray.Y * ((double)b.x - a.x);
                if (denominator == 0d)
                {
                    return Math.Max(0d, a.x * ray.X + a.y * ray.Y);
                }

                return Math.Max(0d, Cross(a, b) / denominator);
            }
        }

        internal static bool Draw(VertexHelper mesh, Rect panel, Rect hole, bool ellipse, float cornerRadius, int detail,
            float strokeWidth, float outerAa, float innerAa, ClosedShapeMode mode, in ContourPaint fill,
            in ContourPaint stroke, Vector4 uv)
        {
            if (panel.width <= 0f || panel.height <= 0f)
            {
                return true;
            }

            if (!IsFinite(panel) || !IsFinite(hole) || !float.IsFinite(panel.size.sqrMagnitude))
            {
                return false;
            }

            bool hasFill = mode != ClosedShapeMode.Edge;
            hole = ClipHole(panel, hole);
            if (hole.width <= 0f || hole.height <= 0f)
            {
                return !hasFill || DrawWholePanel(mesh, panel, uv, fill);
            }

            float radius = HoleCornerRadius(hole, cornerRadius);
            if (!ellipse && radius <= 0f && hole == panel)
            {
                return true;
            }

            Vector2 center = hole.center;
            float diagonal = panel.size.magnitude;
            float thickness = mode == ClosedShapeMode.Fill ? 0f : Mathf.Min(Width(strokeWidth), diagonal);
            bool hasStroke = thickness > 0f;
            if (!hasFill && !hasStroke)
            {
                return true;
            }

            float smoothingDepth = Mathf.Min(Width(innerAa), diagonal);
            bool hasSmoothing = hasStroke && smoothingDepth > 0f;
            float featherWidth = Width(outerAa);
            bool hasFringe = featherWidth > 0f;
            int cornerSteps = HoleCornerSteps(detail, ellipse);
            int pointLimit = ellipse ? 4 * cornerSteps : radius > 0f ? 4 * (cornerSteps + 1) : 4;
            int ringCount = 1 + (hasFringe ? 1 : 0) + (hasStroke ? 1 : 0) + (hasSmoothing ? 1 : 0);

            // Each expanded ring can meet each of the four panel sides at two points.
            long rayLimit = pointLimit * ringCount + 4L + (hasStroke ? 8L : 0L) + (hasSmoothing ? 8L : 0L);
            int verticesPerRay = 1 + (hasFringe ? 1 : 0) + (hasStroke ? 1 : 0) +
                (hasSmoothing || hasFill && hasStroke ? 1 : 0) + (hasFill ? 1 : 0);
            if (mesh.currentVertCount + rayLimit * verticesPerRay > GraphicMesh.MaximumVertices)
            {
                return false;
            }

            int stride = pointLimit;
            using var pointOwner = MemoryPool<Point>.Shared.Rent(stride * ringCount);
            Span<Point> storage = pointOwner.Memory.Span;
            Span<Point> holePoints = BuildHole(storage[..stride], hole.size * 0.5f, radius, cornerSteps, ellipse);
            if (holePoints.Length < 3)
            {
                return !hasFill || DrawWholePanel(mesh, panel, uv, fill);
            }

            storage = storage[stride..];
            Span<Point> fringePoints = default;
            Span<Point> strokePoints = default;
            Span<Point> smoothingPoints = default;
            if (hasFringe)
            {
                fringePoints = BuildHoleFringe(holePoints, storage[..holePoints.Length], featherWidth);
                storage = storage[stride..];
            }

            if (hasStroke)
            {
                strokePoints = storage[..holePoints.Length];
                if (!BuildOffset(holePoints, thickness, strokePoints))
                {
                    return false;
                }

                storage = storage[stride..];
            }

            if (hasSmoothing)
            {
                smoothingPoints = storage[..holePoints.Length];
                if (!BuildOffset(holePoints, thickness + smoothingDepth, smoothingPoints))
                {
                    return false;
                }
            }

            var relativePanel = Rect.MinMaxRect(panel.xMin - center.x, panel.yMin - center.y,
                panel.xMax - center.x, panel.yMax - center.y);
            double[] angles = ArrayPool<double>.Shared.Rent((int)rayLimit);
            try
            {
                int count = 0;
                AddAngles(holePoints, angles, ref count);
                AddAngles(fringePoints, angles, ref count);
                AddAngles(strokePoints, angles, ref count);
                AddAngles(smoothingPoints, angles, ref count);
                AddCornerAngles(panel, center, angles, ref count);
                AddPanelIntersections(strokePoints, relativePanel, angles, ref count);
                AddPanelIntersections(smoothingPoints, relativePanel, angles, ref count);
                Array.Sort(angles, 0, count);
                count = RemoveDuplicateAngles(angles, count);

                var holeRing = new Ring(holePoints);
                var fringeRing = new Ring(fringePoints);
                var strokeRing = new Ring(strokePoints);
                var smoothingRing = new Ring(smoothingPoints);
                var holePaint = hasStroke ? stroke : fill;
                using var rowsOwner = MemoryPool<Vert>.Shared.Rent(count);
                Span<Vert> rows = rowsOwner.Memory.Span[..count];
                for (int i = 0; i < rows.Length; i++)
                {
                    var ray = new Ray(angles[i]);
                    double panelRadius = PanelRadius(panel, center, ray, out Vector2 panelPosition);
                    double holeRadius = Math.Min(panelRadius, holeRing.Radius(ray));
                    double widthOutsideHole = panelRadius - holeRadius;
                    double fringeRadius = hasFringe
                        ? Math.Max(fringeRing.Radius(ray), holeRadius - widthOutsideHole)
                        : holeRadius;
                    double strokeRadius = hasStroke
                        ? Math.Max(holeRadius, Math.Min(strokeRing.Radius(ray), panelRadius))
                        : holeRadius;
                    double transitionTarget = hasSmoothing ? smoothingRing.Radius(ray) : strokeRadius;
                    double transitionRadius = Math.Max(strokeRadius, Math.Min(transitionTarget, panelRadius));

                    Vector2 boundary = holeRadius == panelRadius ? panelPosition : ray.Position(center, holeRadius);
                    int boundaryIndex = AddVertex(mesh, boundary, panel, uv, holePaint.Sample(boundary, panel));
                    int strokeIndex = boundaryIndex;
                    if (strokeRadius > holeRadius)
                    {
                        Vector2 position = strokeRadius == panelRadius ? panelPosition : ray.Position(center, strokeRadius);
                        strokeIndex = AddVertex(mesh, position, panel, uv, stroke.Sample(position, panel));
                    }

                    int transitionIndex = strokeIndex;
                    if (transitionRadius > strokeRadius)
                    {
                        Vector2 position = transitionRadius == panelRadius ? panelPosition : ray.Position(center, transitionRadius);
                        Color start = stroke.Sample(position, panel);
                        Color end = hasFill ? fill.Sample(position, panel) : stroke.Sample(position, panel, 0f);
                        float fraction = (float)((transitionRadius - strokeRadius) / (transitionTarget - strokeRadius));
                        transitionIndex = AddVertex(mesh, position, panel, uv, Color.Lerp(start, end, fraction));
                    }

                    int fillIndex = transitionIndex;
                    if (hasFill && hasStroke && !hasSmoothing)
                    {
                        // Two colors at a hard stroke boundary keep the fill from bleeding into the stroke.
                        Vector2 position = strokeRadius == panelRadius ? panelPosition : ray.Position(center, strokeRadius);
                        fillIndex = AddVertex(mesh, position, panel, uv, fill.Sample(position, panel));
                    }

                    int panelIndex = fillIndex;
                    if (hasFill && panelRadius > transitionRadius)
                    {
                        panelIndex = AddVertex(mesh, panelPosition, panel, uv, fill.Sample(panelPosition, panel));
                    }

                    int fringeIndex = boundaryIndex;
                    if (fringeRadius < holeRadius)
                    {
                        Vector2 position = ray.Position(center, fringeRadius);
                        fringeIndex = AddVertex(mesh, position, panel, uv, holePaint.Sample(boundary, panel, 0f));
                    }

                    rows[i] = new Vert
                    {
                        HoleFringe = fringeIndex,
                        Hole = boundaryIndex,
                        Stroke = strokeIndex,
                        Transition = transitionIndex,
                        Fill = fillIndex,
                        Panel = panelIndex,
                    };
                }

                for (int i = 0; i < rows.Length; i++)
                {
                    Vert a = rows[i];
                    Vert b = rows[(i + 1) % rows.Length];
                    GraphicMesh.AddStrip(mesh, a.HoleFringe, a.Hole, b.HoleFringe, b.Hole);
                    GraphicMesh.AddStrip(mesh, a.Hole, a.Stroke, b.Hole, b.Stroke);
                    GraphicMesh.AddStrip(mesh, a.Stroke, a.Transition, b.Stroke, b.Transition);
                    GraphicMesh.AddStrip(mesh, a.Fill, a.Panel, b.Fill, b.Panel);
                }

                return true;
            }
            finally
            {
                ArrayPool<double>.Shared.Return(angles);
            }
        }

        internal static bool ContainsHole(Rect panel, Rect hole, bool ellipse, float cornerRadius, int detail, Vector2 point)
        {
            hole = ClipHole(panel, hole);
            if (!(point.x > hole.xMin && point.x < hole.xMax && point.y > hole.yMin && point.y < hole.yMax))
            {
                return false;
            }

            float radius = HoleCornerRadius(hole, cornerRadius);
            if (!ellipse && radius <= 0f)
            {
                return true;
            }

            Vector2 relative = point - hole.center;
            if (relative.x == 0f && relative.y == 0f)
            {
                return true;
            }

            int quarter = relative.y >= 0f ? (relative.x >= 0f ? 0 : 1) : (relative.x < 0f ? 2 : 3);
            Vector2 startNormal = Axis(quarter);
            Vector2 endNormal = Axis((quarter + 1) % 4);
            Vector2 halfSize = hole.size * 0.5f;
            Vector2 arcCenter = ArcCenter(startNormal, endNormal, halfSize, radius);
            Vector2 arcPoint = ellipse ? new Vector2(relative.x / halfSize.x, relative.y / halfSize.y) : relative - arcCenter;
            float x = Vector2.Dot(arcPoint, startNormal);
            float y = Vector2.Dot(arcPoint, endNormal);
            if (!ellipse && (x <= 0f || y <= 0f))
            {
                return true;
            }

            // The angle selects the same chord that the mesh uses, including coarse tessellation.
            int steps = HoleCornerSteps(detail, ellipse);
            int segment = Mathf.Min((int)(Math.Atan2(y, x) * steps / (Math.PI * 0.5d)), steps - 1);
            Vector2 first = SampleHole(startNormal, endNormal, halfSize, arcCenter, radius, steps, segment, ellipse);
            Vector2 last = SampleHole(startNormal, endNormal, halfSize, arcCenter, radius, steps, segment + 1, ellipse);
            return Cross(last - first, relative - first) > 0d;
        }

        private static Rect ClipHole(Rect panel, Rect hole)
        {
            return Rect.MinMaxRect(Mathf.Max(panel.xMin, hole.xMin), Mathf.Max(panel.yMin, hole.yMin),
                Mathf.Min(panel.xMax, hole.xMax), Mathf.Min(panel.yMax, hole.yMax));
        }

        private static float HoleCornerRadius(Rect hole, float radius)
        {
            return Mathf.Min(Width(radius), Mathf.Min(hole.width, hole.height) * 0.5f);
        }

        private static int HoleCornerSteps(int detail, bool ellipse)
        {
            return ellipse ? (Mathf.Clamp(detail, 4, MaximumEllipseDetail) + 3) / 4
                : Mathf.Clamp(detail, 1, MaximumCornerDetail);
        }

        private static Vector2 ArcCenter(Vector2 startNormal, Vector2 endNormal, Vector2 halfSize, float radius)
        {
            return Vector2.Scale(startNormal + endNormal, halfSize - Vector2.one * radius);
        }

        private static Vector2 SampleHole(Vector2 startNormal, Vector2 endNormal, Vector2 halfSize, Vector2 arcCenter,
            float radius, int steps, int index, bool ellipse)
        {
            float angle = Mathf.PI * 0.5f * index / steps;
            Vector2 normal = index == 0 ? startNormal : index == steps ? endNormal
                : startNormal * Mathf.Cos(angle) + endNormal * Mathf.Sin(angle);
            return ellipse ? Vector2.Scale(halfSize, normal) : arcCenter + normal * radius;
        }

        private static Span<Point> BuildHole(Span<Point> storage, Vector2 halfSize, float radius, int steps, bool ellipse)
        {
            int count = 0;
            for (int quarter = 0; quarter < 4; quarter++)
            {
                Vector2 startNormal = Axis(quarter);
                Vector2 endNormal = Axis((quarter + 1) % 4);
                if (!ellipse && radius <= 0f)
                {
                    AddPoint(storage, ref count, Vector2.Scale(startNormal + endNormal, halfSize));
                    continue;
                }

                Vector2 arcCenter = ArcCenter(startNormal, endNormal, halfSize, radius);
                int last = ellipse ? steps - 1 : steps;
                for (int i = 0; i <= last; i++)
                {
                    Vector2 point = SampleHole(startNormal, endNormal, halfSize, arcCenter, radius, steps, i, ellipse);
                    AddPoint(storage, ref count, point);
                }
            }

            if (count > 1 && (storage[count - 1].Position - storage[0].Position).sqrMagnitude == 0f)
            {
                count--;
            }

            return storage[..count];
        }

        private static void AddPoint(Span<Point> storage, ref int count, Vector2 point)
        {
            if (count == 0 || (point - storage[count - 1].Position).sqrMagnitude > 0f)
            {
                storage[count++] = new Point(point);
            }
        }

        private static Vector2 Axis(int quarter)
        {
            switch (quarter)
            {
                case 0:
                    return Vector2.right;
                case 1:
                    return Vector2.up;
                case 2:
                    return Vector2.left;
                default:
                    return Vector2.down;
            }
        }

        private static Span<Point> BuildHoleFringe(ReadOnlySpan<Point> points, Span<Point> result, float width)
        {
            float limit = float.PositiveInfinity;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i].Position;
                Vector2 b = points[(i + 1) % points.Length].Position;
                var normal = OutwardNormal(a, b);
                limit = Mathf.Min(limit, Vector2.Dot(a, normal));
            }

            if (width >= limit || limit <= 0f)
            {
                return default;
            }

            if (BuildOffset(points, -width, result))
            {
                return result;
            }

            // A short side can disappear before the whole inset closes. A scaled copy remains nested
            // and keeps the AA continuous across the full hole when the mitered inset no longer fits.
            float scale = 1f - width / limit;
            for (int i = 0; i < points.Length; i++)
            {
                result[i] = new Point(points[i].Position * scale);
            }

            return result;
        }

        private static bool BuildOffset(ReadOnlySpan<Point> points, float width, Span<Point> result)
        {
            Vector2 previous = OutwardNormal(points[^1].Position, points[0].Position);
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 position = points[i].Position;
                Vector2 normal = OutwardNormal(position, points[(i + 1) % points.Length].Position);
                float projection = 1f + Vector2.Dot(previous, normal);
                if (projection <= 0f)
                {
                    return false;
                }

                Vector2 offset = position + (previous + normal) * (width / projection);
                if (!float.IsFinite(offset.x) || !float.IsFinite(offset.y))
                {
                    return false;
                }

                result[i] = new Point(offset);
                previous = normal;
            }

            if (width < 0f)
            {
                for (int i = 0; i < points.Length; i++)
                {
                    int next = (i + 1) % points.Length;
                    if (Vector2.Dot(result[next].Position - result[i].Position, points[next].Position - points[i].Position) <= 0f)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static Vector2 OutwardNormal(Vector2 first, Vector2 last)
        {
            Vector2 delta = last - first;
            return new Vector2(delta.y, -delta.x) / delta.magnitude;
        }

        private static void AddAngles(ReadOnlySpan<Point> points, double[] angles, ref int count)
        {
            foreach (Point point in points)
            {
                angles[count++] = point.Angle;
            }
        }

        private static void AddCornerAngles(Rect rect, Vector2 center, double[] angles, ref int count)
        {
            double left = (double)rect.xMin - center.x;
            double bottom = (double)rect.yMin - center.y;
            double right = (double)rect.xMax - center.x;
            double top = (double)rect.yMax - center.y;
            angles[count++] = PolarAngle(right, top);
            angles[count++] = PolarAngle(left, top);
            angles[count++] = PolarAngle(left, bottom);
            angles[count++] = PolarAngle(right, bottom);
        }

        private static void AddPanelIntersections(ReadOnlySpan<Point> points, Rect rect, double[] angles, ref int count)
        {
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i].Position;
                Vector2 b = points[(i + 1) % points.Length].Position;
                AddSideIntersection(a.x, b.x, a.y, b.y, rect.xMin, rect.yMin, rect.yMax, false, angles, ref count);
                AddSideIntersection(a.x, b.x, a.y, b.y, rect.xMax, rect.yMin, rect.yMax, false, angles, ref count);
                AddSideIntersection(a.y, b.y, a.x, b.x, rect.yMin, rect.xMin, rect.xMax, true, angles, ref count);
                AddSideIntersection(a.y, b.y, a.x, b.x, rect.yMax, rect.xMin, rect.xMax, true, angles, ref count);
            }
        }

        private static void AddSideIntersection(float a, float b, float perpendicularA, float perpendicularB,
            float side, float min, float max, bool horizontal, double[] angles, ref int count)
        {
            if ((a < side && b > side) || (b < side && a > side))
            {
                float other = Mathf.LerpUnclamped(perpendicularA, perpendicularB, (side - a) / (b - a));
                if (other >= min && other <= max)
                {
                    angles[count++] = horizontal ? PolarAngle(other, side) : PolarAngle(side, other);
                }
            }
        }

        private static int RemoveDuplicateAngles(double[] angles, int count)
        {
            int write = 1;
            for (int read = 1; read < count; read++)
            {
                if (angles[read] > angles[write - 1])
                {
                    angles[write++] = angles[read];
                }
            }

            return write;
        }

        private static double PolarAngle(double x, double y)
        {
            double angle = Math.Atan2(y, x);
            if (angle < 0d)
            {
                angle += FullTurn;
            }

            return angle == FullTurn ? 0d : angle;
        }

        private static double PanelRadius(Rect rect, Vector2 center, Ray ray, out Vector2 position)
        {
            float x = ray.X >= 0d ? rect.xMax : rect.xMin;
            float y = ray.Y >= 0d ? rect.yMax : rect.yMin;
            double horizontal = ray.X == 0d ? double.PositiveInfinity : ((double)x - center.x) / ray.X;
            double vertical = ray.Y == 0d ? double.PositiveInfinity : ((double)y - center.y) / ray.Y;
            if (horizontal <= vertical)
            {
                position = new Vector2(x, (float)(center.y + ray.Y * horizontal));
                return horizontal;
            }

            position = new Vector2((float)(center.x + ray.X * vertical), y);
            return vertical;
        }

        private static bool DrawWholePanel(VertexHelper mesh, Rect panel, Vector4 uv, in ContourPaint fill)
        {
            if (mesh.currentVertCount + 5 > GraphicMesh.MaximumVertices)
            {
                return false;
            }

            int center = AddVertex(mesh, panel.center, panel, uv, fill.Sample(panel.center, panel));
            Span<Vector2> corners = stackalloc Vector2[4]
            {
                new Vector2(panel.xMax, panel.yMax),
                new Vector2(panel.xMin, panel.yMax),
                new Vector2(panel.xMin, panel.yMin),
                new Vector2(panel.xMax, panel.yMin),
            };
            int first = mesh.currentVertCount;
            foreach (Vector2 corner in corners)
            {
                AddVertex(mesh, corner, panel, uv, fill.Sample(corner, panel));
            }

            for (int i = 0; i < corners.Length; i++)
            {
                GraphicMesh.AddFanTriangle(mesh, center, first + i, first + (i + 1) % corners.Length);
            }

            return true;
        }

        private static int AddVertex(VertexHelper mesh, Vector2 position, Rect panel, Vector4 uv, Color color)
        {
            int index = mesh.currentVertCount;
            position.x = Mathf.Clamp(position.x, panel.xMin, panel.xMax);
            position.y = Mathf.Clamp(position.y, panel.yMin, panel.yMax);
            mesh.AddVert(position, color, GraphicMesh.MapUv(position, panel, uv));
            return index;
        }

        private static bool IsFinite(Rect rect)
        {
            return float.IsFinite(rect.xMin) && float.IsFinite(rect.yMin) &&
                float.IsFinite(rect.xMax) && float.IsFinite(rect.yMax);
        }

        private static float Width(float value)
        {
            return float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }

        private static double Cross(Vector2 left, Vector2 right)
        {
            return (double)left.x * right.y - (double)left.y * right.x;
        }
    }
}
