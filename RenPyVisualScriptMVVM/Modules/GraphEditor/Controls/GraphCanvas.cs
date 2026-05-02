using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using RenPyVisualScriptMVVM.Modules.Editors.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Editors.ViewModels;
using RenPyVisualScriptMVVM.Modules.Editors.Views;
using RenPyVisualScriptMVVM.Modules.GraphEditor.Models;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RenPyVisualScriptMVVM.Modules.GraphEditor.Controls
{
    public class GraphCanvas : Canvas
    {
        public List<Node> Nodes { get; } = new();
        public List<Edge> Edges { get; } = new();
        public List<StoryRoute> Routes { get; } = new();
        public List<GraphNote> Notes { get; } = new();
        public event EventHandler? GraphChanged;

        private Node? _draggingNode;
        private Point _dragOffset;
        private GraphNote? _draggingNote;
        private Point _noteDragOffset;
        private bool _isPanning;
        private Point _panStartPoint;
        private Point _panStartOffset;
        private Node? _lineStartNode;
        private ConnectorPosition? _lineStartConnector;
        private Path? _tempLine;
        private Node? _selectedNode;
        private GraphNote? _selectedNote;
        private Node? _renamingNode;
        private TextBox? _renameTextBox;
        private TextBlock? _renameValidationTextBlock;
        private string _renameText = string.Empty;
        private string _renameValidationMessage = string.Empty;
        private GraphNote? _editingNote;
        private TextBox? _noteTextBox;
        private string _noteEditText = string.Empty;
        private Edge? _selectedEdge;
        private readonly HashSet<Node> _selectedNodes = new();
        private readonly HashSet<Node> _loopNodesWithEndBranch = new();
        private ContextMenu? _activeContextMenu;
        private Point _viewportOffset = new(0, 0);
        private double _viewportScale = 1.0;
        private Bitmap? _tipsBitmap;
        private Rect _viewportScreenRect;
        private Node? _primaryRootNode;
        private HashSet<Node> _reachableNodes = new();
        private HashSet<Node> _nodesWithSelfLoop = new();
        private HashSet<Node> _nodesWithOutgoingEdges = new();
        private Dictionary<Node, int> _nodeIndexMap = new();
        private bool _isViewportNavigating;
        private readonly DispatcherTimer _viewportNavigationTimer;
        private readonly DispatcherTimer _viewportRenderTimer;
        private bool _viewportRebuildPending;
        private string? _highlightedRouteName;
        private HashSet<string> _highlightedRouteNodeTitles = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<Edge, (ConnectorPosition start, ConnectorPosition end)> _edgeConnectorMap = new();
        private static readonly Regex ValidLabelRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        public string? ActiveRouteName { get; set; }

        public Node? SelectedNode
        {
            get => _selectedNode;
            set
            {
                _selectedNode = value;
                _selectedNodes.Clear();
                if (value != null)
                {
                    _selectedNodes.Add(value);
                }
                if (value != null)
                {
                    _selectedNote = null;
                    _selectedEdge = null;
                }
                RebuildChildren();
            }
        }

        public Edge? SelectedEdge
        {
            get => _selectedEdge;
            set
            {
                _selectedEdge = value;
                if (value != null)
                {
                    _selectedNode = null;
                    _selectedNote = null;
                }
                RebuildChildren();
            }
        }

        public GraphCanvas()
        {
            Background = new SolidColorBrush(Color.Parse("#121314"));
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerWheelChanged += OnPointerWheelChanged;
            Focusable = true;
            KeyDown += OnKeyDown;

            _viewportNavigationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _viewportNavigationTimer.Tick += (_, _) =>
            {
                _viewportNavigationTimer.Stop();
                _isViewportNavigating = false;
                _viewportRebuildPending = false;
                RebuildChildren();
            };

            _viewportRenderTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _viewportRenderTimer.Tick += (_, _) =>
            {
                if (!_viewportRebuildPending)
                {
                    if (!_isViewportNavigating)
                    {
                        _viewportRenderTimer.Stop();
                    }

                    return;
                }

                _viewportRebuildPending = false;
                RebuildChildren();
            };
        }


        private void EnsureEdgeConnectorMappings()
        {
            foreach (var edge in Edges)
            {
                if (!_edgeConnectorMap.ContainsKey(edge))
                {
                    _edgeConnectorMap[edge] = (ConnectorPosition.Right, ConnectorPosition.Left);
                }
            }
        }

        public void RebuildChildren()
        {
            EnsureEdgeConnectorMappings();
            RefreshRenderState();
            Children.Clear();

            DrawBackgroundImage();
            DrawEdges();
            DrawTempLine();
            DrawNotes();
            DrawNodes();
        }

        public void FocusWorldPoint(Point worldPoint)
        {
            var viewportWidth = Bounds.Width > 0 ? Bounds.Width : 1280;
            var viewportHeight = Bounds.Height > 0 ? Bounds.Height : 720;

            _viewportOffset = new Point(
                viewportWidth / 2 - worldPoint.X * _viewportScale,
                viewportHeight / 2 - worldPoint.Y * _viewportScale);

            RebuildChildren();
        }

        public void ApplyViewport(double offsetX, double offsetY, double scale)
        {
            _viewportOffset = new Point(offsetX, offsetY);
            _viewportScale = Math.Clamp(scale, 0.25, 3.0);
            RebuildChildren();
        }

        private void DrawBackgroundImage()
        {
            try
            {
                _tipsBitmap ??= new Bitmap(AssetLoader.Open(new Uri("avares://RenPyVisualScriptMVVM/Assets/tips.png")));
                var origin = ToScreen(new Point(0, 0));

                var image = new Image
                {
                    Source = _tipsBitmap,
                    Width = _tipsBitmap.Size.Width * _viewportScale / 2,
                    Height = _tipsBitmap.Size.Height * _viewportScale / 2,
                    Opacity = 1,
                    IsHitTestVisible = false
                };

                Children.Add(image);
                SetLeft(image, origin.X - 600 * _viewportScale);
                SetTop(image, origin.Y);
            }
            catch
            {
                // Ignore missing or unreadable background hint image.
            }
        }

        private void DrawNotes()
        {
            foreach (var note in Notes)
            {
                if (!IsNoteVisible(note))
                {
                    continue;
                }

                DrawNote(note);
            }
        }

        private void DrawEdges()
        {
            foreach (var edge in Edges)
            {
                var isDimmed = IsRouteHighlightActive && (!IsNodeInHighlightedRoute(edge.Start) || !IsNodeInHighlightedRoute(edge.End));
                var isSelected = edge == _selectedEdge;
                var edgeBrush = isSelected
                    ? new SolidColorBrush(Color.Parse("#FFD166"))
                    : isDimmed ? new SolidColorBrush(Color.FromArgb(60, 150, 150, 150)) : GetEdgeBrush(edge.Start);
                var edgeThickness = isSelected ? 3 : isDimmed ? 1 : 2;
                var hitThickness = Math.Max(12, edgeThickness * 5);

                if (edge.Start == edge.End)
                {
                    var loopPoints = GetSelfLoopPoints(edge.Start);
                    if (loopPoints.Count < 2)
                        continue;

                    AddEdgePath(loopPoints, edge, edgeBrush, edgeThickness, hitThickness);
                    DrawArrow(loopPoints[^1], loopPoints[^2], edgeBrush, edgeThickness);
                    continue;
                }

                var hasConnectorPair = _edgeConnectorMap.TryGetValue(edge, out var connectorPair);
                var startConnector = hasConnectorPair ? connectorPair.start : ConnectorPosition.Right;
                var endConnector = hasConnectorPair ? connectorPair.end : ConnectorPosition.Left;
                var points = GetManhattanPoints(edge, startConnector, endConnector);
                if (points.Count < 2 || !IsPolylineVisible(points))
                {
                    continue;
                }

                AddEdgePath(points, edge, edgeBrush, edgeThickness, hitThickness);
                DrawArrow(points.Last(), points[points.Count - 2], edgeBrush, edgeThickness);
            }
        }

        private void AddEdgePath(List<Point> points, Edge edge, IBrush edgeBrush, double edgeThickness, double hitThickness)
        {
            if (!_isViewportNavigating)
            {
                var hitPath = CreateRoundedPath(points, 10, Brushes.Transparent, hitThickness);
                hitPath.DataContext = edge;
                hitPath.IsHitTestVisible = true;
                hitPath.PointerPressed += OnEdgePointerPressed;
                Children.Add(hitPath);
            }

            var visiblePath = CreateRoundedPath(points, 10, edgeBrush, edgeThickness);
            visiblePath.DataContext = edge;
            visiblePath.IsHitTestVisible = false;
            Children.Add(visiblePath);
        }

        private void DrawTempLine()
        {
            if (_tempLine != null)
                Children.Add(_tempLine);
        }

        private void DrawNodes()
        {
            foreach (var node in Nodes)
            {
                if (!IsNodeVisible(node))
                {
                    continue;
                }

                DrawNode(node);
                DrawNodeConnectors(node);
            }
        }

        private void DrawNote(GraphNote note)
        {
            var screenCenter = ToScreen(new Point(note.X, note.Y));
            var noteWidth = note.Width * _viewportScale;
            var noteHeight = note.Height * _viewportScale;
            var noteLeft = screenCenter.X - noteWidth / 2;
            var noteTop = screenCenter.Y - noteHeight / 2;

            var noteBody = new Rectangle
            {
                Width = noteWidth,
                Height = noteHeight,
                Fill = new SolidColorBrush(Color.Parse("#F3D36B")),
                Stroke = note == _selectedNote ? Brushes.White : new SolidColorBrush(Color.Parse("#8F6A00")),
                StrokeThickness = note == _selectedNote ? 2 : 1,
                RadiusX = 8 * _viewportScale,
                RadiusY = 8 * _viewportScale,
                IsHitTestVisible = false
            };

            Children.Add(noteBody);
            SetLeft(noteBody, noteLeft);
            SetTop(noteBody, noteTop);

            if (_editingNote == note)
            {
                var textBox = CreateNoteTextBox(note, noteLeft, noteTop, noteWidth, noteHeight);
                Children.Add(textBox);
                _noteTextBox = textBox;
                return;
            }

            var textBlock = new TextBlock
            {
                Text = note.Text,
                Width = Math.Max(60, noteWidth - 16 * _viewportScale),
                FontSize = 13 * _viewportScale,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Black,
                IsHitTestVisible = false
            };

            Children.Add(textBlock);
            SetLeft(textBlock, noteLeft + 8 * _viewportScale);
            SetTop(textBlock, noteTop + 8 * _viewportScale);
        }

        private IBrush GetNodeFill(Node node)
        {
            if (node.IsScreenConnector || node.IsMenuConnector)
                return node.Background ?? new SolidColorBrush(Color.Parse("#2C3E50"));

            if (node.ImageBackground is not null)
                return node.ImageBackground;

            return Node.CreatePlaceholderImageBrush();
        }

        private void DrawNode(Node node)
        {
            if (node.IsScreenConnector || node.IsMenuConnector)
            {
                DrawConnectorNode(node);
                return;
            }

            var isDimmed = IsRouteHighlightActive && !IsNodeInHighlightedRoute(node);
            var titleBarHeight = 24 * _viewportScale;
            var cornerRadius = 6 * _viewportScale;
            var screenCenter = ToScreen(node.Position);
            var nodeWidth = node.Size.Width * _viewportScale;
            var nodeHeight = node.Size.Height * _viewportScale;
            var nodeLeft = screenCenter.X - nodeWidth / 2;
            var nodeTop = screenCenter.Y - nodeHeight / 2;
            var warningMessage = GetWarningMessage(node);
            var titleBarBrush = GetTitleBarBrush(warningMessage);

            var rect = new Rectangle
            {
                Width = nodeWidth,
                Height = nodeHeight,
                Fill = GetNodeFill(node),
                RadiusX = cornerRadius,
                RadiusY = cornerRadius,
                Stroke = _selectedNodes.Contains(node) ? Brushes.Black : null,
                StrokeThickness = _selectedNodes.Contains(node) ? 2 : 0,
                Opacity = isDimmed ? 0.22 : 1
            };

            var titleBar = new Rectangle
            {
                Width = nodeWidth,
                Height = titleBarHeight,
                Fill = titleBarBrush,
                RadiusX = cornerRadius,
                RadiusY = cornerRadius,
                IsHitTestVisible = false,
                Opacity = isDimmed ? 0.35 : 1
            };

            var textBlock = new TextBlock
            {
                Text = node.Title,
                Width = nodeWidth,
                FontSize = 14 * _viewportScale,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center,
                Foreground = isDimmed ? new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)) : Brushes.White,
                IsHitTestVisible = false
            };

            Children.Add(rect);
            Children.Add(titleBar);

            rect.DataContext = node;
            titleBar.DataContext = node;
            textBlock.DataContext = node;

            SetLeft(rect, nodeLeft);
            SetTop(rect, nodeTop);
            SetLeft(titleBar, nodeLeft);
            SetTop(titleBar, nodeTop);

            if (!_isViewportNavigating)
            {
                // Square off the lower corners of the overlay so it reads like a glass header.
                var titleBarBottomLeft = new Rectangle
                {
                    Width = cornerRadius,
                    Height = cornerRadius,
                    Fill = titleBarBrush,
                    IsHitTestVisible = false
                };
                var titleBarBottomRight = new Rectangle
                {
                    Width = cornerRadius,
                    Height = cornerRadius,
                    Fill = titleBarBrush,
                    IsHitTestVisible = false
                };

                Children.Add(titleBarBottomLeft);
                Children.Add(titleBarBottomRight);

                titleBarBottomLeft.DataContext = node;
                titleBarBottomRight.DataContext = node;

                SetLeft(titleBarBottomLeft, nodeLeft);
                SetTop(titleBarBottomLeft, nodeTop + titleBarHeight - cornerRadius);
                SetLeft(titleBarBottomRight, nodeLeft + nodeWidth - cornerRadius);
                SetTop(titleBarBottomRight, nodeTop + titleBarHeight - cornerRadius);
            }

            if (_renamingNode == node)
            {
                var renameTextBox = CreateRenameTextBox(node, nodeWidth, nodeTop, nodeLeft);
                Children.Add(renameTextBox);
                _renameTextBox = renameTextBox;

                var validationTextBlock = CreateRenameValidationTextBlock(nodeWidth, nodeTop, nodeLeft);
                Children.Add(validationTextBlock);
                _renameValidationTextBlock = validationTextBlock;
                UpdateRenameValidationState(renameTextBox, node);
            }
            else
            {
                Children.Add(textBlock);
                SetLeft(textBlock, nodeLeft);
                SetTop(textBlock, nodeTop + 3 * _viewportScale);
            }

            if (warningMessage != null)
            {
                DrawWarningIndicator(node, warningMessage, nodeLeft, nodeTop, nodeWidth);
            }

            if (ShouldShowEndNode(node))
            {
                DrawEndNode(node);
            }

            if (ShouldShowStartNode(node))
            {
                DrawStartNode(node);
            }
        }

        private void DrawNodeConnectors(Node node)
        {
            if (_isViewportNavigating)
            {
                return;
            }

            foreach (ConnectorPosition position in Enum.GetValues(typeof(ConnectorPosition)))
            {
                var connectorPoint = GetConnectorPoint(node, position);
                var ellipse = CreateConnectorEllipse(node, position, connectorPoint);
                Children.Add(ellipse);

                SetLeft(ellipse, connectorPoint.X - ellipse.Width / 2);
                SetTop(ellipse, connectorPoint.Y - ellipse.Height / 2);
            }
        }

        private Rectangle CreateConnectorEllipse(Node node, ConnectorPosition position, Point connectorPoint)
        {
            var ellipse = new Rectangle
            {
                Width = 12 * _viewportScale,
                Height = 18 * _viewportScale,
                RadiusX = 6 * _viewportScale,
                RadiusY = 6 * _viewportScale,
                Fill = new SolidColorBrush(Color.Parse("#6FCF97")),
                Stroke = new SolidColorBrush(Color.Parse("#244131")),
                StrokeThickness = 1,
                IsHitTestVisible = true
            };

            if (_lineStartNode == node && _lineStartConnector == position)
            {
                ellipse.Fill = Brushes.Orange;
                ellipse.Stroke = Brushes.SaddleBrown;
            }

            ellipse.DataContext = (node, position);
            ellipse.PointerPressed += OnConnectorPressed;

            return ellipse;
        }

        private void DrawConnectorNode(Node node)
        {
            var screenCenter = ToScreen(node.Position);
            var nodeWidth = node.Size.Width * _viewportScale;
            var nodeHeight = node.Size.Height * _viewportScale;
            var nodeLeft = screenCenter.X - nodeWidth / 2;
            var nodeTop = screenCenter.Y - nodeHeight / 2;
            var isMenuConnector = node.IsMenuConnector;
            var borderBrush = isMenuConnector
                ? new SolidColorBrush(Color.Parse("#F2B36D"))
                : new SolidColorBrush(Color.Parse("#8EC5FF"));
            var readOnlyMessage = isMenuConnector
                ? "Сложная логика menu. Этот коннектор вычислен автоматически и не редактируется."
                : "Сложная логика screen. Этот коннектор вычислен автоматически и не редактируется.";

            var rect = new Rectangle
            {
                Width = nodeWidth,
                Height = nodeHeight,
                Fill = GetNodeFill(node),
                RadiusX = 16 * _viewportScale,
                RadiusY = 16 * _viewportScale,
                Stroke = _selectedNodes.Contains(node) ? Brushes.White : borderBrush,
                StrokeThickness = _selectedNodes.Contains(node) ? 2 : 1.5
            };

            var textBlock = new TextBlock
            {
                Text = isMenuConnector
                    ? node.MenuName ?? node.Title
                    : node.ScreenName ?? node.Title,
                Width = Math.Max(40, nodeWidth - 12 * _viewportScale),
                FontSize = 11 * _viewportScale,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Brushes.White,
                IsHitTestVisible = false
            };

            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20)),
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8 * _viewportScale),
                Padding = new Thickness(6 * _viewportScale, 2 * _viewportScale),
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = "READ ONLY",
                    FontSize = 8 * _viewportScale,
                    FontWeight = FontWeight.Bold,
                    Foreground = borderBrush,
                    IsHitTestVisible = false
                }
            };

            Children.Add(rect);
            Children.Add(textBlock);
            Children.Add(badge);

            rect.DataContext = node;
            textBlock.DataContext = node;
            badge.DataContext = node;
            ToolTip.SetTip(rect, readOnlyMessage);

            SetLeft(rect, nodeLeft);
            SetTop(rect, nodeTop);
            SetLeft(textBlock, nodeLeft + 6 * _viewportScale);
            SetTop(textBlock, nodeTop + 14 * _viewportScale);
            SetLeft(badge, nodeLeft + (nodeWidth - 64 * _viewportScale) / 2);
            SetTop(badge, nodeTop + nodeHeight - 16 * _viewportScale);
        }

        private string? GetWarningMessage(Node node)
        {
            if (node.IsScreenConnector || node.IsMenuConnector)
                return null;

            var hasSelfLoop = _nodesWithSelfLoop.Contains(node);
            var hasChildren = HasOutgoingBranch(node);

            if (hasSelfLoop && !hasChildren)
            {
                return "Узел зациклен и не имеет выхода";
            }

            if (!IsConnectedToPrimaryRoot(node))
            {
                return "Узел не соединен с главной веткой";
            }

            return null;
        }

        private bool ShouldShowEndNode(Node node)
        {
            if (node.IsScreenConnector || node.IsMenuConnector)
            {
                return false;
            }

            if (_loopNodesWithEndBranch.Contains(node))
            {
                return true;
            }

            var hasSelfLoop = Edges.Any(edge => edge.Start == node && edge.End == node);
            if (hasSelfLoop)
            {
                return false;
            }

            return !HasOutgoingBranch(node);
        }

        private bool ShouldShowStartNode(Node node)
        {
            if (node.IsScreenConnector || node.IsMenuConnector)
            {
                return false;
            }

            return GetPrimaryRootNode() == node;
        }

        private Node? GetPrimaryRootNode()
        {
            return _primaryRootNode;
        }

        private bool IsConnectedToPrimaryRoot(Node node)
        {
            var root = _primaryRootNode;
            if (root == null)
            {
                return true;
            }

            if (node == root)
            {
                return true;
            }

            return _reachableNodes.Contains(node);
        }

        private bool HasOutgoingBranch(Node node)
        {
            return _nodesWithOutgoingEdges.Contains(node) || _loopNodesWithEndBranch.Contains(node);
        }

        private IBrush GetEdgeBrush(Node node)
        {
            var nodeIndex = _nodeIndexMap.TryGetValue(node, out var index) ? index : 0;
            var hue = (nodeIndex * 137.508) % 360;
            return new SolidColorBrush(ColorFromHsv(hue, 0.78, 0.95));
        }

        private Color ColorFromHsv(double hue, double saturation, double value)
        {
            var chroma = value * saturation;
            var x = chroma * (1 - Math.Abs((hue / 60.0 % 2) - 1));
            var match = value - chroma;

            double red = 0;
            double green = 0;
            double blue = 0;

            if (hue < 60)
            {
                red = chroma;
                green = x;
            }
            else if (hue < 120)
            {
                red = x;
                green = chroma;
            }
            else if (hue < 180)
            {
                green = chroma;
                blue = x;
            }
            else if (hue < 240)
            {
                green = x;
                blue = chroma;
            }
            else if (hue < 300)
            {
                red = x;
                blue = chroma;
            }
            else
            {
                red = chroma;
                blue = x;
            }

            return Color.FromRgb(
                (byte)Math.Round((red + match) * 255),
                (byte)Math.Round((green + match) * 255),
                (byte)Math.Round((blue + match) * 255));
        }

        private IBrush GetTitleBarBrush(string? warningMessage)
        {
            if (warningMessage == null)
            {
                return new SolidColorBrush(Color.FromArgb(185, 0, 0, 0));
            }

            if (warningMessage.Contains("не имеет выхода"))
            {
                return new SolidColorBrush(Color.FromArgb(215, 110, 72, 0));
            }

            return new SolidColorBrush(Color.FromArgb(215, 95, 45, 45));
        }

        private void DrawWarningIndicator(Node node, string message, double nodeLeft, double nodeTop, double nodeWidth)
        {
            const double foldScale = 1.5;
            var foldSize = 16 * _viewportScale * foldScale;
            var foldMain = new Polygon
            {
                Points = new Points
                {
                    new Point(0, 0),
                    new Point(foldSize, 0),
                    new Point(foldSize, foldSize),
                },
                Fill = new SolidColorBrush(Color.FromArgb(235, 255, 196, 87)),
                Stroke = new SolidColorBrush(Color.FromArgb(255, 70, 45, 0)),
                StrokeThickness = 1,
                IsHitTestVisible = true
            };

            var icon = new TextBlock
            {
                Text = "!",
                Width = foldSize * 0.55,
                FontSize = 9 * _viewportScale * foldScale,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Center,
                IsHitTestVisible = false
            };

            ToolTip.SetTip(foldMain, message);

            Children.Add(foldMain);
            Children.Add(icon);

            var diagonalOffset = 4 * _viewportScale;
            var foldLeft = nodeLeft + nodeWidth - foldSize - 4 * _viewportScale + diagonalOffset;
            var foldTop = nodeTop + 3 * _viewportScale - diagonalOffset;

            SetLeft(foldMain, foldLeft);
            SetTop(foldMain, foldTop);
            SetLeft(icon, foldLeft + foldSize * 0.34);
            SetTop(icon, foldTop + 0.35 * _viewportScale);
        }

        private void DrawEndNode(Node node)
        {
            var gap = 36 * _viewportScale;
            var width = 64 * _viewportScale;
            var height = 28 * _viewportScale;
            var verticalOffset = 42 * _viewportScale;

            var start = ToScreen(node.RightPoint);
            var hasRegularOutgoingEdges = Edges.Any(edge => edge.Start == node && edge.End != node);
            var endCenterY = !hasRegularOutgoingEdges
                ? start.Y
                : HasOutgoingEdgeAtOrBelow(node) ? start.Y - verticalOffset : start.Y + verticalOffset;
            var endCenter = new Point(start.X + gap + width / 2, endCenterY);
            var endRect = new Rectangle
            {
                Width = width,
                Height = height,
                RadiusX = 14 * _viewportScale,
                RadiusY = 14 * _viewportScale,
                Fill = Brushes.Transparent,
                Stroke = Brushes.LightGray,
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };

            var endLabel = new TextBlock
            {
                Text = "End",
                FontSize = 12 * _viewportScale,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.LightGray,
                IsHitTestVisible = false
            };

            var connectorPath = CreateRoundedPath(
                new List<Point>
                {
                    start,
                    new Point(start.X + gap / 2, start.Y),
                    new Point(start.X + gap / 2, endCenter.Y),
                    new Point(endCenter.X - width / 2, endCenter.Y),
                },
                8 * _viewportScale,
                Brushes.LightGray,
                1.5);
            connectorPath.IsHitTestVisible = false;

            Children.Add(connectorPath);
            Children.Add(endRect);
            Children.Add(endLabel);

            SetLeft(endRect, endCenter.X - width / 2);
            SetTop(endRect, endCenter.Y - height / 2);
            SetLeft(endLabel, endCenter.X - 14 * _viewportScale);
            SetTop(endLabel, endCenter.Y - 9 * _viewportScale);
        }

        private bool HasOutgoingEdgeAtOrBelow(Node node)
        {
            return Edges.Any(edge => edge.Start == node && edge.End != node && edge.End.Y >= node.Y);
        }

        private void DrawStartNode(Node node)
        {
            var gap = 36 * _viewportScale;
            var width = 64 * _viewportScale;
            var height = 28 * _viewportScale;

            var end = ToScreen(node.LeftPoint);
            var startCenter = new Point(end.X - gap - width / 2, end.Y);
            var startRect = new Rectangle
            {
                Width = width,
                Height = height,
                RadiusX = 14 * _viewportScale,
                RadiusY = 14 * _viewportScale,
                Fill = Brushes.Transparent,
                Stroke = Brushes.LightGray,
                StrokeThickness = 1.5,
                IsHitTestVisible = false
            };

            var startLabel = new TextBlock
            {
                Text = "Start",
                FontSize = 12 * _viewportScale,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.LightGray,
                IsHitTestVisible = false
            };

            var connectorPath = CreateRoundedPath(
                new List<Point>
                {
                    new Point(startCenter.X + width / 2, startCenter.Y),
                    end,
                },
                8 * _viewportScale,
                Brushes.LightGray,
                1.5);
            connectorPath.IsHitTestVisible = false;

            Children.Add(connectorPath);
            Children.Add(startRect);
            Children.Add(startLabel);

            SetLeft(startRect, startCenter.X - width / 2);
            SetTop(startRect, startCenter.Y - height / 2);
            SetLeft(startLabel, startCenter.X - 18 * _viewportScale);
            SetTop(startLabel, startCenter.Y - 9 * _viewportScale);
        }


        private void OnEdgePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Path path || path.DataContext is not Edge edge)
            {
                return;
            }

            var currentPoint = e.GetCurrentPoint(this);
            SelectedEdge = edge;

            if (currentPoint.Properties.IsRightButtonPressed)
            {
                ShowEdgeContextMenu(edge);
            }

            e.Handled = true;
        }

        private static bool IsDerivedNode(Node? node)
        {
            return node is not null && (node.IsScreenConnector || node.IsMenuConnector);
        }

        private static bool IsReadOnlyEdge(Edge? edge)
        {
            return edge is not null && (IsDerivedNode(edge.Start) || IsDerivedNode(edge.End));
        }

        private void RemoveEdge(Edge edge)
        {
            if (IsReadOnlyEdge(edge))
            {
                return;
            }

            Edges.Remove(edge);
            _edgeConnectorMap.Remove(edge);
            if (_selectedEdge == edge)
            {
                _selectedEdge = null;
            }
            NotifyGraphChanged();
            RebuildChildren();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            Focus();
            var point = ToWorld(e.GetPosition(this));
            var pointerPoint = e.GetCurrentPoint(this);

            if (pointerPoint.Properties.IsRightButtonPressed)
            {
                HandleRightClick(point);
                return;
            }

            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && pointerPoint.Properties.IsLeftButtonPressed)
            {
                ClearGraph();
                return;
            }

            if (pointerPoint.Properties.IsMiddleButtonPressed)
            {
                StartMiddleButtonInteraction(point, e.GetPosition(this));
                return;
            }

            if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                if (e.ClickCount >= 2)
                {
                    if (HitTestNode(point, out var existingNode) && existingNode != null)
                    {
                        NavigateToNodeDeclaration(existingNode);
                        return;
                    }

                    if (HitTestNote(point, out var existingNote) && existingNote != null)
                    {
                        BeginNoteEdit(existingNote);
                        return;
                    }

                    if (!HitTestNode(point, out _) && !HitTestEdge(point, out _))
                    {
                        AddNewNote(point);
                        return;
                    }
                }

                HandleLeftClick(point, e.KeyModifiers);
            }
        }

        private void NavigateToNodeDeclaration(Node node)
        {
            if (node.IsScreenConnector || node.IsMenuConnector)
                return;

            if (string.IsNullOrWhiteSpace(node.SourceFilePath) || node.SourceStartLine <= 0)
                return;

            var editorNavigation = Locator.Current.GetService<IEditorNavigationService>();
            editorNavigation?.NavigateTo(node.SourceFilePath, node.SourceStartLine);
            ActivateScriptEditorWindow();
        }

        private static void ActivateScriptEditorWindow()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            var editorWindow = desktop.Windows
                .OfType<ScriptEditor>()
                .FirstOrDefault(window => window.IsVisible);

            editorWindow?.Activate();
        }

        private static void RunNodeFromHere(Node node)
        {
            if (node.IsScreenConnector || node.IsMenuConnector || string.IsNullOrWhiteSpace(node.Title))
                return;

            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            var editorWindow = desktop.Windows
                .OfType<ScriptEditor>()
                .FirstOrDefault(window => window.IsVisible);

            if (editorWindow?.DataContext is ScriptEditorViewModel scriptEditorViewModel)
                scriptEditorViewModel.RunProjectFromLabel(node.Title);
        }

        private void HandleRightClick(Point point)
        {
            CloseContextMenu();

            if (_lineStartNode != null)
            {
                CancelLineDrawing();
                return;
            }

            if (HitTestNode(point, out var hitNode) && hitNode != null)
            {
                EnsureNodeSelectionForContextMenu(hitNode);
                ShowNodeContextMenu(hitNode);
                return;
            }

            if (HitTestNote(point, out var hitNote) && hitNote != null)
            {
                _selectedNote = hitNote;
                ShowNoteContextMenu(hitNote);
                RebuildChildren();
                return;
            }

            if (HitTestEdge(point, out var hitEdge) && hitEdge != null)
            {
                SelectedEdge = hitEdge;
                ShowEdgeContextMenu(hitEdge);
                return;
            }

            if (!HitTestNode(point, out _))
            {
                SelectedNode = null;
                SelectedEdge = null;
                AddNewNode(point);
            }
        }

        private void CancelLineDrawing()
        {
            _lineStartNode = null;
            _lineStartConnector = null;
            _tempLine = null;
            RebuildChildren();
        }

        private void ShowNodeContextMenu(Node node)
        {
            if (IsDerivedNode(node))
            {
                return;
            }

            var targetNodes = GetContextMenuTargetNodes(node);

            var renameMenuItem = new MenuItem
            {
                Header = "Переименовать label"
            };
            renameMenuItem.Click += (_, _) =>
            {
                CloseContextMenu();
                BeginInlineRename(node);
            };

            var runFromHereMenuItem = new MenuItem
            {
                Header = "Run from here"
            };
            runFromHereMenuItem.Click += (_, _) =>
            {
                CloseContextMenu();
                RunNodeFromHere(node);
            };

            var endBranchMenuItem = new MenuItem
            {
                Header = _loopNodesWithEndBranch.Contains(node)
                    ? "Убрать ветвление в End"
                    : "Сделать ветвление в End"
            };
            endBranchMenuItem.Click += (_, _) =>
            {
                if (_loopNodesWithEndBranch.Contains(node))
                {
                    _loopNodesWithEndBranch.Remove(node);
                }
                else
                {
                    _loopNodesWithEndBranch.Add(node);
                }

                CloseContextMenu();
                RebuildChildren();
            };

            var assignRouteMenuItem = new MenuItem
            {
                Header = string.IsNullOrWhiteSpace(ActiveRouteName)
                    ? "Добавить в route"
                    : $"Добавить в route '{ActiveRouteName}'",
                IsEnabled = !string.IsNullOrWhiteSpace(ActiveRouteName)
            };
            assignRouteMenuItem.Click += (_, _) =>
            {
                foreach (var targetNode in targetNodes)
                {
                    targetNode.RouteName = ActiveRouteName;
                }

                CloseContextMenu();
                RebuildRoutes();
                NotifyGraphChanged();
                RebuildChildren();
            };

            var removeFromRouteMenuItem = new MenuItem
            {
                Header = "Убрать из route",
                IsEnabled = targetNodes.Any(target => !string.IsNullOrWhiteSpace(target.RouteName))
            };
            removeFromRouteMenuItem.Click += (_, _) =>
            {
                foreach (var targetNode in targetNodes)
                {
                    targetNode.RouteName = null;
                }

                CloseContextMenu();
                RebuildRoutes();
                NotifyGraphChanged();
                RebuildChildren();
            };

            _activeContextMenu = new ContextMenu
            {
                ItemsSource = new[] { runFromHereMenuItem, renameMenuItem, endBranchMenuItem, assignRouteMenuItem, removeFromRouteMenuItem },
                Placement = PlacementMode.Pointer
            };
            _activeContextMenu.Closed += (_, _) => _activeContextMenu = null;
            _activeContextMenu.Open(this);
        }

        private void ShowEdgeContextMenu(Edge edge)
        {
            if (IsReadOnlyEdge(edge))
            {
                return;
            }

            var menuItem = new MenuItem
            {
                Header = "Удалить связь"
            };
            menuItem.Click += (_, _) =>
            {
                CloseContextMenu();
                RemoveEdge(edge);
            };

            _activeContextMenu = new ContextMenu
            {
                ItemsSource = new[] { menuItem },
                Placement = PlacementMode.Pointer
            };
            _activeContextMenu.Closed += (_, _) => _activeContextMenu = null;
            _activeContextMenu.Open(this);
        }

        private void ShowNoteContextMenu(GraphNote note)
        {
            var editMenuItem = new MenuItem
            {
                Header = "Редактировать заметку"
            };
            editMenuItem.Click += (_, _) =>
            {
                CloseContextMenu();
                BeginNoteEdit(note);
            };

            var deleteMenuItem = new MenuItem
            {
                Header = "Удалить заметку"
            };
            deleteMenuItem.Click += (_, _) =>
            {
                CloseContextMenu();
                Notes.Remove(note);
                if (_selectedNote == note)
                    _selectedNote = null;
                NotifyGraphChanged();
                RebuildChildren();
            };

            _activeContextMenu = new ContextMenu
            {
                ItemsSource = new[] { editMenuItem, deleteMenuItem },
                Placement = PlacementMode.Pointer
            };
            _activeContextMenu.Closed += (_, _) => _activeContextMenu = null;
            _activeContextMenu.Open(this);
        }

        private void CloseContextMenu()
        {
            if (_activeContextMenu != null)
            {
                _activeContextMenu.Close();
                _activeContextMenu = null;
            }
        }

        private void AddNewNode(Point point)
        {
            EndInlineRename(commitChanges: true);
            var title = $"N{Nodes.Count + 1}";
            Nodes.Add(new Node
            {
                X = point.X,
                Y = point.Y,
                Title = title,
                IsGeneratedManually = true,
                BodyLines = new List<string> { $"    \"TODO: {title}\"" }
            });
            NotifyGraphChanged();
            RebuildChildren();
        }

        private void ClearGraph()
        {
            CancelInlineRename();
            CancelNoteEdit();
            Nodes.Clear();
            Edges.Clear();
            Notes.Clear();
            _edgeConnectorMap.Clear();
            Routes.Clear();
            ActiveRouteName = null;
            _loopNodesWithEndBranch.Clear();
            _lineStartNode = null;
            _lineStartConnector = null;
            _tempLine = null;
            NotifyGraphChanged();
            RebuildChildren();
        }

        private void StartMiddleButtonInteraction(Point worldPoint, Point screenPoint)
        {
            foreach (var note in Notes)
            {
                var noteRect = GetNoteRect(note);
                if (noteRect.Contains(worldPoint))
                {
                    _draggingNote = note;
                    _noteDragOffset = new Point(worldPoint.X - note.X, worldPoint.Y - note.Y);
                    return;
                }
            }

            foreach (var node in Nodes)
            {
                var rect = GetNodeRect(node);
                if (rect.Contains(worldPoint))
                {
                    _draggingNode = node;
                    _dragOffset = new Point(worldPoint.X - node.X, worldPoint.Y - node.Y);
                    return;
                }
            }

            _isPanning = true;
            _panStartPoint = screenPoint;
            _panStartOffset = _viewportOffset;
        }

        private void HandleLeftClick(Point point, KeyModifiers keyModifiers)
        {
            if (_renamingNode != null)
            {
                EndInlineRename(commitChanges: true);
            }
            if (_editingNote != null)
            {
                EndNoteEdit(commitChanges: true);
            }

            var isCtrlSelection = keyModifiers.HasFlag(KeyModifiers.Control);

            foreach (var node in Nodes)
            {
                var rect = GetNodeRect(node);
                if (rect.Contains(point))
                {
                    if (isCtrlSelection)
                    {
                        ToggleNodeSelection(node);
                    }
                    else
                    {
                        SelectSingleNode(node);
                    }
                    return;
                }
            }

            if (HitTestNote(point, out var hitNote) && hitNote != null)
            {
                _selectedNote = hitNote;
                _selectedNode = null;
                _selectedNodes.Clear();
                _selectedEdge = null;
                RebuildChildren();
                return;
            }

            if (!isCtrlSelection)
            {
                ClearNodeSelection();
            }

            _selectedNote = null;
            SelectedEdge = null;
        }

        private Rect GetNodeRect(Node node)
        {
            return new Rect(
                node.X - node.Size.Width / 2,
                node.Y - node.Size.Height / 2,
                node.Size.Width,
                node.Size.Height);
        }

        private Rect GetNoteRect(GraphNote note)
        {
            return new Rect(
                note.X - note.Width / 2,
                note.Y - note.Height / 2,
                note.Width,
                note.Height);
        }


        private void OnConnectorPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Rectangle ellipse)
                return;

            // Явно приводим тип DataContext к кортежу
            if (ellipse.DataContext is not ValueTuple<Node, ConnectorPosition> tuple)
                return;

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            var (node, position) = tuple;
            if (IsDerivedNode(node))
            {
                e.Handled = true;
                return;
            }

            HandleConnectorClick(node, position);
            e.Handled = true;
        }

        private void HandleConnectorClick(Node node, ConnectorPosition position)
        {
            if (_lineStartNode == null)
            {
                StartLineDrawing(node, position);
                return;
            }

            if (_lineStartNode == node && _lineStartConnector == position)
            {
                CancelLineDrawing();
                return;
            }

            // Проверяем, не соединены ли уже эти узлы
            if (AreNodesConnected(_lineStartNode, node))
            {
                CancelLineDrawing();
                return;
            }

            CompleteLineDrawing(node, position);
        }

        private bool AreNodesConnected(Node node1, Node node2)
        {
            if (node1 == node2)
            {
                return Edges.Any(edge => edge.Start == node1 && edge.End == node2);
            }

            return Edges.Any(edge =>
                (edge.Start == node1 && edge.End == node2) ||
                (edge.Start == node2 && edge.End == node1));
        }

        private void HighlightExistingConnection(Node node1, Node node2)
        {
            var existingEdge = Edges.FirstOrDefault(edge =>
                (edge.Start == node1 && edge.End == node2) ||
                (edge.Start == node2 && edge.End == node1));

            if (existingEdge != null)
            {
                // Временно меняем цвет существующего ребра
                var edgePath = Children.OfType<Path>()
                    .FirstOrDefault(p => p.DataContext == existingEdge);

                if (edgePath != null)
                {
                    var originalStroke = edgePath.Stroke;
                    edgePath.Stroke = Brushes.Red;

                    // Возвращаем исходный цвет через 1 секунду
                    DispatcherTimer.RunOnce(() =>
                    {
                        edgePath.Stroke = originalStroke;
                    }, TimeSpan.FromSeconds(1));
                }
            }
        }

        private void StartLineDrawing(Node node, ConnectorPosition position)
        {
            _lineStartNode = node;
            _lineStartConnector = position;

            var startPoint = GetConnectorPoint(node, position);
            _tempLine = CreateRoundedPath(new List<Point> { startPoint, startPoint }, 10, Brushes.Red, 2);

            RebuildChildren();
        }

        private void CompleteLineDrawing(Node endNode, ConnectorPosition endConnector)
        {
            // Дополнительная проверка на случай, если состояние изменилось
            if (AreNodesConnected(_lineStartNode!, endNode))
            {
                CancelLineDrawing();
                return;
            }

            var edge = new Edge(_lineStartNode!, endNode);
            Edges.Add(edge);
            _edgeConnectorMap[edge] = (_lineStartConnector!.Value, endConnector);
            NotifyGraphChanged();

            CancelLineDrawing();
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var screenPoint = e.GetPosition(this);
            var point = ToWorld(screenPoint);

            if (_draggingNode != null)
            {
                DragNode(point);
            }
            else if (_draggingNote != null)
            {
                DragNote(point);
            }
            else if (_isPanning)
            {
                PanViewport(screenPoint);
            }

            if (_lineStartNode != null && _tempLine != null && _lineStartConnector != null)
            {
                UpdateTempLine(screenPoint);
            }
        }

        private void DragNode(Point point)
        {
            _draggingNode!.X = point.X - _dragOffset.X;
            _draggingNode.Y = point.Y - _dragOffset.Y;
            RebuildChildren();
        }

        private void DragNote(Point point)
        {
            _draggingNote!.X = point.X - _noteDragOffset.X;
            _draggingNote.Y = point.Y - _noteDragOffset.Y;
            RebuildChildren();
        }

        private void PanViewport(Point screenPoint)
        {
            BeginViewportNavigation();
            _viewportOffset = new Point(
                _panStartOffset.X + screenPoint.X - _panStartPoint.X,
                _panStartOffset.Y + screenPoint.Y - _panStartPoint.Y);
            RequestViewportRebuild();
        }

        private void UpdateTempLine(Point screenPoint)
        {
            var startPoint = GetConnectorPoint(_lineStartNode!, _lineStartConnector.Value);
            var tempPoints = GetManhattanPointsToPoint(_lineStartNode!, screenPoint, _lineStartConnector);
            _tempLine!.Data = CreateRoundedPathGeometry(tempPoints, 10);
            RebuildChildren();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (_renamingNode != null)
            {
                return;
            }

            if (e.Key == Key.R && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                if (TryBeginRenameSelectedNode())
                    e.Handled = true;

                return;
            }

            if (e.Key != Key.Delete)
            {
                return;
            }

            if (SelectedEdge != null)
            {
                if (!IsReadOnlyEdge(SelectedEdge))
                {
                    RemoveEdge(SelectedEdge);
                    e.Handled = true;
                }
                return;
            }

            if (SelectedNode != null)
            {
                if (!IsDerivedNode(SelectedNode))
                {
                    RemoveSelectedNode();
                    e.Handled = true;
                }
                return;
            }

            if (_selectedNote != null)
            {
                Notes.Remove(_selectedNote);
                _selectedNote = null;
                NotifyGraphChanged();
                RebuildChildren();
                e.Handled = true;
            }
        }

        private void RemoveSelectedNode()
        {
            var nodesToRemove = _selectedNodes.Count > 0
                ? _selectedNodes.ToList()
                : SelectedNode is not null ? new List<Node> { SelectedNode } : new List<Node>();

            nodesToRemove = nodesToRemove
                .Where(node => !IsDerivedNode(node))
                .ToList();

            if (nodesToRemove.Count == 0)
            {
                return;
            }

            var edgesToRemove = Edges
                .Where(edge => nodesToRemove.Contains(edge.Start) || nodesToRemove.Contains(edge.End))
                .ToList();

            foreach (var edge in edgesToRemove)
            {
                RemoveEdge(edge);
            }

            foreach (var node in nodesToRemove)
            {
                _loopNodesWithEndBranch.Remove(node);
                Nodes.Remove(node);
            }

            RebuildRoutes();
            ClearNodeSelection();
            NotifyGraphChanged();
        }

        private void SelectSingleNode(Node node)
        {
            _selectedNode = node;
            _selectedNodes.Clear();
            _selectedNodes.Add(node);
            _selectedEdge = null;
            RebuildChildren();
        }

        private void ToggleNodeSelection(Node node)
        {
            if (!_selectedNodes.Add(node))
            {
                _selectedNodes.Remove(node);
            }

            _selectedNode = _selectedNodes.LastOrDefault();
            _selectedEdge = null;
            RebuildChildren();
        }

        private void ClearNodeSelection()
        {
            _selectedNode = null;
            _selectedNodes.Clear();
            RebuildChildren();
        }

        private void EnsureNodeSelectionForContextMenu(Node node)
        {
            if (_selectedNodes.Contains(node))
            {
                _selectedNode = node;
                _selectedEdge = null;
                RebuildChildren();
                return;
            }

            SelectSingleNode(node);
        }

        private List<Node> GetContextMenuTargetNodes(Node node)
        {
            if (_selectedNodes.Contains(node) && _selectedNodes.Count > 1)
            {
                return _selectedNodes.ToList();
            }

            return new List<Node> { node };
        }

        public void SetNodeImage(Node node, string imagePath)
        {
            if (node == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(imagePath))
            {
                node.ImageBackground = Node.CreatePlaceholderImageBrush();
                RebuildChildren();
                return;
            }

            try
            {
                node.ImageBackground = new ImageBrush
                {
                    Source = new Bitmap(imagePath),
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
                node.ImageBackground = Node.CreatePlaceholderImageBrush();
            }
            RebuildChildren();
        }
        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_draggingNode != null || _draggingNote != null)
            {
                NotifyGraphChanged();
            }

            _draggingNode = null;
            _draggingNote = null;
            _isPanning = false;
            ScheduleViewportNavigationEnd();
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            var mouseScreen = e.GetPosition(this);
            var worldBeforeZoom = ToWorld(mouseScreen);
            var zoomFactor = e.Delta.Y > 0 ? 1.1 : 1 / 1.1;
            var newScale = Math.Clamp(_viewportScale * zoomFactor, 0.4, 2.5);

            if (Math.Abs(newScale - _viewportScale) < 0.0001)
            {
                return;
            }

            BeginViewportNavigation();
            _viewportScale = newScale;
            _viewportOffset = new Point(
                mouseScreen.X - worldBeforeZoom.X * _viewportScale,
                mouseScreen.Y - worldBeforeZoom.Y * _viewportScale);

            RequestViewportRebuild();
            ScheduleViewportNavigationEnd();
            e.Handled = true;
        }
        private bool HitTestNode(Point p, out Node? hitNode)
        {
            foreach (var node in Nodes)
            {
                var rect = new Rect(node.X - node.Size.Width / 2, node.Y - node.Size.Height / 2, node.Size.Width, node.Size.Height);
                if (rect.Contains(p))
                {
                    hitNode = node;
                    return true;
                }
            }
            hitNode = null;
            return false;
        }

        private bool HitTestEdge(Point worldPoint, out Edge? hitEdge)
        {
            var screenPoint = ToScreen(worldPoint);
            var tolerance = Math.Max(10, 12 * _viewportScale);

            foreach (var edge in Edges.AsEnumerable().Reverse())
            {
                var hasConnectorPair = _edgeConnectorMap.TryGetValue(edge, out var connectorPair);
                var points = edge.Start == edge.End
                    ? GetSelfLoopPoints(edge.Start)
                    : GetManhattanPoints(
                        edge,
                        hasConnectorPair ? connectorPair.start : ConnectorPosition.Right,
                        hasConnectorPair ? connectorPair.end : ConnectorPosition.Left);

                if (IsPointNearPolyline(screenPoint, points, tolerance))
                {
                    hitEdge = edge;
                    return true;
                }
            }

            hitEdge = null;
            return false;
        }

        private bool IsPointNearPolyline(Point point, List<Point> polyline, double tolerance)
        {
            if (polyline.Count < 2)
            {
                return false;
            }

            for (int i = 0; i < polyline.Count - 1; i++)
            {
                if (DistanceToSegment(point, polyline[i], polyline[i + 1]) <= tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private double DistanceToSegment(Point point, Point segmentStart, Point segmentEnd)
        {
            var segment = segmentEnd - segmentStart;
            var lengthSquared = segment.X * segment.X + segment.Y * segment.Y;

            if (lengthSquared <= double.Epsilon)
            {
                return Distance(point, segmentStart);
            }

            var projection = ((point.X - segmentStart.X) * segment.X + (point.Y - segmentStart.Y) * segment.Y) / lengthSquared;
            projection = Math.Clamp(projection, 0, 1);

            var closestPoint = new Point(
                segmentStart.X + projection * segment.X,
                segmentStart.Y + projection * segment.Y);

            return Distance(point, closestPoint);
        }

        private Point ToScreen(Point worldPoint)
        {
            return new Point(
                worldPoint.X * _viewportScale + _viewportOffset.X,
                worldPoint.Y * _viewportScale + _viewportOffset.Y);
        }

        private Point ToWorld(Point screenPoint)
        {
            return new Point(
                (screenPoint.X - _viewportOffset.X) / _viewportScale,
                (screenPoint.Y - _viewportOffset.Y) / _viewportScale);
        }

        private void RefreshRenderState()
        {
            var viewportWidth = Bounds.Width > 0 ? Bounds.Width : 1280;
            var viewportHeight = Bounds.Height > 0 ? Bounds.Height : 720;
            _viewportScreenRect = new Rect(0, 0, viewportWidth, viewportHeight);

            _nodeIndexMap = Nodes
                .Select((node, index) => new { node, index })
                .ToDictionary(item => item.node, item => item.index);

            _nodesWithSelfLoop = Edges
                .Where(edge => edge.Start == edge.End)
                .Select(edge => edge.Start)
                .ToHashSet();

            _nodesWithOutgoingEdges = Edges
                .Where(edge => edge.Start != edge.End)
                .Select(edge => edge.Start)
                .ToHashSet();

            _primaryRootNode = FindPrimaryRootNode();
            _reachableNodes = BuildReachableNodes(_primaryRootNode);
        }

        private Node? FindPrimaryRootNode()
        {
            if (Nodes.Count == 0)
            {
                return null;
            }

            var scriptRoot = Nodes
                .Where(node => !node.IsScreenConnector
                    && !node.IsMenuConnector
                    && !string.IsNullOrWhiteSpace(node.SourceFilePath)
                    && node.SourceFilePath!.Replace('\\', '/').EndsWith("script.rpy", StringComparison.OrdinalIgnoreCase))
                .OrderBy(node => node.SourceStartLine)
                .ThenBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (scriptRoot != null)
            {
                return scriptRoot;
            }

            var nodesWithIncomingEdges = Edges
                .Where(edge => edge.Start != edge.End)
                .Select(edge => edge.End)
                .ToHashSet();

            var rootCandidates = Nodes
                .Where(node => !nodesWithIncomingEdges.Contains(node))
                .OrderBy(node => node.X)
                .ThenBy(node => node.Y)
                .ToList();

            if (rootCandidates.Count > 0)
            {
                return rootCandidates[0];
            }

            return Nodes
                .OrderBy(node => node.X)
                .ThenBy(node => node.Y)
                .FirstOrDefault();
        }

        private HashSet<Node> BuildReachableNodes(Node? root)
        {
            if (root == null)
            {
                return new HashSet<Node>();
            }

            var visited = new HashSet<Node> { root };
            var queue = new Queue<Node>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var child in Edges.Where(edge => edge.Start == current && edge.End != current).Select(edge => edge.End))
                {
                    if (visited.Add(child))
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            return visited;
        }

        private bool IsNodeVisible(Node node, double margin = 200)
        {
            var screenCenter = ToScreen(node.Position);
            var nodeWidth = node.Size.Width * _viewportScale;
            var nodeHeight = node.Size.Height * _viewportScale;
            var nodeRect = new Rect(
                screenCenter.X - nodeWidth / 2,
                screenCenter.Y - nodeHeight / 2,
                nodeWidth,
                nodeHeight);

            return ExpandRect(_viewportScreenRect, margin).Intersects(nodeRect);
        }

        private bool IsNoteVisible(GraphNote note, double margin = 160)
        {
            var screenCenter = ToScreen(new Point(note.X, note.Y));
            var noteWidth = note.Width * _viewportScale;
            var noteHeight = note.Height * _viewportScale;
            var noteRect = new Rect(
                screenCenter.X - noteWidth / 2,
                screenCenter.Y - noteHeight / 2,
                noteWidth,
                noteHeight);

            return ExpandRect(_viewportScreenRect, margin).Intersects(noteRect);
        }

        private bool IsPolylineVisible(IReadOnlyList<Point> points, double margin = 220)
        {
            if (points.Count == 0)
            {
                return false;
            }

            var minX = points.Min(point => point.X);
            var minY = points.Min(point => point.Y);
            var maxX = points.Max(point => point.X);
            var maxY = points.Max(point => point.Y);
            var bounds = new Rect(new Point(minX, minY), new Point(maxX, maxY));
            return ExpandRect(_viewportScreenRect, margin).Intersects(bounds);
        }

        private static Rect ExpandRect(Rect rect, double margin)
        {
            return new Rect(
                rect.X - margin,
                rect.Y - margin,
                rect.Width + margin * 2,
                rect.Height + margin * 2);
        }

        private void BeginViewportNavigation()
        {
            _isViewportNavigating = true;
            _viewportNavigationTimer.Stop();
        }

        private void ScheduleViewportNavigationEnd()
        {
            _viewportNavigationTimer.Stop();
            _viewportNavigationTimer.Start();
        }

        private void RequestViewportRebuild()
        {
            _viewportRebuildPending = true;

            if (!_viewportRenderTimer.IsEnabled)
            {
                _viewportRenderTimer.Start();
            }
        }

        public void AddEdge(Node start, Node end)
        {
            EndInlineRename(commitChanges: true);
            var edge = new Edge(start, end);
            Edges.Add(edge);
            _edgeConnectorMap[edge] = (ConnectorPosition.Right, ConnectorPosition.Left);
            NotifyGraphChanged();
            RebuildChildren();
        }

        private TextBox CreateRenameTextBox(Node node, double nodeWidth, double nodeTop, double nodeLeft)
        {
            var textBox = new TextBox
            {
                Text = _renameText,
                Width = Math.Max(60, nodeWidth - 16 * _viewportScale),
                Height = 22 * _viewportScale,
                FontSize = 13 * _viewportScale,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            UpdateRenameValidationState(textBox, node);

            textBox.TextChanged += (_, _) =>
            {
                _renameText = textBox.Text ?? string.Empty;
                UpdateRenameValidationState(textBox, node);
            };
            textBox.KeyDown += OnRenameTextBoxKeyDown;
            textBox.LostFocus += OnRenameTextBoxLostFocus;

            SetLeft(textBox, nodeLeft + 8 * _viewportScale);
            SetTop(textBox, nodeTop + 1 * _viewportScale);

            Dispatcher.UIThread.Post(() =>
            {
                if (_renameTextBox == textBox)
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }
            }, DispatcherPriority.Input);

            return textBox;
        }

        private TextBlock CreateRenameValidationTextBlock(double nodeWidth, double nodeTop, double nodeLeft)
        {
            var textBlock = new TextBlock
            {
                Text = _renameValidationMessage,
                Width = Math.Max(80, nodeWidth - 16 * _viewportScale),
                FontSize = Math.Max(10, 11 * _viewportScale),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.Parse("#ff6b6b")),
                Background = new SolidColorBrush(Color.FromArgb(210, 45, 20, 20)),
                Padding = new Thickness(4, 2),
                IsVisible = !string.IsNullOrWhiteSpace(_renameValidationMessage),
                IsHitTestVisible = false
            };

            SetLeft(textBlock, nodeLeft + 8 * _viewportScale);
            SetTop(textBlock, nodeTop + 26 * _viewportScale);
            return textBlock;
        }

        private void UpdateRenameValidationState(TextBox textBox, Node node)
        {
            _renameValidationMessage = GetRenameValidationMessage(node, _renameText);
            var isValid = string.IsNullOrWhiteSpace(_renameValidationMessage);
            textBox.BorderBrush = isValid
                ? new SolidColorBrush(Color.Parse("#777777"))
                : new SolidColorBrush(Color.Parse("#ff6b6b"));
            textBox.BorderThickness = new Thickness(isValid ? 1 : 2);
            ToolTip.SetTip(textBox, isValid ? null : _renameValidationMessage);

            if (_renameValidationTextBlock is not null)
            {
                _renameValidationTextBlock.Text = _renameValidationMessage;
                _renameValidationTextBlock.IsVisible = !isValid;
            }
        }

        private void BeginInlineRename(Node node)
        {
            _renamingNode = node;
            _renameText = node.Title;
            _renameValidationMessage = string.Empty;
            SelectedNode = node;
        }

        public bool TryBeginRenameSelectedNode()
        {
            var node = SelectedNode;
            if (node is null || IsDerivedNode(node))
                return false;

            BeginInlineRename(node);
            RebuildChildren();
            return true;
        }

        private void OnRenameTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_renamingNode is not null && !CanRenameNode(_renamingNode, (_renameText ?? string.Empty).Trim()))
                {
                    if (sender is TextBox textBox)
                        UpdateRenameValidationState(textBox, _renamingNode);

                    e.Handled = true;
                    return;
                }

                EndInlineRename(commitChanges: true);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelInlineRename();
                e.Handled = true;
            }
        }

        private void OnRenameTextBoxLostFocus(object? sender, RoutedEventArgs e)
        {
            if (_renamingNode != null)
            {
                EndInlineRename(commitChanges: true);
            }
        }

        private void EndInlineRename(bool commitChanges)
        {
            if (_renamingNode == null)
            {
                return;
            }

            var node = _renamingNode;
            var newTitle = (_renameText ?? string.Empty).Trim();
            var shouldCommit = commitChanges && CanRenameNode(node, newTitle);

            _renamingNode = null;
            _renameTextBox = null;
            _renameValidationTextBlock = null;
            _renameText = string.Empty;
            _renameValidationMessage = string.Empty;

            if (shouldCommit && !string.Equals(node.Title, newTitle, StringComparison.Ordinal))
            {
                node.Title = newTitle;
                RebuildRoutes();
                NotifyGraphChanged();
            }

            RebuildChildren();
        }

        public void LoadRoutes(IEnumerable<StoryRoute> routes)
        {
            Routes.Clear();
            foreach (var route in routes)
            {
                Routes.Add(new StoryRoute
                {
                    Name = route.Name,
                    NodeTitles = route.NodeTitles.ToList()
                });
            }

            foreach (var node in Nodes)
            {
                node.RouteName = Routes
                    .FirstOrDefault(route => route.NodeTitles.Any(title =>
                        string.Equals(title, node.Title, StringComparison.OrdinalIgnoreCase)))
                    ?.Name;
            }

            SyncRouteNodes();
        }

        public void LoadNotes(IEnumerable<GraphNote> notes)
        {
            Notes.Clear();
            foreach (var note in notes)
            {
                Notes.Add(new GraphNote
                {
                    X = note.X,
                    Y = note.Y,
                    Width = note.Width,
                    Height = note.Height,
                    Text = note.Text
                });
            }
        }

        public void ApplySavedNodePositions(IEnumerable<GraphNodePosition> positions)
        {
            var positionByTitle = positions.ToDictionary(position => position.NodeTitle, StringComparer.OrdinalIgnoreCase);
            foreach (var node in Nodes)
            {
                if (positionByTitle.TryGetValue(node.Title, out var savedPosition))
                {
                    node.X = savedPosition.X;
                    node.Y = savedPosition.Y;
                }
            }
        }

        public GraphViewState BuildViewState()
        {
            return new GraphViewState
            {
                Routes = Routes
                    .Select(route => new StoryRoute
                    {
                        Name = route.Name,
                        NodeTitles = route.NodeTitles.ToList()
                    })
                    .ToList(),
                NodePositions = Nodes
                    .Select(node => new GraphNodePosition
                    {
                        NodeTitle = node.Title,
                        X = node.X,
                        Y = node.Y
                    })
                    .ToList(),
                Notes = Notes
                    .Select(note => new GraphNote
                    {
                        X = note.X,
                        Y = note.Y,
                        Width = note.Width,
                        Height = note.Height,
                        Text = note.Text
                    })
                    .ToList(),
                ViewportOffsetX = _viewportOffset.X,
                ViewportOffsetY = _viewportOffset.Y,
                ViewportScale = _viewportScale,
                HasViewport = true
            };
        }

        public void SyncRouteNodes()
        {
            RebuildRoutes();
        }

        public void ToggleRouteHighlight(StoryRoute route)
        {
            if (string.Equals(_highlightedRouteName, route.Name, StringComparison.OrdinalIgnoreCase))
            {
                ClearRouteHighlight();
                return;
            }
            else
            {
                _highlightedRouteName = route.Name;
                _highlightedRouteNodeTitles = route.NodeTitles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            RebuildChildren();
        }

        public void ClearRouteHighlight()
        {
            if (!IsRouteHighlightActive && string.IsNullOrWhiteSpace(_highlightedRouteName))
                return;

            _highlightedRouteName = null;
            _highlightedRouteNodeTitles.Clear();
            RebuildChildren();
        }

        private bool IsRouteHighlightActive => _highlightedRouteNodeTitles.Count > 0;

        private bool IsNodeInHighlightedRoute(Node node)
        {
            if (node.IsScreenConnector || node.IsMenuConnector)
                return true;

            return _highlightedRouteNodeTitles.Contains(node.Title);
        }

        private void AddNewNote(Point point)
        {
            EndInlineRename(commitChanges: true);
            EndNoteEdit(commitChanges: true);

            var note = new GraphNote
            {
                X = point.X,
                Y = point.Y,
                Text = "New note"
            };

            Notes.Add(note);
            _selectedNote = note;
            NotifyGraphChanged();
            BeginNoteEdit(note);
        }

        private bool HitTestNote(Point point, out GraphNote? hitNote)
        {
            for (var i = Notes.Count - 1; i >= 0; i--)
            {
                if (GetNoteRect(Notes[i]).Contains(point))
                {
                    hitNote = Notes[i];
                    return true;
                }
            }

            hitNote = null;
            return false;
        }

        private void BeginNoteEdit(GraphNote note)
        {
            _editingNote = note;
            _noteEditText = note.Text;
            _selectedNote = note;
            RebuildChildren();
        }

        private TextBox CreateNoteTextBox(GraphNote note, double noteLeft, double noteTop, double noteWidth, double noteHeight)
        {
            var textBox = new TextBox
            {
                Text = _noteEditText,
                Width = Math.Max(80, noteWidth - 16 * _viewportScale),
                Height = Math.Max(48, noteHeight - 16 * _viewportScale),
                FontSize = 13 * _viewportScale,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Background = new SolidColorBrush(Color.Parse("#F3D36B")),
                Foreground = Brushes.Black,
                BorderBrush = new SolidColorBrush(Color.Parse("#8F6A00"))
            };

            textBox.TextChanged += (_, _) => _noteEditText = textBox.Text ?? string.Empty;
            textBox.KeyDown += OnNoteTextBoxKeyDown;
            textBox.LostFocus += OnNoteTextBoxLostFocus;

            SetLeft(textBox, noteLeft + 8 * _viewportScale);
            SetTop(textBox, noteTop + 8 * _viewportScale);

            Dispatcher.UIThread.Post(() =>
            {
                if (_noteTextBox == textBox)
                {
                    textBox.Focus();
                    textBox.CaretIndex = textBox.Text?.Length ?? 0;
                }
            }, DispatcherPriority.Input);

            return textBox;
        }

        private void OnNoteTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelNoteEdit();
                e.Handled = true;
            }
        }

        private void OnNoteTextBoxLostFocus(object? sender, RoutedEventArgs e)
        {
            if (_editingNote != null)
            {
                EndNoteEdit(commitChanges: true);
            }
        }

        private void EndNoteEdit(bool commitChanges)
        {
            if (_editingNote == null)
                return;

            var note = _editingNote;
            _editingNote = null;
            _noteTextBox = null;

            if (commitChanges)
            {
                note.Text = string.IsNullOrWhiteSpace(_noteEditText) ? "Note" : _noteEditText.Trim();
                NotifyGraphChanged();
            }

            _noteEditText = string.Empty;
            RebuildChildren();
        }

        private void CancelNoteEdit()
        {
            EndNoteEdit(commitChanges: false);
        }

        private void RebuildRoutes()
        {
            var previousRouteNames = Routes
                .GroupBy(GetRouteSignature, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);
            var generatedRoutes = BuildAutomaticStoryRoutes();
            var usedRouteNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var route in generatedRoutes)
            {
                var signature = GetRouteSignature(route);
                if (previousRouteNames.TryGetValue(signature, out var previousName)
                    && !string.IsNullOrWhiteSpace(previousName))
                {
                    route.Name = MakeUniqueRouteName(previousName, usedRouteNames);
                }
                else
                {
                    route.Name = MakeUniqueRouteName(route.Name, usedRouteNames);
                }
            }

            Routes.Clear();

            foreach (var route in generatedRoutes)
            {
                Routes.Add(route);
            }
        }

        private List<StoryRoute> BuildAutomaticStoryRoutes()
        {
            var root = FindPrimaryRootNode();
            if (root is null)
                return new List<StoryRoute>();

            var routeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var routes = new List<StoryRoute>();
            var cycleGroups = BuildRouteCycleGroups();
            BuildRouteTree(root, new List<Node>(), "main", routes, routeNames, new HashSet<Node>(), cycleGroups);

            return routes;
        }

        public bool RenameRoute(StoryRoute route, string? newName)
        {
            var normalizedName = (newName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
                return false;

            var targetRoute = Routes.FirstOrDefault(candidate =>
                ReferenceEquals(candidate, route)
                || string.Equals(candidate.Name, route.Name, StringComparison.OrdinalIgnoreCase)
                && RouteNodeTitlesEqual(candidate, route));

            if (targetRoute is null)
                return false;

            if (Routes.Any(candidate =>
                    !ReferenceEquals(candidate, targetRoute)
                    && string.Equals(candidate.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var oldName = targetRoute.Name;
            targetRoute.Name = normalizedName;

            foreach (var node in Nodes.Where(node => string.Equals(node.RouteName, oldName, StringComparison.OrdinalIgnoreCase)))
                node.RouteName = normalizedName;

            if (string.Equals(ActiveRouteName, oldName, StringComparison.OrdinalIgnoreCase))
                ActiveRouteName = normalizedName;

            if (string.Equals(_highlightedRouteName, oldName, StringComparison.OrdinalIgnoreCase))
                _highlightedRouteName = normalizedName;

            NotifyGraphChanged();
            return true;
        }

        private void BuildRouteTree(
            Node start,
            IReadOnlyList<Node> inheritedPath,
            string routeName,
            List<StoryRoute> routes,
            HashSet<string> routeNames,
            HashSet<Node> visitedPath,
            IReadOnlyDictionary<Node, RouteCycleGroup> cycleGroups)
        {
            var path = inheritedPath.ToList();
            var current = start;

            while (true)
            {
                if (cycleGroups.TryGetValue(current, out var cycleGroup))
                {
                    if (cycleGroup.Nodes.Any(visitedPath.Contains))
                        return;

                    foreach (var node in cycleGroup.Nodes)
                    {
                        visitedPath.Add(node);
                        path.Add(node);
                    }

                    var cycleChildren = cycleGroup.Exits;
                    if (cycleGroup.HasEnd)
                        AddRoute(routes, routeNames, routeName, path);

                    if (cycleChildren.Count == 0)
                        return;

                    current = cycleChildren[0];
                    if (cycleChildren.Count > 1)
                    {
                        for (var i = 1; i < cycleChildren.Count; i++)
                        {
                            var child = cycleChildren[i];
                            var childRouteName = MakeUniqueRouteName($"{routeName}_{i}_{SanitizeRoutePart(child.Title)}", routeNames);
                            BuildRouteTree(child, path, childRouteName, routes, routeNames, new HashSet<Node>(visitedPath), cycleGroups);
                        }
                    }

                    continue;
                }

                if (!visitedPath.Add(current))
                    return;

                if (IsRouteLabelNode(current))
                    path.Add(current);

                var children = GetRouteChildren(current);
                if (IsRouteEndNode(current, children))
                    AddRoute(routes, routeNames, routeName, path);

                if (children.Count == 0)
                    return;

                current = children[0];
                if (children.Count > 1)
                {
                    for (var i = 1; i < children.Count; i++)
                    {
                        var child = children[i];
                        var childRouteName = MakeUniqueRouteName($"{routeName}_{i}_{SanitizeRoutePart(child.Title)}", routeNames);
                        BuildRouteTree(child, path, childRouteName, routes, routeNames, new HashSet<Node>(visitedPath), cycleGroups);
                    }
                }
            }
        }

        private Dictionary<Node, RouteCycleGroup> BuildRouteCycleGroups()
        {
            var index = 0;
            var stack = new Stack<Node>();
            var indexes = new Dictionary<Node, int>();
            var lowLinks = new Dictionary<Node, int>();
            var onStack = new HashSet<Node>();
            var groups = new Dictionary<Node, RouteCycleGroup>();

            foreach (var node in Nodes.Where(IsRouteLabelNode))
            {
                if (!indexes.ContainsKey(node))
                    StrongConnect(node);
            }

            return groups;

            void StrongConnect(Node node)
            {
                indexes[node] = index;
                lowLinks[node] = index;
                index++;
                stack.Push(node);
                onStack.Add(node);

                foreach (var child in GetRouteChildren(node).Where(IsRouteLabelNode))
                {
                    if (!indexes.ContainsKey(child))
                    {
                        StrongConnect(child);
                        lowLinks[node] = Math.Min(lowLinks[node], lowLinks[child]);
                    }
                    else if (onStack.Contains(child))
                    {
                        lowLinks[node] = Math.Min(lowLinks[node], indexes[child]);
                    }
                }

                if (lowLinks[node] != indexes[node])
                    return;

                var component = new List<Node>();
                Node current;
                do
                {
                    current = stack.Pop();
                    onStack.Remove(current);
                    component.Add(current);
                }
                while (current != node);

                if (!IsCyclicRouteComponent(component))
                    return;

                var componentSet = component.ToHashSet();
                var orderedNodes = component
                    .OrderBy(item => item.SourceStartLine <= 0 ? int.MaxValue : item.SourceStartLine)
                    .ThenBy(item => item.Y)
                    .ThenBy(item => item.X)
                    .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var exits = component
                    .SelectMany(GetRouteChildren)
                    .Where(child => !componentSet.Contains(child))
                    .Distinct()
                    .OrderBy(child => child.Y)
                    .ThenBy(child => child.X)
                    .ThenBy(child => child.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var hasExplicitEnd = component.Any(item => _loopNodesWithEndBranch.Contains(item));
                var group = new RouteCycleGroup(orderedNodes, exits, hasExplicitEnd);
                foreach (var item in component)
                    groups[item] = group;
            }
        }

        private bool IsCyclicRouteComponent(IReadOnlyCollection<Node> component)
        {
            return component.Count > 1
                   || component.Any(node => Edges.Any(edge => edge.Start == node && edge.End == node));
        }

        private sealed record RouteCycleGroup(
            IReadOnlyList<Node> Nodes,
            IReadOnlyList<Node> Exits,
            bool HasEnd);

        private List<Node> GetRouteChildren(Node node)
        {
            var children = new List<Node>();
            var visitedConnectors = new HashSet<Node> { node };

            foreach (var child in GetOrderedOutgoingNodes(node))
                AddRouteChildThroughConnectors(child, children, visitedConnectors);

            return children
                .Distinct()
                .OrderBy(child => child.Y)
                .ThenBy(child => child.X)
                .ThenBy(child => child.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void AddRouteChildThroughConnectors(
            Node candidate,
            List<Node> children,
            HashSet<Node> visitedConnectors)
        {
            if (candidate.IsScreenConnector || candidate.IsMenuConnector)
            {
                if (!visitedConnectors.Add(candidate))
                    return;

                foreach (var child in GetOrderedOutgoingNodes(candidate))
                    AddRouteChildThroughConnectors(child, children, visitedConnectors);

                return;
            }

            children.Add(candidate);
        }

        private List<Node> GetOrderedOutgoingNodes(Node node)
        {
            return Edges
                .Where(edge => edge.Start == node && edge.End != node)
                .Select(edge => edge.End)
                .Distinct()
                .OrderBy(child => child.Y)
                .ThenBy(child => child.X)
                .ThenBy(child => child.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsRouteLabelNode(Node node)
        {
            return !node.IsScreenConnector && !node.IsMenuConnector;
        }

        private bool IsRouteEndNode(Node node, IReadOnlyCollection<Node> routeChildren)
        {
            if (!IsRouteLabelNode(node))
                return false;

            if (_loopNodesWithEndBranch.Contains(node))
                return true;

            var hasSelfLoop = Edges.Any(edge => edge.Start == node && edge.End == node);
            return !hasSelfLoop && routeChildren.Count == 0;
        }

        private static void AddRoute(
            List<StoryRoute> routes,
            HashSet<string> routeNames,
            string name,
            IReadOnlyList<Node> nodes)
        {
            if (nodes.Count == 0)
                return;

            var routeName = MakeUniqueRouteName(name, routeNames);
            routes.Add(new StoryRoute
            {
                Name = routeName,
                NodeTitles = nodes
                    .Select(node => node.Title)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            });
        }

        private static string MakeUniqueRouteName(string name, HashSet<string> routeNames)
        {
            var normalized = string.IsNullOrWhiteSpace(name) ? "route" : name;
            if (routeNames.Add(normalized))
                return normalized;

            var suffix = 2;
            while (!routeNames.Add($"{normalized}_{suffix}"))
                suffix++;

            return $"{normalized}_{suffix}";
        }

        private static string GetRouteSignature(StoryRoute route)
        {
            return string.Join(
                "\n",
                route.NodeTitles
                    .Where(title => !string.IsNullOrWhiteSpace(title))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(title => title, StringComparer.OrdinalIgnoreCase));
        }

        private static bool RouteNodeTitlesEqual(StoryRoute left, StoryRoute right)
        {
            return string.Equals(GetRouteSignature(left), GetRouteSignature(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeRoutePart(string value)
        {
            var sanitized = Regex.Replace(value, @"[^A-Za-z0-9_]+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(sanitized) ? "branch" : sanitized;
        }

        private void CancelInlineRename()
        {
            EndInlineRename(commitChanges: false);
        }

        private bool CanRenameNode(Node node, string candidate)
        {
            return string.IsNullOrWhiteSpace(GetRenameValidationMessage(node, candidate));
        }

        private string GetRenameValidationMessage(Node node, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return "Label name cannot be empty.";
            }

            if (!ValidLabelRegex.IsMatch(candidate))
            {
                return "Use letters, digits and '_'. First character must be a letter or '_'.";
            }

            var alreadyExists = Nodes
                .Where(existingNode => existingNode != node)
                .Any(existingNode => string.Equals(existingNode.Title, candidate, StringComparison.OrdinalIgnoreCase));

            return alreadyExists
                ? "A label with this name already exists."
                : string.Empty;
        }

        public void NotifyGraphChanged()
        {
            RebuildRoutes();
            GraphChanged?.Invoke(this, EventArgs.Empty);
        }
        private List<Point> GetManhattanPoints(Edge edge, ConnectorPosition? forcedStart = null, ConnectorPosition? forcedEnd = null)
        {
            // Получаем точки коннекторов
            var startPoints = GetConnectorPoints(edge.Start);
            var endPoints = GetConnectorPoints(edge.End);

            // Выбираем оптимальные коннекторы
            var (bestStartPos, bestEndPos) = SelectOptimalConnectors(edge.Start, edge.End, startPoints, endPoints, forcedStart, forcedEnd);
            var bestStart = GetEdgeConnectorPoint(edge.Start, bestStartPos, edge, isStart: true);
            var bestEnd = GetEdgeConnectorPoint(edge.End, bestEndPos, edge, isStart: false);

            // Строим строго ортогональный путь
            return BuildOrthogonalPath(edge.Start, edge.End, bestStart, bestEnd, bestStartPos, bestEndPos);
        }


        private List<Point> BuildOrthogonalPath(Node start, Node end, Point startPoint, Point endPoint,
    ConnectorPosition? forcedStart, ConnectorPosition? forcedEnd)
        {
            // Определяем направления коннекторов
            var startDir = forcedStart ?? GetConnectorDirection(startPoint, start);
            var endDir = forcedEnd ?? GetConnectorDirection(endPoint, end);

            // Строим путь в зависимости от комбинации направлений
            return BuildPathByDirections(startPoint, endPoint, startDir, endDir, start, end);
        }


        private ConnectorPosition GetConnectorDirection(Point connectorPoint, Node node)
        {
            var screenLeftX = ToScreen(node.LeftPoint).X;
            return Math.Abs(connectorPoint.X - screenLeftX) < 1
                ? ConnectorPosition.Left
                : ConnectorPosition.Right;
        }

        private List<Point> BuildPathByDirections(Point start, Point end,
    ConnectorPosition startDir, ConnectorPosition endDir,
    Node startNode, Node endNode)
        {
            var points = new List<Point> { start };

            if (start.X > end.X)
            {
                var clearance = 30 * _viewportScale;
                var startExitX = start.X + (startDir == ConnectorPosition.Left ? -clearance : clearance);
                var endEntryX = end.X + (endDir == ConnectorPosition.Left ? -clearance : clearance);
                var topY = Math.Min(
                    ToScreen(startNode.Position).Y - startNode.Size.Height * _viewportScale / 2,
                    ToScreen(endNode.Position).Y - endNode.Size.Height * _viewportScale / 2) - clearance;

                points.Add(new Point(startExitX, start.Y));
                points.Add(new Point(startExitX, topY));
                points.Add(new Point(endEntryX, topY));
                points.Add(new Point(endEntryX, end.Y));
                points.Add(end);

                return CleanupPath(points);
            }

            points.Add(new Point((start.X + end.X) / 2, start.Y));
            points.Add(new Point((start.X + end.X) / 2, end.Y));
            points.Add(end);

            // Убираем дублирующиеся точки и точки, слишком близкие друг к другу
            return CleanupPath(points);
        }

        private List<Point> CleanupPath(List<Point> points)
        {
            var result = new List<Point> { points[0] };

            for (int i = 1; i < points.Count; i++)
            {
                // Проверяем, что точка достаточно далеко от предыдущей
                if (Distance(result[result.Count - 1], points[i]) > 5)
                {
                    // Проверяем, что направление изменилось (ортогональность)
                    if (result.Count < 2 || IsOrthogonalTurn(result[result.Count - 2], result[result.Count - 1], points[i]))
                    {
                        result.Add(points[i]);
                    }
                    else
                    {
                        // Заменяем последнюю точку, если направление не изменилось
                        result[result.Count - 1] = points[i];
                    }
                }
            }

            return result;
        }

        private double Distance(Point p1, Point p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        private bool IsOrthogonalTurn(Point p1, Point p2, Point p3)
        {
            // Проверяем, что поворот ортогональный (90 градусов)
            bool isHorizontal1 = Math.Abs(p1.Y - p2.Y) < 0.1;
            bool isHorizontal2 = Math.Abs(p2.Y - p3.Y) < 0.1;
            bool isVertical1 = Math.Abs(p1.X - p2.X) < 0.1;
            bool isVertical2 = Math.Abs(p2.X - p3.X) < 0.1;

            // Должно быть: горизонтальный -> вертикальный или вертикальный -> горизонтальный
            return (isHorizontal1 && isVertical2) || (isVertical1 && isHorizontal2);
        }

        private List<Point> RemoveDuplicatePoints(List<Point> points)
        {
            var result = new List<Point>();
            for (int i = 0; i < points.Count; i++)
            {
                if (i == 0 || !PointsAreEqual(points[i], points[i - 1]))
                {
                    result.Add(points[i]);
                }
            }
            return result;
        }

        private bool PointsAreEqual(Point p1, Point p2)
        {
            return Math.Abs(p1.X - p2.X) < 0.1 && Math.Abs(p1.Y - p2.Y) < 0.1;
        }

        private Dictionary<ConnectorPosition, Point> GetConnectorPoints(Node node)
        {
            return new Dictionary<ConnectorPosition, Point>
            {
                [ConnectorPosition.Left] = ToScreen(node.LeftPoint),
                [ConnectorPosition.Right] = ToScreen(node.RightPoint),
            };
        }

        private Point GetEdgeConnectorPoint(Node node, ConnectorPosition position, Edge edge, bool isStart)
        {
            var basePoint = position switch
            {
                ConnectorPosition.Left => ToScreen(node.LeftPoint),
                ConnectorPosition.Right => ToScreen(node.RightPoint),
                _ => ToScreen(node.Position)
            };

            if (isStart || edge.Start == edge.End)
            {
                return basePoint;
            }

            var relatedEdges = Edges
                .Where(current => current.End == node && current.Start != node)
                .OrderBy(current => current.Start.Y)
                .ThenBy(current => current.Start.X)
                .ToList();

            var edgeIndex = relatedEdges.IndexOf(edge);
            if (edgeIndex < 0 || relatedEdges.Count <= 1)
            {
                return basePoint;
            }

            var spacing = 7 * _viewportScale;
            var totalSpread = (relatedEdges.Count - 1) * spacing;
            var offsetY = edgeIndex * spacing - totalSpread / 2;
            var maxOffset = Math.Max(0, node.Size.Height * _viewportScale / 2 - 16 * _viewportScale);
            offsetY = Math.Clamp(offsetY, -maxOffset, maxOffset);

            return new Point(basePoint.X, basePoint.Y + offsetY);
        }

        private (ConnectorPosition start, ConnectorPosition end) SelectOptimalConnectors(Node start, Node end,
            Dictionary<ConnectorPosition, Point> startPoints,
            Dictionary<ConnectorPosition, Point> endPoints,
            ConnectorPosition? forcedStart, ConnectorPosition? forcedEnd)
        {
            // По умолчанию все связи строим строго: выход справа -> вход слева.
            // Форсированные значения оставляем только для ручного соединения коннекторов.
            var startConnector = forcedStart ?? ConnectorPosition.Right;
            var endConnector = forcedEnd ?? ConnectorPosition.Left;
            return (startConnector, endConnector);
        }

        private ConnectorPosition GetPreferredConnector(Node currentNode, Node otherNode, double dx, double dy, bool isStart)
        {
            return isStart ? ConnectorPosition.Right : ConnectorPosition.Left;
        }

        private List<Point> GetManhattanPointsToPoint(Node start, Point endPoint, ConnectorPosition? forcedStart = null)
        {
            var startPoints = GetConnectorPoints(start);
            var targetWorldPoint = ToWorld(endPoint);
            var startPoint = forcedStart.HasValue ?
                startPoints[forcedStart.Value] :
                SelectOptimalConnectorForPoint(start, targetWorldPoint, startPoints);

            var startDir = forcedStart ?? GetConnectorDirection(startPoint, start);

            // Строим простой L-образный путь
            var points = new List<Point> { startPoint };

            // Всегда строим путь с одним изгибом под 90 градусов
            switch (startDir)
            {
                case ConnectorPosition.Left:
                case ConnectorPosition.Right:
                    // Сначала горизонтально, затем вертикально
                    points.Add(new Point(endPoint.X, startPoint.Y));
                    break;
            }

            points.Add(endPoint);

            return CleanupPath(points);
        }


        private Point SelectOptimalConnectorForPoint(Node node, Point targetPoint, Dictionary<ConnectorPosition, Point> connectorPoints)
        {
            double dx = targetPoint.X - node.X;
            double dy = targetPoint.Y - node.Y;

            var preferredConnector = GetPreferredConnector(node, new Node { X = targetPoint.X, Y = targetPoint.Y }, dx, dy, true);
            return connectorPoints[preferredConnector];
        }

        private void DrawArrow(Point end, Point beforeEnd, IBrush? stroke, double thickness)
        {
            Vector dir = end - beforeEnd;
            if (dir.Length == 0) return;
            dir = dir / dir.Length * 12; // длина стрелки
            double angle = Math.PI / 6; // угол стрелки 30°
            var p1 = end - RotateVector(dir, angle);
            var p2 = end - RotateVector(dir, -angle);
            var arrow = new Polyline
            {
                Points = new Points { end, p1, end, p2 },
                Stroke = stroke,
                StrokeThickness = thickness,
                IsHitTestVisible = false
            };
            Children.Add(arrow);
        }

        private List<Point> GetSelfLoopPoints(Node node)
        {
            var horizontalOffset = 36 * _viewportScale;
            var verticalOffset = 36 * _viewportScale;

            var start = ToScreen(node.RightPoint);
            var end = ToScreen(node.LeftPoint);
            var rightOuterX = start.X + horizontalOffset;
            var leftOuterX = end.X - horizontalOffset;
            var topY = start.Y - node.Size.Height * _viewportScale / 2 - verticalOffset;

            return new List<Point>
            {
                start,
                new Point(rightOuterX, start.Y),
                new Point(rightOuterX, topY),
                new Point(leftOuterX, topY),
                new Point(leftOuterX, end.Y),
                end,
            };
        }

        private Path CreateRoundedPath(List<Point> points, double cornerRadius, IBrush stroke, double thickness)
        {
            return new Path
            {
                Data = CreateRoundedPathGeometry(points, cornerRadius),
                Stroke = stroke,
                StrokeThickness = thickness,
                // IsHitTestVisible оставляем значение по умолчанию; для ребер включаем в RebuildChildren
            };
        }
        private Geometry CreateRoundedPathGeometry(List<Point> points, double cornerRadius)
        {
            if (points == null || points.Count == 0)
                return new PathGeometry();
            if (points.Count == 1)
                return new LineGeometry(points[0], points[0]);
            var pathGeometry = new PathGeometry();
            using (var context = pathGeometry.Open())
            {
                context.BeginFigure(points[0], false);
                for (int i = 1; i < points.Count - 1; i++)
                {
                    Point prev = points[i - 1];
                    Point current = points[i];
                    Point next = points[i + 1];
                    Vector inVec = current - prev;
                    Vector outVec = next - current;
                    double inLength = inVec.Length;
                    double outLength = outVec.Length;
                    if (IsStraightLine(inVec, outVec))
                    {
                        context.LineTo(current);
                        continue;
                    }
                    double radius = Math.Min(cornerRadius, Math.Min(inLength / 2, outLength / 2));
                    if (radius > 0 && inLength > 0 && outLength > 0)
                    {
                        Vector inDir = inVec / inLength;
                        Vector outDir = outVec / outLength;
                        Point arcStart = current - inDir * radius;
                        Point arcEnd = current + outDir * radius;
                        context.LineTo(arcStart);
                        SweepDirection sweepDirection = GetCornerSweepDirection(inDir, outDir);
                        context.ArcTo(arcEnd, new Size(radius, radius), 0, false, sweepDirection);
                    }
                    else
                    {
                        context.LineTo(current);
                    }
                }
                context.LineTo(points[points.Count - 1]);
            }
            return pathGeometry;
        }
        private bool IsStraightLine(Vector inVec, Vector outVec)
        {
            return (Math.Abs(inVec.X + outVec.X) < 0.001 && Math.Abs(inVec.Y + outVec.Y) < 0.001) ||
                   (Math.Abs(inVec.X - outVec.X) < 0.001 && Math.Abs(inVec.Y - outVec.Y) < 0.001);
        }
        private SweepDirection GetCornerSweepDirection(Vector inDir, Vector outDir)
        {
            double angle = Math.Atan2(outDir.Y, outDir.X) - Math.Atan2(inDir.Y, inDir.X);
            if (angle > Math.PI) angle -= 2 * Math.PI;
            if (angle < -Math.PI) angle += 2 * Math.PI;
            return angle > 0 ? SweepDirection.Clockwise : SweepDirection.CounterClockwise;
        }
        private Vector RotateVector(Vector v, double angle)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            return new Vector(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }
        // Возвращает координату указанного коннектора узла
        private Point GetConnectorPoint(Node node, ConnectorPosition pos)
        {
            return pos switch
            {
                ConnectorPosition.Left => ToScreen(node.LeftPoint),
                ConnectorPosition.Right => ToScreen(node.RightPoint),
                _ => ToScreen(new Point(node.X, node.Y))
            };
        }
        // Позиции коннектора
        private enum ConnectorPosition
        {
            Right,
            Left
        }
    }
}
