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
        public double X { get; set; }
        public double Y { get; set; }
        public string Title { get; set; } = "";
        public Size Size { get; set; } = new Size(192, 108);
        public IBrush? Background  { get; set; } = Brushes.LightBlue;

        public ImageBrush? ImageBackground { get; set; } = new ImageBrush // Изменено: значение по умолчанию с заглушкой
        {
            
            Source = new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri("avares://RenPyVisualScriptMVVM/Assets/GraphEditor/placeholder.jpg"))),
            Stretch = Stretch.UniformToFill, // Ресайз под узел с заполнением (может обрезать)
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };

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
