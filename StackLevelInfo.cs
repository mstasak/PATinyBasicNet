using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NewPaloAltoTB;
internal enum StackLevelType {
    ForNext,
    ForNextCStyle,
    GosubReturn,
    WhileExprDoBodyEnd,
    DoWhileExprBodyEnd,
}
internal class StackLevelInfo {
    internal int StackDepth;
    internal StackLevelType LevelType;
    internal Interpreter TBInterpreter = Interpreter.Shared;
    internal ParserTools parser = ParserTools.Shared;
    //SymbolTable LocalSymbols; //symbols defined at current level
    //SymbolTable LocalScope; //level of last new scope (sub/function)
    internal ProgramLocation EntryPoint;
    internal ProgramLocation? EndPoint; //1st stmt after next or other loop terminator
    
    internal ProgramLocation? FindEndPoint() {
        if (EndPoint == null) {
            //tricky here, for a richer language: scan over code to find "next forvariable"
            //we'll make it simpler using some clean nesting rules - for..next must be 1:1 with next after for, with 
            //any nested loops begun and ended inside loop body - no crazy spaghetti, and next must name correct var
            var (linesave, positionsave) = (parser.Line, parser.LinePosition);
            foreach (var (linenum, src) in TBInterpreter.Program.Where(e => e.linenum >= EntryPoint.LineNumber &&
                                                                       e.src.Contains("next",StringComparison.InvariantCultureIgnoreCase))) {
                //skip until find a line matching next\s+forvariable; make sure it is not a comment or print literal
                while (!parser.EoL()) {
                    (parser.Line, parser.LinePosition) = (src, 0);
                    var match = parser.ScanRegex("\\s*next\\s+" + ForVariable + "(\\;|$)");
                    if (match != null) {
                        EndPoint = new ProgramLocation(EntryPoint.FileName, linenum, parser.LinePosition);
                        break;
                    }
                    else {
                        parser.SkipToEolOrNextStatementOnLine(); //skip past next non-quoted ';'
                    }
                }
                if (EndPoint != null) {
                    break;
                }
            }
            (parser.Line, parser.LinePosition) = (linesave, positionsave);
        }
        return EndPoint;
    }
    internal string ForVariable = "";
    internal short ForInitial;
    internal short ForLimit;
    internal short ForIncrement;

    internal StackLevelInfo(string file, int lineNum, int colNum) {
        EntryPoint = new(file, lineNum, colNum);
    }


}

