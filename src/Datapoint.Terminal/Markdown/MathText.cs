using System.Text;

namespace Datapoint.Terminal.Markdown;

/// <summary>
/// Shows TeX maths as Unicode text: Greek letters, operators, arrows, fractions as a/b, roots as √x, and simple
/// superscripts and subscripts. It isn't a typesetter: matrices, big operators with limits and alignment stay linear.
/// </summary>
internal static class MathText
{
    private static readonly Dictionary<string, string> s_symbols = Symbols();

    private static readonly Dictionary<char, char> s_superscripts = Pairs("0123456789+-=()ni", "⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻⁼⁽⁾ⁿⁱ");

    private static readonly Dictionary<char, char> s_subscripts = Pairs("0123456789+-=()aeoxhklmnpstijruv", "₀₁₂₃₄₅₆₇₈₉₊₋₌₍₎ₐₑₒₓₕₖₗₘₙₚₛₜᵢⱼᵣᵤᵥ");

    private static readonly Dictionary<char, char> s_doubleStruck = Pairs("RNZQCPH", "ℝℕℤℚℂℙℍ");

    public static string ToUnicode(string tex)
    {
        var position = 0;
        return Convert(tex, ref position, stopAtBrace: false).Trim();
    }

    private static string Convert(string tex, ref int position, bool stopAtBrace)
    {
        var text = new StringBuilder();
        while (position < tex.Length)
        {
            var character = tex[position++];
            if (character == '}' && stopAtBrace)
            {
                break;
            }

            text.Append(Piece(character, tex, ref position));
        }

        return text.ToString();
    }

    private static string Piece(char character, string tex, ref int position) => character switch
    {
        '\\' => Command(ReadName(tex, ref position), tex, ref position),
        '^' => Script(Group(tex, ref position), s_superscripts, '^'),
        '_' => Script(Group(tex, ref position), s_subscripts, '_'),
        '{' => Convert(tex, ref position, stopAtBrace: true),
        '}' => string.Empty,
        '&' or '~' => " ",
        _ => character.ToString(),
    };

    private static string Command(string name, string tex, ref int position) => name switch
    {
        "frac" or "dfrac" or "tfrac" => Fraction(Group(tex, ref position), Group(tex, ref position)),
        "sqrt" => Root(tex, ref position),
        "text" or "mathrm" or "mathbf" or "mathit" or "mathsf" or "mathtt" or "textbf" or "textit" or "operatorname" => Group(tex, ref position),
        "mathbb" => string.Concat(Group(tex, ref position).Select(letter => s_doubleStruck.GetValueOrDefault(letter, letter))),
        "left" or "right" or "big" or "Big" or "bigl" or "bigr" or "Bigl" or "Bigr" or "displaystyle" or "limits" or "!" => string.Empty,
        "," or ";" or ":" or " " => " ",
        "quad" or "qquad" => "  ",
        "\\" => "\n",
        _ => s_symbols.TryGetValue(name, out var symbol) ? symbol : name.Length == 1 ? name : "\\" + name,
    };

    private static string ReadName(string tex, ref int position)
    {
        var start = position;
        while (position < tex.Length && char.IsAsciiLetter(tex[position]))
        {
            position++;
        }

        if (position == start && position < tex.Length)
        {
            position++;
        }

        return tex[start..position];
    }

    /// <summary>Reads one argument: a {group}, a \command, or a single character, and converts it.</summary>
    private static string Group(string tex, ref int position)
    {
        while (position < tex.Length && tex[position] == ' ')
        {
            position++;
        }

        if (position >= tex.Length)
        {
            return string.Empty;
        }

        var character = tex[position++];
        return character switch
        {
            '{' => Convert(tex, ref position, stopAtBrace: true),
            '\\' => Command(ReadName(tex, ref position), tex, ref position),
            _ => character.ToString(),
        };
    }

    private static string Root(string tex, ref int position)
    {
        var index = string.Empty;
        if (position < tex.Length && tex[position] == '[')
        {
            var end = tex.IndexOf(']', position);
            index = end < 0 ? string.Empty : Script(tex[(position + 1)..end], s_superscripts, '^');
            position = end < 0 ? position : end + 1;
        }

        return index + "√" + Wrap(Group(tex, ref position));
    }

    private static string Fraction(string numerator, string denominator) => Wrap(numerator) + "/" + Wrap(denominator);

    private static string Wrap(string text) => text.Length <= 1 || text.All(char.IsLetterOrDigit) ? text : "(" + text + ")";

    private static string Script(string text, Dictionary<char, char> map, char marker) =>
        text.Length > 0 && text.All(map.ContainsKey) ? string.Concat(text.Select(character => map[character])) : marker + (text.Length <= 1 ? text : "(" + text + ")");

    private static Dictionary<char, char> Pairs(string from, string to) =>
        from.Zip(to).ToDictionary(pair => pair.First, pair => pair.Second);

    private static Dictionary<string, string> Symbols()
    {
        var symbols = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(symbols, "alpha α beta β gamma γ delta δ epsilon ϵ varepsilon ε zeta ζ eta η theta θ vartheta ϑ iota ι kappa κ lambda λ mu μ nu ν xi ξ pi π varpi ϖ rho ρ sigma σ tau τ upsilon υ phi ϕ varphi φ chi χ psi ψ omega ω");
        Add(symbols, "Gamma Γ Delta Δ Theta Θ Lambda Λ Xi Ξ Pi Π Sigma Σ Upsilon Υ Phi Φ Psi Ψ Omega Ω");
        Add(symbols, "times × cdot · div ÷ pm ± mp ∓ ast ∗ star ⋆ circ ∘ oplus ⊕ otimes ⊗ setminus ∖ le ≤ leq ≤ ge ≥ geq ≥ ne ≠ neq ≠ approx ≈ equiv ≡ sim ∼ simeq ≃ cong ≅ propto ∝ ll ≪ gg ≫");
        Add(symbols, "infty ∞ partial ∂ nabla ∇ sum ∑ prod ∏ int ∫ iint ∬ oint ∮ in ∈ notin ∉ ni ∋ subset ⊂ subseteq ⊆ supset ⊃ supseteq ⊇ cup ∪ cap ∩ emptyset ∅ varnothing ∅ forall ∀ exists ∃ neg ¬ lnot ¬ land ∧ wedge ∧ lor ∨ vee ∨");
        Add(symbols, "to → rightarrow → leftarrow ← gets ← Rightarrow ⇒ Leftarrow ⇐ leftrightarrow ↔ Leftrightarrow ⇔ iff ⇔ implies ⇒ mapsto ↦ uparrow ↑ downarrow ↓");
        Add(symbols, "ldots … cdots ⋯ dots … vdots ⋮ ddots ⋱ prime ′ angle ∠ perp ⊥ parallel ∥ mid ∣ vert | Vert ‖ lvert | rvert | langle ⟨ rangle ⟩ lfloor ⌊ rfloor ⌋ lceil ⌈ rceil ⌉ ell ℓ hbar ℏ Re ℜ Im ℑ aleph ℵ degree °");
        Add(symbols, "sin sin cos cos tan tan log log ln ln exp exp lim lim max max min min det det gcd gcd sup sup inf inf arg arg");
        symbols["{"] = "{";
        symbols["}"] = "}";
        symbols["%"] = "%";
        symbols["$"] = "$";
        symbols["_"] = "_";
        symbols["#"] = "#";
        symbols["&"] = "&";
        return symbols;
    }

    private static void Add(Dictionary<string, string> symbols, string pairs)
    {
        var parts = pairs.Split(' ');
        for (var index = 0; index + 1 < parts.Length; index += 2)
        {
            symbols[parts[index]] = parts[index + 1];
        }
    }
}
