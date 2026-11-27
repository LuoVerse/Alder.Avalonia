using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Alder.Avalonia.Controls;

internal sealed class WorkflowCanvas : Canvas
{
    public const double GraphWidth = 840;
    public const double GraphHeight = 704;
    private const double NodeWidth = 222;
    private const double NodeHeight = 78;

    private readonly Wires wires;
    private readonly Dictionary<WorkflowNode, NodeCard> cards = [];

    private WorkflowGraph? workflow;
    private WorkflowNode? selected;

    public event Action<WorkflowNode>? NodeSelected;
    public event Action? NodeDragged;

    public WorkflowCanvas()
    {
        Width = GraphWidth;
        Height = GraphHeight;
        wires = new Wires(this) { Width = Width, Height = Height, IsHitTestVisible = false };
        Children.Add(wires);
    }

    public void Update(WorkflowGraph value, WorkflowNode? selection, Func<string, string> fieldName,
        WorkflowAssigneeSources sources)
    {
        workflow = value;
        selected = selection;
        foreach (var removed in cards.Keys.Except(value.Nodes).ToArray())
        {
            Children.Remove(cards[removed]);
            cards.Remove(removed);
        }

        foreach (var node in value.Nodes)
        {
            if (!cards.TryGetValue(node, out var card))
            {
                card = new NodeCard(this, node);
                cards.Add(node, card);
                Children.Add(card);
            }
            Position(card, node.Position);
            card.Update(node == selection, fieldName, sources);
        }
        wires.InvalidateRoutes();
    }

    public void SelectCard(WorkflowNode node)
    {
        if (selected is { } previous && cards.TryGetValue(previous, out var oldCard)) oldCard.SetSelected(false);
        selected = node;
        if (cards.TryGetValue(node, out var newCard)) newCard.SetSelected(true);
        wires.InvalidateVisual();
    }

    public Point FreePosition()
    {
        // Start near the top and use the gaps in the current graph.
        for (int y = 44; y + NodeHeight < GraphHeight - 28; y += 17)
        for (int x = 37; x + NodeWidth < GraphWidth - 24; x += 17)
        {
            var next = new Rect(new Point(x, y), new Size(NodeWidth, NodeHeight)).Inflate(10);
            if (workflow is not null && workflow.Nodes.All(node => !next.Intersects(NodeBounds(node))))
                return next.TopLeft + new Vector(10, 10);
        }
        return new Point(40, GraphHeight - NodeHeight - 36);
    }

    private void Select(WorkflowNode node) => NodeSelected?.Invoke(node);

    private void Move(NodeCard card, WorkflowNode node, Point point)
    {
        node.Position = new Point(Math.Clamp(point.X, 16, GraphWidth - NodeWidth - 16),
            Math.Clamp(point.Y, 16, GraphHeight - NodeHeight - 30));
        Position(card, node.Position);
        wires.InvalidateRoutes();
    }

    private static void Position(Control child, Point position)
    {
        SetLeft(child, position.X);
        SetTop(child, position.Y);
    }

    private static Rect NodeBounds(WorkflowNode node) => new(node.Position, new Size(NodeWidth, NodeHeight));

    internal static bool ContainsNodePosition(Point position) =>
        position.X >= 0 && position.X <= GraphWidth - NodeWidth && position.Y >= 0 && position.Y <= GraphHeight - NodeHeight;

    internal static string KindName(WorkflowNodeKind kind) => kind switch
    {
        WorkflowNodeKind.Start => "开始",
        WorkflowNodeKind.SingleApproval => "单人审批",
        WorkflowNodeKind.Countersign => "会签审批",
        WorkflowNodeKind.Condition => "条件分支",
        WorkflowNodeKind.Copy => "抄送",
        _ => "结束"
    };

    internal static string IconKey(WorkflowNodeKind kind) => kind switch
    {
        WorkflowNodeKind.Start => "AlderIconChevronRightCircle",
        WorkflowNodeKind.SingleApproval => "AlderIconAvatarPerson",
        WorkflowNodeKind.Countersign => "AlderIconCheckCircleFilled",
        WorkflowNodeKind.Condition => "WorkflowBranchIcon",
        WorkflowNodeKind.Copy => "AlderIconEnvelope",
        _ => "AlderIconCheckCircle"
    };

    internal static string Tone(WorkflowNodeKind kind) => kind switch
    {
        WorkflowNodeKind.Start => "AlderSoftInk",
        WorkflowNodeKind.Condition => "WorkflowAmberInk",
        WorkflowNodeKind.SingleApproval or WorkflowNodeKind.Countersign => "WorkflowVioletInk",
        WorkflowNodeKind.End => "WorkflowGreenInk",
        _ => "AlderMuted"
    };

    internal static string Surface(WorkflowNodeKind kind) => kind switch
    {
        WorkflowNodeKind.Start => "WorkflowBlueSurface",
        WorkflowNodeKind.Condition => "WorkflowAmberSurface",
        WorkflowNodeKind.SingleApproval or WorkflowNodeKind.Countersign => "WorkflowVioletSurface",
        WorkflowNodeKind.End => "WorkflowGreenSurface",
        _ => "AlderNeutral"
    };

    internal static IBrush Brush(Control control, string key) =>
        control.TryFindResource(key, control.ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Transparent;

    private sealed class NodeCard : Decorator
    {
        private readonly WorkflowCanvas owner;
        private readonly WorkflowNode node;
        private readonly AlderCard card;
        private readonly TextBlock title;
        private readonly TextBlock details;
        private readonly Border iconBox;
        private readonly AlderPathIcon icon;
        private readonly BrushTransition borderTransition = new()
        {
            Property = AlderCard.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(150), Easing = new CubicEaseOut()
        };
        private IPointer? pointer;
        private Point grab;
        private bool isSelected;
        private Point dragStart;

        public NodeCard(WorkflowCanvas owner, WorkflowNode node)
        {
            this.owner = owner;
            this.node = node;
            Width = NodeWidth;
            Height = NodeHeight;
            Cursor = new Cursor(StandardCursorType.Hand);
            Focusable = true;
            FocusAdorner = null;

            icon = new AlderPathIcon { Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            iconBox = new Border { Child = icon, CornerRadius = new CornerRadius(12), Width = 36, Height = 36 };
            title = new AlderTextBlock
            {
                FontSize = 15, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            details = new AlderTextBlock { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
            var main = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Children = { title, details } };
            var panel = new Grid { ColumnDefinitions = new ColumnDefinitions("36,*"), ColumnSpacing = 10 };
            Grid.SetColumn(main, 1);
            panel.Children.Add(iconBox);
            panel.Children.Add(main);
            card = new AlderCard { Content = panel, BorderThickness = new Thickness(1) };
            Child = card;
        }

        public void Update(bool selection, Func<string, string> fieldName, WorkflowAssigneeSources sources)
        {
            iconBox.Background = Brush(owner, Surface(node.Kind));
            icon.Data = owner.FindResource(IconKey(node.Kind)) as Geometry;
            icon.Theme = owner.FindResource("AlderFittedPathIconTheme") as ControlTheme;
            icon.Foreground = Brush(owner, Tone(node.Kind));
            title.Foreground = Brush(owner, "AlderInk");
            details.Foreground = Brush(owner, "AlderMuted");
            title.Text = string.IsNullOrWhiteSpace(node.Name) ? "未命名节点" : node.Name;
            details.Text = node.Kind switch
            {
                WorkflowNodeKind.Start => "提供申请表单数据",
                WorkflowNodeKind.Condition => node.Rule.FieldKey is { } key
                    ? node.Rule.IsConfigured ? $"{fieldName(key)} {node.Rule.DescribeBranch(true)}" : fieldName(key)
                    : "选择判断字段",
                WorkflowNodeKind.SingleApproval => $"{(node.Assignee.Id is not null ? sources.Name(node.Assignee) : "选择审批对象")}{DeadlineLabel(node)}",
                WorkflowNodeKind.Countersign => $"会签 · {node.Signers.Length} 人{DeadlineLabel(node)}",
                WorkflowNodeKind.Copy => node.Recipients.Length > 0 ? string.Join("、", node.Recipients.Select(sources.Name))
                    : node.LegacyRecipients ?? "选择抄送对象",
                _ => "记录审批结果"
            };

            bool reducedMotion = AlderMotion.GetReduced(this);
            if (card.Transitions is null && !reducedMotion) card.Transitions = new Transitions { borderTransition };
            else if (card.Transitions is not null && reducedMotion) card.Transitions = null;
            SetSelected(selection);
            AutomationProperties.SetName(this, $"{KindName(node.Kind)}，{title.Text}，{details.Text}");
            ToolTip.SetTip(this, $"{title.Text}\n{details.Text}");
        }

        public void SetSelected(bool selection)
        {
            isSelected = selection;
            ZIndex = selection ? 1 : 0;
            UpdateBorder();
        }

        private static string DeadlineLabel(WorkflowNode node) => node.HasDeadline ? $" · {node.Hours} 小时" : "";

        private void UpdateBorder()
        {
            bool keyboardFocus = Classes.Contains(":focus-visible");
            card.Background = Brush(owner, keyboardFocus ? "AlderFieldHover" : "AlderSurface");
            card.BorderBrush = Brush(owner, isSelected ? "AlderAccent"
                : IsPointerOver || keyboardFocus ? "AlderMuted" : "AlderBorder");
        }

        protected override void OnPointerEntered(PointerEventArgs e) { base.OnPointerEntered(e); UpdateBorder(); }
        protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); UpdateBorder(); }
        protected override void OnGotFocus(FocusChangedEventArgs e) { base.OnGotFocus(e); UpdateBorder(); }
        protected override void OnLostFocus(FocusChangedEventArgs e) { base.OnLostFocus(e); UpdateBorder(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key is Key.Enter or Key.Space)
            {
                owner.Select(node);
                e.Handled = true;
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            Focus(NavigationMethod.Pointer);
            owner.Select(node);
            grab = e.GetPosition(this);
            dragStart = node.Position;
            pointer = e.Pointer;
            pointer.Capture(this);
            e.PreventGestureRecognition();
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (pointer != e.Pointer) return;
            owner.Move(this, node, e.GetPosition(owner) - new Vector(grab.X, grab.Y));
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (pointer != e.Pointer || e.InitialPressMouseButton != MouseButton.Left) return;
            pointer = null;
            e.Pointer.Capture(null);
            CompleteDrag();
            e.Handled = true;
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            if (pointer != e.Pointer) return;
            pointer = null;
            CompleteDrag();
        }
        private void CompleteDrag()
        {
            if (dragStart != node.Position) owner.NotifyNodeDragged();
        }
    }

    private void NotifyNodeDragged() => NodeDragged?.Invoke();

    private sealed class Wires(WorkflowCanvas owner) : Control
    {
        private readonly Dictionary<(WorkflowNode Node, WorkflowRoute Route), StreamGeometry> paths = [];

        public void InvalidateRoutes()
        {
            paths.Clear();
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            var workflow = owner.workflow;
            if (workflow is null) return;
            foreach (var node in workflow.Nodes)
                foreach (var (route, target) in node.Links) DrawRoute(node, route, target);

            void DrawRoute(WorkflowNode source, WorkflowRoute route, WorkflowNode target)
            {
                bool rejected = route == WorkflowRoute.Reject;
                bool selected = owner.selected == source && !rejected;
                var ink = Brush(owner, rejected ? "WorkflowRejectLine" : selected ? "AlderAccent" : "WorkflowLine");
                var pen = new Pen(ink, selected ? 1.9 : 1.35, rejected ? new DashStyle([4, 4], 0) : null, PenLineCap.Round);
                var from = NodeBounds(source);
                var to = NodeBounds(target);
                if (rejected)
                {
                    DrawReject(context, pen, source, from, to);
                    return;
                }

                var x = route switch
                {
                    WorkflowRoute.Match => from.Left + from.Width * .76,
                    WorkflowRoute.Otherwise => from.Left + from.Width * .24,
                    _ => from.Center.X
                };
                var entry = source.IsApproval && target.Kind == WorkflowNodeKind.Copy
                    ? to.Left + to.Width * (from.Center.X < to.Center.X ? .26 : .74) : to.Center.X;
                var start = new Point(x, from.Bottom);
                var end = new Point(entry, to.Top - 3);
                var middle = (start.Y + end.Y) / 2;
                DrawPoints(context, pen, source, route, [start, new(x, middle), new(end.X, middle), end]);
                Arrow(context, pen, end, new Vector(0, 1));
                if (route is WorkflowRoute.Match or WorkflowRoute.Otherwise)
                {
                    DrawCaption(context, source.Rule.DescribeBranch(route == WorkflowRoute.Match),
                        new Point((start.X + end.X) / 2, middle - 15), selected ? "AlderSoftInk" : "AlderMuted");
                }
                else if (route == WorkflowRoute.Approve)
                    DrawCaption(context, "同意", new Point(start.X, start.Y + 15), selected ? "AlderSoftInk" : "AlderMuted");
            }
        }

        private void DrawReject(DrawingContext context, Pen pen, WorkflowNode source, Rect from, Rect to)
        {
            bool useLeft = from.Center.X < to.Center.X;
            var start = new Point(useLeft ? from.Left : from.Right, from.Center.Y);
            var end = new Point(useLeft ? to.Left - 3 : to.Right + 3, to.Center.Y);
            double rail = useLeft ? Math.Max(5, Math.Min(start.X, end.X) - 28)
                : Math.Min(GraphWidth - 5, Math.Max(start.X, end.X) + 28);
            DrawPoints(context, pen, source, WorkflowRoute.Reject, [start, new(rail, start.Y), new(rail, end.Y), end]);
            Arrow(context, pen, end, new Vector(useLeft ? 1 : -1, 0));
            DrawCaption(context, "拒绝", new Point(rail + (useLeft ? 17 : -17), start.Y + 18), "AlderDangerInk");
        }

        private void DrawPoints(DrawingContext context, Pen pen, WorkflowNode source, WorkflowRoute route, ReadOnlySpan<Point> points)
        {
            if (!paths.TryGetValue((source, route), out var path))
            {
                path = BuildPath(points);
                paths.Add((source, route), path);
            }
            context.DrawGeometry(null, pen, path);
        }

        private static StreamGeometry BuildPath(ReadOnlySpan<Point> points)
        {
            var path = new StreamGeometry();
            using (var geometry = path.Open())
            {
                geometry.BeginFigure(points[0], false);
                for (int i = 1; i < points.Length - 1; i++)
                {
                    var prior = points[i - 1];
                    var corner = points[i];
                    var next = points[i + 1];
                    var a = (Vector)(prior - corner);
                    var b = (Vector)(next - corner);
                    double radius = Math.Min(9, Math.Min(a.Length, b.Length) / 2);
                    if (radius <= 0) { geometry.LineTo(corner); continue; }
                    geometry.LineTo(corner + a * (radius / a.Length));
                    geometry.QuadraticBezierTo(corner, corner + b * (radius / b.Length));
                }
                geometry.LineTo(points[^1]);
                geometry.EndFigure(false);
            }
            return path;
        }

        private static void Arrow(DrawingContext context, Pen pen, Point end, Vector direction)
        {
            var origin = end - direction * 5;
            var side = new Vector(direction.Y, -direction.X) * 3;
            context.DrawLine(new Pen(pen.Brush, pen.Thickness), origin + side, end);
            context.DrawLine(new Pen(pen.Brush, pen.Thickness), origin - side, end);
        }

        private void DrawCaption(DrawingContext context, string value, Point point, string color)
        {
            var typeface = new Typeface(TextElement.GetFontFamily(owner), weight: FontWeight.Medium);
            var text = new FormattedText(value, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, Brush(owner, color))
            {
                MaxTextWidth = 155, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis
            };
            var origin = new Point(point.X - text.Width / 2, point.Y - text.Height / 2);
            context.DrawRectangle(Brush(owner, "WorkflowCanvasBrush"), null,
                new Rect(origin, new Size(text.Width, text.Height)).Inflate(new Thickness(5, 1)), 5, 5);
            context.DrawText(text, origin);
        }
    }
}
