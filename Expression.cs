using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewPaloAltoTB;

/// <summary>
/// Expression evaluation service
/// </summary>
internal class Expression { //: IExpression { -- embarassing, you can't base an internal class on an interface

    /// <summary>
    /// Accessable singleton object for this class.
    /// </summary>
    internal static Expression Shared => shared.Value;
    private static readonly Lazy<Expression> shared = new(() => new Expression());

    internal CodeParser Parser = CodeParser.Shared;

    /// <summary>
    /// Evaluate an expression fragment, at 1st level of operator precedence (comparison operators).
    /// This type of operation cannot be repeated (1<2<3 and a=b=c are illegal).
    /// </summary>
    /// <returns>Signed short value of expression, if successful</returns>
    /// <exception cref="RuntimeException">Thrown if parsing or calculation fails</exception>
    internal Value TryEvaluateExpr(out Value? value) {
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

        expression tree (for now) <=>, +-, * /, 

         */
        Value? a, b;
        //var oldLinePos = Parser.LinePosition;
        try {
            a = TryExprComparableTerm();
            if (a == null) {
                value = null;
                return new Value(false);
            }
            var whichOp = Parser.ScanStringTableEntry(["=", "<=", "<>", "<", ">=", ">", "#"]);
            if (whichOp == null) {
                value = a;
                return new Value(true);
            }
            //fix terms into signed short range (-32768..32767)
            b = TryExprComparableTerm();
            if (b == null) {
                throw new RuntimeException($"Value expected after comparison operator.");
            }
            //if (notbothcompatiblecomparables) { } //but = may allow more types than other relops
            a = whichOp switch {
                0 => a.EqualTo(b),
                1 => a.LessThanOrEqualTo(b),
                2 => a.NotEqualTo(b),
                3 => a.LessThan(b),
                4 => a.GreaterThanOrEqualTo(b),
                5 => a.GreaterThan(b),
                6 => a.NotEqualTo(b),
                _ => null,
            };
            value = a;
            return new Value(true);
        } catch (Exception) {
            value = null;
            //return new Value(false);
            throw;
        }
    }

    /// <summary>
    /// Evaluate an expression fragment, at 2nd level of operator precedence ( [-] a (+|-) b )
    /// </summary>
    /// <returns></returns>
    /// <exception cref="RuntimeException"></exception>
    private Value? TryExprComparableTerm() {
        Value? a, b;
        if (Parser.ScanString("-")) {
            //- prefix
            a = TryExprAddSubTerm();
            if (a == null) {
                return null;
            }
            a = a.NegativeValue();
        } else {
            a = TryExprAddSubTerm();
            if (a == null) {
                return null;
            }
        }
        int? match;
        while ((match = Parser.ScanStringTableEntry(["+", "-"])) != null) {
            b = TryExprAddSubTerm();
            if (b == null) {
                throw new RuntimeException($"OValue expected after addition/subtraction operator.");
            }
            //TODO: trap overflow/underflow/incompatible types/etc?
            a = match switch {
                0 => a.Add(b),
                1 => a.Subtract(b),
                _ => null,
            };
        }
        //if (a < short.MinValue) {
        //    throw new RuntimeException("Arithmetic underflow");
        //}
        //if (a > short.MaxValue) {
        //    throw new RuntimeException("Arithmetic overflow");
        //}
        return a;
    }

    private Value? TryExprAddSubTerm() {
        Value? a, b;
        a = TryExprMulDivTerm();
        if (a == null) {
            return null;
        }
        int? match;
        while (true) {
            Parser.SkipSpaces();
            match = Parser.ScanStringTableEntry(["*", "/", "%", "mod"]);
            if (match == null) {
                break;
            }

            b = TryExprMulDivTerm();
            if (b == null) {
                throw new RuntimeException($"Value expected after multiplication/division operator.");
            }
            if ((match == 1 || match == 2 || match == 3) && b.IsZero()) {
                throw new RuntimeException("Division by zero.");
            }
            a = match switch {
                0 => a.Multiply(b),
                1 => a.Divide(b),
                2 => a.Modulo(b),
                3 => a.Modulo(b),
                _ => a,
            };
        }
        //if (a < short.MinValue) {
        //    throw new RuntimeException("Arithmetic underflow");
        //}
        //if (a > short.MaxValue) {
        //    throw new RuntimeException("Arithmetic overflow");
        //}
        return a!;
    }

    private Value? TryExprMulDivTerm() {
        //test for fn
        Value rslt;
        Parser.SkipSpaces();
        if (!TryGetLiteral(out rslt) &&
            !TryGetFunction(out rslt) &&
            !TryGetParen(out rslt) &&
            !TryGetVariable(out rslt)) {
            //throw new RuntimeException("OValue expected.");
            return null;
        }
        return rslt;
    }

    private bool TryGetLiteral(out Value value) {
        Parser.SkipSpaces();
        Value rslt;
        if (Parser.ScanLiteralValue(out rslt)) {
            value = rslt;
            return true;
        } else { 
            value = Value.NullValue;
            return false;
        }
    }

    private bool TryGetFunction(out Value value) {
        var oldPos = Parser.LinePosition;
        var rslt = false;
        value = Value.NullValue;
        var whichMatch = Parser.ScanStringTableEntry(["RND", "INP", "PEEK", "USR", "ABS", "SIZE"]);
        if (whichMatch.HasValue) {
            switch (whichMatch.Value) {
                case 0: //RND(n)
                    var a = ParenExpr();
                    value = new Value(Random.Shared.Next((int)(a!.OValue ?? 1)));
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

                    value = new Value(Math.Abs((int)a!.OValue!));
                    value.OValue = int.Abs((int)(a!.OValue!));
                    rslt = true;
                    break;
                case 5: //SIZE()
                    throw new RuntimeException("Function not supported.");
                    //if (Parser.ScanEmptyParens()) {
                    //    value = short.MaxValue;
                    //    rslt = true;
                    //}
                    //break;
                default:
                    //value = 0;
                    break;
            }
        }
        if (rslt == false) {
            Parser.LinePosition = oldPos;
        }
        return rslt;
    }

    private bool TryGetVariable(out Value value) {
        var oldPos = Parser.LinePosition;
        var rslt = false;
        value = Value.NullValue;
        //two choices here - autoinit undeclared var to zero, or return false.
        var vName = Parser.ScanName();
        if (vName != null) {
            var vVar = Variable.FindVariable(vName);
            if (vVar == null) {
                //var not previously created, so reject if vname[index...]
                if (Parser.ScanChar('[', true) != null) {
                    throw new RuntimeException("An array must be declared before referencing it.");
                }
                //create scalar with value = 0
                vVar = new Variable(vName: vName, value: 0, autoAddToStore: false); //constructing Variable adds it to Variable.VariableStore
                value = 0;
                rslt = true;
            } else {
                //variable exists, look for index problems
                switch (vVar.VType) {
                    case VariableType.Short:
                        value = vVar.ShortValue ?? 0;
                        if (Parser.ScanChar('[') != null) {
                            throw new RuntimeException("Unexpected array index value list after scalar variable.");
                        }
                        rslt = true;
                        break;
                    case VariableType.ShortArray:
                        var indices = Parser.ScanIndices(vVar.DimensionCount);
                        if (indices == null) {
                            throw new RuntimeException("Missing or incorrect index value list after array variable.");
                        } else {
                            value = vVar.ElementValue(indices) ?? 0;
                            rslt = true;
                            break;
                        }
                        //if (Parser.ScanChar('[', true) == null) {
                        //    throw new RuntimeException("Expected: [arrayindex]");
                        //}
                        //short arrIndex;
                        //if (!TryEvaluateExpr(out arrIndex)) {
                        //    throw new RuntimeException("Expected: arrayindex");
                        //}
                        //if (Parser.ScanChar(']', true) == null) {
                        //    throw new RuntimeException("Expected: ]");
                        //}
                        //value = vVar.ShortElementValue(arrIndex);
                    default:
                        Parser.LinePosition = oldPos;
                        rslt = false;
                        break;
                }
            }
        }
        return rslt;
    }

    //internal bool TryGetVariable(out short value) {
    //    var oldPos = Parser.LinePosition;
    //    var rslt = false;
    //    value = 0;
    //    //two choices here - autoinit undeclared var to zero, or return false.
    //    var c = Parser.CurrentChar ?? ' ';
    //    c = char.ToUpperInvariant(c);
    //    if (char.IsLetter(c)) {
    //        var c2 = char.ToUpperInvariant(Parser.NextChar ?? ' ');
    //        if (!char.IsLetter(c2)) {
    //            //we found a variable (a single letter followed by nothing or a non-letter) (easy dumb parsing rule)
    //            var variableName = c.ToString();
    //            Parser.LinePosition++;
    //            Variable? vValue;
    //            if (VariableStore.Shared.Globals.TryGetValue(variableName, out vValue)) {
    //                var variableValue = vValue.ShortValue ?? 0;
    //                value = variableValue;
    //                rslt = true;
    //            } else {
    //                vValue = new Variable(variableName, 0);
    //                VariableStore.Shared.Globals[variableName] = vValue;
    //                value = 0;
    //                rslt = true; //allow referencing an uninitialized var (for now)
    //            }
    //        }
    //    } else if (c == '@') {
    //        var arrIndex = ParenExpr();
    //        var arr = VariableStore.Shared.Globals["@"];
    //        if (arr == null) {
    //            arr = new Variable("@", new short[16384]);
    //            VariableStore.Shared.Globals["@"] = arr;
    //        }
    //        value = ((short[])arr.VValue)[arrIndex];
    //        rslt = true;
    //        //the '@[n]' array
    //    }
    //    if (rslt == false) {
    //        Parser.LinePosition = oldPos;
    //    }
    //    return rslt;
    //}

    public Value? ParenExpr() {
        Value? rslt = null;
        if (Parser.ScanString("(")) {
            if (TryEvaluateExpr(out rslt)) {
                Parser.SkipSpaces();
                if (!Parser.ScanString(")")) {
                    throw new RuntimeException("Expected ')'.");
                }
            } else {
                throw new RuntimeException("Expected expression.");
            }
        } else { 
            throw new RuntimeException("Expected '('.");
        }
        return rslt;
    }

    public bool TryGetParen(out Value? value) {
        var oldPos = Parser.LinePosition;
        Value? rValue = null;
        var rslt = false;
        if (Parser.ScanString("(")) {
            if (TryEvaluateExpr(out rValue)) {
                Parser.SkipSpaces();
                if (Parser.ScanString(")")) {
                    rslt = true;
                }
            } else {
                //throw new RuntimeException("Expected ')'."); just return false
            }
        }
        if (rslt == false) {
            Parser.LinePosition = oldPos;
        }
        value = rValue;
        return rslt;
    }

    internal bool TryEvaluateIntExpr(out int indexVal) => throw new NotImplementedException();
}

/*
    C++ operator precedence:
    https://learn.microsoft.com/en-us/cpp/cpp/cpp-built-in-operators-precedence-and-associativity?view=msvc-170

Operator Description	Operator	Alternative

Group 1 precedence, no associativity		
    Scope resolution	::	
Group 2 precedence, left to right associativity		
    Member selection (object or pointer)	. or ->	
    Array subscript	[]	
    Function call	()	
    Postfix increment	++	
    Postfix decrement	--	
    ValueType name	typeid	
    Constant type conversion	const_cast	
    Dynamic type conversion	dynamic_cast	
    Reinterpreted type conversion	reinterpret_cast	
    Static type conversion	static_cast	
Group 3 precedence, right to left associativity		
    Size of object or type	sizeof	
    Prefix increment	++	
    Prefix decrement	--	
    One's complement	~	compl
    Logical not	!	not
    Unary negation	-	
    Unary plus	+	
    Address-of	&	
    Indirection	*	
    Create object	new	
    Destroy object	delete	
    Cast	()	
Group 4 precedence, left to right associativity		
    Pointer-to-member (objects or pointers)	.* or ->*	
Group 5 precedence, left to right associativity		
    Multiplication	*	
    Division	/	
    Modulus	%	
Group 6 precedence, left to right associativity		
    Addition	+	
    Subtraction	-	
Group 7 precedence, left to right associativity		
    Left shift	<<	
    Right shift	>>	
Group 8 precedence, left to right associativity		
    Less than	<	
    Greater than	>	
    Less than or equal to	<=	
    Greater than or equal to	>=	
Group 9 precedence, left to right associativity		
    Equality	==	
    Inequality	!=	not_eq
Group 10 precedence left to right associativity		
    Bitwise AND	&	bitand
Group 11 precedence, left to right associativity		
    Bitwise exclusive OR	^	xor
Group 12 precedence, left to right associativity		
    Bitwise inclusive OR	|	bitor
Group 13 precedence, left to right associativity		
    Logical AND	&&	and
Group 14 precedence, left to right associativity		
    Logical OR	||	or
Group 15 precedence, right to left associativity		
    Conditional	? :	
    Assignment	=	
    Multiplication assignment	*=	
    Division assignment	/=	
    Modulus assignment	%=	
    Addition assignment	+=	
    Subtraction assignment	-=	
    Left-shift assignment	<<=	
    Right-shift assignment	>>=	
    Bitwise AND assignment	&=	and_eq
    Bitwise inclusive OR assignment	|=	or_eq
    Bitwise exclusive OR assignment	^=	xor_eq
    throw expression	throw	
Group 16 precedence, left to right associativity		
    Comma	,	

 
 */
