using System;
using Tools.Collections.Spans;
using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/Arrow Graphic", 12)]
    public class ArrowGraphic : MaskableGraphic
    {
        internal struct LineVert
        {
            public Vector2 Position;
            public Vector2 LeftPosition;
            public Vector2 LeftPositionAa;
            public Vector2 RightPosition;
            public Vector2 RightPositionAa;
            public short Index;
            public short LeftIndex;
            public short LeftIndexAa;
            public short RightIndex;
            public short RightIndexAa;
        }
        
        internal struct CornerVert
        {
            public Vector2 Position;
            public Vector2 PositionAa0;
            public Vector2 PositionAa1;
            public Vector2 PositionAa2;
            public short Index;
            public short IndexAa0;
            public short IndexAa1;
            public short IndexAa2;
        }
        
        internal struct Vert
        {
            public Vector2 Position;
            public Vector2 PositionAa;
            public short Index;
            public short IndexAa;
        }
        
        internal readonly ref struct HullSettings
        {
            public readonly bool IsDashed;
            public readonly Rect EnclosingRect;
            public readonly bool IsAaEnabled;
            public readonly bool IsRounded;

            public HullSettings(Rect enclosingRect, bool isAaEnabled, bool isRounded, bool isDashed)
            {
                EnclosingRect = enclosingRect;
                IsAaEnabled = isAaEnabled;
                IsRounded = isRounded;
                IsDashed = isDashed;
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
        
        internal readonly struct EndingsOffsets
        {
            public readonly float StartEndingOffset;
            public readonly float EndEndingOffset;

            public float TotalOffset => StartEndingOffset + EndEndingOffset;
            
            public EndingsOffsets(float startEndingOffset, float endEndingOffset)
            {
                StartEndingOffset = startEndingOffset;
                EndEndingOffset = endEndingOffset;
            }
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
            public MaybeVector2 PrevTangentPoint;
            public MaybeVector2 NextTangentPoint;
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

            public int PointsNumber => SegmentsNumber + 1;
            public float HalfThickness => Thickness / 2.0f;
            public float HalfThicknessAa => HalfThickness + AntiAliasingLayerWidth;
        }

        [SerializeField]
        private Settings _settings;
        
        [SerializeField, HideInInspector]
        private MeshDebugInfo _meshInfo;
        
        [SerializeField, HideInInspector]
        private Rect _lastUsedRect;

        internal ref Settings SettingsAccess => ref _settings;
        internal ref MeshDebugInfo MeshInfoAccess => ref _meshInfo;
        
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            ref var settings = ref _settings;

            if (!settings.KnotsConfig.IsEligible)
            {
                return;
            }
            
            var rect = GetPixelAdjustedRect();
            AdjustPointsToNewDimensions(rect, ref _lastUsedRect, ref settings);
            
            var aaEnabled = settings.AntiAliasingLayerWidth > 0;
            var hullSettings = new HullSettings(rect, aaEnabled, settings.IsRounded, settings.DashConfig.DashesEnabled);
            var knotsCount = settings.KnotsConfig.Knots.Length;
            
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(settings.PointsNumber * (knotsCount - 1), out SpanBuffer<LineVert> buffer);
            FillInVerts(in settings, in hullSettings, in vh, default, ref buffer);

            var filledVerts = buffer.FilledBufferSlice();

            if (!settings.IsEndingsOnly)
            {
                if (settings.DashConfig.DashesEnabled)
                {
                    DrawDashedLineTriangles(vh, filledVerts, settings.DashConfig, hullSettings);
                }
                else
                {
                    DrawNotDashedLineTriangles(vh, filledVerts, in hullSettings);
                }
            }

            DrawBothEndings(vh, settings, filledVerts, hullSettings);
            
            _meshInfo.Set(vh.currentVertCount, vh.currentIndexCount / 3);
        }

        private void AdjustPointsToNewDimensions(Rect newRect, ref Rect lastUsedRect, ref Settings settings)
        {
            if (lastUsedRect == Rect.zero)
            {
                lastUsedRect = newRect;
                return;
            }

            if (lastUsedRect == newRect)
            {
                return;
            }

            ref var knots = ref settings.KnotsConfig.Knots;
            for (int i = 0; i < knots.Length; i++)
            {
                ref var knot = ref knots[i];
                knot.Position = AdjustPoint(knot.Position, lastUsedRect, newRect);
                if (knot.NextTangentPoint.HasValue)
                {
                    knot.NextTangentPoint = AdjustPoint(knot.NextTangentPoint.SureValue, lastUsedRect, newRect);
                }
                
                if (knot.PrevTangentPoint.HasValue)
                {
                    knot.PrevTangentPoint = AdjustPoint(knot.PrevTangentPoint.SureValue, lastUsedRect, newRect);
                }
            }

            lastUsedRect = newRect;
            
            return;

            static Vector2 AdjustPoint(in Vector2 point, in Rect oldRect, in Rect newRect)
            {
                var relativeCoords = new Vector2(point.x / oldRect.width, point.y / oldRect.height);
                var newCoords = new Vector2(relativeCoords.x * newRect.width, relativeCoords.y * newRect.height);

                return newCoords;
            }
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();

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

        private void DrawDashedLineTriangles(
            VertexHelper vh,
            Span<LineVert> filledVerts,
            in DashesConfiguration dashConfig,
            in HullSettings hullSettings)
        {
            var period = dashConfig.DashLengthInSegments + dashConfig.GapLengthInSegments;
            for (int i = 1; i < filledVerts.Length; i++)
            {
                var offsettedI = i - dashConfig.OffsetInSegments - 1;
                if (offsettedI < 0)
                {
                    offsettedI = (offsettedI % period) + period;
                }
                
                var reminder = offsettedI % period;
                if (reminder >= dashConfig.DashLengthInSegments)
                {
                    continue;
                }
                
                (LineVert vert, LineVert prevVert) = filledVerts.GetCircularConsecutivePair(i);

                vh.AddTriangle(vert.LeftIndex, vert.RightIndex, prevVert.LeftIndex);
                vh.AddTriangle(vert.RightIndex, prevVert.RightIndex, prevVert.LeftIndex);

                if (hullSettings.IsAaEnabled)
                {
                    vh.AddTriangle(vert.LeftIndex, vert.LeftIndexAa, prevVert.LeftIndex);
                    vh.AddTriangle(prevVert.LeftIndex, prevVert.LeftIndexAa, vert.LeftIndexAa);

                    vh.AddTriangle(vert.RightIndex, vert.RightIndexAa, prevVert.RightIndex);
                    vh.AddTriangle(prevVert.RightIndex, prevVert.RightIndexAa, vert.RightIndexAa);
                }

                if (hullSettings.IsRounded)
                {
                    var isDashStart = reminder - 1 < 0;
                    var isDashEnd = reminder + 1 >= dashConfig.DashLengthInSegments;

                    if (isDashStart)
                    {
                        DrawRoundCap(in prevVert, true, in vh, in hullSettings, default, color);
                    }

                    if (isDashEnd)
                    {
                        DrawRoundCap(in vert, false, in vh, in hullSettings, default, color);
                    }
                }
            }
        }

        private static void DrawNotDashedLineTriangles(in VertexHelper vh, in Span<LineVert> lineVertices, in HullSettings hullSettings)
        {
            DrawLineVertTriangles(vh, lineVertices, hullSettings);
        }

        private void DrawBothEndings(in VertexHelper vh, in Settings settings, in Span<LineVert> lineVertices, in HullSettings hullSettings)
        {
            var knotsConfig = settings.KnotsConfig;
            var startPoint = knotsConfig.Knots[0].Position;
            var endPoint = knotsConfig.Knots[^1].Position;

            var startDirection = GetDirection(startPoint, lineVertices[1].Position, lineVertices[0].Position);
            var endDirection = GetDirection(endPoint, lineVertices[^1].Position, lineVertices[^2].Position);

            if (settings.StartEnding.HasEnding)
            {
                DrawEnding(in startPoint, in startDirection, in settings.StartEnding, hullSettings, settings.AntiAliasingLayerWidth, settings.Thickness, in vh);
            }
            else if (!settings.IsEndingsOnly && hullSettings.IsRounded && !hullSettings.IsDashed)
            {
                var startVert = lineVertices[0];
                DrawRoundCap(in startVert, true, in vh, in hullSettings, default, color);
            }

            if (settings.EndEnding.HasEnding)
            {
                DrawEnding(in endPoint, in endDirection, in settings.EndEnding, hullSettings, settings.AntiAliasingLayerWidth, settings.Thickness, in vh);
            }
            else if (!settings.IsEndingsOnly && hullSettings.IsRounded && !hullSettings.IsDashed)
            {
                var endVert = lineVertices[^1];
                DrawRoundCap(in endVert, false, in vh, in hullSettings, default, color);
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
                    var halfThickness = thickness / 2.0f;
                    var angleRad = (90 - settings.Angle / 2.0f) * Mathf.Deg2Rad;
                    var cos = Mathf.Cos(angleRad);
                    return halfThickness / cos;
                case EndingType.FilledArrow:
                    return length * Mathf.Cos(settings.AngleRadians / 2.0f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(EndingType), settings.Type, "Unexpected ending type.");
            }
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

            var angleRad = (90 - endingSettings.Angle / 2.0f) * Mathf.Deg2Rad;
            var cos = Mathf.Cos(angleRad);
            var topLen = halfThickness / cos;
            var topLenAa = halfThicknessAa / cos;
            
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
                (LineVert vert, LineVert prevVert) = lineVertices.GetCircularConsecutivePair(i);

                vh.AddTriangle(vert.LeftIndex, vert.RightIndex, prevVert.LeftIndex);
                vh.AddTriangle(vert.RightIndex, prevVert.RightIndex, prevVert.LeftIndex);

                if (hullSettings.IsAaEnabled)
                {
                    vh.AddTriangle(vert.LeftIndex, vert.LeftIndexAa, prevVert.LeftIndex);
                    vh.AddTriangle(prevVert.LeftIndex, prevVert.LeftIndexAa, vert.LeftIndexAa);

                    vh.AddTriangle(vert.RightIndex, vert.RightIndexAa, prevVert.RightIndex);
                    vh.AddTriangle(prevVert.RightIndex, prevVert.RightIndexAa, vert.RightIndexAa);
                }
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

        private Vector2 GetDirection(in Vector2 point0, in Vector2 point1, in Vector2 point2)
        {
            if (point0 == point1)
            {
                var diff = point1 - point2;
                return diff.normalized;
            }
            else
            {
                var diff = point0 - point1;
                return diff.normalized;
            }
        }

        private void FillInVerts(
            in Settings settings,
            in HullSettings hullSettings,
            in VertexHelper vh,
            in UvRect uvRect,
            ref SpanBuffer<LineVert> verts)
        {
            var startEndingHeight = CalculateEndingOffset(in settings.StartEnding, settings.Thickness);
            var endEndingHeight = CalculateEndingOffset(in settings.EndEnding, settings.Thickness);
            var endingsOffsets = new EndingsOffsets(startEndingHeight, endEndingHeight);

            if (settings.KnotsConfig.UseSpline)
            {
                FillInBezierLineVerts(settings, hullSettings, vh, uvRect, endingsOffsets, ref verts);
            }
            else
            {
                FillInLerpLineVerts(settings, hullSettings, vh, uvRect, endingsOffsets, ref verts);
            }
        }
        
        private void FillInBezierLineVerts(
            in Settings settings,
            in HullSettings hullSettings,
            in VertexHelper vh,
            in UvRect uvRect,
            in EndingsOffsets endingOffsets,
            ref SpanBuffer<LineVert> verts)
        {
            var knotsConfig = settings.KnotsConfig;
            ref var startPoint = ref knotsConfig.Knots[0];
            ref var endPoint = ref knotsConfig.Knots[1];
            
            const int precision = 1000;
            using var selfOwnedBuffer = SpanBufferSharedPool.RentBuffer(precision + 1, out SpanBuffer<float> bezierLutBuffer);
            
            FillInBezierLut(in startPoint, in endPoint, precision, ref bezierLutBuffer);
            var lut = bezierLutBuffer.FilledBufferSlice();

            var totalLength = lut[^1];
            var length = totalLength - endingOffsets.TotalOffset;

            var segmentsNumber = settings.SegmentsNumber;
            var segmentLength = length / segmentsNumber;

            for (int i = 0; i < settings.PointsNumber; i++)
            {
                var distance = endingOffsets.StartEndingOffset + (segmentLength * i);
                var t = GetTFromDistance(distance, in lut);
                
                var vertPosition = CubicBezier(startPoint.Position, startPoint.NextTangentPoint.SureValue, endPoint.Position, endPoint.PrevTangentPoint.SureValue, t);
                var direction = CalculateCubicBezierTangent(startPoint.Position, startPoint.NextTangentPoint.SureValue, endPoint.Position, endPoint.PrevTangentPoint.SureValue, t);

                var correctedPosition = vertPosition;
                if (i == 0 || i == settings.PointsNumber - 1)
                {
                    if (i == 0)
                    {
                        correctedPosition = startPoint.Position + (vertPosition - startPoint.Position).normalized * endingOffsets.StartEndingOffset;
                    }
                    else
                    {
                        correctedPosition = endPoint.Position + (vertPosition - endPoint.Position).normalized * endingOffsets.EndEndingOffset;
                    }
                }
               
                var vert = CreateLineVert(correctedPosition, direction, settings.HalfThickness, settings.HalfThicknessAa);
                AddLineVert(in vh, ref vert, ref verts, in hullSettings, in uvRect, color);
            }
        }

        private float GetTFromDistance(float distance, in Span<float> lut)
        {
            if (distance <= 0)
            {
                return 0.0f;
            }
            
            for (int i = 1; i < lut.Length; i++)
            {
                if (lut[i - 1] <= distance && distance <= lut[i])
                {
                    var tForT = distance - lut[i - 1] / lut[i] - lut[i - 1];
                    return Mathf.Lerp((1.0f / lut.Length) * (i - 1), (1.0f / lut.Length) * i, tForT);
                }
            }

            return 1.0f;
        }

        private void FillInBezierLut(in LineKnot startKnot, in LineKnot endKnot, int precision, ref SpanBuffer<float> bezierLutBuffer)
        {
            float step = 1.0f / precision;
            bezierLutBuffer.Add(0.0f);

            var startTangent = startKnot.NextTangentPoint.SureValue;
            var endTangent = endKnot.PrevTangentPoint.SureValue;
            
            Vector2 prevPoint = startKnot.Position;
            float cumulativeLength = 0.0f;
            for (int i = 1; i <= precision; i++)
            {
                var t = step * i;
                var newPoint = CubicBezier(startKnot.Position, startTangent, endKnot.Position, endTangent, t);
                var segmentLength = (newPoint - prevPoint).magnitude;
                cumulativeLength += segmentLength;
                bezierLutBuffer.Add(cumulativeLength);
                
                prevPoint = newPoint;
            }
        }

        private void FillInLerpLineVerts(
            in Settings settings,
            in HullSettings hullSettings,
            in VertexHelper vh,
            in UvRect uvRect,
            in EndingsOffsets endingOffsets,
            ref SpanBuffer<LineVert> verts)
        {
            var knotsConfig = settings.KnotsConfig;
            var knots = knotsConfig.Knots;
            
            for (int knotIndex = 1; knotIndex < knots.Length; knotIndex++)
            {
                var startPoint = knots[knotIndex - 1].Position;
                var endPoint = knots[knotIndex].Position;
            
                var diff = endPoint - startPoint;
                var direction = diff.normalized;
                var length = diff.magnitude - endingOffsets.TotalOffset;

                var segmentsNumber = settings.SegmentsNumber;
                var segmentLength = length / segmentsNumber;

                for (int i = 0; i < settings.PointsNumber; i++)
                {
                    var vertPosition = startPoint + (direction * (endingOffsets.StartEndingOffset + (segmentLength * i)));
                    var vert = CreateLineVert(vertPosition, direction, settings.HalfThickness, settings.HalfThicknessAa);

                    AddLineVert(in vh, ref vert, ref verts, in hullSettings, in uvRect, color);
                }
            }
        }

        private static LineVert CreateLineVert(Vector2 vertPosition, Vector2 direction, float halfThickness, float halfThicknessAa)
        {
            var leftPerpendicular = Vector2.Perpendicular(direction);
            return new LineVert
            {
                Position = vertPosition,
                LeftPosition = vertPosition + (leftPerpendicular * halfThickness),
                LeftPositionAa = vertPosition + (leftPerpendicular * halfThicknessAa),
                RightPosition = vertPosition - (leftPerpendicular * halfThickness),
                RightPositionAa = vertPosition - (leftPerpendicular * halfThicknessAa),
            };
        }

        private Vector2 CubicBezier(Vector2 pointX, Vector2 tangentX, Vector2 pointY, Vector2 tangentY, float t)
        {
            var xtx = Vector2.Lerp(pointX, tangentX, t);
            var txty = Vector2.Lerp(tangentX, tangentY, t);
            var tyy = Vector2.Lerp(tangentY, pointY, t);
            
            var a = Vector2.Lerp(xtx, txty, t);
            var b = Vector2.Lerp(txty, tyy, t);
            
            var c = Vector2.Lerp(a, b, t);
            
            return c;
        }
        
        private Vector2 CalculateCubicBezierTangent(Vector2 pointX, Vector2 tangentX, Vector2 pointY, Vector2 tangentY, float t)
        {
            float oneMinusT = 1 - t;

            var result = 3f * oneMinusT * oneMinusT * (tangentX - pointX)
                       + 6f * t * oneMinusT * (tangentY - tangentX)
                       + 3f * t * t * (pointY - tangentY);

            return result.normalized;
        }
        
        private Vector2 QuadraticBezier(Vector2 pointX, Vector2 controlPoint, Vector2 pointY, float t)
        {
            var xc = Vector2.Lerp(pointX, controlPoint, t);
            var cy = Vector2.Lerp(controlPoint, pointY, t);
            var result = Vector2.Lerp(xc, cy, t);
            
            return result;
        }
        
        private Vector2 CalculateQuadraticBezierTangent(Vector2 pointX, Vector2 controlPoint, Vector2 pointY, float t)
        {
            float oneMinusT = 1 - t;

            var result = 2f * oneMinusT * (controlPoint - pointX)
                              + 2f * t * (pointY - controlPoint);

            return result.normalized;
        }

        /*private UvRect RecalculateSpriteUvsIfNeeded(Sprite sourceSprite, ref Sprite uvRectCalculatedForSprite, ref UvRect lastUvRect)
        {
            if (!lastUvRect.IsEmpty && sourceSprite == uvRectCalculatedForSprite)
            {
                return lastUvRect;
            }
            
            if (sourceSprite == null || sourceSprite.packed == false)
            {
                uvRectCalculatedForSprite = sourceSprite;
                lastUvRect = default;
                
                return lastUvRect;
            }

            if (sprite.packingMode == SpritePackingMode.Tight || sprite.uv.Length > 4)
            {
                var bottomLeft = new Vector2(float.MaxValue, float.MaxValue);
                var topRight = new Vector2(float.MinValue, float.MinValue);

                foreach (var uv in sprite.uv)
                {
                    bottomLeft.x = Mathf.Min(bottomLeft.x, uv.x);
                    bottomLeft.y = Mathf.Min(bottomLeft.y, uv.y);
                    
                    topRight.x = Mathf.Max(topRight.x, uv.x);
                    topRight.y = Mathf.Max(topRight.y, uv.y);
                }
                
                Debug.LogWarning($"Packed sprites with {nameof(SpritePackingMode.Tight)} packing mode may appear wrongly. Consider using unpacked sprite or {nameof(SpritePackingMode.Rectangle)} packing.");

                uvRectCalculatedForSprite = sourceSprite;
                lastUvRect = new UvRect(bottomLeft, topRight);
                
                return lastUvRect;
            }
            else
            {
                var uvs = sprite.uv;
                var topRight = uvs[1];
                var bottomLeft = uvs[2];
            
                uvRectCalculatedForSprite = sourceSprite;
                lastUvRect = new UvRect(bottomLeft, topRight);
                
                return lastUvRect;
            }
        }*/
        
        private static void AddLineVert(in VertexHelper vh, ref LineVert vertex, ref SpanBuffer<LineVert> verts, in HullSettings hullSettings, in UvRect uvRect, in Color vertColor)
        {
            /*var colorData = new ColorCalculationData(hullSettings.EnclosingRect, color, _gradientColor, _gradientType, _shouldUseGradient);
            var vertColor = CalculateColor(in vertex.Position, in colorData);*/
            var vertUv = CalculateUv(in vertex.Position, in hullSettings.EnclosingRect, in uvRect);
            
            vertex.Index = (short)vh.currentVertCount;
            vh.AddVert(vertex.Position, vertColor, vertUv);
            
            vertex.LeftIndex = (short)vh.currentVertCount;
            vh.AddVert(vertex.LeftPosition, vertColor, vertUv);
            
            vertex.RightIndex = (short)vh.currentVertCount;
            vh.AddVert(vertex.RightPosition, vertColor, vertUv);
            
            if (hullSettings.IsAaEnabled)
            {
                vertex.LeftIndexAa = (short)vh.currentVertCount;
                vh.AddVert(vertex.LeftPositionAa, vertColor.WithAlpha(0.0f), vertUv);
            
                vertex.RightIndexAa = (short)vh.currentVertCount;
                vh.AddVert(vertex.RightPositionAa, vertColor.WithAlpha(0.0f), vertUv);
            }
            
            verts.Add(vertex);
        }
        
        private static void AddEndingVert(in VertexHelper vh, ref CornerVert vertex, ref SpanBuffer<CornerVert> verts, in HullSettings hullSettings, in UvRect uvRect, in Color vertColor)
        {
            var vertUv = CalculateUv(in vertex.Position, in hullSettings.EnclosingRect, in uvRect);
            
            vertex.Index = (short)vh.currentVertCount;
            vh.AddVert(vertex.Position, vertColor, vertUv);
            
            if (hullSettings.IsAaEnabled)
            {
                vertex.IndexAa0 = (short)vh.currentVertCount;
                vh.AddVert(vertex.PositionAa0, vertColor.WithAlpha(0.0f), vertUv);
                
                vertex.IndexAa1 = (short)vh.currentVertCount;
                vh.AddVert(vertex.PositionAa1, vertColor.WithAlpha(0.0f), vertUv);
                
                vertex.IndexAa2 = (short)vh.currentVertCount;
                vh.AddVert(vertex.PositionAa2, vertColor.WithAlpha(0.0f), vertUv);
            }
            
            verts.Add(vertex);
        }
        
        private static void AddSimpleVert(in VertexHelper vh, ref Vert vertex, ref SpanBuffer<Vert> verts, in HullSettings hullSettings, in UvRect uvRect, in Color vertColor)
        {
            var vertUv = CalculateUv(in vertex.Position, in hullSettings.EnclosingRect, in uvRect);
            
            vertex.Index = (short)vh.currentVertCount;
            vh.AddVert(vertex.Position, vertColor, vertUv);
            
            if (hullSettings.IsAaEnabled)
            {
                vertex.IndexAa = (short)vh.currentVertCount;
                vh.AddVert(vertex.PositionAa, vertColor.WithAlpha(0.0f), vertUv);
            }
            
            verts.Add(vertex);
        }
        
        private static int AddSimplePoint(in VertexHelper vh, in Vector2 position, in HullSettings hullSettings, in UvRect uvRect, in Color vertColor)
        {
            var vertUv = CalculateUv(in position, in hullSettings.EnclosingRect, in uvRect);
            
            var index = (short)vh.currentVertCount;
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
