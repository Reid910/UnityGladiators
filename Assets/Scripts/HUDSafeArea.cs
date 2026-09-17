using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class HUDSafeArea : MonoBehaviour
{
    private Rect lastArea;
    private Vector2Int lastSize;
    private void Update()
    {
        var size = new Vector2Int(Screen.width, Screen.height);
        if (size.x <= 0 || size.y <= 0 || (lastArea == Screen.safeArea && lastSize == size)) return;
        lastArea = Screen.safeArea;
        lastSize = size;
        var rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(lastArea.xMin / size.x, lastArea.yMin / size.y);
        rect.anchorMax = new Vector2(lastArea.xMax / size.x, lastArea.yMax / size.y);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
