using UnityEngine;

namespace UnityTools
{
    public static class ColorHelpers
    {
        public static Color WithAlpha(this Color initialColor, float alpha)
        {
            var newColor = initialColor;
            newColor.a = alpha;

            return newColor;
        }
        
        public static Color32 WithAlpha(this Color32 initialColor, float alpha)
        {
            var newColor = initialColor;
            newColor.a = (byte)Mathf.RoundToInt(alpha * 255);

            return newColor;
        }
    }
}
