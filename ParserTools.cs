using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NewPaloAltoTB;
internal partial class ParserTools {
    internal bool IgnoreCaseDefault = false;
    internal RegexOptions RegexOptionsDefault = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
    internal int RegexTimeoutDefault = 250;
    
    internal string Line = "";
    internal int LinePosition = 0;
    internal int? LineNumber = 0; //null for immediate statement
    
    //private static readonly ParserTools shared = new();
    //internal static ParserTools Shared => shared;

    internal static ParserTools Shared => shared.Value;
    private static readonly Lazy<ParserTools> shared = new(() => new ParserTools());


    internal void SetLine(string line, int linePosition = 0, int? lineNumber = null) {
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
            if (string.Equals(target, Line[LinePosition..(LinePosition + target.Length)],StringComparison.InvariantCulture | StringComparison.InvariantCultureIgnoreCase)) {
                LinePosition += target.Length;
                return true;
            }
        }
        return false;
    }

    internal int? StrToInt(string s) {
        int rsltVal;
        var found = int.TryParse(s, out rsltVal);
        return found ? rsltVal : null;
    }
    internal short? StrToShort(string s) {
        short rsltVal;
        var found = short.TryParse(s, out rsltVal);
        return found ? rsltVal : null;
    }

    [GeneratedRegex("^\\s*(\\+|\\-)?\\d+")]
    private static partial Regex RegexLiteralInt();

    internal bool ScanInt(out int value) {
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
    internal bool ScanShort(out short value) {
        var rslt = false;
        value = 0;
        var numMatch = RegexLiteralInt().Match(Line![LinePosition..]);
        if (numMatch.Success) {
            var matchStr = Line.Substring(LinePosition, numMatch.Length);
            var shortVal = StrToShort(matchStr);
            if (shortVal.HasValue) {
                rslt = true;
                value = shortVal.Value;
                LinePosition += numMatch.Length;
            }
        }
        return rslt;
    }

    internal string? ScanName() {
        SkipSpaces();
        return ScanRegex("^[@A-Z][A-z0-9]*");
    }

    internal string? ScanRegex(string pattern) {
        string? rslt = null;
        var match = Regex.Match(Line![LinePosition..], pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
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
                    '\\' => '\t',
                    'r' => '\r',
                    'n' => '\n',
                    _ => c2
                };
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
}
