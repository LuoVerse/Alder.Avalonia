using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Alder.Avalonia.Controls;

/// <summary>Edits a workflow graph and sends immutable workflow snapshots to the host's save command.</summary>
public sealed partial class AlderWorkflowEditor : UserControl
{
    /// <summary>Identifies the <see cref="Workflow"/> Avalonia property.</summary>
    public static readonly StyledProperty<AlderWorkflowDefinition?> WorkflowProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, AlderWorkflowDefinition?>(nameof(Workflow), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Identifies the <see cref="SaveCommand"/> Avalonia property.</summary>
    public static readonly StyledProperty<ICommand?> SaveCommandProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, ICommand?>(nameof(SaveCommand));

    /// <summary>Gets or sets the loaded definition. Two-way bindings receive edits and completed node drags.</summary>
    public AlderWorkflowDefinition? Workflow { get => GetValue(WorkflowProperty); set => SetValue(WorkflowProperty, value); }

    /// <summary>Invoked by the Save button with the current <see cref="AlderWorkflowDefinition"/> as its parameter.</summary>
    public ICommand? SaveCommand { get => GetValue(SaveCommandProperty); set => SetValue(SaveCommandProperty, value); }

    private readonly WorkflowCanvas canvas = new();
    private readonly AlderContentControl paletteHost = new();
    private readonly AlderContentControl inspectorHost = new();
    private readonly TextBlock zoomLabel;
    private readonly TextBlock summary;
    private readonly LayoutTransformControl scaledCanvas;
    private readonly AlderScrollViewer viewport;
    private readonly AlderButton saveButton;
    private readonly TextBlock workflowTitle = Text("", 22, weight: FontWeight.SemiBold);
    private WorkflowAssigneeSources sources = new([], []);
    private WorkflowGraph workflow = WorkflowGraph.New();
    private WorkflowNode? selected;
    private bool attached;
    private bool publishing;
    private bool compactLayout;
    private bool stackedLayout;
    private bool fit = true;
    private double zoom = 1;

    /// <summary>Creates an editor that can be initialized by binding a workflow and the host's catalogs.</summary>
    public AlderWorkflowEditor()
    {
        Resources.MergedDictionaries.Add(WorkflowResources.Create());
        this[!BackgroundProperty] = new DynamicResourceExtension("WorkflowShellBrush");
        selected = workflow.StartNode;
        UpdateWorkflowTitle();

        summary = Text("", 12, "AlderMuted");
        saveButton = new AlderButton { Content = "保存", Size = AlderSize.Small, IsVisible = false };
        saveButton[!AlderButton.IconProperty] = new DynamicResourceExtension("AlderIconCheck");
        zoomLabel = Text("100%", 12, "AlderMuted", FontWeight.Medium);
        zoomLabel.Width = 47;
        zoomLabel.TextAlignment = TextAlignment.Center;

        scaledCanvas = new LayoutTransformControl
        {
            Child = canvas, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            LayoutTransform = new ScaleTransform(1, 1)
        };
        viewport = Scroll(scaledCanvas, horizontal: true);
        viewport.Name = "GraphViewport";
        viewport.SizeChanged += (_, _) => { if (fit) FitCanvas(); };

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("194,*,320"), RowDefinitions = new RowDefinitions("*,0"),
            ColumnSpacing = 12, Margin = new Thickness(14)
        };
        var inspector = PanelSurface(Scroll(inspectorHost), variant: SurfaceVariant.Secondary);
        body.SizeChanged += (_, e) =>
        {
            bool compact = e.NewSize.Width < 1150;
            bool stacked = e.NewSize.Width < 740;
            if (compactLayout == compact && stackedLayout == stacked) return;
            compactLayout = compact;
            stackedLayout = stacked;
            body.ColumnDefinitions[0].Width = new GridLength(stacked ? 138 : compact ? 156 : 194);
            body.ColumnDefinitions[2].Width = new GridLength(stacked ? 0 : compact ? 292 : 320);
            body.RowDefinitions[1].Height = stacked ? new GridLength(.8, GridUnitType.Star) : new GridLength(0);
            body.RowSpacing = stacked ? 12 : 0;
            Grid.SetColumn(inspector, stacked ? 0 : 2);
            Grid.SetRow(inspector, stacked ? 1 : 0);
            Grid.SetColumnSpan(inspector, stacked ? 3 : 1);
            foreach (var entry in paletteNodes.Values) entry.Label.MaxWidth = PaletteLabelWidth;
        };
        Place(body, PanelSurface(Scroll(paletteHost)), 0);
        Place(body, BuildCanvasPanel(), 1);
        Place(body, inspector, 2);
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        layout.Children.Add(BuildHeader());
        Grid.SetRow(body, 1);
        layout.Children.Add(body);
        Content = layout;

        canvas.NodeSelected += SelectNode;
        canvas.NodeDragged += PublishWorkflow;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        attached = true;
        ReadCatalogs();
        RefreshAll();
        UpdateSaveButton();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        attached = false;
        StopObservingCatalogs();
        base.OnDetachedFromVisualTree(e);
    }

    private Control BuildHeader()
    {
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(14, 10, 14, 0)
        };
        var mark = IconTile("WorkflowBranchIcon", "WorkflowBlueSurface", "AlderSoftInk", 32);
        mark.IsHitTestVisible = false;
        mark.VerticalAlignment = VerticalAlignment.Center;
        workflowTitle.HorizontalAlignment = HorizontalAlignment.Left;
        workflowTitle.TextTrimming = TextTrimming.CharacterEllipsis;
        workflowTitle.Margin = new Thickness(10, 0, 0, 0);
        saveButton.Margin = new Thickness(12, 0, 0, 0);
        saveButton.VerticalAlignment = VerticalAlignment.Center;
        // The button theme draws the keyboard focus ring outside the button bounds.
        saveButton.ClipToBounds = false;
        Place(header, mark, 0);
        Place(header, workflowTitle, 1);
        Place(header, saveButton, 2);
        return header;
    }

    private AlderSurface BuildCanvasPanel()
    {
        var frame = new Grid { RowDefinitions = new RowDefinitions("52,*,48") };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(18, 0, 15, 0) };
        Place(header, Row(11, Text("流程画布", 15, weight: FontWeight.SemiBold), summary), 0);
        frame.Children.Add(header);

        var field = new Grid { ClipToBounds = true };
        field.Children.Add(new DotField { IsHitTestVisible = false });
        field.Children.Add(viewport);
        var viewportSurface = new Border { Child = field, BorderThickness = new Thickness(0, 1) };
        viewportSurface[!Border.BorderBrushProperty] = new DynamicResourceExtension("AlderBorder");
        Grid.SetRow(viewportSurface, 1);
        frame.Children.Add(viewportSurface);

        var legend = Row(14,
            Row(6, new LineMark { VerticalAlignment = VerticalAlignment.Center }, Text("正常流转", 11, "AlderMuted")),
            Row(6, new LineMark { Rejected = true, VerticalAlignment = VerticalAlignment.Center }, Text("拒绝后结束", 11, "AlderMuted")));
        var scale = Row(1, Action("缩小", ButtonVariant.Ghost, "AlderIconMinus", () => ChangeZoom(-.10), true),
            zoomLabel, Action("放大", ButtonVariant.Ghost, "AlderIconPlus", () => ChangeZoom(.10), true),
            Action("适应画布", ButtonVariant.Ghost, "AlderIconResize", FitCanvas, true));
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(16, 0, 10, 0) };
        Place(footer, legend, 0);
        Place(footer, scale, 1);
        footer.SizeChanged += (_, e) => legend.IsVisible = e.NewSize.Width >= 295;
        Grid.SetRow(footer, 2);
        frame.Children.Add(footer);
        return PanelSurface(frame);
    }

    private readonly Dictionary<WorkflowNode, (AlderButton Button, TextBlock Label)> paletteNodes = [];
    private double PaletteLabelWidth => stackedLayout ? 64 : compactLayout ? 80 : 114;

    private void BuildPalette()
    {
        paletteNodes.Clear();
        var body = new StackPanel { Spacing = 5, Margin = new Thickness(12, 16) };
        body.Children.Add(Text("节点库", 15, weight: FontWeight.SemiBold));
        var palette = new StackPanel { Spacing = 3, Margin = new Thickness(0, 5, 0, 2) };
        foreach (var kind in new[]
        {
            WorkflowNodeKind.Start, WorkflowNodeKind.SingleApproval, WorkflowNodeKind.Countersign, WorkflowNodeKind.Condition, WorkflowNodeKind.Copy, WorkflowNodeKind.End
        })
        {
            var button = Action("", ButtonVariant.Ghost, null, () => AddNode(kind));
            button.Content = Row(8,
                IconTile(WorkflowCanvas.IconKey(kind), WorkflowCanvas.Surface(kind), WorkflowCanvas.Tone(kind), 25),
                Text(WorkflowCanvas.KindName(kind), 13, weight: FontWeight.Medium));
            button.IsEnabled = kind != WorkflowNodeKind.Start || workflow.StartNode is null;
            button.FullWidth = true;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            AutomationProperties.SetName(button, $"添加{WorkflowCanvas.KindName(kind)}节点");
            palette.Children.Add(button);
        }
        body.Children.Add(palette);

        var divider = Separator();
        divider.Margin = new Thickness(2, 8, 2, 8);
        body.Children.Add(divider);
        body.Children.Add(Text("流程节点", 13, weight: FontWeight.SemiBold));
        foreach (var node in workflow.Nodes)
        {
            var button = Action("", node == selected ? ButtonVariant.Secondary : ButtonVariant.Ghost, null, () => SelectNode(node));
            var label = Text(node.Name, 12, node == selected ? "AlderSoftInk" : "AlderInk");
            label.MaxWidth = PaletteLabelWidth;
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            button.Content = Row(7, Glyph(WorkflowCanvas.IconKey(node.Kind), WorkflowCanvas.Tone(node.Kind), 15), label);
            AlderToolTip.SetTip(button, node.Name);
            button.FullWidth = true;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            AutomationProperties.SetName(button, $"定位{node.Name}");
            paletteNodes.Add(node, (button, label));
            body.Children.Add(button);
        }
        paletteHost.Content = body;
    }

    private void BuildInspector()
    {
        var body = new StackPanel { Spacing = 14, Margin = new Thickness(18, 18, 18, 20) };
        if (selected is not { } node)
        {
            body.Children.Add(Text("选择一个节点", 16));
            ReplaceInspectorContent(inspectorHost, body);
            return;
        }

        body.Children.Add(Text("节点设置", 15, weight: FontWeight.SemiBold));
        var nodeTitle = Text(node.Name, 18, weight: FontWeight.SemiBold);
        nodeTitle.TextTrimming = TextTrimming.CharacterEllipsis;
        var nodeHeading = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
        Place(nodeHeading, IconTile(WorkflowCanvas.IconKey(node.Kind), WorkflowCanvas.Surface(node.Kind), WorkflowCanvas.Tone(node.Kind), 38), 0);
        Place(nodeHeading, new StackPanel
        {
            Spacing = 2, VerticalAlignment = VerticalAlignment.Center,
            Children = { nodeTitle, Text(WorkflowCanvas.KindName(node.Kind) + "节点", 12, "AlderMuted") }
        }, 1);
        body.Children.Add(nodeHeading);
        body.Children.Add(Field("节点名称", Input(node.Name, "输入节点名称", text =>
        {
            Edit(() => node.Name = text);
            nodeTitle.Text = text;
            if (paletteNodes.TryGetValue(node, out var row))
            {
                row.Label.Text = text;
                AlderToolTip.SetTip(row.Button, text);
                AutomationProperties.SetName(row.Button, $"定位{text}");
            }
        })));

        switch (node.Kind)
        {
            case WorkflowNodeKind.SingleApproval:
            case WorkflowNodeKind.Countersign:
                BuildApproval(body, node);
                break;
            case WorkflowNodeKind.Condition: BuildCondition(body, node); break;
            case WorkflowNodeKind.Copy:
                BuildCopyRecipients(body, node);
                break;
            case WorkflowNodeKind.Start:
                body.Children.Add(Field("流程名称", Input(workflow.Name, "输入流程名称", value =>
                {
                    Edit(() => workflow.Name = value);
                    UpdateWorkflowTitle();
                })));
                body.Children.Add(Note("由提交者发起，并将申请表单数据传给条件分支。"));
                break;
            case WorkflowNodeKind.End:
                body.Children.Add(Note("结束本次流程并记录审批结果。"));
                break;
        }

        if (node.EditableRoutes.Length > 0)
        {
            AddSection(body, "流转设置");
            foreach (var route in node.EditableRoutes)
                body.Children.Add(Field(WorkflowGraph.PortName(route), RoutePicker(node, route)));
        }

        var remove = Action("删除节点", ButtonVariant.DangerSoft, "AlderIconTrash", () => DeleteNode(node));
        remove.HorizontalAlignment = HorizontalAlignment.Left;
        body.Children.Add(remove);
        ReplaceInspectorContent(inspectorHost, body);
    }

    private void BuildApproval(StackPanel body, WorkflowNode node)
    {
        bool countersign = node.Kind == WorkflowNodeKind.Countersign;
        if (countersign)
        {
            body.Children.Add(Field("会签人员", MultiAssigneePicker(WorkflowAssigneeKind.Person, node.Signers, "选择会签人员",
                signers => node.Signers = signers), "至少选择两人。全部同意后继续，任一人拒绝即结束流程。"));
        }
        else
        {
            AddAssigneeFields(body, false, node.Assignee, selection => node.Assignee = selection);
        }

        AddSection(body, "审批时限");
        var deadline = new AlderSwitch { Content = "启用审批时限", IsChecked = node.HasDeadline, Size = AlderSize.Small };
        deadline.IsCheckedChanged += (_, _) => Edit(() => node.HasDeadline = deadline.IsChecked == true, rebuild: true);
        body.Children.Add(deadline);
        if (!node.HasDeadline) return;
        var hours = new AlderNumberField
        {
            Minimum = 1, Maximum = 720, Increment = 1, Value = node.Hours, FormatString = "0",
            ParsingNumberStyle = NumberStyles.Integer, FullWidth = true
        };
        hours.ValueChanged += (_, _) =>
        {
            if (hours.Value is { } value) Edit(() => node.Hours = (int)value);
        };
        body.Children.Add(Field("处理时限（小时）", hours));
        WorkflowTimeoutAction[] actions = countersign ? [WorkflowTimeoutAction.Remind, WorkflowTimeoutAction.Return] : [WorkflowTimeoutAction.Remind, WorkflowTimeoutAction.Escalate, WorkflowTimeoutAction.Return];
        body.Children.Add(Field("超时处理", Picker(actions.Select(action => action switch
        {
            WorkflowTimeoutAction.Remind => "提醒并继续等待",
            WorkflowTimeoutAction.Escalate => "转交指定审批对象",
            _ => "结束流程"
        }).ToArray(), Array.IndexOf(actions, node.TimeoutAction),
            index => Edit(() => node.TimeoutAction = actions[index], rebuild: true))));
        if (node.TimeoutAction == WorkflowTimeoutAction.Escalate)
            AddAssigneeFields(body, true, node.Escalation, selection => node.Escalation = selection);
    }

    private void BuildCopyRecipients(StackPanel body, WorkflowNode node)
    {
        var recipientField = new AlderContentControl();
        var kinds = AssigneeKindPicker(node.CopyRecipientKind, kind =>
        {
            if (node.CopyRecipientKind == kind) return;
            Edit(() =>
            {
                node.CopyRecipientKind = kind;
                node.Recipients = node.Recipients.Where(recipient => recipient.Kind == kind).ToArray();
            });
            RefreshRecipients();
        });
        body.Children.Add(Field("抄送对象类型", kinds));
        body.Children.Add(recipientField);
        RefreshRecipients();

        if (node.LegacyRecipients is { } previous)
        {
            body.Children.Add(Note($"旧版抄送名单：{previous}\n请从目录重新选择对应的人员或角色，核对后清除旧名单。"));
            var clear = Action("清除旧版名单", ButtonVariant.Secondary, null,
                () => Edit(() => node.LegacyRecipients = null, rebuild: true));
            clear.HorizontalAlignment = HorizontalAlignment.Left;
            body.Children.Add(clear);
        }

        void RefreshRecipients()
        {
            if (node.CopyRecipientKind is not { } kind)
            {
                var people = string.Join("、", node.Recipients.Where(item => item.Kind == WorkflowAssigneeKind.Person).Select(sources.Name));
                var roles = string.Join("、", node.Recipients.Where(item => item.Kind == WorkflowAssigneeKind.Role).Select(sources.Name));
                ReplaceInspectorContent(recipientField, Note($"旧版名单含两种类型，选择后保留对应名单。\n人员：{people}\n角色：{roles}"));
                return;
            }

            string label = kind == WorkflowAssigneeKind.Role ? "抄送角色" : "抄送人员";
            var picker = MultiAssigneePicker(kind, node.Recipients, "选择" + label, recipients => node.Recipients = recipients);
            ReplaceInspectorContent(recipientField, Field(label, picker, picker.Items.Count == 0 ? "此目录暂无可选对象。" : null));
        }
    }

    private AlderSelect MultiAssigneePicker(WorkflowAssigneeKind kind, AlderWorkflowAssignee[] selections, string placeholder,
        Action<AlderWorkflowAssignee[]> update)
    {
        var options = AssigneeOptions(kind, selections);
        var byId = options.ToDictionary(option => option.Id, StringComparer.Ordinal);
        var previous = selections.ToDictionary(selection => selection.Id!, StringComparer.Ordinal);
        var picker = AssigneePicker(options, placeholder, out var items);
        picker.SelectionMode = SelectionMode.Multiple;
        picker.SelectedItems = selections.Select(selection => byId[selection.Id!].PickerItem).ToList();
        picker.ShowClearButton = true;
        picker.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, picker)) return;
            var selected = picker.SelectedItems!.Cast<object>().Select(item => items[item])
                .Select(option => option.IsUnavailable ? previous[option.Id] : new AlderWorkflowAssignee(kind, option.Id, option.DisplayName))
                .ToArray();
            Edit(() => update(selected));
        };
        return picker;
    }

    private void AddAssigneeFields(StackPanel body, bool escalation, AlderWorkflowAssignee selection, Action<AlderWorkflowAssignee> update)
    {
        var assigneeField = new AlderContentControl();
        var kinds = AssigneeKindPicker(selection.Kind, kind =>
        {
            if (selection.Kind == kind) return;
            SaveSelection(new(kind));
            RefreshAssignee();
        });
        body.Children.Add(Field(escalation ? "转交对象类型" : "审批对象类型", kinds));
        body.Children.Add(assigneeField);
        RefreshAssignee();

        void SaveSelection(AlderWorkflowAssignee value) => Edit(() =>
        {
            selection = value;
            update(value);
        });

        void RefreshAssignee()
        {
            var kind = selection.Kind;
            string kindLabel = kind == WorkflowAssigneeKind.Role ? "角色" : "人员";
            var options = AssigneeOptions(kind, selection.Id is null ? [] : [selection]);
            var picker = AssigneePicker(options, "选择" + kindLabel, out var optionsByItem);
            picker.ShowClearButton = true;
            picker.SelectedItem = options.FirstOrDefault(option => option.Id == selection.Id)?.PickerItem;
            picker.SelectionChanged += (_, e) =>
            {
                if (!ReferenceEquals(e.Source, picker)) return;
                var option = picker.SelectedItem is { } item ? optionsByItem[item] : null;
                SaveSelection(option switch
                {
                    null => new AlderWorkflowAssignee(kind),
                    { IsUnavailable: true } => selection,
                    _ => new AlderWorkflowAssignee(kind, option.Id, option.DisplayName)
                });
            };
            ReplaceInspectorContent(assigneeField, Field((escalation ? "转交" : "审批") + kindLabel, picker,
                options.Length == 0 ? $"目录中没有可选{kindLabel}。" : null));
        }
    }

    private WorkflowAssigneeOption[] AssigneeOptions(WorkflowAssigneeKind kind, IEnumerable<AlderWorkflowAssignee> selections)
    {
        var catalog = sources.For(kind);
        var missing = selections.Where(selection => sources.Find(selection) is null);
        return [.. catalog, .. missing.Select(selection => new WorkflowAssigneeOption(selection.Id!, selection.Label ?? selection.Id!) { IsUnavailable = true })];
    }

    private static AlderSelect AssigneePicker(IReadOnlyList<WorkflowAssigneeOption> options, string placeholder,
        out Dictionary<object, WorkflowAssigneeOption> optionsByItem)
    {
        var repeatedNames = options.GroupBy(option => option.DisplayName).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        var items = options.ToDictionary(option => option.PickerItem, ReferenceEqualityComparer.Instance);
        optionsByItem = items;
        return new AlderSelect
        {
            ItemsSource = options.Select(option => option.PickerItem).ToArray(),
            TextSelector = item => item is not null && items.TryGetValue(item, out var option)
                ? OptionLabel(option)
                : null,
            PlaceholderText = placeholder,
            FullWidth = true
        };

        string OptionLabel(WorkflowAssigneeOption option)
        {
            var label = repeatedNames.Contains(option.DisplayName) ? $"{option.DisplayName}（{option.Id}）" : option.DisplayName;
            return option.IsUnavailable ? label + "（不可用）" : label;
        }
    }

    private void BuildCondition(StackPanel body, WorkflowNode node)
    {
        var rule = node.Rule;
        var fields = conditions.Values.ToList();
        if (rule.FieldKey is { } key && !conditions.ContainsKey(key))
            fields.Add(new WorkflowConditionOption(key, ConditionName(key), rule.ValueKind));

        var repeatedNames = fields.GroupBy(field => field.DisplayName).Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet();
        var fieldPicker = new AlderSelect
        {
            ItemsSource = fields,
            TextSelector = item => item is WorkflowConditionOption field
                ? repeatedNames.Contains(field.DisplayName) ? $"{field.DisplayName}（{field.Id}）" : field.DisplayName : null,
            SelectedItem = fields.FirstOrDefault(field => field.Id == rule.FieldKey),
            PlaceholderText = "选择判断字段",
            ShowClearButton = true,
            FullWidth = true
        };
        var comparisonFields = new StackPanel { Spacing = body.Spacing };
        fieldPicker.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, fieldPicker)) return;
            Edit(() => node.Rule = fieldPicker.SelectedItem is WorkflowConditionOption field ? ConditionRule.For(field) : ConditionRule.Empty);
            BuildComparisonFields(comparisonFields, node);
        };
        body.Children.Add(Field("判断字段", fieldPicker, fields.Count == 0 ? "请先由宿主提供条件字段。" : null));
        body.Children.Add(comparisonFields);
        BuildComparisonFields(comparisonFields, node);
    }

    private void BuildComparisonFields(StackPanel body, WorkflowNode node)
    {
        var rule = node.Rule;
        var operators = rule.Operators.ToArray();
        var operatorPicker = Picker(operators.Select(ConditionRule.OperatorName).ToArray(), Array.IndexOf(operators, rule.Operator),
            index => Edit(() => node.Rule = node.Rule with { Operator = operators[index] }));
        var comparison = Input(rule.ExpectedValue, ConditionRule.ValueHint(rule.ValueKind),
            value => Edit(() => node.Rule = node.Rule with { ExpectedValue = value }), maxLength: 0);
        operatorPicker.IsEnabled = comparison.IsEnabled = rule.FieldKey is not null;
        body.Children.Clear();
        body.Children.Add(Field("比较方式", operatorPicker));
        body.Children.Add(Field("比较值", comparison));
    }

    private void Edit(Action apply, bool rebuild = false)
    {
        apply();
        PublishWorkflow();
        UpdateCanvas();
        if (rebuild) BuildInspector();
    }

    private void SelectNode(WorkflowNode node)
    {
        if (ReferenceEquals(selected, node)) return;
        var previous = selected;
        selected = node;
        canvas.SelectCard(node);
        SetPaletteSelection(previous, false);
        SetPaletteSelection(node, true);
        BuildInspector();
    }

    private void SetPaletteSelection(WorkflowNode? node, bool isSelected)
    {
        if (node is null || !paletteNodes.TryGetValue(node, out var row)) return;
        row.Button.Variant = isSelected ? ButtonVariant.Secondary : ButtonVariant.Ghost;
        row.Label[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(isSelected ? "AlderSoftInk" : "AlderInk");
    }

    private void RefreshAll() { UpdateCanvas(); BuildPalette(); BuildInspector(); }
    private void UpdateCanvas()
    {
        canvas.Update(workflow, selected, ConditionName, sources);
        summary.Text = $"{workflow.Nodes.Count} 个节点 · {workflow.Nodes.Sum(n => n.Links.Count)} 条连线";
    }

    private void AddNode(WorkflowNodeKind kind)
    {
        var node = new WorkflowNode(kind, kind switch
        {
            WorkflowNodeKind.Start => "提交申请",
            WorkflowNodeKind.End => "流程结束",
            _ => $"新{WorkflowCanvas.KindName(kind)}节点"
        }, canvas.FreePosition());
        if (selected is { ContinuationRoute: { } route } previous && kind != WorkflowNodeKind.Start)
        {
            if (node.ContinuationRoute is { } continuation && previous.Links.TryGetValue(route, out var next))
                node.Links[continuation] = next;
            if (node.ContinuationRoute is not null || !previous.Links.ContainsKey(route)) previous.Links[route] = node;
        }
        workflow.Nodes.Add(node);
        var ends = workflow.Nodes.Where(candidate => candidate.Kind == WorkflowNodeKind.End).ToArray();
        if (ends.Length == 1 && kind == WorkflowNodeKind.End)
            foreach (var approval in workflow.Nodes.Where(candidate => candidate.IsApproval))
                approval.Links.TryAdd(WorkflowRoute.Reject, ends[0]);
        else if (ends.Length == 1 && node.IsApproval)
            node.Links[WorkflowRoute.Reject] = ends[0];
        PublishWorkflow();
        selected = node;
        RefreshAll();
    }

    private void DeleteNode(WorkflowNode node)
    {
        workflow.Nodes.Remove(node);
        foreach (var candidate in workflow.Nodes)
            foreach (var link in candidate.Links.Where(link => link.Value == node).ToArray())
                candidate.Links.Remove(link.Key);
        selected = workflow.Nodes.FirstOrDefault();
        PublishWorkflow();
        RefreshAll();
    }

    /// <summary>Checks graph completeness against the catalogs currently supplied by the host.</summary>
    public IReadOnlyList<string> ValidateWorkflow() => workflow.Validate(
        attached && !catalogRefreshQueued ? sources : ReadAssigneeSources(observeLabels: false),
        attached && !catalogRefreshQueued ? conditions : ReadConditionCatalog());

    /// <summary>Returns the current draft with the latest display labels from the bound catalogs.</summary>
    public AlderWorkflowDefinition GetWorkflow() => WorkflowPersistence.Capture(workflow,
        attached && !catalogRefreshQueued ? sources : ReadAssigneeSources(observeLabels: false));

    private void ApplyWorkflow(AlderWorkflowDefinition? definition)
    {
        workflow = definition is { } value ? WorkflowPersistence.Restore(value) : WorkflowGraph.New();
        selected = workflow.Nodes.FirstOrDefault(node => node.IsApproval) ?? workflow.StartNode ?? workflow.Nodes.FirstOrDefault();
        UpdateWorkflowTitle();
        UpdateSaveButton();
        if (!attached) return;
        FitCanvas();
        RefreshAll();
    }

    private void PublishWorkflow()
    {
        var snapshot = WorkflowPersistence.Capture(workflow, sources);
        publishing = true;
        try { SetCurrentValue(WorkflowProperty, snapshot); }
        finally { publishing = false; }
        saveButton.CommandParameter = snapshot;
    }

    private void UpdateSaveButton()
    {
        saveButton.CommandParameter = WorkflowPersistence.Capture(workflow, sources);
        saveButton.Command = SaveCommand;
        saveButton.IsVisible = SaveCommand is not null;
    }

    private void UpdateWorkflowTitle()
    {
        string title = string.IsNullOrWhiteSpace(workflow.Name) ? "未命名流程" : workflow.Name;
        workflowTitle.Text = title;
        AlderToolTip.SetTip(workflowTitle, title);
    }

    private void FitCanvas()
    {
        fit = true;
        if (viewport.Bounds.Width <= 0 || viewport.Bounds.Height <= 0) return;
        SetZoom(Math.Min(1, Math.Min((viewport.Bounds.Width - 14) / WorkflowCanvas.GraphWidth,
            (viewport.Bounds.Height - 12) / WorkflowCanvas.GraphHeight)));
        viewport.Offset = default;
    }

    private void ChangeZoom(double step) { fit = false; SetZoom(zoom + step); }
    private void SetZoom(double scale)
    {
        zoom = Math.Clamp(scale, .45, 1.6);
        scaledCanvas.LayoutTransform = new ScaleTransform(zoom, zoom);
        zoomLabel.Text = $"{zoom:P0}";
    }

    private Control RoutePicker(WorkflowNode from, WorkflowRoute route)
    {
        var candidates = workflow.Nodes.Where(node => node != from &&
            (route == WorkflowRoute.Reject ? node.Kind == WorkflowNodeKind.End : node.Kind != WorkflowNodeKind.Start)).ToArray();
        int index = Array.IndexOf(candidates, from.Links.GetValueOrDefault(route));
        return Picker([route == WorkflowRoute.Reject ? "请选择结束节点" : "请选择节点", .. candidates.Select(node => node.Name)], index + 1, selectedIndex =>
            Edit(() =>
            {
                if (selectedIndex == 0) from.Links.Remove(route);
                else from.Links[route] = candidates[selectedIndex - 1];
            }));
    }

    private static AlderInput Input(string value, string placeholder, Action<string> onChange, int maxLength = 100)
    {
        var input = new AlderInput { Text = value, PlaceholderText = placeholder, FullWidth = true, MaxLength = maxLength };
        input.PropertyChanged += (_, change) =>
        {
            if (change.Property == TextBox.TextProperty) onChange(change.GetNewValue<string?>() ?? "");
        };
        return input;
    }

    private static AlderSelect Picker(string[] choices, int selected, Action<int> onChange)
    {
        var picker = new AlderSelect { ItemsSource = choices, SelectedIndex = selected, FullWidth = true };
        picker.SelectionChanged += (_, _) => { if (picker.SelectedIndex >= 0) onChange(picker.SelectedIndex); };
        return picker;
    }

    private static AlderRadioButtonGroup AssigneeKindPicker(WorkflowAssigneeKind? selected, Action<WorkflowAssigneeKind> onChange)
    {
        var group = new AlderRadioButtonGroup
        {
            Layout = RadioButtonGroupLayout.Grid, Columns = 2, MinColumnWidth = 92, Spacing = 8,
            Orientation = Orientation.Horizontal
        };
        var person = new AlderRadioButtonGroupItem
        {
            Content = "人员", Padding = new Thickness(12, 8), IndicatorVerticalAlignment = VerticalAlignment.Center
        };
        var role = new AlderRadioButtonGroupItem
        {
            Content = "角色", Padding = new Thickness(12, 8), IndicatorVerticalAlignment = VerticalAlignment.Center
        };
        AlderChoiceGroup.SetKey(person, nameof(WorkflowAssigneeKind.Person));
        AlderChoiceGroup.SetKey(role, nameof(WorkflowAssigneeKind.Role));
        group.Children.Add(person);
        group.Children.Add(role);
        group.SelectedKey = selected?.ToString();
        group.PropertyChanged += (_, change) =>
        {
            if (change.Property != AlderRadioButtonGroup.SelectedKeyProperty) return;
            if (group.SelectedKey == nameof(WorkflowAssigneeKind.Person)) onChange(WorkflowAssigneeKind.Person);
            else if (group.SelectedKey == nameof(WorkflowAssigneeKind.Role)) onChange(WorkflowAssigneeKind.Role);
        };
        return group;
    }

    private static void ReplaceInspectorContent(ContentControl host, Control content)
    {
        if (host.Content is Control previous)
        {
            // Keep the old popup anchor alive through the start of its exit transition.
            foreach (var picker in previous.GetSelfAndVisualDescendants().OfType<AlderPicker>().Where(picker => picker.IsDropDownOpen).ToArray())
                picker.IsDropDownOpen = false;
        }
        host.Content = content;
    }

    private static Control Field(string label, Control input, string? description = null)
    {
        AutomationProperties.SetName(input, label);
        if (input is AlderPicker picker)
        {
            picker.Label = label;
            picker.Description = description;
            return picker;
        }

        var caption = new AlderLabel { Content = label, Target = input };
        AutomationProperties.SetLabeledBy(input, caption);
        var panel = new StackPanel { Spacing = 4, Children = { caption, input } };
        if (description is not null) panel.Children.Add(Text(description, 12, "AlderMuted", wrap: true));
        return panel;
    }

    private static void AddSection(StackPanel body, string title)
    {
        body.Children.Add(Separator());
        body.Children.Add(Text(title, 14, weight: FontWeight.SemiBold));
    }

    private static AlderSurface Note(string text, string color = "AlderMuted") =>
        PanelSurface(Text(text, 12, color, wrap: true), 12, new Thickness(10, 8));

    private static AlderSurface PanelSurface(Control child, double radius = 20, Thickness padding = default, SurfaceVariant variant = SurfaceVariant.Default)
    {
        var surface = new AlderSurface
        {
            Content = child, Variant = variant, CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(1), Padding = padding
        };
        surface[!Border.BorderBrushProperty] = new DynamicResourceExtension("AlderBorder");
        return surface;
    }

    private static Border IconTile(string icon, string fill, string color, double size)
    {
        var tile = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size > 36 ? 13 : 8),
            Child = Glyph(icon, color, size > 36 ? 21 : 15)
        };
        tile[!Border.BackgroundProperty] = new DynamicResourceExtension(fill);
        return tile;
    }

    private static AlderPathIcon Glyph(string key, string color, double size)
    {
        var icon = new AlderPathIcon { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        icon[!PathIcon.DataProperty] = new DynamicResourceExtension(key);
        icon[!TemplatedControl.ForegroundProperty] = new DynamicResourceExtension(color);
        icon[!ThemeProperty] = new DynamicResourceExtension("AlderFittedPathIconTheme");
        return icon;
    }

    private AlderButton Action(string label, ButtonVariant variant, string? icon, Action action, bool iconOnly = false)
    {
        var button = new AlderButton { Content = iconOnly ? null : label, Variant = variant, Size = AlderSize.Small, IsIconOnly = iconOnly, VerticalAlignment = VerticalAlignment.Center };
        if (icon is not null) button[!AlderButton.IconProperty] = new DynamicResourceExtension(icon);
        if (label.Length > 0) AutomationProperties.SetName(button, label);
        if (iconOnly) AlderToolTip.SetTip(button, label);
        button.Click += (_, _) => action();
        return button;
    }

    private static TextBlock Text(string value, int size, string color = "AlderInk", FontWeight weight = FontWeight.Normal, bool wrap = false)
    {
        var text = new AlderTextBlock { Text = value, FontSize = size, FontWeight = weight, VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap };
        text[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(color);
        return text;
    }

    private static AlderSeparator Separator(bool vertical = false) => new()
    {
        Height = vertical ? 20 : 1,
        Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center,
        IsDecorative = true
    };

    private static StackPanel Row(int spacing, params Control[] children)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.AddRange(children);
        return panel;
    }

    private static void Place(Grid grid, Control child, int column)
    {
        Grid.SetColumn(child, column);
        grid.Children.Add(child);
    }

    private static AlderScrollViewer Scroll(Control content, bool horizontal = false) => new()
    {
        Content = content, HorizontalScrollBarVisibility = horizontal ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    private sealed class LineMark : Control
    {
        public bool Rejected { get; init; }

        public LineMark()
        {
            Width = 19;
            Height = 8;
            ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            var pen = new Pen(WorkflowCanvas.Brush(this, Rejected ? "WorkflowRejectLine" : "WorkflowLine"), 1.5,
                Rejected ? new DashStyle([3, 2], 0) : null, PenLineCap.Round);
            context.DrawLine(pen, new Point(0, 4), new Point(19, 4));
        }
    }

    private sealed class DotField : Control
    {
        public DotField() => ActualThemeVariantChanged += (_, _) => InvalidateVisual();

        public override void Render(DrawingContext context)
        {
            context.FillRectangle(WorkflowCanvas.Brush(this, "WorkflowCanvasBrush"), new Rect(Bounds.Size));
            var dot = WorkflowCanvas.Brush(this, "WorkflowDots");
            for (int y = 12; y < Bounds.Height; y += 22)
            for (int x = 12; x < Bounds.Width; x += 22)
                context.DrawEllipse(dot, null, new Point(x, y), .75, .75);
        }
    }
}
