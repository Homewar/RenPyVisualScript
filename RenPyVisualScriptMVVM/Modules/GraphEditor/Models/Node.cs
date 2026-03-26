using Avalonia.Media;
using System;
using Avalonia.Media.Imaging;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Platform;

namespace RenPyVisualScriptMVVM.Modules.GraphEditor.Models
{
    public class Node
    {
        public static readonly Size DefaultSize = new(210, 96);

        public double X { get; set; }
        public double Y { get; set; }
        public string Title { get; set; } = "";
        public Size Size { get; set; } = DefaultSize;
        public IBrush? Background  { get; set; } = Brushes.LightBlue;

        public static ImageBrush CreatePlaceholderImageBrush()
        {
            return new ImageBrush
            {
                Source = new Bitmap(AssetLoader.Open(new Uri("avares://RenPyVisualScriptMVVM/Assets/GraphEditor/placeholder.jpg"))),
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
        }

        public ImageBrush? ImageBackground { get; set; } = CreatePlaceholderImageBrush();

        public bool IsGeneratedManually { get; set; }
        public string? RouteName { get; set; }
        public string? SourceFilePath { get; set; }
        public int SourceStartLine { get; set; }
        public int SourceEndLine { get; set; }
        public List<string> BodyLines { get; set; } = new();

        public Avalonia.Point Position => new Point(X, Y);

        public Point UpPoint => new Point(X, Y - Size.Height / 2);
        public Point DownPoint => new Point(X, Y + Size.Height / 2);
        public Point LeftPoint => new Point(X - Size.Width / 2, Y);
        public Point RightPoint => new Point(X + Size.Width / 2, Y);
    }

    public class Edge
    {
        public Node Start { get; set; }
        public Node End { get; set; }

        public Edge(Node start, Node end) 
        {
            Start = start;
            End = end;
        }   
    }
}
