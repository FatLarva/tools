using System;
using UnityEngine;

namespace UnityTools.UI
{
    public static class ScreenHelper
    {
        public static bool IsSquarishScreen()
        {
            var screenSize = new Vector2(Screen.width, Screen.height);
            var longerSide = Math.Max(screenSize.x, screenSize.y);
            var shorterSide = Math.Min(screenSize.x, screenSize.y);
            
            var screenAspect = longerSide / (float)shorterSide;
            var result = screenAspect < 1.6f;

            return result;
        }
    }
}