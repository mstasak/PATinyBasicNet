using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewPaloAltoTB;

/// <summary>
/// Expression evaluation service
/// </summary>
internal class Expression {

    /// <summary>
    /// Accessable singleton object for this class.
    /// </summary>
    internal static Expression Shared => shared.Value;
    private static readonly Lazy<Expression> shared = new(() => new Expression());

    internal ParserTools parser = ParserTools.Shared;

    /// <summary>
    /// Evaluate an expression fragment, at 1st level of operator precedence (comparison operators).
    /// This operator cannot be repeated (1<2<3 and a=b=c are illegal).
    /// </summary>
    /// <returns>Signed short value of expression, if successful</returns>
    /// <exception cref="RuntimeException">Thrown if parsing or calculation fails</exception>
    internal bool TryEvaluateExpr(out short value) {
        /*
            ;*
            ;**************************************************************
            ;*
            ;* *** EXPR ***
            ;*
            ;* 'EXPR' EVALUATES ARITHMETICAL OR LOGICAL EXPRESSIONS.
            ;* <EXPR>::=<EXPR2>
            ;*          <EXPR2><REL.OP.><EXPR2>
            ;* WHERE <REL.OP.> IS ONE OF THE OPERATORSs IN TAB8 AND THE
            ;* RESULT OF THESE OPERATIONS IS 1 IFF TRUE AND 0 IFF FALSE.
            ;* <EXPR2>::=(+ OR -)<EXPR3>(+ OR -<EXPR3>)(....)
            ;* WHERE () ARE OPTIONAL AND (....) ARE OPTIONAL REPEATS.
            ;* <EXPR3>::=<EXPR4>(<* OR /><EXPR4>)(....)
            ;* <EXPR4>::=<VARIABLE>
            ;*           <FUNCTION>
            ;*           (<EXPR>)
            ;* <EXPR> IS RECURSIVE SO THAT VARIABLE '@' CAN HAVE AN <EXPR>
            ;* AS INDEX, FUNCTIONS CAN HAVE AN <EXPR> AS ARGUMENTS, AND
            ;* <EXPR4> CAN BE AN <EXPR> IN PARENTHESES.
            ;*
         */
        int a, b;
        var oldLinePos = parser.LinePosition;
        try {
            a = ExprComparisonTerm();
            var whichOp = parser.ScanStringTableEntry(["=", "<=", "<>", "<", ">=", ">", "#"]);
            if (whichOp == null) {
                value = (short)a;
                return true;
            }
            //fix terms into signed short range (-32768..32767)
            b = ExprComparisonTerm();
            a = whichOp switch {
                0 => (a == b) ? 1 : 0,
                1 => (a <= b) ? 1 : 0,
                2 => (a != b) ? 1 : 0,
                3 => (a < b) ? 1 : 0,
                4 => (a >= b) ? 1 : 0,
                5 => (a > b) ? 1 : 0,
                6 => (a != b) ? 1 : 0,
                _ => a,
            };
            value = (short)a;
            return true;
        } catch (Exception) {
            value = 0;
            return false;
            //throw;
        }
    }

    /// <summary>
    /// Evaluate an expression fragment, at 2nd level of operator precedence ( [-] a (+|-) b )
    /// </summary>
    /// <returns></returns>
    /// <exception cref="RuntimeException"></exception>
    internal short ExprComparisonTerm() {
        int a, b;
        if (parser.ScanString("-")) {
            //- prefix
            a = -ExprAddSubTerm();
        }
        else {
            a = ExprAddSubTerm();
        }
        int? match;
        while ((match = parser.ScanStringTableEntry(["+", "-"])) != null) {
            b = ExprAddSubTerm();
            a = match switch {
                0 => a + b,
                1 => a - b,
                _ => a,
            };
        }
        if (a < short.MinValue) {
            throw new RuntimeException("Arithmetic underflow");
        }
        if (a > short.MaxValue) {
            throw new RuntimeException("Arithmetic overflow");
        }
        return (short)a;
    }

    internal short ExprAddSubTerm() {
        int a, b;
        a = ExprMulDivTerm();
        int? match;
        while (true) {
            parser.SkipSpaces();
            match = parser.ScanStringTableEntry(["*", "/", "%"]);
            if (match == null) {
                break;
            }

            b = ExprMulDivTerm();
            if ((match == 1 || match == 2) && b == 0) {
                throw new RuntimeException("Division by zero.");
            }
            a = match switch {
                0 => a * b,
                1 => a / b,
                2 => a % b,
                _ => a,
            };
        }
        if (a < short.MinValue) {
            throw new RuntimeException("Arithmetic underflow");
        }
        if (a > short.MaxValue) {
            throw new RuntimeException("Arithmetic overflow");
        }
        return (short)a;
    }

    internal short ExprMulDivTerm() {
        //test for fn
        short rslt;
        parser.SkipSpaces();
        if (!parser.ScanShort(out rslt) &&
            !TryGetFunction(out rslt) && 
            !TryGetVariable(out rslt) && 
            !TryGetParen(out rslt)) {
            throw new RuntimeException("Value expected.");
        }
        return rslt;
    }

    internal bool TryGetFunction(out short value) {
        var oldPos = parser.LinePosition;
        var rslt = false;
        value = 0;
        var whichMatch = parser.ScanStringTableEntry(["RND", "INP", "PEEK", "USR", "ABS", "SIZE"]);
        if (whichMatch.HasValue) {
            switch (whichMatch.Value) {
                case 0: //RND(n)
                    var a = ParenExpr();
                    value = (short)Random.Shared.Next(a);
                    rslt = true;
                    break;
                case 1: //INP(port#)
                    throw new RuntimeException("Port I/O not supported.");
                //break;
                case 2: //PEEK(addr)
                    throw new RuntimeException("Random access to memory not supported.");
                //break;
                case 3: //USR(address)
                    throw new RuntimeException("Random memory address execution not supported.");
                //break;
                case 4: //ABS(n)
                    a = ParenExpr();
                    value = short.Abs(a);
                    rslt = true;
                    break;
                case 5: //SIZE()
                    if (parser.ScanEmptyParens()) {
                        value = short.MaxValue;
                        rslt = true;
                    }
                    break;
                default:
                    value = 0;
                    break;
            }
        }
        if (rslt == false) {
            parser.LinePosition = oldPos;
        }
        return rslt;
    }

    internal bool TryGetVariable(out short value) {
        var oldPos = parser.LinePosition;
        var rslt = false;
        value = 0;
        //two choices here - autoinit undeclared var to zero, or return false.
        var c = parser.CurrentChar ?? ' ';
        c = char.ToUpperInvariant(c);
        if (char.IsLetter(c)) {
            var c2 = char.ToUpperInvariant(parser.NextChar ?? ' ');
            if (!char.IsLetter(c2)) {
                //we found a variable (a single letter followed by nothing or a non-letter) (easy dumb parsing rule)
                var variableName = c.ToString();
                parser.LinePosition++;
                var vValue = VariableStore.Shared.Globals[variableName];
                if (vValue == null) {
                    vValue = new Variable(variableName, 0);
                    VariableStore.Shared.Globals[variableName] = vValue;
                }
                var variableValue = vValue.ShortValue ?? 0;
                value = variableValue;
                rslt = true;
            }
        }
        else if (c == '@') {
            var arrIndex = ParenExpr();
            var arr = VariableStore.Shared.Globals["@"];
            if (arr == null) {
                arr = new Variable("@", new short[16384]);
                VariableStore.Shared.Globals["@"] = arr;
            }
            value = ((short[])arr.VValue)[arrIndex];
            rslt = true;
            //the '@[n]' array
        }
        if (rslt == false) {
            parser.LinePosition = oldPos;
        }
        return rslt;
    }

    internal short ParenExpr() {
        short rslt = 0;
        if (parser.ScanString("(")) {
            if (TryEvaluateExpr(out rslt)) {
                parser.SkipSpaces();
                if (!parser.ScanString(")")) {
                    throw new RuntimeException("Expected ')'.");
                }
            } else {
                throw new RuntimeException("Expected expression.");
            }
        }
        return rslt;
    }

    internal bool TryGetParen(out short value) {
        var oldPos = parser.LinePosition;
        value = 0;
        var rslt = false;
        if (parser.ScanString("(")) {
            if (TryEvaluateExpr(out value)) {
                parser.SkipSpaces();
                if (parser.ScanString(")")) {
                    rslt = true;
                }
            } else {
                //throw new RuntimeException("Expected ')'."); just return false
            }
        }
        if (rslt == false) {
            parser.LinePosition = oldPos;
        }
        return rslt;
    }

}
