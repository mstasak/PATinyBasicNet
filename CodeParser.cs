﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NewPaloAltoTB;
internal partial class CodeParser {
    internal bool IgnoreCaseDefault = false;
    internal RegexOptions RegexOptionsDefault = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
    internal int RegexTimeoutDefault = 250;

    internal string Line = "";
    internal int LinePosition = 0;
    internal int LineNumber = 0; // 0 for immediate statement

    //private static readonly ParserTools shared = new();
    //internal static ParserTools Shared => shared;

    internal static CodeParser Shared => shared.Value;
    private static readonly Lazy<CodeParser> shared = new(() => new CodeParser());


    internal void SetLine(string line, int linePosition, int lineNumber) {
        Line = line;
        LinePosition = linePosition;
        LineNumber = lineNumber;
        //PosStack.Clear();
    }

    //private Stack<int> PosStack = new();

    //internal void PushPos() {
    //    PosStack.Push(LinePosition);
    //}

    //internal void PopPos() {
    //    LinePosition = PosStack.Pop();
    //}

    //internal void PopDiscardPos() {
    //    _ = PosStack.Pop();
    //}

    internal void SkipSpaces() {
        while (LinePosition < Line.Length) {
            if (!char.IsWhiteSpace(Line[LinePosition])) {
                break;
            }
            LinePosition++;
        }
    }

    /// <summary>
    /// Peek at current char of source line; return null if past end of line or line is null
    /// </summary>
    internal char? CurrentChar {
        get {
            if ((Line == null) || (LinePosition >= Line.Length)) {
                return null;
            }
            return Line[LinePosition];
        }
    }

    /// <summary>
    /// Peek at next char (after current) of source line; return null if past end of line or line is null
    /// </summary>
    internal char? NextChar {
        get {
            if ((Line == null) || (LinePosition + 1 >= Line.Length)) {
                return null;
            }
            return Line[LinePosition + 1];
        }
    }

    /* internal void AdvanceChar(int repeat = 1) {
    ///// <summary>
    ///// Advance to next character in source line (may point to line[length], which is 1 past final character)
    ///// </summary>
    //internal void AdvanceChar(int repeat = 1) {
    //    var l = CurrentLine;
    //    if (l == null) {
    //        return;
    //    }
    //    LinePosition = int.Min(LinePosition + repeat, l.Length);
    //} */

    /* internal bool Eol() {
    //    var l = CurrentLine;
    //    if (l == null) {
    //        return true;
    //    }
    //    return LinePosition >= l.Length;
    //} */



    internal bool EoL() {
        return LinePosition >= Line.Length;
    }

    internal char? ScanChar(char c, bool skipSpaces = false) {
        if (skipSpaces) {
            SkipSpaces();
        }
        var c2 = CurrentChar;
        if (c2.HasValue) {
            if (string.Equals(c2!.ToString(), c.ToString(), StringComparison.InvariantCulture)) {
                LinePosition++;
                return c2;
            }
        }
        return null;
    }

    internal int? ScanStringTableEntry(string[] targetList, bool skipSpaces = true) {
        if (skipSpaces) {
            SkipSpaces();
        }
        var which = 0;
        foreach (var target in targetList) {
            if (ScanString(target)) {
                return which;
            }
            which++;
        }
        return null;
    }

    internal bool ScanString(string target, bool skipSpaces = true) {
        if (skipSpaces) {
            SkipSpaces();
        }
        if (target.Length <= Line.Length - LinePosition) {
            if (string.Equals(target, Line[LinePosition..(LinePosition + target.Length)], StringComparison.InvariantCulture | StringComparison.InvariantCultureIgnoreCase)) {
                LinePosition += target.Length;
                return true;
            }
        }
        return false;
    }

    internal static int? StrToInt(string s) {
        int rsltVal;
        var found = int.TryParse(s, out rsltVal);
        return found ? rsltVal : null;
    }
    internal static short? StrToShort(string s) {
        short rsltVal;
        var found = short.TryParse(s, out rsltVal);
        return found ? rsltVal : null;
    }


    internal static double? StrToDouble(string s) {
        double rsltVal;
        // double.TryParse looks for this: [ws][sign][integral-digits,]integral-digits[.[fractional-digits]][e[sign]exponential-digits][ws]
        //looks like 0.5 is matched, but not .5
        var found = double.TryParse(s, out rsltVal);
        return found ? rsltVal : null;
    }

    internal bool ScanLiteralValue(out Value value) {
        return ScanLiteralDoubleValue(out value) ||
               ScanLiteralShortValue(out value) ||
               ScanLiteralIntValue(out value) ||
               ScanLiteralStringValue(out value);
    }

    [GeneratedRegex("^\\s*([-+]?\\d*\\.?\\d+)(?:[eE]([-+]?\\d+))?")]
    private static partial Regex RegexLiteralDouble();
// from: https://rgxdb.com/r/1RSPF8MG ; note a mantissa ending in . is not accepted, like 3. or 1.E+3
    internal bool ScanLiteralDoubleValue(out Value value) {
        var numMatch = RegexLiteralDouble().Match(Line![LinePosition..]);
        if (numMatch.Success) {
            var matchStr = Line.Substring(LinePosition, numMatch.Length);
            var dblVal = StrToDouble(matchStr);
            if (dblVal.HasValue) {
                value = new Value(dblVal.Value);
                LinePosition += numMatch.Length;
                return true;
            }
        }
        value = Value.NullValue;
        return false;
    }

    [GeneratedRegex("^\\s*[-+]?\\d+(?![eE.])")]
    private static partial Regex RegexLiteralShort();

    internal bool ScanLiteralShortValue(out Value value) {
        var rslt = false;
        value = Value.NullValue;
        var numMatch = RegexLiteralShort().Match(Line![LinePosition..]);
        if (numMatch.Success) {
            var matchStr = Line.Substring(LinePosition, numMatch.Length);
            var shortVal = StrToShort(matchStr);
            if (shortVal.HasValue) {
                rslt = true;
                value = new Value(shortVal.Value);
                LinePosition += numMatch.Length;
            }
        }
        return rslt;
    }

    [GeneratedRegex("^\\s*[-+]?\\d+(?:[eE.])")]
    private static partial Regex RegexLiteralInt();

    internal bool ScanLiteralIntValue(out Value value) {
        var rslt = false;
        value = Value.NullValue;
        var numMatch = RegexLiteralInt().Match(Line![LinePosition..]);
        if (numMatch.Success) {
            var matchStr = Line.Substring(LinePosition, numMatch.Length);
            var intVal = StrToInt(matchStr);
            if (intVal.HasValue) {
                rslt = true;
                value = new Value(intVal.Value);
                LinePosition += numMatch.Length;
            }
        }
        return rslt;
    }

    internal bool ScanLiteralStringValue(out Value value) {
        var rslt = false;
        value = Value.NullValue;
        var numMatch = RegexLiteralInt().Match(Line![LinePosition..]);
        if (numMatch.Success) {
            var matchStr = Line.Substring(LinePosition, numMatch.Length);
            var shortVal = StrToShort(matchStr);
            if (shortVal.HasValue) {
                rslt = true;
                value = new Value(shortVal.Value);
                LinePosition += numMatch.Length;
            }
        }
        return rslt;
    }

    //variations without Value boxing
    internal bool ScanLiteralShort(out short value) {
        var rslt = false;
        value = 0;
        var numMatch = RegexLiteralShort().Match(Line![LinePosition..]);
        if (numMatch.Success) {
            var matchStr = Line.Substring(LinePosition, numMatch.Length);
            var shortIntVal = StrToShort(matchStr);
            if (shortIntVal.HasValue) {
                rslt = true;
                value = shortIntVal.Value;
                LinePosition += numMatch.Length;
            }
        }
        return rslt;
    }

    internal bool ScanLiteralInt(out int value) {
        var rslt = false;
        value = 0;
        var numMatch = RegexLiteralInt().Match(Line![LinePosition..]);
        if (numMatch.Success) {
            var matchStr = Line.Substring(LinePosition, numMatch.Length);
            var intVal = StrToInt(matchStr);
            if (intVal.HasValue) {
                rslt = true;
                value = intVal.Value;
                LinePosition += numMatch.Length;
            }
        }
        return rslt;
    }

    internal string? ScanName() {
        SkipSpaces();
        return ScanRegex("^(@|[A-Z][a-z0-9_]*)");
    }

    internal LValue? ScanLValue() {
        LValue? rslt = null;
        var vName = ScanName();
        if (vName != null) {
            var vVar = Variable.FindVariable(vName);
            if (vVar != null) {
                //it exists, check for appropriate indices for VType
                switch (vVar.VType) {
                    case VariableType.Short:
                        var vIndices = ScanIndices();
                        if (vIndices != null) {
                            throw new RuntimeException("Unexpected array index list following scalar variable.");
                        }
                        rslt = new LValue(vVar, null);
                        break;
                    case VariableType.ShortArray:
                        vIndices = ScanIndices(vVar.DimensionCount);
                        if (vIndices == null) {
                            throw new RuntimeException("Missing index list following array variable.");
                        }
                        rslt = new LValue(vVar, vIndices);
                        break;
                }
            } else {
                //undefined variable: create if scalar, throw if array
                if (ScanChar('(', true) == null) {
                    vVar = new Variable(vName: vName, value: 0, autoAddToStore: true);
                    rslt = new LValue(vVar, null);
                } else {
                    throw new RuntimeException("Undeclared array has no dimensions, must use DIM to declare it prior to using..");
                }
            }
        //} else {
            //just fall through, no LValue found
            //rslt = null;
        }
        return rslt;
    }


    /// <summary>
    /// Parse the list of array index values for an array element.  i.e. parse (5) as part of the term x(5).
    /// </summary>
    /// <param name="requiredCount"></param>
    /// <returns>A list of index values.</returns>
    internal List<int>? ScanIndices(int requiredCount = 0) {
        var rslt = new List<int>();
        var needTerm = requiredCount > 0;
        var needRBracket = false;
        if (ScanChar('(', true) != null) {
            while (true) {
                int indexVal;
                if (Expression.Shared.TryEvaluateIntExpr(out indexVal)) {
                    rslt.Add((int)indexVal);
                    var gotComma = (ScanChar(',', true) != null);
                    needTerm = gotComma;
                    needRBracket = !gotComma;
                } else {
                    if (needTerm) {
                        throw new RuntimeException("Expected: array index expression.");
                    } else {
                        needRBracket = true;
                    }
                }
                if (needRBracket) {
                    if (ScanChar(')', true) != null) {
                        break;
                    } else {
                        throw new RuntimeException("Expected: ')'.");
                    }
                }
            }
        }
        if (rslt.Count == requiredCount && requiredCount == 0) {
            rslt = null;
        }
        if (rslt != null && requiredCount > 0 && rslt.Count != requiredCount) {
            throw new RuntimeException($"Expected {requiredCount} index expressions.");
        }
        return rslt;
    }

    /*
   10 dim arr(10)
  100 for i=1 to 10
  110   arr(i) = 10 * i
  120 next i
  200 for i=1 to 10
  210   print arr(i)
  220 next i     
     */

    internal (int low, int high)? ScanArrayDimension() {
        (int low, int high)? rslt = null;
        int n;
        if (Expression.Shared.TryEvaluateIntExpr(out n)) {
            if (ScanString("to") || ScanString("..")) {
                int n2;
                if (Expression.Shared.TryEvaluateIntExpr(out n2)) {
                    rslt = (n, n2);
                } else {
                    throw new RuntimeException("Expected: array upper bound.");
                }
            } else {
                rslt = (1, n);
            }
        //} else {
        //    return null;    
        }
        return rslt;
    }

    internal List<(int low, int high)>? ScanArrayDimensions() {
        // allow a variety of formats
        // dim as int a(1 to 10)
        // dim as int a(1..10)
        // dim as int a(10) 'relies on option base to determine lower index value (one for now, may allow zero later)
        // dim a(10) as int
        //per BASIC norms, we will wrap dimensions in parentheses
        
        List<(int low, int high)>? rslt = null;

        if (ScanChar('(', true) != null) {
            var dims = new List<(int low, int high)>();
            while (true) { 
                var dim = ScanArrayDimension();
                if (dim == null) {
                    throw new RuntimeException("Expected: array dimension, e.g. 1 to 10");
                } else {
                    dims.Add(dim.Value);
                }
                if (ScanChar(',') != null) {
                    continue;
                }
                if (ScanChar(')') != null) {
                    rslt = dims;
                    break;
                } else { 
                    throw new RuntimeException("Expected: ',' or ')'.");
                }
            }
        //} else {
        //    return null;
        }
        return rslt;
    }

    /// <summary>
    /// Search for a given RegEx pattern, returning matched string if found or null if nomatch
    /// </summary>
    /// <param name="pattern"></param>
    /// <param name="ignoreCase"></param>
    /// <returns></returns>
    internal string? ScanRegex(string pattern, bool ignoreCase = true) {
        string? rslt = null;
        var match = Regex.Match(Line![LinePosition..], pattern, RegexOptions.CultureInvariant | (ignoreCase ?  RegexOptions.IgnoreCase : RegexOptions.None));
        if (match.Success) {
            rslt = Line.Substring(LinePosition, match.Length);
            LinePosition += match.Length;
        }
        return rslt;
    }

    /// <summary>
    /// Go to EoL or start of next statement, if a ';' separator is present
    /// Called at start of a statement
    /// REM statement uses rest of line
    /// </summary>
    /// 
    internal void SkipToEolOrNextStatementOnLine() {
        if (ScanRegex("^\\s*REM(\\s|$)") != null) {
            LinePosition = Line.Length;
            return;
        }
        var inquotes = false;
        var indblquotes = false;
        var lastwassemi = false;
        while (!EoL()) {
            var c = Line[LinePosition];
            LinePosition++;
            switch (c) {
                case '\'':
                    if (!indblquotes) {
                        inquotes = !inquotes;
                    }
                    break;
                case '"':
                    if (!inquotes) {
                        indblquotes = !indblquotes;
                    }
                    break;
                case ';':
                    if (!inquotes && !indblquotes) {
                        lastwassemi = true;
                    }
                    break;
                default: break;
            }
            if (lastwassemi) {
                break;
            }
        }
    }

    /// <summary>
    /// Look for a string literal, like 'Jack' or "ball"
    /// </summary>
    /// <returns>The literal string, if found (minus enclosing quotes/doublequotes); otherwise null</returns>
    /// <exception cref="RuntimeException"></exception>
    internal string? ScanStringLiteral() {
        string? rslt;
        SkipSpaces();
        char quote;
        if (ScanChar('\'') != null) {
            quote = '\'';
        } else if (ScanChar('"') != null) {
            quote = '"';
        } else {
            return null;
        }
        rslt = "";
        while (true) {
            var c = CurrentChar ?? throw new RuntimeException("String literal missing closing quote.");
            LinePosition++;
            if (c == quote) {
                break;
            }
            if (c == '\\') {
                var c2 = CurrentChar ?? '\\';
                rslt += c2 switch {
                    '\\' => '\\',
                    't' => '\t',
                    'r' => '\r',
                    'n' => '\n',
                    _ => c2
                };
                //todo: consider more escapes, \0x, \uXXXX, \xX{1,4} \0, \esc?, +unnnn
                LinePosition++;
            } else {
                rslt += c;
            }
        }
        return rslt;
    }

    internal bool ScanEmptyParens() { //for builtin functions with empty argument lists
        return ScanRegex("^\\s*\\(\\s*\\)") != null;
    }

    /// <summary>
    /// Test for end of statement; skip spaces and test for ';' or EoL()
    /// </summary>
    /// <returns></returns>
    internal bool EoStmt() {
        SkipSpaces();
        return EoL() || NextChar == ';';
    }

    internal (bool success, int low, int high, int lineCount, string search) ScanLineRange() {
        (bool success, int low, int high, int lineCount, string search) rslt = (false, -1, -1, -1, "");
        int i;
        bool procedOne;
        do {
            var cursorSave = LinePosition;
            procedOne = false;
            if (rslt.low == -1 && ScanLiteralInt(out i)) {
                rslt.low = i;
                procedOne = true;
            } else {
                LinePosition = cursorSave;
                if (rslt.high == -1 && ScanChar('-', true) != null & ScanLiteralInt(out i)) {
                    rslt.high = i;
                    procedOne = true;
                } else {
                    LinePosition = cursorSave;
                    if (rslt.high == -1 && ScanChar(',', true) != null & ScanLiteralInt(out i)) {
                        rslt.lineCount = i;
                        procedOne = true;
                    } else {
                        LinePosition = cursorSave;
                        string? s;
                        if ((s = ScanStringLiteral()) != null) {
                            rslt.search = s;
                            procedOne = true;
                        }
                    }
                }
            }
        } while (procedOne);
        return rslt;
    }

}
