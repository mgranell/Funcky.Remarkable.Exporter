// -----------------------------------------------------------------------
//  <copyright file="LinesDrawer.cs" company="Prism">
//  Copyright (c) Prism. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

namespace Funcky.Remarkable.Exporter.Drawer
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;

    using NLog;

    using SkiaSharp;

    public class LinesDrawer
    {
        private const int CanvanWidth = 1404;

        private const int CanvasHeight = 1872;
        
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly List<Page> pages;

        private readonly List<string> templates;

        private readonly int startingPage;

        private string TemplateRoot => ConfigurationService.AppSettings["TemplateRoot"];

        public LinesDrawer(List<Page> pages, List<string> templates, int startingPage)
        {
            this.pages = pages;
            this.templates = templates;
            this.startingPage = startingPage;
        }

        public List<byte[]> Draw()
        {
            Logger.Info("Start drawing");

            var images = new List<byte[]>();

            var currentPage = this.startingPage;
            foreach (var page in this.pages)
            {
                var template = string.Empty;

                if (currentPage < this.templates.Count)
                {
                    template = this.templates[currentPage];
                }
                
                currentPage++;
                Logger.Debug($"Drawing page {currentPage} on  {this.pages.Count}");

                using (var bitmap = new SKBitmap(CanvanWidth, CanvasHeight))
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.DrawRect(0, 0, CanvanWidth, CanvasHeight, new SKPaint { Color = new SKColor(255, 255, 255) });
                    
                    if (!string.IsNullOrWhiteSpace(template))
                    {
                        var templateBitmap = SKBitmap.Decode($"{this.TemplateRoot}{template}.png");
                        canvas.DrawBitmap(templateBitmap, 0, 0);
                    }

                    var currentLayer = 0;
                    foreach (var layer in page.Layers)
                    {
                        currentLayer++;
                        Logger.Debug($"Drawing layer {currentLayer} on  {page.Layers.Count}");

                        var currentStroke = 0;
                        foreach (var stroke in layer.Strokes)
                        {
                            currentStroke++;
                            Logger.Debug($"Drawing stroke {currentStroke} on  {layer.Strokes.Count}");

                            for (var currentSegment = 0; currentSegment < stroke.Segments.Count; currentSegment++)
                            {
                                Logger.Debug($"Drawing segment {currentSegment} on  {stroke.Segments.Count}");

                                if (currentSegment < stroke.Segments.Count - 1)
                                {
                                    this.DrawSegment(stroke, stroke.Segments[currentSegment], stroke.Segments[currentSegment + 1], canvas);
                                }
                            }
                        }
                    }

                    canvas.Flush();

                    var image = SKImage.FromBitmap(bitmap);
                    var data = image.Encode(SKEncodedImageFormat.Png, 100);

                    using (var stream = new MemoryStream())
                    {
                        data.SaveTo(stream);
                        images.Add(data.ToArray());
                    }
                }
            }

            return images;
        }

        private void DrawSegment(Stroke stroke, Segment start, Segment end, SKCanvas canvas)
        {
            var paint = this.GetPaint(stroke, start);
            if (paint == null)
            {
                canvas.DrawRect(0, 0, CanvanWidth, CanvasHeight, new SKPaint { Color = new SKColor(255, 255, 255) });
                return;
            }
            
            canvas.DrawLine(start.HorizontalPosition, start.VerticalPosition, end.HorizontalPosition, end.VerticalPosition, paint);
        }

        private SKPaint GetPaint(Stroke stroke, Segment segment)
        {
            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment));
            }
            
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

            var width = stroke.PenWidth;
            var opacity = 1f;

            // Manage the "simple" pen type
            switch (stroke.PenType)
            {
                case PenTypes.PenBallpoint:
                case PenTypes.PenFineLiner:
                    width = 32 * width * width - 116 * width + 107;
                    break;
                case PenTypes.PenMarker:
                    width = 64 * width - 112;
                    opacity = 0.9f;
                    break;
                case PenTypes.Highlighter:
                    width = 30;
                    opacity = 0.2f;
                    break;
                case PenTypes.Eraser:
                    width = 1280 * width * width - 4800 * width + 4510;
                    color = new SKColor(255, 255, 255);
                    break;
                case PenTypes.PencilSharp:
                    width = 16 * width - 27;
                    opacity = 0.9f;
                    break;
                case PenTypes.EraseArea:
                    // Empty the canvas
                    return null;
            }
            
            // Manage the pressure / tilt sensitive pens
            switch (stroke.PenType)
            {
                    case PenTypes.Brush:
                        width = (5 * segment.Tilt) * (6 * width - 10) * (1 + 2 * segment.Pressure * segment.Pressure * segment.Pressure);
                        break;
                    case PenTypes.PencilTilt:
                        width = (10 * segment.Tilt - 2) * (8 * width - 14);
                        opacity = (segment.Pressure - 0.2f) * (segment.Pressure - 0.2f);
                        break;
            }

            opacity = Math.Min(opacity, 1);
            opacity = Math.Max(opacity, 0);

            // Create the paint
            return new SKPaint
                       {
                           Color = color.WithAlpha(Convert.ToByte(255 * opacity)),
                           StrokeWidth = width
                       };
        }
    }
}