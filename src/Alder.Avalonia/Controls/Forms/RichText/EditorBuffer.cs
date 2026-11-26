using System.Text;

namespace Alder.Editor;

/// <summary>An AVL rope of slices of immutable UTF-16 strings. Offsets and rows are zero based.</summary>
internal sealed class EditorBuffer
{
    private const int LeafCapacity = 1024;
    private abstract class Node(int length, int breaks, int height)
    {
        public int Length { get; } = length;
        public int Breaks { get; } = breaks;
        public int Height { get; } = height;
    }
    private sealed class Leaf(ReadOnlyMemory<char> chars) : Node(chars.Length, chars.Span.Count('\n'), 1)
    {
        public ReadOnlyMemory<char> Chars { get; } = chars;
    }
    private sealed class Branch(Node left, Node right) :
        Node(left.Length + right.Length, left.Breaks + right.Breaks, Math.Max(left.Height, right.Height) + 1)
    {
        public Node Left { get; } = left;
        public Node Right { get; } = right;
    }

    private Node? root;
    private string? snapshot;
    public int Length => root?.Length ?? 0;
    public int LineCount => (root?.Breaks ?? 0) + 1;
    public string Text => snapshot ??= Slice(0, Length);
    public event Action<TextEdit>? Changed;

    public int Row(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, Length);
        var node = root;
        int row = 0;
        while (node is Branch b)
        {
            if (offset <= b.Left.Length) node = b.Left;
            else { row += b.Left.Breaks; offset -= b.Left.Length; node = b.Right; }
        }
        return row + (node is Leaf leaf ? leaf.Chars.Span[..offset].Count('\n') : 0);
    }

    public int Start(int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, LineCount);
        if (row == 0) return 0;
        var node = root;
        int offset = 0;
        while (node is Branch b)
        {
            if (row <= b.Left.Breaks) node = b.Left;
            else { offset += b.Left.Length; row -= b.Left.Breaks; node = b.Right; }
        }
        var chars = ((Leaf)node!).Chars.Span;
        for (var i = 0; i < chars.Length; i++)
            if (chars[i] == '\n' && --row == 0) return offset + i + 1;
        throw new InvalidOperationException("The line index is inconsistent.");
    }

    public Lsp.TextPosition Position(int offset)
    {
        var row = Row(offset);
        return new(row, offset - Start(row));
    }

    public string Slice(int start, int length)
    {
        Validate(start, length);
        if (length == 0) return "";
        var result = new StringBuilder(length);
        Copy(root!, start, length, result);
        return result.ToString();
    }

    private static void Copy(Node node, int start, int length, StringBuilder result)
    {
        if (node is Leaf leaf) { result.Append(leaf.Chars.Span.Slice(start, length)); return; }
        var branch = (Branch)node;
        int leftCount = Math.Clamp(branch.Left.Length - start, 0, length);
        if (leftCount > 0) Copy(branch.Left, start, leftCount, result);
        if (leftCount < length) Copy(branch.Right, Math.Max(0, start - branch.Left.Length), length - leftCount, result);
    }

    public void Replace(int start, int length, string text)
    {
        Validate(start, length);
        if (length == 0 && text.Length == 0) return;
        var range = Changed is null ? default : new Lsp.TextRange(Position(start), Position(start + length));
        var (left, rest) = Split(root, start);
        var (_, right) = Split(rest, length);
        root = Join(Join(left, Build(text.AsMemory())), right);
        snapshot = null;
        Changed?.Invoke(new(range, text));
    }

    private void Validate(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, Length - start);
    }

    private static Node? Build(ReadOnlyMemory<char> text)
    {
        if (text.IsEmpty) return null;
        if (text.Length <= LeafCapacity) return new Leaf(text);
        int mid = text.Length / 2;
        return new Branch(Build(text[..mid])!, Build(text[mid..])!);
    }

    private static (Node?, Node?) Split(Node? node, int offset)
    {
        if (offset == 0 || node is null) return (null, node);
        if (offset == node.Length) return (node, null);
        if (node is Leaf leaf) return (new Leaf(leaf.Chars[..offset]), new Leaf(leaf.Chars[offset..]));
        var b = (Branch)node;
        if (offset < b.Left.Length)
        {
            var (left, tail) = Split(b.Left, offset);
            return (left, Join(tail, b.Right));
        }
        var (head, right) = Split(b.Right, offset - b.Left.Length);
        return (Join(b.Left, head), right);
    }

    private static Node? Join(Node? left, Node? right)
    {
        if (left is null) return right;
        if (right is null) return left;
        if (left is Leaf a && right is Leaf b && a.Length + b.Length <= LeafCapacity)
            return new Leaf(string.Concat(a.Chars.Span, b.Chars.Span).AsMemory());
        if (left.Height > right.Height + 1)
        {
            var heavy = (Branch)left;
            return Balance(heavy.Left, Join(heavy.Right, right)!);
        }
        if (right.Height > left.Height + 1)
        {
            var heavy = (Branch)right;
            return Balance(Join(left, heavy.Left)!, heavy.Right);
        }
        return new Branch(left, right);
    }

    private static Node Balance(Node left, Node right)
    {
        if (left.Height > right.Height + 1 && left is Branch a)
        {
            if (a.Right.Height > a.Left.Height && a.Right is Branch pivot)
                return new Branch(new Branch(a.Left, pivot.Left), new Branch(pivot.Right, right));
            return new Branch(a.Left, new Branch(a.Right, right));
        }
        if (right.Height > left.Height + 1 && right is Branch b)
        {
            if (b.Left.Height > b.Right.Height && b.Left is Branch pivot)
                return new Branch(new Branch(left, pivot.Left), new Branch(pivot.Right, b.Right));
            return new Branch(new Branch(left, b.Left), b.Right);
        }
        return new Branch(left, right);
    }
}
internal readonly record struct TextEdit(Lsp.TextRange Range, string Text);
