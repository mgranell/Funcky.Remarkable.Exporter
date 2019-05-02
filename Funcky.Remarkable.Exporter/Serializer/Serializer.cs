using System;
using System.Collections.Generic;
using System.Text;

namespace Funcky.Remarkable.Exporter.Serializer
{
    using BinarySerialization;

    using Funcky.Remarkable.Exporter.Drawer;

    public enum ReMarkableFileVersion
    {
        [SerializeAsEnum("reMarkable lines with selections and layers")]
        V2,
        [SerializeAsEnum("reMarkable .lines file, version=3          ")]
        V3
    }

    public class ReMarkableFile
    {
        [FieldOrder(0)]
        [FieldLength(43)]
        [FieldEncoding("ascii")]
        public ReMarkableFileVersion Version { get; set; }

        [FieldOrder(1)]
        [SerializeWhen(nameof(Version), ReMarkableFileVersion.V2)]
        public int PageCount { get; set; } = 1;

        [FieldOrder(2)]
        [FieldCount("PageCount")]
        public List<FilePage> Pages { get; set; }
    }

    public class FilePage
    {
        [FieldOrder(0)] public int LayerCount { get; set; }

        [FieldOrder(1)]
        [FieldCount(nameof(LayerCount))]
        public List<FileLayer> Layers { get; set; }
    }

    public class FileLayer
    {
        [FieldOrder(0)] public int LineCount { get; set; }

        [FieldOrder(1)]
        [FieldCount(nameof(LineCount))]
        public List<FileLine> Lines { get; set; }
    }

    public class FileLine
    {
        [FieldOrder(0)] public int BrushType { get; set; }
        [FieldOrder(1)] public int Color { get; set; }
        [FieldOrder(2)] public int Padding { get; set; }
        [FieldOrder(3)] public float BaseBrushSize { get; set; }
        [FieldOrder(4)] public int PointCount { get; set; }

        [FieldOrder(5)]
        [ItemSubtype(nameof(ReMarkableFile.Version), ReMarkableFileVersion.V2, typeof(FilePointV2), AncestorType = typeof(ReMarkableFile), RelativeSourceMode = RelativeSourceMode.FindAncestor)]
        [ItemSubtype(nameof(ReMarkableFile.Version), ReMarkableFileVersion.V3, typeof(FilePointV3), AncestorType = typeof(ReMarkableFile), RelativeSourceMode = RelativeSourceMode.FindAncestor)]
        [FieldCount(nameof(PointCount))]
        public List<IFilePoint> Points { get; set; } = new List<IFilePoint>();
    }

    public interface IFilePoint
    {
        [Ignore]
        float X { get; set; }

        [Ignore]
        float Y { get; set; }

        [Ignore]
        float Pressure { get; set; }
    }

    public class FilePointV2 : IFilePoint
    {
        [FieldOrder(0)] public float X { get; set; }
        [FieldOrder(1)] public float Y { get; set; }
        [FieldOrder(2)] public float Pressure { get; set; }
        [FieldOrder(3)] public float RotationToX { get; set; }
        [FieldOrder(4)] public float RotationToY { get; set; }
    }

    public class FilePointV3 : IFilePoint
    {
        [FieldOrder(0)] public float X { get; set; }
        [FieldOrder(1)] public float Y { get; set; }
        [FieldOrder(2)] public float Speed { get; set; }
        [FieldOrder(3)] public float Direction { get; set; }
        [FieldOrder(4)] public float Width { get; set; }
        [FieldOrder(5)] public float Pressure { get; set; }
    }
}
