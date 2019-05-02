// -----------------------------------------------------------------------
//  <copyright file="LinesDrawer.cs" company="Prism">
//  Copyright (c) Prism. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

namespace Funcky.Remarkable.Exporter.Drawer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using BinarySerialization;

    using Funcky.Remarkable.Exporter.Serializer;

    using NLog;

    public class LinesParserMultiVersion
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private readonly string fileName;

        private readonly byte[] content;

        public LinesParserMultiVersion(byte[] content, string fileName)
        {
            this.content = content;
            this.fileName = fileName;
        }

        public List<Page> Parse()
        {
            Logger.Info($"Start Parsing {this.fileName}");

            var serializer = new BinarySerializer();
            var file = serializer.Deserialize<ReMarkableFile>(this.content);

            List<Segment> PointsToSegments(FileLine fileLine)
            {
                return (from point in fileLine.Points
                        select new Segment
                                   {
                                       HorizontalPosition = point.X,
                                       VerticalPosition = point.Y,
                                       Pressure = point.Pressure,
                                       Speed = (point as FilePointV3)?.Speed ?? ((FilePointV2)point).RotationToX,
                                       Tilt = (point as FilePointV3)?.Direction ?? ((FilePointV2)point).RotationToY
                                   }).ToList();
            }

            List<Stroke> LinesToStrokes(FileLayer fileLayer)
            {
                return (from line in fileLayer.Lines
                        select new Stroke
                                   {
                                       PenColor = (PenColors)line.Color,
                                       PenType = (PenTypes)line.BrushType,
                                       PenWidth =
                                           line.BaseBrushSize,
                                       Segments =
                                           PointsToSegments(line)
                                   }).ToList();
            }

            var pages = (from p in file.Pages
                         select new Page
                                    {
                                        Layers = (from layer in p.Layers
                                                  select new Layer { Strokes = LinesToStrokes(layer) }).ToList()
                                    }).ToList();

            var segmentCount = pages.SelectMany(p => p.Layers.SelectMany(l => l.Strokes.SelectMany(s => s.Segments)))
                .Count();

            var strokesCount = pages.SelectMany(p => p.Layers.SelectMany(l => l.Strokes)).Count();
            var layerCount = pages.SelectMany(p => p.Layers).Count();

            Logger.Info($"Pages: {pages.Count}, Layers: {layerCount}, Strokes: {strokesCount}, Segments: {segmentCount}");
            return pages;
        }

        private float GetFloat(Queue<byte> workingData)
        {
            var data = this.Read(workingData, 4);
            return BitConverter.ToSingle(data, 0);
        }

        private int GetInteger(Queue<byte> workingData)
        {
            var data = this.Read(workingData, 4);
            return BitConverter.ToInt32(data, 0);
        }

        private byte[] Read(Queue<byte> workingData, int amount)
        {
            var data = new byte[amount];

            for (var i = 0; i < amount; i++)
            {
                data[i] = workingData.Dequeue();
            }

            return data;
        }

        private void Skip(Queue<byte> workingData, int amount)
        {
            for (var i = 0; i < amount; i++)
            {
                workingData.Dequeue();
            }
        }
    }
}