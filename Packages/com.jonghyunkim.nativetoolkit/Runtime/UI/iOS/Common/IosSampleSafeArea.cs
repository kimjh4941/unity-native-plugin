#nullable enable

#if UNITY_IOS || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Keeps the iOS sample screens inside the safe area: pads the UIDocument's root by the parts of the screen
/// outside <see cref="Screen.safeArea"/> (the status bar, the notch or Dynamic Island, the home indicator),
/// and marks the root with <see cref="LandscapeClass"/> while the screen is wider than tall.
/// </summary>
/// <remarks>
/// One component for every iOS sample screen (artifact/ios/sample-layout, sample plan v3, D-1):
/// <see cref="NativeToolkitSampleNavigator.EnsureSafeArea"/> adds it. The navigator swaps the screens by
/// replacing the UIDocument's tree, so the padding, the class and the colour are applied again whenever the
/// root, the safe area, the screen size or the padding colour changes. The padding takes the colour the
/// screen's first element declares in <see cref="InsetColorProperty"/> (the iOS screens declare the colour of
/// their header and list), or else that element's background colour, so the bars read as part of the screen.
/// <para>
/// Each screen's USS changes its header under <see cref="LandscapeClass"/> (D-7): UI Toolkit has no media
/// queries, and in landscape the portrait header would leave the button list less than a button tall.
/// </para>
/// </remarks>
[RequireComponent(typeof(UIDocument))]
public sealed class IosSampleSafeArea : MonoBehaviour
{
    /// <summary>The class on the UIDocument's root while the screen is wider than tall.</summary>
    public const string LandscapeClass = "ios-sample-landscape";

    /// <summary>The USS custom property a screen's first element sets to colour the padding.</summary>
    public const string InsetColorProperty = "--ios-sample-inset-color";

    private const string LogTag = "IosSampleSafeArea";

    private static readonly CustomStyleProperty<Color> s_insetColor = new CustomStyleProperty<Color>(InsetColorProperty);

    private UIDocument? _document;
    private VisualElement? _root;
    private Rect _safeArea;
    private int _width;
    private int _height;
    private Color _color;

    private void Awake()
    {
        Debug.Log($"[{LogTag}][{nameof(Awake)}]");
        _document = GetComponent<UIDocument>();
    }

    private void LateUpdate()
    {
        VisualElement? root = _document != null ? _document.rootVisualElement : null;
        // No panel yet: the tree has not been attached, so there is nothing to measure against.
        if (root == null || root.panel == null)
        {
            return;
        }

        if (root != _root)
        {
            // A new tree: its padding colour has not been set, whatever colour the previous one had.
            _color = Color.clear;
        }

        Rect safeArea = Screen.safeArea;
        int width = Screen.width;
        int height = Screen.height;
        // Read every frame: a screen just swapped in may not have resolved its styles yet, and reads as
        // transparent until it has.
        Color color = PaddingColor(root);
        bool colorChanged = color.a > 0f && color != _color;

        if (root == _root && safeArea == _safeArea && width == _width && height == _height && !colorChanged)
        {
            return;
        }

        _root = root;
        _safeArea = safeArea;
        _width = width;
        _height = height;
        Apply(root, safeArea, width, height);
        if (color.a > 0f)
        {
            _color = color;
            root.style.backgroundColor = color;
        }
    }

    private static void Apply(VisualElement root, Rect safeArea, int width, int height)
    {
        IPanel panel = root.panel;
        // Screen y grows upwards, panel y downwards.
        Vector2 topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safeArea.xMin, height - safeArea.yMax));
        Vector2 bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safeArea.xMax, height - safeArea.yMin));
        Vector2 panelSize = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(width, height));

        float top = Mathf.Max(0f, topLeft.y);
        float left = Mathf.Max(0f, topLeft.x);
        float bottom = Mathf.Max(0f, panelSize.y - bottomRight.y);
        float right = Mathf.Max(0f, panelSize.x - bottomRight.x);
        root.style.paddingTop = top;
        root.style.paddingBottom = bottom;
        root.style.paddingLeft = left;
        root.style.paddingRight = right;
        root.EnableInClassList(LandscapeClass, width > height);

        Debug.Log($"[{LogTag}][{nameof(Apply)}] padding top={top:F0} bottom={bottom:F0} left={left:F0} right={right:F0} landscape={width > height}");
    }

    private static Color PaddingColor(VisualElement root)
    {
        if (root.childCount == 0)
        {
            return Color.clear;
        }

        VisualElement screen = root[0];
        return screen.customStyle.TryGetValue(s_insetColor, out Color declared) ? declared : screen.resolvedStyle.backgroundColor;
    }
}
#endif
