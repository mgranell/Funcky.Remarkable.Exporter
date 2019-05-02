// -----------------------------------------------------------------------
//  <copyright file="Stroke.cs" company="Prism">
//  Copyright (c) Prism. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------

namespace Funcky.Remarkable.Exporter.Drawer
{
    using System.Collections.Generic;

    public class Stroke
    {
        public PenTypes PenType { get; set; }

        public PenColors PenColor { get; set; }

        public int Padding { get; set; }

        public float PenWidth { get; set; }

        public List<Segment> Segments { get; set; } = new List<Segment>();
    }

    public enum PenColors
    {
        Black = 0,
        Grey = 1,
        White = 2
    }

    public enum PenTypes
    {
        PenBallpoint = 2,
        PenMarker = 3,
        PenFineLiner = 4,
        PencilSharp = 7,
        PencilTilt = 1,
        Brush = 0,
        Highlighter = 5,
        Eraser = 6,
        EraseArea = 8,
        EraseAll = 9,
        SelectionBrush1 = 10,
        SelectionBrush2 = 11,
        FineLine1 = 12,
        FineLine2 = 13,
        FineLine3 = 14,
    }
}