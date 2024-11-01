namespace NewPaloAltoTB;

public interface IExpression {
    //internal static Expression Shared;
    //private static readonly Lazy<Expression> shared = new(() => new Expression());

    //internal CodeParser Parser = CodeParser.Shared;

    /// <summary>
    /// Evaluate an expression fragment, at 1st level of operator precedence (comparison operators).
    /// This operator cannot be repeated (1<2<3 and a=b=c are illegal).
    /// </summary>
    /// <returns>Signed short value of expression, if successful</returns>
    /// <exception cref="RuntimeException">Thrown if parsing or calculation fails</exception>
    public bool TryEvaluateExpr(out short value);

    /// <summary>
    /// Evaluate an expression fragment, at 2nd level of operator precedence ( [-] a (+|-) b )
    /// </summary>
    /// <returns></returns>
    /// <exception cref="RuntimeException"></exception>
    //public short? TryExprComparisonTerm();

    //public short? TryExprAddSubTerm();

    //public short? TryExprMulDivTerm();

    //public bool TryGetFunction(out short value);

    //public bool TryGetVariable(out short value);

    public short ParenExpr();

    public bool TryGetParen(out short value);
}