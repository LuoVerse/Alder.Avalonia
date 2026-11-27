using System.Globalization;

namespace Alder.Avalonia.Controls;

/// <summary>Determines how the comparison value and the request value are interpreted.</summary>
public enum WorkflowConditionValueKind
{
    /// <summary>Infers the type from the expected value.</summary>
    Auto,
    /// <summary>Compares decimal numbers.</summary>
    Number,
    /// <summary>Compares periods of time expressed in hours or workdays.</summary>
    Duration,
    /// <summary>Compares local dates and times.</summary>
    DateTime,
    /// <summary>Compares text for equality.</summary>
    Text
}

/// <summary>A field available to condition nodes, identified by the host's stable field key.</summary>
public sealed record WorkflowConditionOption(string Id, string DisplayName, WorkflowConditionValueKind ValueKind = WorkflowConditionValueKind.Auto);

/// <summary>The operator for a condition rule.</summary>
public enum WorkflowConditionOperator
{
    /// <summary>Greater than.</summary>
    Greater,
    /// <summary>Greater than or equal to.</summary>
    AtLeast,
    /// <summary>Less than.</summary>
    Less,
    /// <summary>Less than or equal to.</summary>
    AtMost,
    /// <summary>Equal to.</summary>
    Equal,
    /// <summary>Not equal to.</summary>
    NotEqual
}

internal sealed record ConditionRule(string? FieldKey, WorkflowConditionValueKind ValueKind, WorkflowConditionOperator Operator, string ExpectedValue)
{
    private static readonly WorkflowConditionOperator[] AllOperators = Enum.GetValues<WorkflowConditionOperator>();
    private static readonly WorkflowConditionOperator[] EqualityOperators = [WorkflowConditionOperator.Equal, WorkflowConditionOperator.NotEqual];
    private static readonly string[] TimestampFormats =
    [
        "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.FFFFFFF",
        "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"
    ];

    public static ConditionRule Empty { get; } = new(null, WorkflowConditionValueKind.Auto, WorkflowConditionOperator.Equal, "");
    public IReadOnlyList<WorkflowConditionOperator> Operators => ValueKind == WorkflowConditionValueKind.Text ? EqualityOperators : AllOperators;
    public bool IsConfigured => FieldKey is not null && TryMatch(ExpectedValue, out _);

    public static ConditionRule For(WorkflowConditionOption option) => new(option.Id, option.ValueKind, WorkflowConditionOperator.Equal, "");

    public bool TryMatch(string? actual, out bool matches)
    {
        matches = false;
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(ExpectedValue) || !Operators.Contains(Operator))
            return false;

        var left = actual.Trim();
        var right = ExpectedValue.Trim();
        int comparison;
        switch (ResolveKind(right))
        {
            case WorkflowConditionValueKind.Number:
                if (!TryNumber(left, out var leftNumber) || !TryNumber(right, out var rightNumber)) return false;
                comparison = leftNumber.CompareTo(rightNumber);
                break;
            case WorkflowConditionValueKind.Duration:
                if (!TryDuration(left, out var leftHours) || !TryDuration(right, out var rightHours)) return false;
                comparison = leftHours.CompareTo(rightHours);
                break;
            case WorkflowConditionValueKind.DateTime:
                if (!TryTimestamp(left, out var leftTime) || !TryTimestamp(right, out var rightTime)) return false;
                comparison = leftTime.CompareTo(rightTime);
                break;
            default:
                if (!EqualityOperators.Contains(Operator)) return false;
                comparison = StringComparer.OrdinalIgnoreCase.Compare(left, right);
                break;
        }

        matches = Operator switch
        {
            WorkflowConditionOperator.Greater => comparison > 0,
            WorkflowConditionOperator.AtLeast => comparison >= 0,
            WorkflowConditionOperator.Less => comparison < 0,
            WorkflowConditionOperator.AtMost => comparison <= 0,
            WorkflowConditionOperator.Equal => comparison == 0,
            _ => comparison != 0
        };
        return true;
    }

    private WorkflowConditionValueKind ResolveKind(string value)
    {
        if (ValueKind != WorkflowConditionValueKind.Auto) return ValueKind;
        if (TryNumber(value, out _)) return WorkflowConditionValueKind.Number;
        if (TryTimestamp(value, out _)) return WorkflowConditionValueKind.DateTime;
        if (TryDuration(value, out _)) return WorkflowConditionValueKind.Duration;
        return WorkflowConditionValueKind.Text;
    }

    private static bool TryNumber(string value, out decimal number) =>
        decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);

    private static bool TryDuration(string value, out decimal hours)
    {
        bool workday = value.EndsWith("工作日", StringComparison.Ordinal) || value.EndsWith("d", StringComparison.OrdinalIgnoreCase);
        int suffix = value.EndsWith("工作日", StringComparison.Ordinal) ? 3
            : value.EndsWith("小时", StringComparison.Ordinal) ? 2
            : workday || value.EndsWith("h", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        if (!TryNumber(value[..(value.Length - suffix)].TrimEnd(), out hours) || hours < 0) return false;
        if (workday)
        {
            try { hours *= 8; }
            catch (OverflowException) { return false; }
        }
        return true;
    }

    private static bool TryTimestamp(string value, out DateTime timestamp) =>
        DateTime.TryParseExact(value, TimestampFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);

    public static string ValueHint(WorkflowConditionValueKind kind) => kind switch
    {
        WorkflowConditionValueKind.Number => "例如 10000 或 2.5",
        WorkflowConditionValueKind.Duration => "例如 16 小时或 2 工作日",
        WorkflowConditionValueKind.DateTime => "YYYY-MM-DD HH:mm",
        WorkflowConditionValueKind.Text => "输入要比较的文本",
        _ => "输入数字、时长、日期时间或文本"
    };

    public string DescribeBranch(bool matches) => !IsConfigured
        ? (matches ? "满足条件" : "不满足条件")
        : $"{Symbol(matches ? Operator : Complement(Operator))} {ExpectedValue.Trim()}";

    public static string OperatorName(WorkflowConditionOperator comparison)
    {
        var label = comparison switch
        {
            WorkflowConditionOperator.Greater => "大于",
            WorkflowConditionOperator.AtLeast => "大于等于",
            WorkflowConditionOperator.Less => "小于",
            WorkflowConditionOperator.AtMost => "小于等于",
            WorkflowConditionOperator.Equal => "等于",
            _ => "不等于"
        };
        return $"{label}（{Symbol(comparison)}）";
    }

    private static string Symbol(WorkflowConditionOperator comparison) => comparison switch
    {
        WorkflowConditionOperator.Greater => ">",
        WorkflowConditionOperator.AtLeast => "≥",
        WorkflowConditionOperator.Less => "<",
        WorkflowConditionOperator.AtMost => "≤",
        WorkflowConditionOperator.Equal => "=",
        _ => "≠"
    };

    private static WorkflowConditionOperator Complement(WorkflowConditionOperator comparison) => comparison switch
    {
        WorkflowConditionOperator.Greater => WorkflowConditionOperator.AtMost,
        WorkflowConditionOperator.AtLeast => WorkflowConditionOperator.Less,
        WorkflowConditionOperator.Less => WorkflowConditionOperator.AtLeast,
        WorkflowConditionOperator.AtMost => WorkflowConditionOperator.Greater,
        WorkflowConditionOperator.Equal => WorkflowConditionOperator.NotEqual,
        _ => WorkflowConditionOperator.Equal
    };
}
