using System;
using System.Collections.Generic;
using System.Text;

namespace Funcky.Remarkable.Exporter.Drawer
{
    using System.Linq;

    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.InkML;

    using SkiaSharp;

    public class InkMLGenerator
    {
        private readonly List<Page> pages;

        private readonly List<string> templates;

        private readonly int templateIndex;

        public InkMLGenerator(List<Page> pages, List<string> templates, int templateIndex)
        {
            this.pages = pages;
            this.templates = templates;
            this.templateIndex = templateIndex;
        }

        public List<Ink> GenerateInkML()
        {
            return this.pages.Select(this.InkForPage).ToList();
        }

        private Ink InkForPage(Page page)
        {
            var forceMax = 4096;
            var inkDef = new Definitions(
                new Context(
                    new TraceFormat(
                        new Channel { Name = "X", Type = ChannelDataTypeValues.Integer, Max = 1872, Units = "himetric" },
                        new Channel { Name = "Y", Type = ChannelDataTypeValues.Integer, Max = 1872, Units = "himetric" },
                        new Channel { Name = "F", Type = ChannelDataTypeValues.Integer, Max = forceMax, Units = "dev" })
                    { Id = "inkSrcCoordinatesWithPressure" }
                    ) { Id = "ctxCoordinatesWithPressure" },
                new ChannelProperties(
                    new ChannelProperty { Channel = "X", Name = "resolution", Value = 1, Units = "1/himetric" },
                    new ChannelProperty { Channel = "Y", Name = "resolution", Value = 1, Units = "1/himetric" },
                    new ChannelProperty { Channel = "F", Name = "resolution", Value = 1, Units = "1/dev" }));

            var traceGroup = new TraceGroup();
            var ink = new Ink(inkDef, traceGroup);

            int brushId = 0;
            int traceId = 0;
            var brushes = new Dictionary<(PenTypes, PenColors, int), Brush>();
            foreach (var layer in page.Layers)
            {
                foreach (var stroke in layer.Strokes)
                {
                    // Get the base color
                    var color = new SKColor(0, 0, 0);
                    switch (stroke.PenColor)
                    {
                        case PenColors.Black:
                            color = new SKColor(0, 0, 0);
                            break;
                        case PenColors.Grey:
                            color = new SKColor(69, 69, 69);
                            break;
                        case PenColors.White:
                            color = new SKColor(255, 255, 255);
                            break;
                    }

                    var width = (int)stroke.PenWidth;
                    var opacity = 256;

                    // Manage the "simple" pen type
                    switch (stroke.PenType)
                    {
                        case PenTypes.PenBallpoint:
                        case PenTypes.PenFineLiner:
                            width = 32 * width * width - 116 * width + 107;
                            break;
                        case PenTypes.PenMarker:
                            width = 64 * width - 112;
                            opacity = (int)(0.9f * 256);
                            break;
                        case PenTypes.Highlighter:
                            width = 30;
                            opacity = (int)(0.2f * 256);
                            break;
                        case PenTypes.PencilSharp:
                        case PenTypes.PencilTilt:
                            width = 16 * width - 27;
                            opacity = (int)(0.9f * 256);
                            break;
                        case PenTypes.Eraser:
                            width = 1280 * width * width - 4800 * width + 4510;
                            color = new SKColor(255, 255, 255);
                            break;
                        case PenTypes.EraseArea:
                            // Empty the canvas
                            traceGroup.RemoveAllChildren();
                            continue;
                    }


                    var brushKey = (stroke.PenType, stroke.PenColor, width);
                    if (!brushes.TryGetValue(brushKey, out var brush))
                    {
                        bool hasPressure = stroke.PenType != PenTypes.PenFineLiner
                                           && stroke.PenType != PenTypes.PencilSharp;

                        bool fitToCurve = stroke.PenType == PenTypes.PenFineLiner
                                          || stroke.PenType == PenTypes.PenBallpoint
                                          || stroke.PenType == PenTypes.PenMarker;

                        brush = new Brush(
                                    new BrushProperty { Name = "width", Value = width.ToString() },
                                    new BrushProperty { Name = "height", Value = width.ToString() },
                                    new BrushProperty
                                        {
                                            Name = "color", Value = $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}"
                                        },
                                    new BrushProperty { Name = "transparency", Value = (256 - opacity).ToString() },
                                    new BrushProperty { Name = "tip", Value = "ellipse" },
                                    new BrushProperty { Name = "rasterOp", Value = "copyPen" },
                                    new BrushProperty
                                        {
                                            Name = "ignorePressure",
                                            Value = (!hasPressure).ToString().ToLowerInvariant()
                                        },
                                    new BrushProperty { Name = "antiAliased", Value = "true" },
                                    new BrushProperty
                                        {
                                            Name = "fitToCurve", Value = fitToCurve.ToString().ToLowerInvariant()
                                        }) { Id = "br" + (brushId++) };

                        inkDef.AppendChild(brush);
                        brushes.Add(brushKey, brush);
                    }

                    var coords = string.Join(
                        ", ",
                        stroke.Segments.Select(
                            s => $"{(int)s.HorizontalPosition} {(int)s.VerticalPosition} {(int)(s.Pressure * forceMax)}"));

                    var trace = new Trace(coords)
                                    {
                                        BrushRef = "#" + brush.Id,
                                        Id = $"st{traceId++}",
                                        ContextRef = "#ctxCoordinatesWithPressure"
                    };
                    traceGroup.AppendChild(trace);
                }
            }

            return ink;
        }
    }
}
