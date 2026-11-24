using UnityEngine;
using UnityEngine.Rendering;

namespace UnityTools.GraphicPrimitives
{
    internal sealed class StencilMaterialOverride
    {
        private static readonly int Stencil = Shader.PropertyToID("_Stencil");
        private static readonly int Comparison = Shader.PropertyToID("_StencilComp");
        private static readonly int Operation = Shader.PropertyToID("_StencilOp");
        private static readonly int ColorMask = Shader.PropertyToID("_ColorMask");
        private static readonly int ReadMask = Shader.PropertyToID("_StencilReadMask");
        private static readonly int WriteMask = Shader.PropertyToID("_StencilWriteMask");
        private static readonly int AlphaClip = Shader.PropertyToID("_UseUIAlphaClip");

        private Material _material;

        internal Material Resolve(Material source, bool enabled, int reference, CompareFunction comparison,
            StencilOp operation, bool overrideOperation, bool hideGraphics, bool overrideMasks, int readMask, int writeMask)
        {
            if (!enabled || source == null || !source.HasProperty(Stencil) || !source.HasProperty(Comparison))
            {
                Release();
                return source;
            }

            if (_material == null || _material.shader != source.shader)
            {
                Release();
                _material = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            }
            else
            {
                _material.CopyPropertiesFromMaterial(source);
            }

            _material.SetInt(Stencil, Mathf.Clamp(reference, 0, 255));
            _material.SetInt(Comparison, (int)comparison);
            if (overrideOperation && _material.HasProperty(Operation))
            {
                _material.SetInt(Operation, (int)operation);
            }

            if (hideGraphics && _material.HasProperty(ColorMask))
            {
                _material.SetInt(ColorMask, 0);
            }

            if (overrideMasks && _material.HasProperty(ReadMask) && _material.HasProperty(WriteMask))
            {
                _material.SetInt(ReadMask, Mathf.Clamp(readMask, 0, 255));
                _material.SetInt(WriteMask, Mathf.Clamp(writeMask, 0, 255));
            }

            if (overrideOperation && operation != StencilOp.Keep && _material.HasProperty(AlphaClip) &&
                (!_material.HasProperty(WriteMask) || _material.GetInt(WriteMask) != 0))
            {
                _material.SetFloat(AlphaClip, 1f);
                _material.EnableKeyword("UNITY_UI_ALPHACLIP");
            }

            return _material;
        }

        internal void Release()
        {
            if (_material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(_material);
            }
            else
            {
                Object.DestroyImmediate(_material);
            }

            _material = null;
        }
    }
}
