using System;
using System.Collections.Generic;
using System.Text;

namespace Funcky.Remarkable.Exporter.Drawer
{
    using System.Linq;

    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.InkML;

    using Funcky.Remarkable.Exporter.Serializer;

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
            var forceMax = 32767;
            var xScale = 32767 / 1872.0;
            var yScale = 32767 / 1872.0;
            var inkDef = new Definitions(
                new Context(
                    new  InkSource(
                    new TraceFormat(
                        new Channel { Name = "X", Type = ChannelDataTypeValues.Integer, Max = 32767, Units = "himetric" },
                        new Channel { Name = "Y", Type = ChannelDataTypeValues.Integer, Max = 32767, Units = "himetric" },
                        new Channel { Name = "F", Type = ChannelDataTypeValues.Integer, Max = forceMax, Units = "dev" }),
                    new ChannelProperties(
                        new ChannelProperty { Channel = "X", Name = "resolution", Value = 1, Units = "1/himetric" },
                        new ChannelProperty { Channel = "Y", Name = "resolution", Value = 1, Units = "1/himetric" },
                        new ChannelProperty { Channel = "F", Name = "resolution", Value = 1, Units = "1/dev" }))
                                    { Id = "inkSrcCoordinatesWithPressure" }
                    ) { Id = "ctxCoordinatesWithPressure" });

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

                    var rasterOp = "copyPen";
                    var minPressure = 0f;
                    var minWidth = 0f;
                    var maxWidth = 0f;
                    var widthScaleFactor = 1f;
                    var transparency = 0f;
                    bool hasPressure = true;

                    // Manage the "simple" pen type
                    switch (stroke.PenType)
                    {
                        case PenTypes.PenBallpoint:
                            minPressure = 0.5f;
                            maxWidth = 4.44444f;
                            // Ballpoint pen starts at 0.29629 at 0.5f and gets larger
                            // but doesn't go below
                            minWidth = maxWidth - (maxWidth - 2.9629f)*2;
                            break;

                        case PenTypes.PenFineLiner:
                            minWidth = maxWidth = 4;
                            hasPressure = false;
                            break;

                        case PenTypes.PenMarker:
                            minWidth = 5;
                            maxWidth = 14.1f;
                            break;

                        case PenTypes.Brush:
                            minWidth = 4;
                            maxWidth = 25;
                            widthScaleFactor = 0.9f;
                            break;

                        case PenTypes.Highlighter:
                            rasterOp = "maskPen";
                            hasPressure = false;
                            transparency = 0.5f;
                            minWidth = maxWidth = 60;
                            color = new SKColor(255, 255, 0);
                            break;

                        case PenTypes.PencilSharp:
                            hasPressure = false;
                            minWidth = maxWidth = 4.4444f;
                            color = new SKColor(96, 96, 96);
                            break;

                        case PenTypes.PencilTilt:
                            minWidth = 2.3f;
                            maxWidth = 40;
                            color = new SKColor(32, 32, 32);
                            break;

                        case PenTypes.Eraser:
                            hasPressure = false;
                            minWidth = maxWidth = 30;
                            color = new SKColor(255, 255, 255);
                            break;

                        case PenTypes.EraseArea:
                            // Empty the canvas
                            traceGroup.RemoveAllChildren();
                            continue;
                    }

                    minWidth *= (stroke.PenWidth / 2f);
                    maxWidth *= (stroke.PenWidth / 2f);
                    var oneNoteBrushWidth = 0.51f * maxWidth;

                    var brushKey = (stroke.PenType, stroke.PenColor, (int)(stroke.PenWidth));
                    if (!brushes.TryGetValue(brushKey, out var brush))
                    {
                        bool fitToCurve = stroke.PenType == PenTypes.PenFineLiner
                                          || stroke.PenType == PenTypes.PenBallpoint
                                          || stroke.PenType == PenTypes.PenMarker;

                        // Need to map the scale of  MinWidth - MaxWidth in pixels 
                        // to:            0 - 32767, where 0 if 0% of brush width
                        //                           and 32767 is 200% of brush width.
                        //
                        brush = new Brush(
                                    new BrushProperty { Name = "width", Value = (oneNoteBrushWidth * widthScaleFactor * xScale).ToString(), Units = "himetric" },
                                    new BrushProperty { Name = "height", Value = (oneNoteBrushWidth * widthScaleFactor * yScale).ToString(), Units = "himetric" },
                                    new BrushProperty
                                        {
                                            Name = "color", Value = $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}"
                                        },
                                    new BrushProperty { Name = "transparency", Value = $"{transparency}" },
                                    new BrushProperty { Name = "tip", Value = "ellipse" },
                                    new BrushProperty { Name = "rasterOp", Value = rasterOp },
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
                            s =>
                                {
                                    float effectivePressure;
                                    if (hasPressure && stroke.PenType != PenTypes.PenBallpoint)
                                    {
                                        // Calculate and effective pressure so that the brush width is correct in onenote.
                                        effectivePressure = (s.Width / oneNoteBrushWidth);
                                        if (effectivePressure > 2)
                                        {
                                            effectivePressure = 2;
                                        }
                                    }
                                    else
                                    {
                                        // Not using it for width, so send through original pressure as useful for
                                        // ink-to-text
                                        effectivePressure = s.Pressure;
                                    }

                                    return
                                        $"{(int)(s.HorizontalPosition * xScale)} {(int)(s.VerticalPosition * yScale)} {(int)(effectivePressure * forceMax)}";
                                }));

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
