namespace ApiQueryOptions.Filter;

internal enum TokenKind
{
    Identifier,
    StringLiteral,
    IntLiteral,
    DecimalLiteral,
    BoolLiteral,
    NullLiteral,
    LParen,
    RParen,
    Comma,
    Eof
}

internal readonly struct Token
{
    public Token(TokenKind kind, string raw, int position)
    {
        Kind = kind;
        Raw = raw;
        Position = position;
    }

    public TokenKind Kind { get; }
    public int Position { get; }
    public string Raw { get; }

    public override string ToString() => $"[{Kind} '{Raw}' @{Position}]";
}

/// <summary>
/// Tokenizes a raw OData-style <c>$filter</c> string.
/// </summary>
internal sealed class FilterLexer
{
    private readonly string _input;
    private int _pos;

    public FilterLexer(string input)
    {
        _input = input;
        _pos = 0;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            SkipWhitespace();
            if (_pos >= _input.Length)
            {
                tokens.Add(new Token(TokenKind.Eof, "", _pos));
                break;
            }

            int start = _pos;
            char ch = _input[_pos];

            if (ch == '(') { _pos++; tokens.Add(new Token(TokenKind.LParen, "(", start)); continue; }
            if (ch == ')') { _pos++; tokens.Add(new Token(TokenKind.RParen, ")", start)); continue; }
            if (ch == ',') { _pos++; tokens.Add(new Token(TokenKind.Comma, ",", start)); continue; }

            if (ch == '\'')
            {
                tokens.Add(ReadStringLiteral(start));
                continue;
            }

            if (char.IsDigit(ch) || (ch == '-' && _pos + 1 < _input.Length && char.IsDigit(_input[_pos + 1])))
            {
                tokens.Add(ReadNumber(start));
                continue;
            }

            if (char.IsLetter(ch) || ch == '_')
            {
                tokens.Add(ReadIdentifierOrKeyword(start));
                continue;
            }

            throw new Exceptions.FilterParseException(
                $"Unexpected character '{ch}' at position {_pos}.", _input, _pos);
        }
        return tokens;
    }

    private Token ReadIdentifierOrKeyword(int start)
    {
        var sb = new System.Text.StringBuilder();
        while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_' || _input[_pos] == '.'))
        {
            sb.Append(_input[_pos++]);
        }
        string word = sb.ToString();
        TokenKind kind = word.ToLowerInvariant() switch
        {
            "true" or "false" => TokenKind.BoolLiteral,
            "null" => TokenKind.NullLiteral,
            _ => TokenKind.Identifier
        };
        return new Token(kind, word, start);
    }

    private Token ReadNumber(int start)
    {
        var sb = new System.Text.StringBuilder();
        if (_input[_pos] == '-') { sb.Append('-'); _pos++; }
        while (_pos < _input.Length && char.IsDigit(_input[_pos]))
        {
            sb.Append(_input[_pos++]);
        }
        bool isDecimal = false;
        if (_pos < _input.Length && _input[_pos] == '.')
        {
            isDecimal = true;
            sb.Append(_input[_pos++]);
            while (_pos < _input.Length && char.IsDigit(_input[_pos]))
            {
                sb.Append(_input[_pos++]);
            }
        }
        return new Token(isDecimal ? TokenKind.DecimalLiteral : TokenKind.IntLiteral, sb.ToString(), start);
    }

    private Token ReadStringLiteral(int start)
    {
        _pos++; // consume opening '
        var sb = new System.Text.StringBuilder();
        while (_pos < _input.Length)
        {
            char ch = _input[_pos];
            if (ch == '\'')
            {
                _pos++;
                // OData escape: '' = literal single quote
                if (_pos < _input.Length && _input[_pos] == '\'')
                {
                    sb.Append('\'');
                    _pos++;
                }
                else
                {
                    break;
                }
            }
            else
            {
                sb.Append(ch);
                _pos++;
            }
        }
        return new Token(TokenKind.StringLiteral, sb.ToString(), start);
    }

    private void SkipWhitespace()
    {
        while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
        {
            _pos++;
        }
    }
}