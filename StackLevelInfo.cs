using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NewPaloAltoTB;

internal enum StackEntryKind {
    None,
    For,
    Gosub
}
internal class StackLevelInfo {
    //SymbolTable LocalSymbols; //symbols defined at current level
    //SymbolTable LocalScope; //level of last new scope (sub/function)
    internal ProgramLocation EntryPoint; //1st statement of loop body, or statement after gosub
    internal ProgramLocation? EndPoint; //1st stmt after next or other loop terminator; unused by gosub
    internal StackEntryKind Kind = StackEntryKind.None;

    internal LValue? ForLValue;
    internal short ForInitial;
    internal short ForLimit;
    internal short ForIncrement;
    
    internal ProgramLocation? FindForLoopEndPoint() {
        if (EndPoint == null) {
            var Parser = CodeParser.Shared;
            var Interpreter = CodeInterpreter.Shared;
            //tricky here, for a richer language: scan over code to find "next forvariable"
            //we'll make it simpler using some clean nesting rules - for..next must be 1:1 with next after for, with 
            //any nested loops begun and ended inside loop body - no crazy spaghetti, and next must name correct var
            var (linesave, positionsave) = (Parser.Line, Parser.LinePosition);
            foreach (var (linenum, src) in Interpreter.ProgramSource.Where(e => e.linenum >= EntryPoint.LineNumber &&
                                                                       e.src.Contains("next",StringComparison.InvariantCultureIgnoreCase))) {
                //skip until find a line matching next\s+forvariable; make sure it is not a comment or print literal
                while (!Parser.EoL()) {
                    (Parser.Line, Parser.LinePosition) = (src, 0);
                    var match = Parser.ScanRegex("\\s*next\\s+" + ForLValue!.LVar.VName + "(\\;|$)");
                    if (match != null) {
                        EndPoint = new ProgramLocation(EntryPoint.FileName, linenum, Parser.LinePosition, Parser.Line);
                        break;
                    }
                    else {
                        Parser.SkipToEolOrNextStatementOnLine(); //skip past next non-quoted ';'
                    }
                }
                if (EndPoint != null) {
                    break;
                }
            }
            (Parser.Line, Parser.LinePosition) = (linesave, positionsave);
        }
        return EndPoint;
    }

    internal StackLevelInfo(string file, int lineNum, int colNum, string? srcLine) {
        EntryPoint = new(file, lineNum, colNum, srcLine);
    }


}

