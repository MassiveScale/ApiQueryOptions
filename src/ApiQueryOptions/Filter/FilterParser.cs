using ApiQueryOptions.Exceptions;

namespace ApiQueryOptions.Filter;

/// <summary>
/// Recursive descent parser for OData-style <c>$filter</c> expressions.
/// Grammar:
/// <code>
/// expr       → or_expr
/// or_expr    → and_expr ( 'or'  and_expr )*
/// and_expr   → primary  ( 'and' primary  )*
/// primary    → '(' expr ')' | func_call | comparison
/// func_call  → ('startsWith'|'endsWith'|'contains') '(' identifier ',' string ')'
/// comparison → identifier op value
/// op         → 'eq'|'ne'|'lt'|'gt'|'le'|'ge'
/// value      → string | int | decimal | bool | null
/// </code>
/// Parenthesis nesting depth and the total node count are bounded so that hostile input
/// cannot exhaust the stack or build an arbitrarily large expression tree.
/// </summary>
public sealed class FilterParser
{
    private readonly int _maxDepth;
    private readonly int _maxNodeCount;
    private int _depth;
    private int _nodeCount;
    private int _pos;
    private string _raw = null!;
    private List<Token> _tokens = null!;

    /// <summary>
    /// Initializes a new parser using <see cref="ApiQueryOptionsSettings.DefaultMaxFilterDepth"/>
    /// and <see cref="ApiQueryOptionsSettings.DefaultMaxFilterNodeCount"/>.
    /// </summary>
    public FilterParser()
        : this(ApiQueryOptionsSettings.DefaultMaxFilterDepth, ApiQueryOptionsSettings.DefaultMaxFilterNodeCount)
    {
    }

    /// <summary>
    /// Initializes a new parser with explicit limits.
    /// </summary>
    /// <param name="maxDepth">The maximum parenthesis nesting depth. Must be positive.</param>
    /// <param name="maxNodeCount">
    /// The maximum number of comparison, function, and logical nodes. Must be positive.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">When either limit is zero or negative.</exception>
    public FilterParser(int maxDepth, int maxNodeCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxNodeCount);

        _maxDepth = maxDepth;
        _maxNodeCount = maxNodeCount;
    }

    /// <summary>
    /// Parses a raw OData-style <c>$filter</c> string into a <see cref="FilterClause"/> AST.
    /// </summary>
    /// <param name="rawFilter">The raw filter expression, e.g. <c>Species eq 'Dog' and IsAvailable eq true</c>.</param>
    /// <returns>A <see cref="FilterClause"/> containing the root AST node.</returns>
    /// <exception cref="Exceptions.FilterParseException">
    /// Thrown when the expression cannot be parsed, or when it exceeds the nesting depth or node count limit.
    /// </exception>
    public FilterClause Parse(string rawFilter)
    {
        if (string.IsNullOrWhiteSpace(rawFilter))
        {
            throw new FilterParseException("Filter expression cannot be empty.", rawFilter ?? "", 0);
        }

        _raw = rawFilter;
        _tokens = new FilterLexer(rawFilter).Tokenize();
        _pos = 0;
        _depth = 0;
        _nodeCount = 0;

        FilterNode node = ParseOr();
        Expect(TokenKind.Eof);
        return new FilterClause(node);
    }

    private Token Consume()
    {
        Token tok = Peek();
        if (tok.Kind != TokenKind.Eof)
        {
            _pos++;
        }

        return tok;
    }

    private void CountNode(int position)
    {
        _nodeCount++;
        if (_nodeCount > _maxNodeCount)
        {
            throw new FilterParseException(
                $"Filter expression exceeds the maximum of {_maxNodeCount} nodes at position {position}.",
                _raw, position);
        }
    }

    private void Expect(TokenKind kind)
    {
        Token tok = Peek();
        if (tok.Kind != kind)
        {
            throw new FilterParseException(
                $"Expected '{kind}' but found '{tok.Raw}' at position {tok.Position}.",
                _raw, tok.Position);
        }

        if (tok.Kind != TokenKind.Eof)
        {
            _pos++;
        }
    }

    private Token ExpectKind(TokenKind kind, string description)
    {
        Token tok = Peek();
        if (tok.Kind != kind)
        {
            throw new FilterParseException(
                $"Expected {description} but found '{tok.Raw}' at position {tok.Position}.",
                _raw, tok.Position);
        }

        _pos++;
        return tok;
    }

    private FilterNode ParseAnd()
    {
        FilterNode left = ParsePrimary();
        while (PeekIdentifier("and"))
        {
            Token andToken = Consume();
            CountNode(andToken.Position);
            FilterNode right = ParsePrimary();
            left = new LogicalFilterNode(left, LogicalOperator.And, right);
        }
        return left;
    }

    private BinaryFilterNode ParseComparison()
    {
        Token propToken = ExpectKind(TokenKind.Identifier, "property name");
        string property = propToken.Raw;
        CountNode(propToken.Position);

        Token opToken = ExpectKind(TokenKind.Identifier, "operator (eq, ne, lt, gt, le, ge)");
        FilterOperator op = opToken.Raw.ToLowerInvariant() switch
        {
            "eq" => FilterOperator.Eq,
            "ne" => FilterOperator.Ne,
            "lt" => FilterOperator.Lt,
            "gt" => FilterOperator.Gt,
            "le" => FilterOperator.Le,
            "ge" => FilterOperator.Ge,
            _ => throw new FilterParseException(
                    $"Unknown operator '{opToken.Raw}' at position {opToken.Position}. Expected eq, ne, lt, gt, le, or ge.",
                    _raw, opToken.Position)
        };

        object? value = ParseValue();
        return new BinaryFilterNode(property, op, value);
    }

    private FunctionFilterNode ParseFunctionCall()
    {
        Token funcToken = Consume(); // function name
        StringFunction func = funcToken.Raw.ToLowerInvariant() switch
        {
            "startswith" => StringFunction.StartsWith,
            "endswith" => StringFunction.EndsWith,
            "contains" => StringFunction.Contains,
            _ => throw new FilterParseException(
                    $"Unknown function '{funcToken.Raw}' at position {funcToken.Position}.",
                    _raw, funcToken.Position)
        };
        CountNode(funcToken.Position);

        Expect(TokenKind.LParen);

        Token propToken = ExpectKind(TokenKind.Identifier, "property name");
        string property = propToken.Raw;

        Expect(TokenKind.Comma);

        Token valToken = ExpectKind(TokenKind.StringLiteral, "string literal");
        string value = valToken.Raw;

        Expect(TokenKind.RParen);

        return new FunctionFilterNode(func, property, value);
    }

    private FilterNode ParseOr()
    {
        FilterNode left = ParseAnd();
        while (PeekIdentifier("or"))
        {
            Token orToken = Consume();
            CountNode(orToken.Position);
            FilterNode right = ParseAnd();
            left = new LogicalFilterNode(left, LogicalOperator.Or, right);
        }
        return left;
    }

    private FilterNode ParsePrimary()
    {
        Token tok = Peek();

        if (tok.Kind == TokenKind.LParen)
        {
            if (_depth >= _maxDepth)
            {
                throw new FilterParseException(
                    $"Filter expression exceeds the maximum nesting depth of {_maxDepth} at position {tok.Position}.",
                    _raw, tok.Position);
            }

            Consume(); // '('
            _depth++;
            FilterNode inner = ParseOr();
            Expect(TokenKind.RParen);
            _depth--;
            return inner;
        }

        if (tok.Kind == TokenKind.Identifier)
        {
            string name = tok.Raw.ToLowerInvariant();

            if (name is "startswith" or "endswith" or "contains")
            {
                return ParseFunctionCall();
            }

            return ParseComparison();
        }

        throw new FilterParseException(
            $"Unexpected token '{tok.Raw}' at position {tok.Position}. Expected '(', function name, or property name.",
            _raw, tok.Position);
    }

    private object? ParseValue()
    {
        Token tok = Consume();
        return tok.Kind switch
        {
            TokenKind.NullLiteral => null,
            TokenKind.BoolLiteral => bool.Parse(tok.Raw),
            TokenKind.StringLiteral => tok.Raw,
            TokenKind.IntLiteral => int.TryParse(tok.Raw, out int i)
                                        ? (object)i
                                        : throw new FilterParseException(
                                            $"Integer value '{tok.Raw}' is out of range at position {tok.Position}.",
                                            _raw, tok.Position),
            TokenKind.DecimalLiteral => decimal.TryParse(tok.Raw, System.Globalization.NumberStyles.Number,
                                            System.Globalization.CultureInfo.InvariantCulture, out decimal d)
                                        ? (object)d
                                        : throw new FilterParseException(
                                            $"Decimal value '{tok.Raw}' is out of range at position {tok.Position}.",
                                            _raw, tok.Position),
            _ => throw new FilterParseException(
                    $"Unexpected token '{tok.Raw}' at position {tok.Position}. Expected a value (string, number, bool, null).",
                    _raw, tok.Position)
        };
    }

    private Token Peek() => _pos < _tokens.Count ? _tokens[_pos] : _tokens[^1];

    private bool PeekIdentifier(string keyword)
    {
        Token tok = Peek();
        return tok.Kind == TokenKind.Identifier &&
               string.Equals(tok.Raw, keyword, StringComparison.OrdinalIgnoreCase);
    }
}
