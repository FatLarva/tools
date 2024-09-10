namespace Tools.EditorOnly.UIHelpers
{
    using UnityEditor;
    using UnityEngine;
    
    // From https://discussions.unity.com/t/script-simple-script-that-automatically-adjust-anchor-to-gui-object-size-rect-transform/551154/18
    public static class AnchorSnapper
    {
        [MenuItem("CONTEXT/RectTransform/Anchor Current Position")]
        private static void SnapAnchorAroundObject()
        {
            var oList = Selection.gameObjects;
            for (int i = 0; i < oList.Length; i++)
            {
                var o = oList[i];
                if (o != null && o.GetComponent<RectTransform>() != null)
                {
                    var r = o.GetComponent<RectTransform>();
                    Undo.RecordObject(r, "Set anchors around object");
                    var p = o.transform.parent.GetComponent<RectTransform>();
       
                    var offsetMin = r.offsetMin;
                    var offsetMax = r.offsetMax;
                    var _anchorMin = r.anchorMin;
                    var _anchorMax = r.anchorMax;
       
                    var parent_width = p.rect.width;  
                    var parent_height = p.rect.height;
       
                    var anchorMin = new Vector2(_anchorMin.x + (offsetMin.x / parent_width),
                                                _anchorMin.y + (offsetMin.y / parent_height));
                    var anchorMax = new Vector2(_anchorMax.x + (offsetMax.x / parent_width),
                                                _anchorMax.y + (offsetMax.y / parent_height));
       
                    r.anchorMin = anchorMin;
                    r.anchorMax = anchorMax;
             
                    r.offsetMin = new Vector2(0, 0);
                    r.offsetMax = new Vector2(0, 0);
             
                    r.pivot = new Vector2(0.5f, 0.5f);
                }
            }
        }
        
        [MenuItem("UITools/Anchor Current Position")]
        private static void CustomActionShortcut()
        {
            SnapAnchorAroundObject();
        }
    }
}
