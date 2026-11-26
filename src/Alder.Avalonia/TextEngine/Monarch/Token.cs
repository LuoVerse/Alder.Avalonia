// Adapted from the MIT-licensed Monaco classic token interface.
namespace Alder.NativeCode.Monarch;

internal interface IState
{
    IState Clone();

    bool Equals(IState other);
}

/// <summary>A token: where it starts in the line, its type (such as <c>keyword.cs</c>) and its language.</summary>
internal sealed record Token(int Offset, string Type, string Language)
{
    /// <inheritdoc />
    public override string ToString() => "(" + Offset + ", " + Type + ")";
}

/// <summary>The tokens of a line as text and the state at its end.</summary>
internal sealed class TokenizationResult(IReadOnlyList<Token> tokens, IState endState)
{
    public IReadOnlyList<Token> Tokens { get; } = tokens;

    public IState EndState { get; } = endState;
}

internal sealed class NullState : IState
{
    public static readonly NullState Instance = new();

    private NullState()
    {
    }

    public IState Clone() => this;

    public bool Equals(IState other) => ReferenceEquals(this, other);
}

/// <summary>The tokens of a language without a tokenizer: one token over the whole line (upstream's nullTokenize.ts).</summary>
internal static class NullTokenize
{
    public static TokenizationResult Tokenize(string languageId, IState state) =>
        new([new Token(0, string.Empty, languageId)], state);

}
