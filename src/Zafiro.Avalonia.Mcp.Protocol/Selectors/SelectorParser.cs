namespace Zafiro.Avalonia.Mcp.Protocol.Selectors;

/// <summary>
/// Parser for the Zafiro Avalonia MCP selector syntax.
///
/// Grammar:
///   selectorList   := path ( "," path )*
///   path           := compound ( combinator compound )*
///   combinator     := ">>" | ">"     // descendant or direct child; whitespace = descendant
///   compound       := ( type | "*" )? ( "#" id )? filter*
///   type           := IDENT
///   id             := DIGITS | IDENT
///   filter         := "[" attr "]" | ":" pseudoClass
///   attr           := path op value
///                  | "dc:" predicateString
///   path           := IDENT ( "." IDENT )*
///   op             := "=" | "*=" | "^=" | "$="
///   value          := QUOTED_STRING | UNQUOTED_TOKEN
///   pseudoClass    := IDENT ( "(" QUOTED_STRING | NUMBER | IDENT ")" )?
///
/// Examples:
///   Button
///   Button#42
///   #42
///   Button[Name=Save]
///   Button:has-text("Sign in"):enabled
///   ListBoxItem[dc.Id=42]
///   ListBoxItem[dc:'Id == 42 && IsActive']
///   ListBox >> ListBoxItem:nth(2)
///   *[role=button]:nth(0)
///   Button, MenuItem    // alternatives
/// </summary>
public static class SelectorParser
{
    public static ParsedSelector Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new SelectorParseException("selector is empty", 0);

        var tokens = Tokenize(input);
        var p = new ParserState(tokens, input);
        var alternatives = new List<SelectorPath>();
        alternatives.Add(ParsePath(p));
        while (p.Match(TokenKind.Comma))
        {
            alternatives.Add(ParsePath(p));
        }
        if (!p.IsAtEnd)
            throw new SelectorParseException($"unexpected token '{p.Current.Text}'", p.Current.Position);

        return new ParsedSelector(alternatives);
    }

    private static SelectorPath ParsePath(ParserState p)
    {
        var steps = new List<SelectorStep>();
        steps.Add(new SelectorStep(Combinator.Self, ParseCompound(p)));
        while (true)
        {
            Combinator? combo = null;
            if (p.Match(TokenKind.DescendantOp)) combo = Combinator.Descendant;
            else if (p.Match(TokenKind.ChildOp)) combo = Combinator.Child;
            else if (p.HasImplicitDescendant()) combo = Combinator.Descendant;
            if (combo is null) break;
            steps.Add(new SelectorStep(combo.Value, ParseCompound(p)));
        }
        return new SelectorPath(steps);
    }

    private static CompoundSelector ParseCompound(ParserState p)
    {
        string? typeName = null;
        int? nodeId = null;
        var filters = new List<SelectorFilter>();

        if (p.Match(TokenKind.Star))
        {
            typeName = null;
        }
        else if (p.Current.Kind == TokenKind.Ident)
        {
            typeName = p.Consume(TokenKind.Ident).Text;
        }

        while (true)
        {
            if (p.Match(TokenKind.Hash))
            {
                var idTok = p.Current;
                if (idTok.Kind == TokenKind.Number)
                {
                    p.Advance();
                    nodeId = int.Parse(idTok.Text);
                }
                else if (idTok.Kind == TokenKind.Ident)
                {
                    p.Advance();
                    filters.Add(new AttributeFilter("Name", AttrOp.Equal, idTok.Text, false));
                }
                else
                {
                    throw new SelectorParseException("expected nodeId or name after '#'", idTok.Position);
                }
            }
            else if (p.Match(TokenKind.LBracket))
            {
                filters.Add(ParseAttributeFilter(p));
                p.Consume(TokenKind.RBracket);
            }
            else if (p.Match(TokenKind.Colon))
            {
                filters.Add(ParsePseudoFilter(p));
            }
            else
            {
                break;
            }
        }

        if (typeName is null && nodeId is null && filters.Count == 0)
            throw new SelectorParseException("compound selector cannot be empty", p.Current.Position);

        return new CompoundSelector(typeName, nodeId, filters);
    }

    private static SelectorFilter ParseAttributeFilter(ParserState p)
    {
        // Special form: [dc:'expr']
        if (p.Current.Kind == TokenKind.Ident && p.Current.Text == "dc"
            && p.Peek(1).Kind == TokenKind.Colon)
        {
            p.Advance();      // dc
            p.Advance();      // :
            var v = p.Current;
            if (v.Kind != TokenKind.QuotedString)
                throw new SelectorParseException("expected quoted predicate after dc:", v.Position);
            p.Advance();
            return new DataContextPredicateFilter(v.Text);
        }

        var pathParts = new List<string>();
        if (p.Current.Kind != TokenKind.Ident)
            throw new SelectorParseException("expected attribute name", p.Current.Position);
        pathParts.Add(p.Consume(TokenKind.Ident).Text);
        while (p.Match(TokenKind.Dot))
        {
            if (p.Current.Kind != TokenKind.Ident)
                throw new SelectorParseException("expected identifier after '.'", p.Current.Position);
            pathParts.Add(p.Consume(TokenKind.Ident).Text);
        }
        var path = string.Join('.', pathParts);
        var isDc = pathParts[0].Equals("dc", StringComparison.Ordinal);
        if (isDc) path = pathParts.Count > 1 ? string.Join('.', pathParts.Skip(1)) : "";

        AttrOp op;
        if (p.Match(TokenKind.OpEq)) op = AttrOp.Equal;
        else if (p.Match(TokenKind.OpContains)) op = AttrOp.Contains;
        else if (p.Match(TokenKind.OpStartsWith)) op = AttrOp.StartsWith;
        else if (p.Match(TokenKind.OpEndsWith)) op = AttrOp.EndsWith;
        else throw new SelectorParseException("expected operator (=, *=, ^=, $=)", p.Current.Position);

        var valueTok = p.Current;
        string value;
        if (valueTok.Kind == TokenKind.QuotedString || valueTok.Kind == TokenKind.Ident
            || valueTok.Kind == TokenKind.Number)
        {
            value = valueTok.Text;
            p.Advance();
        }
        else
        {
            throw new SelectorParseException("expected attribute value", valueTok.Position);
        }

        return new AttributeFilter(path, op, value, isDc);
    }

    private static SelectorFilter ParsePseudoFilter(ParserState p)
    {
        if (p.Current.Kind != TokenKind.Ident)
            throw new SelectorParseException("expected pseudo-class name", p.Current.Position);
        var name = p.Consume(TokenKind.Ident).Text;
        // hyphenated names (has-text, nth-child)
        while (p.Current.Kind == TokenKind.Minus)
        {
            p.Advance();
            if (p.Current.Kind != TokenKind.Ident)
                throw new SelectorParseException("expected identifier after '-'", p.Current.Position);
            name += "-" + p.Consume(TokenKind.Ident).Text;
        }
        string? arg = null;
        if (p.Match(TokenKind.LParen))
        {
            var t = p.Current;
            if (t.Kind == TokenKind.QuotedString || t.Kind == TokenKind.Ident || t.Kind == TokenKind.Number)
            {
                arg = t.Text;
                p.Advance();
            }
            p.Consume(TokenKind.RParen);
        }
        return new PseudoFilter(name, arg);
    }

    // ---------- tokenizer ----------

    private enum TokenKind
    {
        Ident,
        Number,
        QuotedString,
        Hash,
        Dot,
        LBracket,
        RBracket,
        LParen,
        RParen,
        Colon,
        Comma,
        Star,
        Minus,
        DescendantOp,    // >>
        ChildOp,         // >
        OpEq,            // =
        OpContains,      // *=
        OpStartsWith,    // ^=
        OpEndsWith,      // $=
        Whitespace,
        End,
    }

    private readonly record struct Token(TokenKind Kind, string Text, int Position, bool PrecededByWs);

    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        int i = 0;
        bool ws = false;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c))
            {
                ws = true;
                i++;
                continue;
            }
            int start = i;
            if (c == '#') { tokens.Add(new Token(TokenKind.Hash, "#", start, ws)); i++; ws = false; continue; }
            if (c == '.') { tokens.Add(new Token(TokenKind.Dot, ".", start, ws)); i++; ws = false; continue; }
            if (c == '[') { tokens.Add(new Token(TokenKind.LBracket, "[", start, ws)); i++; ws = false; continue; }
            if (c == ']') { tokens.Add(new Token(TokenKind.RBracket, "]", start, ws)); i++; ws = false; continue; }
            if (c == '(') { tokens.Add(new Token(TokenKind.LParen, "(", start, ws)); i++; ws = false; continue; }
            if (c == ')') { tokens.Add(new Token(TokenKind.RParen, ")", start, ws)); i++; ws = false; continue; }
            if (c == ':') { tokens.Add(new Token(TokenKind.Colon, ":", start, ws)); i++; ws = false; continue; }
            if (c == ',') { tokens.Add(new Token(TokenKind.Comma, ",", start, ws)); i++; ws = false; continue; }
            if (c == '-') { tokens.Add(new Token(TokenKind.Minus, "-", start, ws)); i++; ws = false; continue; }
            if (c == '*')
            {
                if (i + 1 < s.Length && s[i + 1] == '=')
                {
                    tokens.Add(new Token(TokenKind.OpContains, "*=", start, ws));
                    i += 2;
                }
                else
                {
                    tokens.Add(new Token(TokenKind.Star, "*", start, ws));
                    i++;
                }
                ws = false;
                continue;
            }
            if (c == '^' && i + 1 < s.Length && s[i + 1] == '=')
            {
                tokens.Add(new Token(TokenKind.OpStartsWith, "^=", start, ws));
                i += 2; ws = false; continue;
            }
            if (c == '$' && i + 1 < s.Length && s[i + 1] == '=')
            {
                tokens.Add(new Token(TokenKind.OpEndsWith, "$=", start, ws));
                i += 2; ws = false; continue;
            }
            if (c == '=')
            {
                tokens.Add(new Token(TokenKind.OpEq, "=", start, ws));
                i++; ws = false; continue;
            }
            if (c == '>')
            {
                if (i + 1 < s.Length && s[i + 1] == '>')
                {
                    tokens.Add(new Token(TokenKind.DescendantOp, ">>", start, ws));
                    i += 2;
                }
                else
                {
                    tokens.Add(new Token(TokenKind.ChildOp, ">", start, ws));
                    i++;
                }
                ws = false; continue;
            }
            if (c == '\'' || c == '"')
            {
                char quote = c;
                i++;
                var sb = new System.Text.StringBuilder();
                while (i < s.Length && s[i] != quote)
                {
                    if (s[i] == '\\' && i + 1 < s.Length)
                    {
                        sb.Append(s[i + 1]);
                        i += 2;
                    }
                    else
                    {
                        sb.Append(s[i]);
                        i++;
                    }
                }
                if (i >= s.Length)
                    throw new SelectorParseException($"unterminated string literal", start);
                i++; // closing quote
                tokens.Add(new Token(TokenKind.QuotedString, sb.ToString(), start, ws));
                ws = false; continue;
            }
            if (char.IsDigit(c))
            {
                int j = i;
                while (j < s.Length && char.IsDigit(s[j])) j++;
                tokens.Add(new Token(TokenKind.Number, s[i..j], start, ws));
                i = j; ws = false; continue;
            }
            if (IsIdentStart(c))
            {
                int j = i;
                while (j < s.Length && IsIdentCont(s[j])) j++;
                tokens.Add(new Token(TokenKind.Ident, s[i..j], start, ws));
                i = j; ws = false; continue;
            }
            throw new SelectorParseException($"unexpected character '{c}'", start);
        }
        tokens.Add(new Token(TokenKind.End, "<end>", s.Length, ws));
        return tokens;
    }

    private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentCont(char c) => char.IsLetterOrDigit(c) || c == '_';

    private sealed class ParserState
    {
        private readonly List<Token> _tokens;
        public string Source { get; }
        public int Index;

        public ParserState(List<Token> tokens, string source)
        {
            _tokens = tokens;
            Source = source;
        }

        public Token Current => _tokens[Index];
        public bool IsAtEnd => Current.Kind == TokenKind.End;

        public Token Peek(int offset)
        {
            int i = Index + offset;
            return i < _tokens.Count ? _tokens[i] : _tokens[^1];
        }

        public bool Match(TokenKind kind)
        {
            if (Current.Kind == kind) { Advance(); return true; }
            return false;
        }

        public Token Consume(TokenKind kind)
        {
            if (Current.Kind != kind)
                throw new SelectorParseException($"expected {kind}, got '{Current.Text}'", Current.Position);
            var t = Current;
            Advance();
            return t;
        }

        public void Advance() => Index++;

        /// <summary>
        /// Whitespace separating two compound selectors implies a descendant combinator,
        /// unless the next token is a structural punctuator (comma, end, closing bracket).
        /// </summary>
        public bool HasImplicitDescendant()
        {
            if (IsAtEnd) return false;
            if (!Current.PrecededByWs) return false;
            return Current.Kind switch
            {
                TokenKind.Comma => false,
                TokenKind.RBracket => false,
                TokenKind.RParen => false,
                TokenKind.End => false,
                _ => true,
            };
        }
    }
}
