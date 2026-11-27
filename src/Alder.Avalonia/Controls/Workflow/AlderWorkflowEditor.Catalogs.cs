using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Threading;

namespace Alder.Avalonia.Controls;

public sealed partial class AlderWorkflowEditor
{
    /// <summary>Identifies the <see cref="UserItems"/> Avalonia property.</summary>
    public static readonly StyledProperty<IEnumerable?> UserItemsProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, IEnumerable?>(nameof(UserItems));
    /// <summary>Identifies the <see cref="UserKey"/> Avalonia property.</summary>
    public static readonly StyledProperty<string> UserKeyProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, string>(nameof(UserKey), nameof(WorkflowAssigneeOption.Id));
    /// <summary>Identifies the <see cref="UserDisplayMemberPath"/> Avalonia property.</summary>
    public static readonly StyledProperty<string> UserDisplayMemberPathProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, string>(nameof(UserDisplayMemberPath), nameof(WorkflowAssigneeOption.DisplayName));
    /// <summary>Identifies the <see cref="UserDisplayMemberBinding"/> Avalonia property.</summary>
    public static readonly StyledProperty<BindingBase?> UserDisplayMemberBindingProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, BindingBase?>(nameof(UserDisplayMemberBinding));

    /// <summary>Identifies the <see cref="RoleItems"/> Avalonia property.</summary>
    public static readonly StyledProperty<IEnumerable?> RoleItemsProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, IEnumerable?>(nameof(RoleItems));
    /// <summary>Identifies the <see cref="RoleKey"/> Avalonia property.</summary>
    public static readonly StyledProperty<string> RoleKeyProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, string>(nameof(RoleKey), nameof(WorkflowAssigneeOption.Id));
    /// <summary>Identifies the <see cref="RoleDisplayMemberPath"/> Avalonia property.</summary>
    public static readonly StyledProperty<string> RoleDisplayMemberPathProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, string>(nameof(RoleDisplayMemberPath), nameof(WorkflowAssigneeOption.DisplayName));
    /// <summary>Identifies the <see cref="RoleDisplayMemberBinding"/> Avalonia property.</summary>
    public static readonly StyledProperty<BindingBase?> RoleDisplayMemberBindingProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, BindingBase?>(nameof(RoleDisplayMemberBinding));

    /// <summary>Identifies the <see cref="ConditionalTypes"/> Avalonia property.</summary>
    public static readonly StyledProperty<IEnumerable?> ConditionalTypesProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, IEnumerable?>(nameof(ConditionalTypes));
    /// <summary>Identifies the <see cref="ConditionalKey"/> Avalonia property.</summary>
    public static readonly StyledProperty<string> ConditionalKeyProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, string>(nameof(ConditionalKey), nameof(WorkflowConditionOption.Id));
    /// <summary>Identifies the <see cref="ConditionalDisplayMemberPath"/> Avalonia property.</summary>
    public static readonly StyledProperty<string> ConditionalDisplayMemberPathProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, string>(nameof(ConditionalDisplayMemberPath), nameof(WorkflowConditionOption.DisplayName));
    /// <summary>Identifies the <see cref="ConditionalValueKindMemberPath"/> Avalonia property.</summary>
    public static readonly StyledProperty<string> ConditionalValueKindMemberPathProperty =
        AvaloniaProperty.Register<AlderWorkflowEditor, string>(nameof(ConditionalValueKindMemberPath), nameof(WorkflowConditionOption.ValueKind));

    /// <summary>Supplies the person items shared by approval and copy nodes.</summary>
    public IEnumerable? UserItems { get => GetValue(UserItemsProperty); set => SetValue(UserItemsProperty, value); }
    /// <summary>Gets or sets the property path for the stable person ID to store in JSON.</summary>
    public string UserKey { get => GetValue(UserKeyProperty); set => SetValue(UserKeyProperty, value); }
    /// <summary>Gets or sets the property path for the person's display name.</summary>
    public string UserDisplayMemberPath { get => GetValue(UserDisplayMemberPathProperty); set => SetValue(UserDisplayMemberPathProperty, value); }
    /// <summary>Binds the display label against each person item. Takes precedence over <see cref="UserDisplayMemberPath"/>.</summary>
    [AssignBinding]
    public BindingBase? UserDisplayMemberBinding { get => GetValue(UserDisplayMemberBindingProperty); set => SetValue(UserDisplayMemberBindingProperty, value); }

    /// <summary>Supplies the role items shared by approval and copy nodes.</summary>
    public IEnumerable? RoleItems { get => GetValue(RoleItemsProperty); set => SetValue(RoleItemsProperty, value); }
    /// <summary>Gets or sets the property path for the stable role ID to store in JSON.</summary>
    public string RoleKey { get => GetValue(RoleKeyProperty); set => SetValue(RoleKeyProperty, value); }
    /// <summary>Gets or sets the property path for the role's display name.</summary>
    public string RoleDisplayMemberPath { get => GetValue(RoleDisplayMemberPathProperty); set => SetValue(RoleDisplayMemberPathProperty, value); }
    /// <summary>Binds the display label against each role item. Takes precedence over <see cref="RoleDisplayMemberPath"/>.</summary>
    [AssignBinding]
    public BindingBase? RoleDisplayMemberBinding { get => GetValue(RoleDisplayMemberBindingProperty); set => SetValue(RoleDisplayMemberBindingProperty, value); }

    /// <summary>Supplies the fields that can be selected in a condition node.</summary>
    public IEnumerable? ConditionalTypes { get => GetValue(ConditionalTypesProperty); set => SetValue(ConditionalTypesProperty, value); }
    /// <summary>Gets or sets the property path for the stable field key to store in JSON.</summary>
    public string ConditionalKey { get => GetValue(ConditionalKeyProperty); set => SetValue(ConditionalKeyProperty, value); }
    /// <summary>Gets or sets the property path for the field name shown by the editor.</summary>
    public string ConditionalDisplayMemberPath { get => GetValue(ConditionalDisplayMemberPathProperty); set => SetValue(ConditionalDisplayMemberPathProperty, value); }
    /// <summary>Gets or sets the property path for the optional comparison value type. Missing types use automatic interpretation.</summary>
    public string ConditionalValueKindMemberPath { get => GetValue(ConditionalValueKindMemberPathProperty); set => SetValue(ConditionalValueKindMemberPathProperty, value); }

    private readonly HashSet<INotifyCollectionChanged> observedCatalogs = new(ReferenceEqualityComparer.Instance);
    private readonly List<(LabelBindingTarget Target, IDisposable Binding)> labelBindings = [];
    private IReadOnlyDictionary<string, WorkflowConditionOption> conditions = new Dictionary<string, WorkflowConditionOption>();
    private bool catalogRefreshQueued;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WorkflowProperty && !publishing)
            ApplyWorkflow(change.GetNewValue<AlderWorkflowDefinition?>());
        else if (change.Property == SaveCommandProperty)
            UpdateSaveButton();
        else if (change.Property == UserItemsProperty || change.Property == UserKeyProperty || change.Property == UserDisplayMemberPathProperty ||
            change.Property == UserDisplayMemberBindingProperty || change.Property == RoleItemsProperty || change.Property == RoleKeyProperty ||
            change.Property == RoleDisplayMemberPathProperty || change.Property == RoleDisplayMemberBindingProperty ||
            change.Property == ConditionalTypesProperty || change.Property == ConditionalKeyProperty ||
            change.Property == ConditionalDisplayMemberPathProperty || change.Property == ConditionalValueKindMemberPathProperty)
            QueueCatalogRefresh();
    }

    private void CatalogChanged(object? sender, NotifyCollectionChangedEventArgs args) => QueueCatalogRefresh();

    private void QueueCatalogRefresh()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(QueueCatalogRefresh);
            return;
        }
        if (!attached || catalogRefreshQueued) return;
        catalogRefreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            catalogRefreshQueued = false;
            if (!attached) return;
            ReadCatalogs();
            UpdateSaveButton();
            UpdateCanvas();
            BuildInspector();
        });
    }

    private void ReadCatalogs()
    {
        StopObservingCatalogs();
        foreach (var items in new[] { UserItems, RoleItems, ConditionalTypes })
            if (items is INotifyCollectionChanged observable && observedCatalogs.Add(observable))
                observable.CollectionChanged += CatalogChanged;

        sources = ReadAssigneeSources(observeLabels: true);
        conditions = ReadConditionCatalog();
    }

    private IReadOnlyDictionary<string, WorkflowConditionOption> ReadConditionCatalog() =>
        MapCatalog(ConditionalTypes, ConditionalKey, ConditionalDisplayMemberPath,
            (row, id, label) => new WorkflowConditionOption(id, label, ReadValueKind(row)))
            .ToDictionary(field => field.Id, StringComparer.Ordinal);

    private WorkflowAssigneeSources ReadAssigneeSources(bool observeLabels) => new(
        ReadAssigneeItems(RoleItems, RoleKey, RoleDisplayMemberPath, RoleDisplayMemberBinding, observeLabels),
        ReadAssigneeItems(UserItems, UserKey, UserDisplayMemberPath, UserDisplayMemberBinding, observeLabels));

    private IEnumerable<WorkflowAssigneeOption> ReadAssigneeItems(IEnumerable? items, string key, string path, BindingBase? binding, bool observeLabels) =>
        MapCatalog(items, key, path, static (row, id, label) => new WorkflowAssigneeOption(id, label) { SourceItem = row },
            binding is null ? null : row => ReadLabel(row, binding, observeLabels));

    private string? ReadLabel(object row, BindingBase binding, bool observe)
    {
        var target = new LabelBindingTarget { DataContext = row };
        var expression = target.Bind(LabelBindingTarget.ValueProperty, binding);
        var label = target.GetValue(LabelBindingTarget.ValueProperty);
        if (observe)
        {
            target.PropertyChanged += LabelChanged;
            labelBindings.Add((target, expression));
        }
        else
            expression.Dispose();
        return label;
    }

    private void LabelChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == LabelBindingTarget.ValueProperty) QueueCatalogRefresh();
    }

    private void StopObservingCatalogs()
    {
        foreach (var catalog in observedCatalogs) catalog.CollectionChanged -= CatalogChanged;
        observedCatalogs.Clear();
        foreach (var (target, binding) in labelBindings)
        {
            target.PropertyChanged -= LabelChanged;
            binding.Dispose();
        }
        labelBindings.Clear();
    }

    private WorkflowConditionValueKind ReadValueKind(object row) =>
        Enum.TryParse<WorkflowConditionValueKind>(AsText(ReadMember(row, ConditionalValueKindMemberPath)), ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value : WorkflowConditionValueKind.Auto;

    private string ConditionName(string key) =>
        conditions.TryGetValue(key, out var field) ? field.DisplayName : $"不可用（{key}）";

    private static IEnumerable<T> MapCatalog<T>(IEnumerable? items, string keyPath, string labelPath, Func<object, string, string, T> create,
        Func<object, string?>? displayBinding = null)
    {
        if (items is null) return [];
        var keyMembers = SplitPath(keyPath);
        var labelMembers = SplitPath(labelPath);
        var rows = new Dictionary<string, T>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in items)
        {
            if (row is null) continue;
            var id = AsText(ReadMember(row, keyMembers));
            if (string.IsNullOrWhiteSpace(id) || duplicates.Contains(id)) continue;
            if (rows.Remove(id))
            {
                // An ambiguous ID must never select an arbitrary person or field.
                duplicates.Add(id);
                continue;
            }
            var label = displayBinding is null ? null : displayBinding(row);
            if (string.IsNullOrWhiteSpace(label)) label = AsText(ReadMember(row, labelMembers));
            rows.Add(id, create(row, id, string.IsNullOrWhiteSpace(label) ? id : label));
        }
        return rows.Values;
    }

    private static string[] SplitPath(string? path) => string.IsNullOrEmpty(path) ? [] : path.Split('.');
    private static object? ReadMember(object item, string? path) => ReadMember(item, SplitPath(path));

    [UnconditionalSuppressMessage("Trimming", "IL2075",
        Justification = "The optional string member paths require preserved host members. Typed catalog records use direct access.")]
    private static object? ReadMember(object? value, string[] members)
    {
        foreach (var member in members)
        {
            if (value is null) return null;
            if (value is WorkflowAssigneeOption assignee && member is nameof(WorkflowAssigneeOption.Id) or nameof(WorkflowAssigneeOption.DisplayName))
                value = member == nameof(WorkflowAssigneeOption.Id) ? assignee.Id : assignee.DisplayName;
            else if (value is WorkflowConditionOption condition)
                value = member switch
                {
                    nameof(WorkflowConditionOption.Id) => condition.Id,
                    nameof(WorkflowConditionOption.DisplayName) => condition.DisplayName,
                    nameof(WorkflowConditionOption.ValueKind) => condition.ValueKind,
                    _ => null
                };
            else if (value is IDictionary dictionary)
                value = dictionary.Contains(member) ? dictionary[member] : null;
            else if (value is IReadOnlyDictionary<string, object?> namedValues)
                value = namedValues.GetValueOrDefault(member);
            else
                value = value.GetType().GetProperty(member) is { CanRead: true } property && property.GetIndexParameters().Length == 0
                    ? property.GetValue(value) : null;
        }
        return value;
    }

    private static string? AsText(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture);

    private sealed class LabelBindingTarget : StyledElement
    {
        public static readonly StyledProperty<string?> ValueProperty =
            AvaloniaProperty.Register<LabelBindingTarget, string?>("Value");
    }
}
