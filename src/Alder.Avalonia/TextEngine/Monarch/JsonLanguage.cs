using static Alder.NativeCode.Monarch.MonarchBuilder;

namespace Alder.NativeCode.Monarch;

/// <summary>Monarch rules for JSON tool arguments and results.</summary>
internal static class JsonLanguage
{
    internal static MonarchLanguage Create() => new()
    {
        DefaultToken = "",
        TokenPostfix = ".json",
        Tokenizer = new MonarchStates
        {
            ["root"] =
            [
                R(@"\s+", "white"),
                R(@"\x22(?:\\.|[^\x22\\])*\x22(?=\s*:)", "attribute"),
                R(@"\x22(?:\\.|[^\x22\\])*\x22?", "string"),
                R(@"-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?", "number"),
                R(@"(?:true|false|null)\b", "keyword"),
                R(@"[{}\[\],:]", "delimiter")
            ]
        }
    };
}
