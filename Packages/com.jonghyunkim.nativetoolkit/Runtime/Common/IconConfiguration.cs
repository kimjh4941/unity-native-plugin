#nullable enable

namespace JonghyunKim.NativeToolkit.Runtime.Common
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Configuration for specifying icons in various formats and styles.
    /// Supports system symbols, file paths, named images, app icons, and system images,
    /// along with rendering modes, colors, sizes, weights, and scales.
    /// </summary>
    [Serializable]
    public class IconConfiguration
    {
        public enum IconType
        {
            SystemSymbol,
            FilePath,
            NamedImage,
            AppIcon,
            SystemImage
        }

        public enum RenderingMode
        {
            Monochrome,
            Hierarchical,
            Palette,
            Multicolor
        }

        public enum Weight
        {
            UltraLight,
            Thin,
            Light,
            Regular,
            Medium,
            Semibold,
            Bold,
            Heavy,
            Black
        }

        public enum Scale
        {
            Small,
            Medium,
            Large
        }

        public IconType type;
        public string? value;
        public RenderingMode? mode;
        public List<string> colors;
        public float? size;
        public Weight? weight;
        public Scale? scale;

        public IconConfiguration(
            IconType type,
            string? value = null,
            RenderingMode? mode = null,
            IEnumerable<string>? colors = null,
            float? size = null,
            Weight? weight = null,
            Scale? scale = null)
        {
            this.type = type;
            this.value = value;
            this.mode = mode;
            this.colors = colors != null ? new List<string>(colors) : new List<string>();
            this.size = size;
            this.weight = weight;
            this.scale = scale;
        }

        public override string ToString()
        {
            return $"IconConfiguration(type={type}, value={value}, mode={mode}, colors=[{string.Join(", ", colors)}], size={size}, weight={weight}, scale={scale})";
        }
    }
}
