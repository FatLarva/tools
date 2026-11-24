using System;
using System.Collections.Generic;
using Tools;
using Tools.Collections.Spans;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Arrow Graphic", 12)]
    public class ArrowGraphic : PrimitiveGraphic
    {
        private const float PathEpsilon = 0.0001f;
        private const float CurveTolerance = 0.2f;
        internal const int MaximumPathPoints = 4096;
        private const int MaximumCurveDepth = 10;
        private const float MiterLimit = 4f;

        internal readonly struct PathPoint
        {
            public readonly Vector2 Position;
            public readonly float Distance;

            public PathPoint(Vector2 position, float distance)
            {
                Position = position;
                Distance = distance;
            }
        }

        internal struct LineVert
        {
            public Vector2 Position;
            public Vector2 LeftPosition;
            public Vector2 LeftPositionAa;
            public Vector2 RightPosition;
            public Vector2 RightPositionAa;
            public int Index;
            public int LeftIndex;
            public int LeftIndexAa;
            public int RightIndex;
            public int RightIndexAa;
        }
        
        internal struct CornerVert
        {
            public Vector2 Position;
            public Vector2 PositionAa0;
            public Vector2 PositionAa1;
            public Vector2 PositionAa2;
            public int Index;
            public int IndexAa0;
            public int IndexAa1;
            public int IndexAa2;
        }
        
        internal struct Vert
        {
            public Vector2 Position;
            public Vector2 PositionAa;
            public int Index;
            public int IndexAa;
        }
        
        internal readonly ref struct HullSettings
        {
            public readonly Rect EnclosingRect;
            public readonly bool IsAaEnabled;
            public readonly bool IsRounded;

            public HullSettings(Rect enclosingRect, bool isAaEnabled, bool isRounded)
            {
                EnclosingRect = enclosingRect;
                IsAaEnabled = isAaEnabled;
                IsRounded = isRounded;
            }
        }

        internal enum EndingType
        {
            None,
            Arrow,
            FilledArrow,
        }
        
        [Serializable]
        internal struct EndingSettings
        {
            public EndingType Type;
            [Range(0, 180)]
            public int Angle;
            [Range(1, 100)]
            public float Length;
            [Range(1, 100)]
            public int RoundedArrowSmoothness;
            
            public bool HasEnding => Type != EndingType.None;
            public float AngleRadians => Angle * Mathf.Deg2Rad;
        }
        
        [Serializable]
        internal struct DashesConfiguration
        {
            public bool DashesEnabled;
            [Min(1)]
            public int DashLengthInSegments;
            [Min(1)]
            public int GapLengthInSegments;
            // [Min(0)]
            public int OffsetInSegments;
        }
        
        [Serializable]
        internal struct LineKnot
        {
            public Vector2 Position;
            public Optional<Vector2> PrevTangentPoint;
            public Optional<Vector2> NextTangentPoint;
            public BezierTangentMode Mode;
        }
        
        [Serializable]
        internal struct KnotsConfiguration
        {
            public bool UseSpline;
            public LineKnot[] Knots;

            public bool IsEligible => Knots is { Length: > 1 };
        }
        
        [Serializable]
        internal struct MeshDebugInfo
        {
            public int Vertices ;
            public int Triangles;
            
            public void Set(int vertCount, int trianglesCount)
            {
                Vertices = vertCount;
                Triangles = trianglesCount;
            }
        }

        [Serializable]
        internal struct Settings
        {
            [Min(0)]
            public float Thickness;
        
            [Range(0, 100)]
            public float AntiAliasingLayerWidth;
            
            [Range(1, 1000)]
            public int SegmentsNumber;
            
            public bool IsRounded;
            public bool IsEndingsOnly;

            public KnotsConfiguration KnotsConfig;
            public DashesConfiguration DashConfig;

            public EndingSettings StartEnding;
            public EndingSettings EndEnding;
        }

        [SerializeField]
        private Settings _settings;
        
        [SerializeField, HideInInspector]
        private MeshDebugInfo _meshInfo;
        
        [SerializeField, HideInInspector]
        private Rect _lastUsedRect;

        private readonly List<PathPoint> _path = new();
        private Vector2 _startDirection;
        private Vector2 _endDirection;

        internal MeshDebugInfo MeshInfoAccess => _meshInfo;

        public float StrokeThickness
        {
            get { return _settings.Thickness; }
            set
            {
                var thickness = float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
                if (_settings.Thickness != thickness)
                {
                    _settings.Thickness = thickness;
                    SetVerticesDirty();
                }
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            _meshInfo.Set(0, 0);
            RebuildPath();

            if (_path.Count < 2 || !float.IsFinite(_settings.Thickness) || _settings.Thickness <= 0f)
            {
                return;
            }

            var rect = GetPixelAdjustedRect();
            var aa = float.IsFinite(_settings.AntiAliasingLayerWidth)
                ? Mathf.Clamp(_settings.AntiAliasingLayerWidth, 0f, 100f)
                : 0f;
            var thickness = _settings.Thickness;
            var startEnding = NormalizeEnding(_settings.StartEnding);
            var endEnding = NormalizeEnding(_settings.EndEnding);
            var isDashed = _settings.DashConfig.DashesEnabled;
            var hull = new HullSettings(rect, aa > 0f, _settings.IsRounded);
            var pathEnd = _path[^1].Distance;

            if (!_settings.IsEndingsOnly)
            {
                var shaftStart = CalculateEndingOffset(in startEnding, thickness);
                var shaftEnd = pathEnd - CalculateEndingOffset(in endEnding, thickness);
                if (shaftEnd - shaftStart > PathEpsilon)
                {
                    if (isDashed)
                    {
                        if (!DrawDashedStroke(vh, in hull, shaftStart, shaftEnd, thickness, aa, startEnding.HasEnding, endEnding.HasEnding))
                        {
                            vh.Clear();
                            return;
                        }
                    }
                    else
                    {
                        DrawStroke(vh, in hull, shaftStart, shaftEnd, thickness, aa, !startEnding.HasEnding, !endEnding.HasEnding);
                    }
                }
            }

            // The ends belong to the entire path, regardless of where the shaft or dashes stop.
            if (startEnding.HasEnding)
            {
                var start = _path[0].Position;
                var direction = -_startDirection;
                DrawEnding(in start, in direction, in startEnding, hull, aa, thickness, in vh);
            }

            if (endEnding.HasEnding)
            {
                var end = _path[^1].Position;
                var direction = _endDirection;
                DrawEnding(in end, in direction, in endEnding, hull, aa, thickness, in vh);
            }

            _meshInfo.Set(vh.currentVertCount, vh.currentIndexCount / 3);
        }

        internal IReadOnlyList<PathPoint> GetPathPreview()
        {
            RebuildPath();
            return _path;
        }

        internal Rect GetPathRect()
        {
            return GetPixelAdjustedRect();
        }

        internal Vector2 ToLocalPathPoint(Vector2 position, Rect rect)
        {
            return ConvertPathPoint(position, _lastUsedRect, rect, false);
        }

        internal Vector2 ToAuthoredPathPoint(Vector2 position, Rect rect)
        {
            return ConvertPathPoint(position, _lastUsedRect, rect, true);
        }

        private static Vector2 ConvertPathPoint(Vector2 position, Rect authoredRect, Rect currentRect, bool toAuthored)
        {
            if (!IsUsableRect(authoredRect) || !IsUsableRect(currentRect))
            {
                return position;
            }

            // Existing assets store the rect that their serialized local points belong to.
            // Sample against that stable reference instead of rescaling authored points after each resize.
            var scale = toAuthored
                ? new Vector2(authoredRect.width / currentRect.width, authoredRect.height / currentRect.height)
                : new Vector2(currentRect.width / authoredRect.width, currentRect.height / authoredRect.height);
            return Vector2.Scale(position, scale);
        }

        internal static bool IsUsableRect(Rect rect)
        {
            return float.IsFinite(rect.width) && float.IsFinite(rect.height) && rect.width > PathEpsilon && rect.height > PathEpsilon;
        }

        internal static CubicBezierSegment GetAuthoredCurve(in LineKnot first, in LineKnot second)
        {
            var from = first.Position;
            var to = second.Position;
            var outgoing = first.NextTangentPoint.GetValueOrDefault(Vector2.LerpUnclamped(from, to, 1f / 3f));
            var incoming = second.PrevTangentPoint.GetValueOrDefault(Vector2.LerpUnclamped(from, to, 2f / 3f));
            return new CubicBezierSegment(from, outgoing, incoming, to);
        }

        private void RebuildPath()
        {
            _path.Clear();
            _startDirection = Vector2.zero;
            _endDirection = Vector2.zero;
            var knotsConfig = _settings.KnotsConfig;
            if (!knotsConfig.IsEligible || knotsConfig.Knots.Length > MaximumPathPoints)
            {
                return;
            }

            var rect = GetPathRect();
            if (!IsUsableRect(rect))
            {
                return;
            }

            var knots = knotsConfig.Knots;
            if (!TryAppendPathPoint(ToLocalPathPoint(knots[0].Position, rect)))
            {
                return;
            }

            var detail = Mathf.Clamp(_settings.SegmentsNumber, 1, 1000);
            var minimumDepth = 0;
            while ((1 << minimumDepth) < detail)
            {
                minimumDepth++;
            }

            for (var i = 1; i < knots.Length; i++)
            {
                var previousCount = _path.Count;
                if (knotsConfig.UseSpline)
                {
                    var authored = GetAuthoredCurve(in knots[i - 1], in knots[i]);
                    var curve = new CubicBezierSegment(
                        ToLocalPathPoint(authored.Start, rect),
                        ToLocalPathPoint(authored.Outgoing, rect),
                        ToLocalPathPoint(authored.Incoming, rect),
                        ToLocalPathPoint(authored.End, rect));
                    if (!IsFinite(curve.Start) || !IsFinite(curve.Outgoing) || !IsFinite(curve.Incoming) || !IsFinite(curve.End) ||
                        !TryAppendCubic(in curve, 0, minimumDepth))
                    {
                        _path.Clear();
                        return;
                    }

                    if (_path.Count > previousCount)
                    {
                        var start = ResolveDirection(curve.Outgoing - curve.Start, curve.Incoming - curve.Start, curve.End - curve.Start);
                        var end = ResolveDirection(curve.End - curve.Incoming, curve.End - curve.Outgoing, curve.End - curve.Start);
                        SetPathDirections(start, end);
                    }
                }
                else
                {
                    if (!TryAppendPathPoint(ToLocalPathPoint(knots[i].Position, rect)))
                    {
                        _path.Clear();
                        return;
                    }

                    if (_path.Count > previousCount)
                    {
                        var direction = (_path[^1].Position - _path[^2].Position).normalized;
                        SetPathDirections(direction, direction);
                    }
                }
            }
        }

        private void SetPathDirections(Vector2 start, Vector2 end)
        {
            if (_startDirection == Vector2.zero)
            {
                _startDirection = start;
            }

            _endDirection = end;
        }

        private static Vector2 ResolveDirection(Vector2 direction, Vector2 firstFallback, Vector2 secondFallback)
        {
            const float minimumDistanceSquared = PathEpsilon * PathEpsilon;
            if (direction.sqrMagnitude <= minimumDistanceSquared)
            {
                direction = firstFallback.sqrMagnitude > minimumDistanceSquared ? firstFallback : secondFallback;
            }

            return direction.normalized;
        }

        private bool TryAppendCubic(in CubicBezierSegment curve, int depth, int minimumDepth)
        {
            if (depth >= MaximumCurveDepth || (depth >= minimumDepth && IsFlatEnough(in curve)))
            {
                return TryAppendPathPoint(curve.End);
            }

            curve.Split(0.5f, out var first, out var second);
            return TryAppendCubic(in first, depth + 1, minimumDepth) &&
                TryAppendCubic(in second, depth + 1, minimumDepth);
        }

        private static bool IsFlatEnough(in CubicBezierSegment curve)
        {
            var chord = curve.End - curve.Start;
            var chordLength = chord.magnitude;
            var controlLength = Vector2.Distance(curve.Start, curve.Outgoing)
                + Vector2.Distance(curve.Outgoing, curve.Incoming)
                + Vector2.Distance(curve.Incoming, curve.End);
            if (controlLength - chordLength > CurveTolerance)
            {
                return false;
            }

            if (chordLength <= PathEpsilon)
            {
                return true;
            }

            var normal = Vector2.Perpendicular(chord / chordLength);
            return Mathf.Abs(Vector2.Dot(curve.Outgoing - curve.Start, normal)) <= CurveTolerance &&
                Mathf.Abs(Vector2.Dot(curve.Incoming - curve.Start, normal)) <= CurveTolerance;
        }

        private bool TryAppendPathPoint(Vector2 position)
        {
            if (!IsFinite(position))
            {
                return false;
            }

            if (_path.Count == 0)
            {
                _path.Add(new PathPoint(position, 0f));
                return true;
            }

            var previous = _path[^1];
            var length = Vector2.Distance(previous.Position, position);
            var distance = previous.Distance + length;
            if (!float.IsFinite(distance))
            {
                return false;
            }

            if (length <= PathEpsilon || distance <= previous.Distance)
            {
                return true;
            }

            if (_path.Count >= MaximumPathPoints)
            {
                return false;
            }

            _path.Add(new PathPoint(position, distance));
            return true;
        }

        internal static bool IsFinite(Vector2 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }

        private int FindPathEndIndex(float distance)
        {
            var low = 1;
            var high = _path.Count - 1;
            while (low < high)
            {
                var mid = low + (high - low) / 2;
                if (_path[mid].Distance < distance)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            return low;
        }

        private Vector2 GetPointAtDistance(float distance, int endIndex)
        {
            var start = _path[endIndex - 1];
            var end = _path[endIndex];
            var t = (distance - start.Distance) / (end.Distance - start.Distance);
            return Vector2.LerpUnclamped(start.Position, end.Position, t);
        }

        private static EndingSettings NormalizeEnding(EndingSettings settings)
        {
            if ((settings.Type != EndingType.Arrow && settings.Type != EndingType.FilledArrow) ||
                !float.IsFinite(settings.Length) || settings.Length <= 0f)
            {
                settings.Type = EndingType.None;
            }

            settings.Length = Mathf.Clamp(settings.Length, 0f, 100f);
            settings.Angle = Mathf.Clamp(settings.Angle, 1, 179);
            settings.RoundedArrowSmoothness = Mathf.Clamp(settings.RoundedArrowSmoothness, 1, 100);
            return settings;
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();

            _lastUsedRect = GetPathRect();
            _settings = new Settings
            {
                AntiAliasingLayerWidth = 0.0f,
                SegmentsNumber = 1,
                Thickness = 1,
                IsRounded = false,
                DashConfig = new DashesConfiguration
                {
                    DashesEnabled = false,
                    OffsetInSegments = 0,
                    DashLengthInSegments = 1,
                    GapLengthInSegments = 1,
                },
                KnotsConfig = new KnotsConfiguration
                {
                    UseSpline = false,
                    Knots = new []
                    {
                      new LineKnot
                      {
                          Position = new Vector2(-30, 0),
                      },
                      new LineKnot
                      {
                          Position = new Vector2(30,0),
                      },
                    },
                },
                StartEnding = new EndingSettings
                {
                    Angle = 20,
                    Length = 10,
                    Type = EndingType.None,
                    RoundedArrowSmoothness = 20,
                },
                EndEnding = new EndingSettings
                {
                    Angle = 20,
                    Length = 10,
                    Type = EndingType.None,
                    RoundedArrowSmoothness = 20,
                },
            };
        }
#endif // UNITY_EDITOR

        private bool DrawDashedStroke(
            VertexHelper vh, in HullSettings hull, float start, float end, float thickness, float aa,
            bool hasStartEnding, bool hasEndEnding)
        {
            var dash = _settings.DashConfig;
            var dashLength = Math.Max(1L, dash.DashLengthInSegments);
            var gapLength = Math.Max(1L, dash.GapLengthInSegments);
            var period = dashLength + gapLength;
            var segmentsPerSpan = Mathf.Clamp(_settings.SegmentsNumber, 1, 1000);
            var slots = (long)segmentsPerSpan * (_settings.KnotsConfig.Knots.Length - 1);
            var runCount = (slots + period - 1) / period + 1;

            // A joint can require two cross-sections. A span adds two cross-sections and two caps.
            // The remaining allowance covers both arrowheads at their highest supported quality.
            const int maximumEndingVertices = 560;
            var verticesPerSection = hull.IsAaEnabled ? 5 : 3;
            var verticesPerCap = hull.IsRounded ? (hull.IsAaEnabled ? 18 : 9) : (hull.IsAaEnabled ? 2 : 0);
            var requiredVertices = maximumEndingVertices + _path.Count * 2L * verticesPerSection
                + runCount * 2L * (verticesPerSection + verticesPerCap);
            if (requiredVertices >= GraphicMesh.MaximumVertices)
            {
                return false;
            }

            // Serialized dash lengths still count segments; each segment now advances the same
            // distance along the entire shaft, including when crossing a Bézier span boundary.
            var phase = -(long)dash.OffsetInSegments % period;
            if (phase < 0)
            {
                phase += period;
            }

            for (var run = -phase; run < slots; run += period)
            {
                var firstSlot = Math.Max(0L, run);
                var lastSlot = Math.Min(slots, run + dashLength);
                if (firstSlot >= lastSlot)
                {
                    continue;
                }

                var from = Mathf.LerpUnclamped(start, end, firstSlot / (float)slots);
                var to = Mathf.LerpUnclamped(start, end, lastSlot / (float)slots);
                if (to - from > PathEpsilon)
                {
                    DrawStroke(vh, in hull, from, to, thickness, aa,
                        firstSlot > 0 || !hasStartEnding, lastSlot < slots || !hasEndEnding);
                }
            }

            return true;
        }

        private void DrawStroke(
            VertexHelper vh, in HullSettings hull, float start, float end, float thickness, float aa,
            bool capStart, bool capEnd)
        {
            var firstInterval = FindPathEndIndex(start);
            var lastInterval = FindPathEndIndex(end);
            var from = GetPointAtDistance(start, firstInterval);
            var to = GetPointAtDistance(end, lastInterval);
            if (firstInterval + 1 < _path.Count && _path[firstInterval].Distance == start)
            {
                firstInterval++;
            }

            var halfThickness = thickness * 0.5f;
            var halfThicknessAa = halfThickness + aa;
            var firstDirection = (_path[firstInterval].Position - _path[firstInterval - 1].Position).normalized;
            var first = CreateLineVert(from, firstDirection, halfThickness, halfThicknessAa);
            WriteLineVert(in vh, ref first, in hull, default, color);
            var previous = first;

            for (var i = firstInterval; i < lastInterval; i++)
            {
                var point = _path[i].Position;
                var before = (point - _path[i - 1].Position).normalized;
                var after = (_path[i + 1].Position - point).normalized;
                var miter = Vector2.Perpendicular((before + after).normalized);
                var projection = Vector2.Dot(miter, Vector2.Perpendicular(after));
                if (projection >= 1f / MiterLimit)
                {
                    var next = CreateLineVertFromNormal(point, miter / projection, halfThickness, halfThicknessAa);
                    AddStrokeSection(vh, in hull, ref previous, ref next);
                }
                else
                {
                    AddBeveledJoin(vh, in hull, ref previous, point, before, after, halfThickness, halfThicknessAa);
                }
            }

            var lastDirection = (_path[lastInterval].Position - _path[lastInterval - 1].Position).normalized;
            var last = CreateLineVert(to, lastDirection, halfThickness, halfThicknessAa);
            AddStrokeSection(vh, in hull, ref previous, ref last);

            if (capStart)
            {
                DrawStrokeCap(vh, in hull, in first, true, aa, firstDirection);
            }

            if (capEnd)
            {
                DrawStrokeCap(vh, in hull, in last, false, aa, lastDirection);
            }
        }

        private void AddBeveledJoin(
            VertexHelper vh, in HullSettings hull, ref LineVert previous, Vector2 point,
            Vector2 before, Vector2 after, float halfThickness, float halfThicknessAa)
        {
            var incoming = CreateLineVert(point, before, halfThickness, halfThicknessAa);
            var outgoing = CreateLineVert(point, after, halfThickness, halfThicknessAa);
            var turn = before.x * after.y - before.y * after.x;
            if (Mathf.Abs(turn) <= PathEpsilon)
            {
                // A reversing path has no well-defined intersection at the turn.
                AddStrokeSection(vh, in hull, ref previous, ref incoming);
                WriteLineVert(in vh, ref outgoing, in hull, default, color);
                previous = outgoing;
                return;
            }

            if (turn > 0f)
            {
                incoming.LeftPosition = outgoing.LeftPosition = point;
                incoming.LeftPositionAa = outgoing.LeftPositionAa = point;
            }
            else
            {
                incoming.RightPosition = outgoing.RightPosition = point;
                incoming.RightPositionAa = outgoing.RightPositionAa = point;
            }

            AddStrokeSection(vh, in hull, ref previous, ref incoming);
            AddStrokeSection(vh, in hull, ref previous, ref outgoing);
        }

        private void AddStrokeSection(VertexHelper vh, in HullSettings hull, ref LineVert previous, ref LineVert next)
        {
            WriteLineVert(in vh, ref next, in hull, default, color);
            ConnectLineVerts(in vh, in hull, in previous, in next);
            previous = next;
        }

        private void DrawStrokeCap(
            VertexHelper vh, in HullSettings hull, in LineVert section, bool atStart, float aa, Vector2 direction)
        {
            if (hull.IsRounded)
            {
                DrawRoundCap(in section, atStart, in vh, in hull, default, color);
                return;
            }

            if (!hull.IsAaEnabled)
            {
                return;
            }

            var extension = direction * (atStart ? -aa : aa);
            var transparent = color.WithAlpha(0f);
            var left = section.LeftPosition + extension;
            var right = section.RightPosition + extension;
            var leftIndex = AddSimplePoint(in vh, in left, in hull, default, in transparent);
            var rightIndex = AddSimplePoint(in vh, in right, in hull, default, in transparent);
            if (atStart)
            {
                vh.AddTriangle(section.LeftIndex, section.RightIndex, leftIndex);
                vh.AddTriangle(section.RightIndex, rightIndex, leftIndex);
                vh.AddTriangle(section.LeftIndex, leftIndex, section.LeftIndexAa);
                vh.AddTriangle(section.RightIndex, section.RightIndexAa, rightIndex);
            }
            else
            {
                vh.AddTriangle(leftIndex, rightIndex, section.LeftIndex);
                vh.AddTriangle(rightIndex, section.RightIndex, section.LeftIndex);
                vh.AddTriangle(section.LeftIndex, section.LeftIndexAa, leftIndex);
                vh.AddTriangle(section.RightIndex, rightIndex, section.RightIndexAa);
            }
        }

        private static float CalculateEndingOffset(in EndingSettings settings, float thickness)
        {
            var length = settings.Length;
            switch (settings.Type)
            {
                case EndingType.None:
                    return 0;
                case EndingType.Arrow:
                    return CalculateHollowTipOffset(settings, thickness * 0.5f);
                case EndingType.FilledArrow:
                    return length * Mathf.Cos(settings.AngleRadians / 2.0f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(EndingType), settings.Type, "Unexpected ending type.");
            }
        }

        private static float CalculateHollowTipOffset(in EndingSettings ending, float halfThickness)
        {
            return halfThickness / Mathf.Max(1f / MiterLimit, Mathf.Sin(ending.AngleRadians * 0.5f));
        }

        private void DrawEnding(
            in Vector2 point,
            in Vector2 direction,
            in EndingSettings endingSettings,
            in HullSettings hullSettings,
            float aaWidth,
            float thickness,
            in VertexHelper vh)
        {
            var backDirection = -direction;
            var halfAngleRadians = (endingSettings.Angle / 2.0f) * Mathf.Deg2Rad;
            var sin = Mathf.Sin(halfAngleRadians);
            var cos = Mathf.Cos(halfAngleRadians);
            
            Vector2 rotatedDirectionCc = new Vector2(
                cos * backDirection.x - sin * backDirection.y,
                sin * backDirection.x + cos * backDirection.y);
            
            Vector2 rotatedDirectionC = new Vector2(
                cos * backDirection.x + sin * backDirection.y,
                -sin * backDirection.x + cos * backDirection.y);
            
            if (endingSettings.Type == EndingType.FilledArrow)
            {
                DrawFilledArrow(point, direction, endingSettings, hullSettings, aaWidth, vh, rotatedDirectionCc, rotatedDirectionC);
            }
            else if (endingSettings.Type == EndingType.Arrow)
            {
                DrawHollowArrow(point, direction, endingSettings, hullSettings, aaWidth, thickness, vh, rotatedDirectionCc, rotatedDirectionC);
            }
        }

        private void DrawFilledArrow(
            in Vector2 point,
            in Vector2 direction,
            in EndingSettings endingSettings,
            in HullSettings hullSettings,
            float aaWidth,
            in VertexHelper vh,
            in Vector2 rotatedDirectionCc,
            in Vector2 rotatedDirectionC)
        {
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(3, out SpanBuffer<CornerVert> buffer);

            var rightPerpendicular = -Vector2.Perpendicular(rotatedDirectionC);
            var leftPerpendicular = Vector2.Perpendicular(rotatedDirectionCc);

            var leftPoint = point + (rotatedDirectionCc * endingSettings.Length);
            var rightPoint = point + (rotatedDirectionC * endingSettings.Length);
            
            var topVert = new CornerVert { Position = point, PositionAa0 = point + (leftPerpendicular * aaWidth), PositionAa1 = point + (direction * aaWidth), PositionAa2 = point + (rightPerpendicular * aaWidth) };
            var rightVert = new CornerVert { Position = rightPoint, PositionAa0 = rightPoint + (rightPerpendicular * aaWidth), PositionAa1 = rightPoint + (rotatedDirectionC * aaWidth), PositionAa2 = rightPoint - (direction * aaWidth) };
            var leftVert = new CornerVert { Position = leftPoint, PositionAa0 = leftPoint - (direction * aaWidth), PositionAa1 = leftPoint + (rotatedDirectionCc * aaWidth), PositionAa2 = leftPoint + (leftPerpendicular * aaWidth) };

            var vertColor = color;
            
            AddEndingVert(in vh, ref topVert, ref buffer, in hullSettings, default, vertColor);
            AddEndingVert(in vh, ref rightVert, ref buffer, in hullSettings, default, vertColor);
            AddEndingVert(in vh, ref leftVert, ref buffer, in hullSettings, default, vertColor);

            var filledVerts = buffer.FilledBufferSlice();

            vh.AddTriangle(filledVerts[0].Index, filledVerts[1].Index, filledVerts[2].Index);

            if (hullSettings.IsAaEnabled)
            {
                for (int i = 0; i < filledVerts.Length; i++)
                {
                    (CornerVert vert, CornerVert prevVert) = filledVerts.GetCircularConsecutivePair(i);

                    vh.AddTriangle(vert.Index, vert.IndexAa0, vert.IndexAa1);
                    vh.AddTriangle(vert.Index, vert.IndexAa1, vert.IndexAa2);
                    
                    vh.AddTriangle(vert.Index, vert.IndexAa0, prevVert.IndexAa2);
                    vh.AddTriangle(vert.Index, prevVert.Index, prevVert.IndexAa2);
                }
            }
        }
        
        private void DrawHollowArrow(
            in Vector2 point,
            in Vector2 direction,
            in EndingSettings endingSettings,
            in HullSettings hullSettings,
            float aaWidth,
            float thickness,
            in VertexHelper vh,
            in Vector2 rightHandDirection,
            in Vector2 leftHandDirection)
        {
            var bufferSize = hullSettings.IsRounded ? endingSettings.RoundedArrowSmoothness + 4 : 3;
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(bufferSize, out SpanBuffer<LineVert> buffer);

            var leftPoint = point + (leftHandDirection * endingSettings.Length);
            var rightPoint = point + (rightHandDirection * endingSettings.Length);
            
            var halfThickness = thickness / 2.0f;
            var halfThicknessAa = halfThickness + aaWidth;
            
            var leftVert = CreateLineVert(leftPoint, -leftHandDirection, halfThickness, halfThicknessAa);
            var rightVert = CreateLineVert(rightPoint, rightHandDirection, halfThickness, halfThicknessAa);

            var topLen = CalculateHollowTipOffset(in endingSettings, halfThickness);
            var topLenAa = CalculateHollowTipOffset(in endingSettings, halfThicknessAa);
            
            var topVert = CreateLineVert(point, -Vector2.Perpendicular(direction), topLen, topLenAa);

            if (hullSettings.IsRounded)
            {
                using var tempBuffOwner = SpanBufferSharedPool.RentBuffer(3, out SpanBuffer<LineVert> tempBuffer);

                tempBuffer.Add(leftVert);
                tempBuffer.Add(topVert);
                tempBuffer.Add(rightVert);

                var bufferSpan = tempBuffer.FilledBufferSlice();
                
                CreateRoundedAngle(in vh, ref bufferSpan, in hullSettings, ref buffer, thickness, aaWidth, endingSettings.RoundedArrowSmoothness, color);

                leftVert = bufferSpan[0];
                rightVert = bufferSpan[2];
                
                var filledVerts = buffer.FilledBufferSlice();

                var leftWing = filledVerts[..2];
                var rightWing = filledVerts[^2..];

                DrawLineVertTriangles(vh, leftWing, hullSettings);
                DrawLineVertTriangles(vh, rightWing, hullSettings);
                
                vh.AddTriangle(leftWing[1].LeftIndex, rightWing[0].LeftIndex, leftWing[1].RightIndex);
            }
            else
            {
                AddLineVert(in vh, ref leftVert, ref buffer, in hullSettings, default, color);
                AddLineVert(in vh, ref topVert, ref buffer, in hullSettings, default, color);
                AddLineVert(in vh, ref rightVert, ref buffer, in hullSettings, default, color);
                
                var filledVerts = buffer.FilledBufferSlice();

                DrawLineVertTriangles(in vh, in filledVerts, in hullSettings);
            }

            if (hullSettings.IsRounded)
            {
                DrawRoundCap(in leftVert, true, in vh, in hullSettings, default, color);
                DrawRoundCap(in rightVert, false, in vh, in hullSettings, default, color);
            }
        }

        private static void DrawLineVertTriangles(in VertexHelper vh, in Span<LineVert> lineVertices, in HullSettings hullSettings)
        {
            for (int i = 1; i < lineVertices.Length; i++)
            {
                ConnectLineVerts(in vh, in hullSettings, in lineVertices[i - 1], in lineVertices[i]);
            }
        }

        private static void ConnectLineVerts(in VertexHelper vh, in HullSettings hull, in LineVert previous, in LineVert current)
        {
            GraphicMesh.AddStrip(vh, previous.LeftIndex, previous.RightIndex, current.LeftIndex, current.RightIndex);
            if (hull.IsAaEnabled)
            {
                GraphicMesh.AddStrip(vh, previous.LeftIndexAa, previous.LeftIndex, current.LeftIndexAa, current.LeftIndex);
                GraphicMesh.AddStrip(vh, previous.RightIndex, previous.RightIndexAa, current.RightIndex, current.RightIndexAa);
            }
        }

        private void CreateRoundedAngle(
            in VertexHelper vh,
            ref Span<LineVert> basicVerts,
            in HullSettings hullSettings,
            ref SpanBuffer<LineVert> buffer,
            float thickness,
            float aaWidth,
            int roundingSegments,
            in Color vertColor)
        {
            var inDiff = basicVerts[1].Position - basicVerts[0].Position;
            var outDiff = basicVerts[2].Position - basicVerts[1].Position;

            var inDirection = inDiff.normalized;
            var outDirection = outDiff.normalized;

            var halfThickness = thickness / 2.0f;
            var halfThicknessAa = halfThickness + aaWidth;

            var intersectionPoint = basicVerts[1].RightPosition;
            var leftWingEndPosition = intersectionPoint + Vector2.Perpendicular(inDirection) * halfThickness;
            var rightWingStartPosition = intersectionPoint + Vector2.Perpendicular(outDirection) * halfThickness;
            
            var leftWingEndVert = CreateLineVert(leftWingEndPosition, inDirection, halfThickness, halfThicknessAa);
            var rightWingStartVert = CreateLineVert(rightWingStartPosition, outDirection, halfThickness, halfThicknessAa);

            ref var startVert = ref basicVerts[0];
            ref var endVert = ref basicVerts[2];
            
            AddLineVert(vh, ref startVert, ref buffer, hullSettings, default, vertColor);
            AddLineVert(vh, ref leftWingEndVert, ref buffer, hullSettings, default, vertColor);
            AddLineVert(vh, ref rightWingStartVert, ref buffer, hullSettings, default, vertColor);
            AddLineVert(vh, ref endVert, ref buffer, hullSettings, default, vertColor);

            var roundingStartPoint = leftWingEndVert.LeftPosition;
            var roundingStartPointAa = leftWingEndVert.LeftPositionAa;
            var roundingEndPoint = rightWingStartVert.LeftPosition;
            var roundingEndPointAa = rightWingStartVert.LeftPositionAa;
            var controlPoint = basicVerts[1].LeftPosition;
            var controlPointAa = basicVerts[1].LeftPositionAa;
            var centerPoint = (roundingStartPoint + roundingEndPoint) / 2.0f;

            var tIncrement = 1.0f / roundingSegments;
            var vertsCount = roundingSegments - 1;
            var vertsCountIncludingBase = vertsCount + 2;
            
            using var roundingVertsBufferOwner = SpanBufferSharedPool.RentBuffer(vertsCountIncludingBase, out SpanBuffer<Vert> roundingVerts);
            
            roundingVerts.Add(new Vert
            {
                Position = leftWingEndVert.LeftPosition,
                PositionAa = leftWingEndVert.LeftPositionAa,
                
                Index = leftWingEndVert.LeftIndex,
                IndexAa = leftWingEndVert.LeftIndexAa,
            });
            
            for (int i = 1; i < vertsCountIncludingBase - 1; i++)
            {
                var t = i * tIncrement;

                var point = QuadraticBezier(roundingStartPoint, controlPoint, roundingEndPoint, t);
                var pointAa = QuadraticBezier(roundingStartPointAa, controlPointAa, roundingEndPointAa, t);

                var vert = new Vert { Position = point, PositionAa = pointAa, };
                
                AddSimpleVert(vh, ref vert, ref roundingVerts, in hullSettings, default, in vertColor);
            }
            
            roundingVerts.Add(new Vert
            {
                Position = rightWingStartVert.LeftPosition,
                PositionAa = rightWingStartVert.LeftPositionAa,
                
                Index = rightWingStartVert.LeftIndex,
                IndexAa = rightWingStartVert.LeftIndexAa,
            });
            
            var filledRoundingVerts = roundingVerts.FilledBufferSlice();
            var centerIndex = AddSimplePoint(in vh, in centerPoint, in hullSettings, default, in vertColor);
            
            for (int i = 1; i < filledRoundingVerts.Length; i++)
            {
                (Vert vert, Vert prevVert) = filledRoundingVerts.GetCircularConsecutivePair(i);
                
                vh.AddTriangle(centerIndex, prevVert.Index, vert.Index);
                
                if (hullSettings.IsAaEnabled)
                {
                    vh.AddTriangle(vert.Index, prevVert.Index, prevVert.IndexAa);
                    vh.AddTriangle(prevVert.IndexAa, vert.IndexAa, vert.Index);
                }
            }
        }

        private static void DrawRoundCap(in LineVert lineVert, bool isLeftToRight, in VertexHelper vh, in HullSettings hull, in UvRect uvRect, in Color vertColor)
        {
            const int roundingSegments = 10;
            const int additionalPoints = roundingSegments - 1;
            
            var deltaAngleRadians = Mathf.PI / roundingSegments;
            
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(additionalPoints + 2, out SpanBuffer<Vert> buffer);
            
            var diff = isLeftToRight ? lineVert.LeftPosition - lineVert.Position : lineVert.RightPosition - lineVert.Position;
            var initialDirection = diff.normalized;
            var halfThickness = diff.magnitude;
            var halfThicknessAa = isLeftToRight ? (lineVert.LeftPositionAa - lineVert.Position).magnitude : (lineVert.RightPositionAa - lineVert.Position).magnitude;
            
            buffer.Add(new Vert
            {
                Position = isLeftToRight ? lineVert.LeftPosition : lineVert.RightPosition,
                PositionAa = isLeftToRight ? lineVert.LeftPositionAa : lineVert.RightPositionAa,
                Index = isLeftToRight ? lineVert.LeftIndex : lineVert.RightIndex,
                IndexAa = isLeftToRight ? lineVert.LeftIndexAa : lineVert.RightIndexAa,
            });
            
            for (int i = 1; i <= additionalPoints; i++)
            {
                var rotateAngle = deltaAngleRadians * i;

                var sin = Mathf.Sin(rotateAngle);
                var cos = Mathf.Cos(rotateAngle);
                
                float rotatedX = initialDirection.x * cos - initialDirection.y * sin;
                float rotatedY = initialDirection.x * sin + initialDirection.y * cos;

                Vector2 rotatedDirection = new Vector2(rotatedX, rotatedY);

                var vert = new Vert
                {
                    Position = lineVert.Position + (rotatedDirection * halfThickness),
                    PositionAa = lineVert.Position + (rotatedDirection * halfThicknessAa),
                };
                
                AddSimpleVert(in vh, ref vert, ref buffer, in hull, in uvRect, in vertColor);
            }
            
            buffer.Add(new Vert
            {
                Position = !isLeftToRight ? lineVert.LeftPosition : lineVert.RightPosition,
                PositionAa = !isLeftToRight ? lineVert.LeftPositionAa : lineVert.RightPositionAa,
                Index = !isLeftToRight ? lineVert.LeftIndex : lineVert.RightIndex,
                IndexAa = !isLeftToRight ? lineVert.LeftIndexAa : lineVert.RightIndexAa,
            });
            
            var filledVerts = buffer.FilledBufferSlice();

            var centerIndex = lineVert.Index;
            for (int i = 1; i < filledVerts.Length; i++)
            {
                (Vert vert, Vert prevVert) = filledVerts.GetCircularConsecutivePair(i);
                
                vh.AddTriangle(centerIndex, vert.Index, prevVert.Index);
                
                if (hull.IsAaEnabled)
                {
                    vh.AddTriangle(vert.Index, vert.IndexAa, prevVert.IndexAa);
                    vh.AddTriangle(prevVert.Index, vert.Index, prevVert.IndexAa);
                }
            }
        }

        private static LineVert CreateLineVert(Vector2 vertPosition, Vector2 direction, float halfThickness, float halfThicknessAa)
        {
            return CreateLineVertFromNormal(vertPosition, Vector2.Perpendicular(direction), halfThickness, halfThicknessAa);
        }

        private static LineVert CreateLineVertFromNormal(Vector2 vertPosition, Vector2 normal, float halfThickness, float halfThicknessAa)
        {
            return new LineVert
            {
                Position = vertPosition,
                LeftPosition = vertPosition + (normal * halfThickness),
                LeftPositionAa = vertPosition + (normal * halfThicknessAa),
                RightPosition = vertPosition - (normal * halfThickness),
                RightPositionAa = vertPosition - (normal * halfThicknessAa),
            };
        }

        private Vector2 QuadraticBezier(Vector2 pointX, Vector2 controlPoint, Vector2 pointY, float t)
        {
            var xc = Vector2.Lerp(pointX, controlPoint, t);
            var cy = Vector2.Lerp(controlPoint, pointY, t);
            var result = Vector2.Lerp(xc, cy, t);
            
            return result;
        }
        
        private static void AddLineVert(in VertexHelper vh, ref LineVert vertex, ref SpanBuffer<LineVert> verts, in HullSettings hullSettings, in UvRect uvRect, in Color vertColor)
        {
            WriteLineVert(in vh, ref vertex, in hullSettings, in uvRect, in vertColor);
            verts.Add(vertex);
        }

        private static void WriteLineVert(in VertexHelper vh, ref LineVert vertex, in HullSettings hullSettings, in UvRect uvRect, in Color vertColor)
        {
            var vertUv = CalculateUv(in vertex.Position, in hullSettings.EnclosingRect, in uvRect);
            
            vertex.Index = vh.currentVertCount;
            vh.AddVert(vertex.Position, vertColor, vertUv);
            
            vertex.LeftIndex = vh.currentVertCount;
            vh.AddVert(vertex.LeftPosition, vertColor, vertUv);
            
            vertex.RightIndex = vh.currentVertCount;
            vh.AddVert(vertex.RightPosition, vertColor, vertUv);
            
            if (hullSettings.IsAaEnabled)
            {
                vertex.LeftIndexAa = vh.currentVertCount;
                vh.AddVert(vertex.LeftPositionAa, vertColor.WithAlpha(0.0f), vertUv);
            
                vertex.RightIndexAa = vh.currentVertCount;
                vh.AddVert(vertex.RightPositionAa, vertColor.WithAlpha(0.0f), vertUv);
            }
        }
        
        private static void AddEndingVert(in VertexHelper vh, ref CornerVert vertex, ref SpanBuffer<CornerVert> verts, in HullSettings hullSettings, in UvRect uvRect, in Color vertColor)
        {
            var vertUv = CalculateUv(in vertex.Position, in hullSettings.EnclosingRect, in uvRect);
            
            vertex.Index = vh.currentVertCount;
            vh.AddVert(vertex.Position, vertColor, vertUv);
            
            if (hullSettings.IsAaEnabled)
            {
                vertex.IndexAa0 = vh.currentVertCount;
                vh.AddVert(vertex.PositionAa0, vertColor.WithAlpha(0.0f), vertUv);
                
                vertex.IndexAa1 = vh.currentVertCount;
                vh.AddVert(vertex.PositionAa1, vertColor.WithAlpha(0.0f), vertUv);
                
                vertex.IndexAa2 = vh.currentVertCount;
                vh.AddVert(vertex.PositionAa2, vertColor.WithAlpha(0.0f), vertUv);
            }
            
            verts.Add(vertex);
        }
        
        private static void AddSimpleVert(in VertexHelper vh, ref Vert vertex, ref SpanBuffer<Vert> verts, in HullSettings hullSettings, in UvRect uvRect, in Color vertColor)
        {
            var vertUv = CalculateUv(in vertex.Position, in hullSettings.EnclosingRect, in uvRect);
            
            vertex.Index = vh.currentVertCount;
            vh.AddVert(vertex.Position, vertColor, vertUv);
            
            if (hullSettings.IsAaEnabled)
            {
                vertex.IndexAa = vh.currentVertCount;
                vh.AddVert(vertex.PositionAa, vertColor.WithAlpha(0.0f), vertUv);
            }
            
            verts.Add(vertex);
        }
        
        private static int AddSimplePoint(in VertexHelper vh, in Vector2 position, in HullSettings hullSettings, in UvRect uvRect, in Color vertColor)
        {
            var vertUv = CalculateUv(in position, in hullSettings.EnclosingRect, in uvRect);
            
            var index = vh.currentVertCount;
            vh.AddVert(position, vertColor, vertUv);

            return index;
        }
        
        /*private static Color32 CalculateColor(in Vector2 vertexPosition, in ColorCalculationData colorData)
        {
            if (!colorData.ShouldUseGradient)
            {
                return colorData.SingleColor;
            }

            var rect = colorData.EnclosingRect;
            
            switch (colorData.GradientType)
            {
                case GradientType.Horizontal:
                {
                    var t = (vertexPosition.x - rect.xMin) / rect.width;
                    return colorData.GradientColor.Evaluate(t);
                }
                case GradientType.Vertical:
                {
                    var t = (vertexPosition.y - rect.yMin) / rect.height;
                    return colorData.GradientColor.Evaluate(t);
                }
                case GradientType.Radial:
                {
                    var halfWidth = rect.width / 2;
                    var centerPosition = new Vector2(rect.xMin + halfWidth, rect.yMax);
                    
                    if (vertexPosition == centerPosition)
                    {
                        return colorData.GradientColor.Evaluate(0.0f);
                    }
                    else
                    {
                        return colorData.GradientColor.Evaluate(1.0f);
                    }
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(colorData.GradientType), colorData.GradientType, "Unknown gradient type used.");
            }
        }*/

        private static Vector4 CalculateUv(in Vector2 vertexPosition, in Rect enclosingRect, in UvRect spriteUvs)
        {
            var u = (vertexPosition.x - enclosingRect.xMin) / enclosingRect.width;
            var v = (vertexPosition.y - enclosingRect.yMin) / enclosingRect.height;

            if (spriteUvs.IsEmpty)
            {
                return new Vector2(u, v);
            }

            var spriteAdjustedU = Mathf.Lerp(spriteUvs.BottomLeft.x, spriteUvs.TopRight.x, u);
            var spriteAdjustedV = Mathf.Lerp(spriteUvs.BottomLeft.y, spriteUvs.TopRight.y, v);
            
            return new Vector2(spriteAdjustedU, spriteAdjustedV);
        }

#if UNITY_EDITOR
        // If the component changes in the inspector, update the mesh
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty();
        }
#endif
    }
}
