using System;
using Tools.Collections.Spans;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UnityTools.UI
{
    public class ConfigurableMask : Graphic
    {
        private static readonly int Stencil = Shader.PropertyToID("_Stencil");
        private static readonly int StencilComp = Shader.PropertyToID("_StencilComp");
        private static readonly int StencilOp = Shader.PropertyToID("_StencilOp");
        private static readonly int ColorMask = Shader.PropertyToID("_ColorMask");
        
        private struct Vert
        {
            public Vector2 Position;
            public short Index;
        }
        
        [SerializeField]
        private CompareFunction _stencilCompareFunction;

        [SerializeField]
        private StencilOp _stencilOp;

        [SerializeField]
        private int _stencilRef;

        private (Material material, bool isSet) _materialContainer;

        public override Material materialForRendering
        {
            get
            {
                if (!_materialContainer.isSet)
                {
                    var materialCopy = new Material(base.materialForRendering);
 
                    materialCopy.SetInt(Stencil, _stencilRef);
                    materialCopy.SetInt(StencilComp, (int)_stencilCompareFunction);
                    materialCopy.SetInt(StencilOp, (int)_stencilOp);
                    materialCopy.SetInt(ColorMask, 0);
                    _materialContainer = (materialCopy, true);
                }
                
                return _materialContainer.material;
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            ApplyStencil();
        }
#endif

        protected override void Start()
        {
            base.Start();
            
            ApplyStencil();
        }
        
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            Rect rect = GetPixelAdjustedRect();
            vh.Clear();

            var smallerSide = Mathf.Min(rect.width, rect.height);
            if (smallerSide <= 0.0f)
            {
                return;
            }
            
            DrawSimpleRect(in vh, in rect);
        }
        
        private void DrawSimpleRect(in VertexHelper vh, in Rect rect)
        {
            Span<Vert> corners = stackalloc Vert[]
            {
                new Vert // Bottom-left
                {
                    Position = new Vector2(rect.xMin, rect.yMin),
                },
                new Vert // Top-left
                {
                    Position = new Vector2(rect.xMin, rect.yMax),
                },
                new Vert // Top-right
                {
                    Position = new Vector2(rect.xMax, rect.yMax),
                },
                new Vert // Bottom-right
                {
                    Position = new Vector2(rect.xMax, rect.yMin),
                },
            };
            
            Span<Vert> verts = stackalloc Vert[4];
            var buffer = new SpanBuffer<Vert>(verts, 0);
            
            for (int i = 0; i < corners.Length; i++)
            {
                ref var cornerPosition = ref corners[i];

                AddHullVert(in vh, ref cornerPosition, in rect, ref buffer);
            }

            vh.AddTriangle(verts[0].Index, verts[1].Index, verts[2].Index);
            vh.AddTriangle(verts[0].Index, verts[2].Index, verts[3].Index);
        }

        private void ApplyStencil()
        {
            if (!_materialContainer.isSet)
            {
                return;
            }
            
            var mat = _materialContainer.material;
            
            mat.SetInt(Stencil, _stencilRef);
            mat.SetInt(StencilComp, (int)_stencilCompareFunction);
            mat.SetInt(StencilOp, (int)_stencilOp);
            mat.SetInt(ColorMask, 0);
        }
        
        private void AddHullVert(in VertexHelper vh, ref Vert vertex, in Rect rect, ref SpanBuffer<Vert> verts)
        {
            var hullVertexUv = CalculateUv(in vertex.Position, in rect);

            var defaultVert = UIVertex.simpleVert;
            
            vertex.Index = (short)vh.currentVertCount;
            vh.AddVert(vertex.Position, Color.white, hullVertexUv, Vector4.zero, defaultVert.normal, defaultVert.tangent);
            
            verts.Add(vertex);
        }
        
        private Vector4 CalculateUv(in Vector2 cornerPosition, in Rect enclosingRect)
        {
            var u = (cornerPosition.x - enclosingRect.xMin) / enclosingRect.width;
            var v = (cornerPosition.y - enclosingRect.yMin) / enclosingRect.height;
            
            return new Vector2(u, v);
        }
    }
}
