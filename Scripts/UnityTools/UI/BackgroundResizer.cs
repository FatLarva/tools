using UnityEngine;
using UnityEngine.UI;

namespace UnityTools.UI
{
    public class BackgroundResizer : MonoBehaviour
    {
        [SerializeField]
        private RectTransform _areaToFill;
        
        [SerializeField]
        private Image _imageToFillInArea;
        
        private Vector2 _fillAreaSize;
        private Vector2 _originalSize;
        private float _originalAspectRatio;

        private void Awake()
        {
            _fillAreaSize = _areaToFill.rect.size;
            var texture = _imageToFillInArea.mainTexture;
            _originalSize = new Vector2(texture.width, texture.height);
            _originalAspectRatio = _originalSize.y / _originalSize.x;
            
            AdjustToFillTheArea(_fillAreaSize);
        }

        private void OnRectTransformDimensionsChange()
        {
            var newScreenSize = _areaToFill.rect.size;

            if (_fillAreaSize != newScreenSize)
            {
                AdjustToFillTheArea(newScreenSize);
                _fillAreaSize = newScreenSize;
            }
        }

        private void AdjustToFillTheArea(Vector2 areaSize)
        {
            if (IsWidthNeedMoreScale(areaSize))
            {
                var newHeight = areaSize.x * _originalAspectRatio;
                var newSize = new Vector2(areaSize.x, newHeight);
                
                _imageToFillInArea.rectTransform.sizeDelta = newSize;
            }
            else
            {
                var newWidth = areaSize.y / _originalAspectRatio;
                var newSize = new Vector2(newWidth, areaSize.y);
                
                _imageToFillInArea.rectTransform.sizeDelta = newSize;
            }
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(_imageToFillInArea.rectTransform);
        }

        private bool IsWidthNeedMoreScale(Vector2 areaSize)
        {
            return areaSize.x / _originalSize.x > areaSize.y / _originalSize.y;
        }
    }
}