using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using OldenEraTemplateEditor.Models;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using IOPath = System.IO.Path;

namespace Olden_Era___Template_Editor
{
    /// <summary>
    /// Visual zone-graph editor (the "HotA-style" canvas). Renders an <see cref="RmgTemplate"/>'s
    /// zones as draggable nodes and connections as edges, reusing the preview layout so it matches
    /// the generated map. Supports inspect/edit of zone &amp; connection properties, add/remove of
    /// zones and connections, validation, and round-trip save back to <c>.rmg.json</c>.
    /// </summary>
    public partial class TemplateEditorWindow : Window
    {
        // Colours mirror TemplatePreviewPngWriter so the editor speaks the same visual language.
        private static readonly Color SpawnFill   = Color.FromRgb( 42,  90,  50);
        private static readonly Color SpawnBorder = Color.FromRgb(100, 200, 120);
        private static readonly Color HubFill     = Color.FromRgb( 55,  80,  95);
        private static readonly Color HubBorder   = Color.FromRgb(130, 180, 200);
        private static readonly Color BronzeFill  = Color.FromRgb(101,  67,  33);
        private static readonly Color BronzeBorder= Color.FromRgb(205, 127,  50);
        private static readonly Color SilverFill  = Color.FromRgb( 72,  76,  80);
        private static readonly Color SilverBorder= Color.FromRgb(192, 192, 192);
        private static readonly Color GoldFill    = Color.FromRgb(120,  90,  20);
        private static readonly Color GoldBorder  = Color.FromRgb(255, 210,  50);
        private static readonly Color DirectLine  = Color.FromRgb(180, 145,  60);
        private static readonly Color PortalLine   = Color.FromArgb(200, 90, 170, 210);
        private static readonly Color RoadLine     = Color.FromRgb(150, 120,  90); // dirt road
        private static readonly Color SelectColor = Color.FromRgb(179, 169, 255); // accent violet
        private static readonly Color GridLine     = Color.FromRgb( 30,  36,  51);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        private RmgTemplate _template;
        private MapTopology _topology;
        private string? _currentPath;
        private bool _dirty;

        private readonly Dictionary<string, Point>   _positions  = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Ellipse> _nodeShapes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, FrameworkElement> _nodeLabels = new(StringComparer.Ordinal);
        private readonly List<(Line Line, Connection Conn)> _edges = [];
        private double _radius = 24;

        // Interaction state
        private bool   _isPanning;
        private Point  _panStartScreen;
        private double _panStartTx, _panStartTy;
        private Zone?  _dragZone;
        private Vector _dragGrab;
        private bool   _movedWhileDragging;
        private bool   _connectMode;
        private Zone?  _connectFrom;
        private object? _selected; // Zone or Connection

        public TemplateEditorWindow(RmgTemplate? template = null, MapTopology topology = MapTopology.Default)
        {
            InitializeComponent();
            _template = template ?? NewEmptyTemplate();
            _topology = topology;
            Loaded += (_, _) =>
            {
                ComputePositions();
                RebuildGraph();
                FitToView();
                UpdateTitle();
                UpdateStatus(L("S.EC.Status0", Zones.Count, Connections.Count));
            };
        }

        // ── Model accessors ────────────────────────────────────────────────────────

        private Variant Variant
        {
            get
            {
                _template.Variants ??= [];
                if (_template.Variants.Count == 0) _template.Variants.Add(new Variant());
                return _template.Variants[0];
            }
        }

        private List<Zone> Zones => Variant.Zones ??= [];
        private List<Connection> Connections => Variant.Connections ??= [];

        private static RmgTemplate NewEmptyTemplate() => new()
        {
            Name = L("S.EC.NewTemplate"),
            Variants = [new Variant { Zones = [], Connections = [] }],
        };

        // ── Layout ──────────────────────────────────────────────────────────────────

        private void ComputePositions()
        {
            _positions.Clear();
            try
            {
                var layout = TemplatePreviewPngWriter.ComputeLayout(_template, _topology);
                foreach (var kv in layout) _positions[kv.Key] = kv.Value;
                _radius = Math.Max(16, TemplatePreviewPngWriter.GetLastZoneRadius());
            }
            catch { /* fall through to placement */ }
            PlaceMissingZones();
        }

        /// <summary>Places any zone lacking a computed position on a tidy grid near the centre.</summary>
        private void PlaceMissingZones()
        {
            int slot = 0;
            foreach (var z in Zones)
            {
                if (_positions.ContainsKey(z.Name)) continue;
                int col = slot % 5, row = slot / 5;
                _positions[z.Name] = new Point(140 + col * 110, 140 + row * 110);
                slot++;
            }
        }

        private void RebuildGraph()
        {
            GraphCanvas.Children.Clear();
            _nodeShapes.Clear();
            _nodeLabels.Clear();
            _edges.Clear();

            DrawGrid();

            // Edges first so nodes sit on top.
            foreach (var c in Connections)
            {
                if (!_positions.TryGetValue(c.From, out var p1) ||
                    !_positions.TryGetValue(c.To,   out var p2)) continue;

                var line = new Line
                {
                    X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
                    Tag = c,
                    Cursor = Cursors.Hand,
                };
                StyleEdge(line, c);
                GraphCanvas.Children.Add(line);
                _edges.Add((line, c));
            }

            foreach (var z in Zones)
                if (_positions.TryGetValue(z.Name, out var p))
                    DrawNode(z, p);

            UpdateSelectionVisuals();
        }

        /// <summary>Draws a subtle 50px reference grid that pans/zooms with the graph.</summary>
        private void DrawGrid()
        {
            var cell = new GeometryDrawing
            {
                Geometry = new RectangleGeometry(new Rect(0, 0, 50, 50)),
                Pen = new Pen(new SolidColorBrush(GridLine), 1),
            };
            var brush = new DrawingBrush
            {
                Drawing = cell, TileMode = TileMode.Tile, Stretch = Stretch.None,
                Viewport = new Rect(0, 0, 50, 50), ViewportUnits = BrushMappingMode.Absolute,
            };
            brush.Freeze();
            GraphCanvas.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = GraphCanvas.Width, Height = GraphCanvas.Height, Fill = brush, IsHitTestVisible = false,
            });
        }

        /// <summary>Applies colour/dash/thickness to a connection line based on its type.</summary>
        private static void StyleEdge(Line line, Connection c)
        {
            bool portal = string.Equals(c.ConnectionType, "portal", StringComparison.OrdinalIgnoreCase);
            bool road   = c.Road == true;
            Color color = portal ? PortalLine : road ? RoadLine : DirectLine;
            line.Stroke = new SolidColorBrush(color);
            line.StrokeThickness = portal ? 2.0 : 3.0;
            line.StrokeDashArray = portal ? new DoubleCollection { 4, 3 }
                                  : road   ? new DoubleCollection { 6, 4 }
                                  : null;
        }

        private void DrawNode(Zone z, Point p)
        {
            var (fill, border) = ClassifyZone(z);
            double r = NodeRadius(z);

            var ellipse = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Fill = new SolidColorBrush(fill),
                Stroke = new SolidColorBrush(border),
                StrokeThickness = 2.5,
                Tag = z,
                Cursor = Cursors.SizeAll,
                ToolTip = ZoneTooltip(z),
            };
            Canvas.SetLeft(ellipse, p.X - r);
            Canvas.SetTop(ellipse, p.Y - r);
            ellipse.MouseEnter += (_, _) => { if (!ReferenceEquals(_selected, z)) ellipse.StrokeThickness = 4; };
            ellipse.MouseLeave += (_, _) => { if (!ReferenceEquals(_selected, z)) ellipse.StrokeThickness = 2.5; };
            GraphCanvas.Children.Add(ellipse);
            _nodeShapes[z.Name] = ellipse;

            // Label inside a translucent "pill" so it stays readable over edges/grid.
            var label = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x0E, 0x0B, 0x16)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1, 5, 1),
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = z.Name,
                    Foreground = Brushes.White,
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center,
                },
            };
            PlaceLabel(label, p, r);
            GraphCanvas.Children.Add(label);
            _nodeLabels[z.Name] = label;
        }

        /// <summary>Centres a node label just below the node circle.</summary>
        private static void PlaceLabel(FrameworkElement label, Point p, double r)
        {
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, p.X - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, p.Y + r + 2);
        }

        private double NodeRadius(Zone z)
        {
            double scale = Math.Sqrt(Math.Max(0.4, z.Size ?? 1.0));
            return Math.Clamp(_radius * scale, 14, 48);
        }

        private static (Color Fill, Color Border) ClassifyZone(Zone z)
        {
            string n = z.Name ?? "";
            if (n.StartsWith("Spawn-", StringComparison.OrdinalIgnoreCase)) return (SpawnFill, SpawnBorder);
            if (n.Contains("Hub", StringComparison.OrdinalIgnoreCase))      return (HubFill, HubBorder);
            return z.Layout switch
            {
                "zone_layout_sides"         => (BronzeFill, BronzeBorder),
                "zone_layout_treasure_zone" => (SilverFill, SilverBorder),
                "zone_layout_center"        => (GoldFill,   GoldBorder),
                _                            => (BronzeFill, BronzeBorder),
            };
        }

        private static string ZoneTooltip(Zone z)
        {
            var parts = new List<string> { z.Name };
            if (!string.IsNullOrEmpty(z.Layout)) parts.Add($"layout: {z.Layout}");
            if (z.Size is { } s) parts.Add($"size: {s:0.##}");
            return string.Join("\n", parts);
        }

        // ── Selection & inspector (Phase B) ──────────────────────────────────────────

        private void Select(object? item)
        {
            _selected = item;
            UpdateSelectionVisuals();
            BuildInspector();
        }

        private void UpdateSelectionVisuals()
        {
            foreach (var (line, conn) in _edges)
            {
                if (ReferenceEquals(_selected, conn))
                {
                    line.Stroke = new SolidColorBrush(SelectColor);
                    line.StrokeThickness = 4.5;
                    line.StrokeDashArray = null;
                }
                else StyleEdge(line, conn);
            }
            foreach (var (name, shape) in _nodeShapes)
            {
                bool sel = _selected is Zone z && string.Equals(z.Name, name, StringComparison.Ordinal);
                bool isConnectFrom = _connectFrom is Zone cf && string.Equals(cf.Name, name, StringComparison.Ordinal);
                if (sel || isConnectFrom)
                {
                    shape.Stroke = new SolidColorBrush(SelectColor);
                    shape.StrokeThickness = 4.0;
                }
                else if (_nodeShapes.ContainsKey(name) && Zones.FirstOrDefault(zz => zz.Name == name) is { } zone)
                {
                    var (_, brd) = ClassifyZone(zone);
                    shape.Stroke = new SolidColorBrush(brd);
                    shape.StrokeThickness = 2.5;
                }
            }
        }

        private void BuildInspector()
        {
            InspectorFields.Children.Clear();
            if (_selected is Zone z)
            {
                TxtInspectorHint.Text = L("S.EC.Zone");
                AddTextField(L("S.EC.Name"), z.Name, v => { RenameZone(z, v); });
                AddTextField(L("S.EC.Size"), (z.Size ?? 1.0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.Size = d; MarkDirty(); RefreshNode(z); } });
                AddComboField(L("S.EC.Layout"), KnownValues.ZoneLayouts, z.Layout,
                    v => { z.Layout = v; MarkDirty(); RefreshNode(z); });
                AddTextField(L("S.EC.Diplomacy"), (z.DiplomacyModifier ?? 0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.DiplomacyModifier = d; MarkDirty(); } });
                AddTextField(L("S.EC.GuardMult"), (z.GuardMultiplier ?? 1.0).ToString(CultureInfo.InvariantCulture),
                    v => { if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) { z.GuardMultiplier = d; MarkDirty(); } });

                int conns = Connections.Count(c => c.From == z.Name || c.To == z.Name);
                AddReadOnly(L("S.EC.ConnCount"), conns.ToString());
            }
            else if (_selected is Connection c)
            {
                TxtInspectorHint.Text = L("S.EC.Conn");
                var zoneNames = Zones.Select(zz => zz.Name).ToArray();
                AddComboField(L("S.EC.From"), zoneNames, c.From, v => { c.From = v; MarkDirty(); RebuildGraph(); });
                AddComboField(L("S.EC.To"),  zoneNames, c.To,   v => { c.To = v;   MarkDirty(); RebuildGraph(); });
                AddComboField(L("S.EC.Type"), KnownValues.ConnectionTypes, c.ConnectionType,
                    v => { c.ConnectionType = v; MarkDirty(); RebuildGraph(); });
                AddTextField(L("S.EC.GuardValue"), (c.GuardValue ?? 0).ToString(),
                    v => { if (int.TryParse(v, out var i)) { c.GuardValue = i; MarkDirty(); } });
                AddCheckField(L("S.EC.Road"), c.Road ?? false, v => { c.Road = v; MarkDirty(); });
            }
            else
            {
                TxtInspectorHint.Text = L("S.Ed.011");
            }
        }

        private void AddSectionLabel(string text) =>
            InspectorFields.Children.Add(new TextBlock
            {
                Text = text, Foreground = (Brush)FindResource("BrushTextDim"),
                FontSize = 12, Margin = new Thickness(0, 8, 0, 2),
            });

        private void AddTextField(string label, string value, Action<string> onCommit)
        {
            AddSectionLabel(label);
            var box = new TextBox { Text = value, Margin = new Thickness(0, 0, 0, 4) };
            box.LostFocus += (_, _) => onCommit(box.Text.Trim());
            box.KeyDown += (_, e) => { if (e.Key == Key.Enter) onCommit(box.Text.Trim()); };
            InspectorFields.Children.Add(box);
        }

        private void AddComboField(string label, string[] options, string? value, Action<string> onCommit)
        {
            AddSectionLabel(label);
            var combo = new ComboBox { IsEditable = true, Margin = new Thickness(0, 0, 0, 4) };
            foreach (var o in options) combo.Items.Add(o);
            combo.Text = value ?? "";
            combo.LostFocus += (_, _) => onCommit(combo.Text.Trim());
            combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string s) onCommit(s); };
            InspectorFields.Children.Add(combo);
        }

        private void AddCheckField(string label, bool value, Action<bool> onCommit)
        {
            var chk = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 6, 0, 4) };
            chk.Checked   += (_, _) => onCommit(true);
            chk.Unchecked += (_, _) => onCommit(false);
            InspectorFields.Children.Add(chk);
        }

        private void AddReadOnly(string label, string value)
        {
            AddSectionLabel(label);
            InspectorFields.Children.Add(new TextBlock
            {
                Text = value, Foreground = (Brush)FindResource("BrushText"),
                Margin = new Thickness(0, 0, 0, 4),
            });
        }

        private void RenameZone(Zone z, string newName)
        {
            newName = newName.Trim();
            if (newName.Length == 0 || newName == z.Name) return;
            if (Zones.Any(zz => zz != z && string.Equals(zz.Name, newName, StringComparison.Ordinal)))
            {
                UpdateStatus(L("S.EC.NameTaken", newName));
                return;
            }
            string old = z.Name;
            // Re-point connections + position map.
            foreach (var c in Connections)
            {
                if (c.From == old) c.From = newName;
                if (c.To == old)   c.To = newName;
            }
            if (_positions.Remove(old, out var pt)) _positions[newName] = pt;
            z.Name = newName;
            MarkDirty();
            RebuildGraph();
            BuildInspector();
        }

        private void RefreshNode(Zone z)
        {
            if (!_positions.TryGetValue(z.Name, out var _)) return;
            RebuildGraph();
        }

        // ── Mouse: pan / zoom / drag / connect ───────────────────────────────────────

        private void CanvasHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hit = FindTagged(e.OriginalSource as DependencyObject);

            if (hit is Zone z)
            {
                if (_connectMode)
                {
                    HandleConnectClick(z);
                    e.Handled = true;
                    return;
                }
                Select(z);
                _dragZone = z;
                _movedWhileDragging = false;
                var click = e.GetPosition(GraphCanvas);
                _dragGrab = click - _positions[z.Name];
                CanvasHost.CaptureMouse();
                e.Handled = true;
                return;
            }

            if (hit is Connection c)
            {
                Select(c);
                e.Handled = true;
                return;
            }

            // Double-click on empty space → add a zone right there (experimental quick-add).
            if (e.ClickCount == 2 && !_connectMode)
            {
                AddZoneAt(e.GetPosition(GraphCanvas));
                e.Handled = true;
                return;
            }

            // Empty space → pan (and clear selection).
            Select(null);
            _isPanning = true;
            _panStartScreen = e.GetPosition(CanvasHost);
            _panStartTx = CanvasTranslate.X;
            _panStartTy = CanvasTranslate.Y;
            CanvasHost.CaptureMouse();
        }

        private void CanvasHost_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragZone is not null && e.LeftButton == MouseButtonState.Pressed)
            {
                var p = e.GetPosition(GraphCanvas) - _dragGrab;
                _positions[_dragZone.Name] = p;
                _movedWhileDragging = true;
                RepositionZone(_dragZone, p);
                return;
            }
            if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                var now = e.GetPosition(CanvasHost);
                CanvasTranslate.X = _panStartTx + (now.X - _panStartScreen.X);
                CanvasTranslate.Y = _panStartTy + (now.Y - _panStartScreen.Y);
            }
        }

        private void CanvasHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragZone is not null && _movedWhileDragging)
                UpdateStatus(L("S.EC.ZoneMoved", _dragZone.Name));
            _dragZone = null;
            _isPanning = false;
            if (CanvasHost.IsMouseCaptured) CanvasHost.ReleaseMouseCapture();
        }

        /// <summary>Moves an already-drawn node + its label + incident edges without a full rebuild (smooth drag).</summary>
        private void RepositionZone(Zone z, Point p)
        {
            double r = NodeRadius(z);
            if (_nodeShapes.TryGetValue(z.Name, out var shape))
            {
                Canvas.SetLeft(shape, p.X - r);
                Canvas.SetTop(shape, p.Y - r);
            }
            if (_nodeLabels.TryGetValue(z.Name, out var label))
                PlaceLabel(label, p, r);
            foreach (var (line, conn) in _edges)
            {
                if (conn.From == z.Name) { line.X1 = p.X; line.Y1 = p.Y; }
                if (conn.To   == z.Name) { line.X2 = p.X; line.Y2 = p.Y; }
            }
        }

        private void CanvasHost_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double factor = e.Delta > 0 ? 1.12 : 1 / 1.12;
            double newScale = Math.Clamp(CanvasScale.ScaleX * factor, 0.2, 6.0);
            Point host   = e.GetPosition(CanvasHost);
            Point local  = e.GetPosition(GraphCanvas);
            CanvasScale.ScaleX = CanvasScale.ScaleY = newScale;
            CanvasTranslate.X = host.X - local.X * newScale;
            CanvasTranslate.Y = host.Y - local.Y * newScale;
            TxtZoomLabel.Text = $"{newScale * 100:0}%";
        }

        private static object? FindTagged(DependencyObject? src)
        {
            while (src is not null)
            {
                if (src is FrameworkElement { Tag: Zone z }) return z;
                if (src is FrameworkElement { Tag: Connection c }) return c;
                src = VisualTreeHelper.GetParent(src);
            }
            return null;
        }

        // ── Connect mode ─────────────────────────────────────────────────────────────

        private void HandleConnectClick(Zone z)
        {
            if (_connectFrom is null)
            {
                _connectFrom = z;
                UpdateStatus(L("S.EC.ConnectFrom", z.Name));
                UpdateSelectionVisuals();
                return;
            }
            if (ReferenceEquals(_connectFrom, z))
            {
                _connectFrom = null;
                UpdateStatus(L("S.EC.ConnectCancelled"));
                UpdateSelectionVisuals();
                return;
            }
            var conn = new Connection { From = _connectFrom.Name, To = z.Name, ConnectionType = "default" };
            Connections.Add(conn);
            UpdateStatus(L("S.EC.ConnAdded", conn.From, conn.To));
            _connectFrom = null;
            _connectMode = false;
            BtnConnectMode.Background = null;
            MarkDirty();
            RebuildGraph();
            Select(conn);
        }

        // ── Toolbar handlers ──────────────────────────────────────────────────────────

        private void BtnConnectMode_Click(object sender, RoutedEventArgs e)
        {
            _connectMode = !_connectMode;
            _connectFrom = null;
            BtnConnectMode.Background = _connectMode ? new SolidColorBrush(Color.FromRgb(60, 50, 90)) : null;
            UpdateStatus(_connectMode ? L("S.EC.ConnectModeOn") : L("S.EC.ConnectModeOff"));
            UpdateSelectionVisuals();
        }

        private void BtnAddZone_Click(object sender, RoutedEventArgs e) => AddZoneAt(ViewportCenterInCanvas());

        /// <summary>Adds a new zone at the given canvas position and selects it.</summary>
        private void AddZoneAt(Point pos)
        {
            string name = UniqueZoneName();
            var z = new Zone { Name = name, Size = 1.0, Layout = "zone_layout_sides" };
            Zones.Add(z);
            _positions[name] = pos;
            MarkDirty();
            RebuildGraph();
            Select(z);
            UpdateStatus(L("S.EC.ZoneAdded", name));
        }

        private string UniqueZoneName()
        {
            for (int i = 1; ; i++)
            {
                string n = $"Zone-{i}";
                if (!Zones.Any(z => string.Equals(z.Name, n, StringComparison.Ordinal))) return n;
            }
        }

        private Point ViewportCenterInCanvas()
        {
            double scale = CanvasScale.ScaleX <= 0 ? 1 : CanvasScale.ScaleX;
            double cx = (CanvasHost.ActualWidth / 2 - CanvasTranslate.X) / scale;
            double cy = (CanvasHost.ActualHeight / 2 - CanvasTranslate.Y) / scale;
            return new Point(cx, cy);
        }

        // ── Keyboard & zoom buttons ──────────────────────────────────────────────

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Don't hijack keys while typing in the inspector.
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox or System.Windows.Controls.ComboBox)
                return;

            if (e.Key == Key.Delete)
            {
                BtnDelete_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_connectMode)
                {
                    _connectMode = false; _connectFrom = null; BtnConnectMode.Background = null;
                    UpdateSelectionVisuals();
                    UpdateStatus(L("S.EC.ConnectModeOff"));
                }
                else Select(null);
                e.Handled = true;
            }
        }

        /// <summary>Experimental: export the zone graph (at natural scale, with grid + labels) to a PNG.</summary>
        private void BtnExportPng_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = L("S.EC.ExportTitle"), Filter = "PNG (*.png)|*.png",
                DefaultExt = ".png", FileName = "zone-graph.png",
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                // Render the canvas at 1:1 (ignore the current zoom/pan), then restore the view.
                double sx = CanvasScale.ScaleX, sy = CanvasScale.ScaleY, tx = CanvasTranslate.X, ty = CanvasTranslate.Y;
                CanvasScale.ScaleX = CanvasScale.ScaleY = 1; CanvasTranslate.X = 0; CanvasTranslate.Y = 0;
                GraphCanvas.UpdateLayout();

                int w = (int)GraphCanvas.Width, h = (int)GraphCanvas.Height;
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(GraphCanvas);

                CanvasScale.ScaleX = sx; CanvasScale.ScaleY = sy; CanvasTranslate.X = tx; CanvasTranslate.Y = ty;

                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using (var fs = File.Create(dlg.FileName)) encoder.Save(fs);
                UpdateStatus(L("S.EC.Exported", IOPath.GetFileName(dlg.FileName)));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L("S.EC.SaveErr", ex.Message), L("S.EC.Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)  => ZoomBy(1.2);
        private void BtnZoomOut_Click(object sender, RoutedEventArgs e) => ZoomBy(1 / 1.2);

        /// <summary>Zooms by a factor around the viewport centre.</summary>
        private void ZoomBy(double factor)
        {
            double oldScale = CanvasScale.ScaleX <= 0 ? 1 : CanvasScale.ScaleX;
            double newScale = Math.Clamp(oldScale * factor, 0.2, 6.0);
            double cx = CanvasHost.ActualWidth / 2, cy = CanvasHost.ActualHeight / 2;
            double localX = (cx - CanvasTranslate.X) / oldScale;
            double localY = (cy - CanvasTranslate.Y) / oldScale;
            CanvasScale.ScaleX = CanvasScale.ScaleY = newScale;
            CanvasTranslate.X = cx - localX * newScale;
            CanvasTranslate.Y = cy - localY * newScale;
            TxtZoomLabel.Text = $"{newScale * 100:0}%";
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selected is Zone z)
            {
                int removedConns = Connections.RemoveAll(c => c.From == z.Name || c.To == z.Name);
                Zones.Remove(z);
                _positions.Remove(z.Name);
                MarkDirty();
                RebuildGraph();
                Select(null);
                UpdateStatus(L("S.EC.ZoneDeleted", z.Name, removedConns));
            }
            else if (_selected is Connection c)
            {
                Connections.Remove(c);
                MarkDirty();
                RebuildGraph();
                Select(null);
                UpdateStatus(L("S.EC.ConnDeleted", c.From, c.To));
            }
            else UpdateStatus(L("S.EC.NothingToDelete"));
        }

        private void BtnValidate_Click(object sender, RoutedEventArgs e)
        {
            var issues = Validate();
            if (issues.Count == 0)
            {
                UpdateStatus(L("S.EC.NoIssues"));
                MessageBox.Show(this, L("S.EC.NoIssuesMsg"), L("S.EC.ValidateTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            UpdateStatus(L("S.EC.IssuesFound", issues.Count));
            MessageBox.Show(this, string.Join("\n", issues.Take(30)), L("S.EC.IssuesTitle", issues.Count),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private List<string> Validate() => ZoneGraphValidator.Validate(Zones, Connections);

        private void BtnRelayout_Click(object sender, RoutedEventArgs e)
        {
            ComputePositions();
            RebuildGraph();
            FitToView();
            UpdateStatus(L("S.EC.Relayout"));
        }

        private void BtnZoomReset_Click(object sender, RoutedEventArgs e) => FitToView();

        private void FitToView()
        {
            if (_positions.Count == 0 || CanvasHost.ActualWidth <= 0) { return; }
            double minX = _positions.Values.Min(p => p.X), maxX = _positions.Values.Max(p => p.X);
            double minY = _positions.Values.Min(p => p.Y), maxY = _positions.Values.Max(p => p.Y);
            double pad = 70;
            double w = Math.Max(1, maxX - minX + pad * 2);
            double h = Math.Max(1, maxY - minY + pad * 2);
            double scale = Math.Clamp(Math.Min(CanvasHost.ActualWidth / w, CanvasHost.ActualHeight / h), 0.2, 3.0);
            CanvasScale.ScaleX = CanvasScale.ScaleY = scale;
            CanvasTranslate.X = (CanvasHost.ActualWidth  - (minX + maxX) * scale) / 2;
            CanvasTranslate.Y = (CanvasHost.ActualHeight - (minY + maxY) * scale) / 2;
            TxtZoomLabel.Text = $"{scale * 100:0}%";
        }

        // ── Load / Save (Phase C round-trip) ─────────────────────────────────────────

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = L("S.EC.LoadTitle"),
                Filter = L("S.EC.LoadFilter"),
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var json = File.ReadAllText(dlg.FileName);
                var loaded = JsonSerializer.Deserialize<RmgTemplate>(json, JsonOptions);
                if (loaded is null) { UpdateStatus(L("S.EC.LoadFail")); return; }
                _template = loaded;
                _topology = MapTopology.Default;
                _currentPath = dlg.FileName;
                _dirty = false;
                UpdateTitle();
                _selected = null; _connectFrom = null; _connectMode = false;
                ComputePositions();
                RebuildGraph();
                FitToView();
                BuildInspector();
                UpdateStatus(L("S.EC.Loaded", IOPath.GetFileName(dlg.FileName), Zones.Count, Connections.Count));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L("S.EC.LoadErr", ex.Message), L("S.EC.Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var issues = Validate();
            if (issues.Count > 0)
            {
                var go = MessageBox.Show(this,
                    L("S.EC.SaveValidate", issues.Count, string.Join("\n", issues.Take(10))),
                    L("S.EC.SaveValidateTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (go != MessageBoxResult.Yes) return;
            }

            var dlg = new SaveFileDialog
            {
                Title = L("S.EC.SaveTitle"),
                Filter = L("S.EC.SaveFilter"),
                DefaultExt = ".rmg.json",
                FileName = _currentPath is not null ? IOPath.GetFileName(_currentPath)
                                                    : $"{_template.Name}.rmg.json",
            };
            if (_currentPath is not null) dlg.InitialDirectory = IOPath.GetDirectoryName(_currentPath);
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(_template, JsonOptions));
                _currentPath = dlg.FileName;
                _dirty = false;
                UpdateTitle();
                UpdateStatus(L("S.EC.Saved", IOPath.GetFileName(dlg.FileName)));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L("S.EC.SaveErr", ex.Message), L("S.EC.Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Misc ──────────────────────────────────────────────────────────────────────

        /// <summary>Test/screenshot hook: select a zone by name (used by --shoot-editor verification).</summary>
        internal void DebugSelectZone(string name)
        {
            var z = Zones.FirstOrDefault(zz => string.Equals(zz.Name, name, StringComparison.Ordinal));
            if (z is not null) Select(z);
        }

        private void MarkDirty() { _dirty = true; UpdateTitle(); }

        private void UpdateTitle()
        {
            string file = _currentPath is not null ? IOPath.GetFileName(_currentPath) : L("S.CB.Untitled");
            Title = L("S.EC.WinTitle", file) + (_dirty ? "*" : "");
        }

        private void UpdateStatus(string text) => TxtStatus.Text = text;

        /// <summary>Localization shortcut.</summary>
        private static string L(string key, params object[] args) => Services.Localization.LocalizationManager.T(key, args);

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_dirty)
            {
                var r = MessageBox.Show(this,
                    L("S.EC.UnsavedMsg"),
                    L("S.EC.UnsavedTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) { e.Cancel = true; return; }
            }
            base.OnClosing(e);
        }
    }
}
