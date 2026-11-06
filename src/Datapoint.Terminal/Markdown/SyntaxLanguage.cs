using System.Text.RegularExpressions;

namespace Datapoint.Terminal.Markdown;

/// <summary>
/// A family of languages coloured the same way: one regular expression whose named groups are comments, strings,
/// variables, numbers and keywords. It's a tokenizer, not a parser, so it can be wrong at the edges; it never changes the text.
/// </summary>
internal sealed class SyntaxLanguage
{
    private const string Number = @"\b(?:0[xX][0-9a-fA-F]+|\d+(?:\.\d+)?(?:[eE][+-]?\d+)?)\b";
    private const string DoubleQuoted = @"""(?:\\.|[^""\\\n])*""";
    private const string SingleQuoted = @"'(?:\\.|[^'\\\n])*'";
    private const string HashComment = @"(?<=^|[ \t;])#[^\n]*";
    private const string SlashComment = @"//[^\n]*|/\*[\s\S]*?\*/";

    private static readonly TimeSpan s_timeout = TimeSpan.FromMilliseconds(250);

    private static readonly SyntaxLanguage s_shell = new(
        HashComment,
        DoubleQuoted + @"|'[^']*'",
        @"\$\{[^}\n]*\}|\$[A-Za-z_]\w*|\$[0-9#?@*$!-]",
        "if then else elif fi for while until do done case esac in function select return exit export local readonly declare unset source alias break continue time");

    private static readonly SyntaxLanguage s_powerShell = new(
        @"<#[\s\S]*?#>|#[^\n]*",
        @"""(?:`.|[^""`])*""|'(?:''|[^'])*'",
        @"\$\{[^}\n]*\}|\$[A-Za-z_][\w:]*",
        "begin break catch class continue data do dynamicparam else elseif end exit filter finally for foreach function if in param process return switch throw trap try until using while",
        ignoreCase: true);

    private static readonly SyntaxLanguage s_python = new(
        "#[^\n]*",
        @"[rbfuRBFU]{0,2}(?:""""""[\s\S]*?""""""|'''[\s\S]*?'''|" + DoubleQuoted + "|" + SingleQuoted + ")",
        variable: null,
        "False None True and as assert async await break case class continue def del elif else except finally for from global if import in is lambda match nonlocal not or pass raise return self try while with yield");

    private static readonly SyntaxLanguage s_cLike = new(
        SlashComment,
        DoubleQuoted + "|" + SingleQuoted + @"|`(?:\\.|[^`\\])*`",
        variable: null,
        "abstract as async await bool boolean break byte case catch char class const continue default defer delete do double else enum export extends false final finally float fn for func function go if impl implements import in int interface internal is let long match mod mut namespace new nil null override package private protected pub public readonly return sealed self static string struct super switch this throw throws trait true try type typeof uint using val var virtual void when where while yield");

    private static readonly SyntaxLanguage s_json = new(
        SlashComment,
        DoubleQuoted,
        variable: null,
        "true false null",
        key: DoubleQuoted + @"(?=\s*:)");

    private static readonly SyntaxLanguage s_yaml = new(
        HashComment,
        DoubleQuoted + "|" + SingleQuoted,
        variable: null,
        "true false null yes no on off",
        key: @"^[ \t-]*[\w.\-/]+(?=[ \t]*:(?:[ \t]|$))");

    private static readonly SyntaxLanguage s_settings = new(
        @"^[ \t]*[#;][^\n]*",
        DoubleQuoted + "|" + SingleQuoted,
        @"\$\{[^}\n]*\}",
        "true false",
        key: @"^[ \t]*\[[^\]\n]*\]|^[ \t]*[\w.\-]+(?=[ \t]*=)");

    private static readonly SyntaxLanguage s_sql = new(
        @"--[^\n]*|/\*[\s\S]*?\*/",
        @"'(?:''|[^'])*'",
        variable: null,
        "add all alter and as asc begin by case commit create default delete desc distinct drop else end exists foreign from full grant group having in index inner insert into is join key left like limit not null offset on or order outer primary references returning revoke right rollback select set table then union update values view when where with",
        ignoreCase: true);

    private static readonly SyntaxLanguage s_docker = new(
        HashComment,
        DoubleQuoted + "|" + SingleQuoted,
        @"\$\{[^}\n]*\}|\$[A-Za-z_]\w*",
        "FROM RUN CMD LABEL EXPOSE ENV ADD COPY ENTRYPOINT VOLUME USER WORKDIR ARG ONBUILD STOPSIGNAL HEALTHCHECK SHELL AS");

    private static readonly SyntaxLanguage s_markup = new(
        @"<!--[\s\S]*?-->",
        @"""[^""]*""|'[^']*'",
        variable: null,
        keywords: string.Empty,
        key: @"</?[A-Za-z][\w:.\-]*|/?>");

    private static readonly Dictionary<string, SyntaxLanguage> s_byName = Names();

    private SyntaxLanguage(string comment, string strings, string? variable, string keywords, bool ignoreCase = false, string? key = null)
    {
        var parts = new List<string> { $"(?<comment>{comment})" };
        parts.AddRange(key is null ? [] : [$"(?<keyword>{key})"]);
        parts.Add($"(?<string>{strings})");
        parts.AddRange(variable is null ? [] : [$"(?<variable>{variable})"]);
        parts.Add($"(?<number>{Number})");
        parts.AddRange(keywords.Length == 0 ? [] : [$@"(?<keyword>\b(?:{string.Join('|', keywords.Split(' '))})\b)"]);
        var options = RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        Pattern = new Regex(string.Join('|', parts), options, s_timeout);
    }

    public Regex Pattern { get; }

    /// <summary>The language a code block's info string names, e.g. <c>bash</c> in <c>```bash</c>; null for one we don't colour.</summary>
    public static SyntaxLanguage? Find(string name) => s_byName.GetValueOrDefault(name);

    private static Dictionary<string, SyntaxLanguage> Names()
    {
        var names = new Dictionary<string, SyntaxLanguage>(StringComparer.OrdinalIgnoreCase);
        Add(names, s_shell, "sh bash zsh shell console shellsession fish ksh terminal");
        Add(names, s_powerShell, "powershell ps ps1 pwsh");
        Add(names, s_python, "python py python3");
        Add(names, s_cLike, "js javascript jsx mjs ts typescript tsx cs csharp c# java go golang rust rs c cpp c++ h hpp swift kotlin kt scala php dart");
        Add(names, s_json, "json jsonc json5");
        Add(names, s_yaml, "yaml yml");
        Add(names, s_settings, "toml ini cfg conf properties env dotenv");
        Add(names, s_sql, "sql mysql postgres postgresql psql sqlite");
        Add(names, s_docker, "dockerfile docker containerfile");
        Add(names, s_markup, "xml html htm svg xaml axaml plist csproj");
        return names;
    }

    private static void Add(Dictionary<string, SyntaxLanguage> names, SyntaxLanguage language, string aliases)
    {
        foreach (var alias in aliases.Split(' '))
        {
            names[alias] = language;
        }
    }
}
