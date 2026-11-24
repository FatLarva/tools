using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UnityTools.GraphicPrimitives
{
    public abstract class PrimitiveImage : Image
    {
        [SerializeField] private bool _overrideStencil;
        [SerializeField] private CompareFunction _stencilComp = CompareFunction.Always;
        [SerializeField, Range(0, 255)] private int _stencilRef;
        [SerializeField] private StencilOp _stencilOp = StencilOp.Keep;
        [SerializeField] private bool _overrideStencilOperation;
        [SerializeField] private bool _hideMaskGraphics;
        [SerializeField] private bool _overrideStencilMasks;
        [SerializeField, Range(0, 255)] private int _stencilReadMask = 255;
        [SerializeField, Range(0, 255)] private int _stencilWriteMask = 255;

        private readonly StencilMaterialOverride _stencil = new();

        public override Material materialForRendering
        {
            get
            {
                return _stencil.Resolve(base.materialForRendering, isActiveAndEnabled && _overrideStencil,
                    _stencilRef, _stencilComp, _stencilOp, _overrideStencilOperation || _stencilOp != StencilOp.Keep,
                    _hideMaskGraphics, _overrideStencilMasks, _stencilReadMask, _stencilWriteMask);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _stencil.Release();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _stencil.Release();
        }
    }
}
