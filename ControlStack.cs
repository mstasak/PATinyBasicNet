using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace NewPaloAltoTB;

/// <summary>
/// Maintain a stack defining active for loops and gosubs
/// </summary>
internal class ControlStack {
    internal Stack<StackLevelInfo> TBStack = new();
    internal static ControlStack Shared => shared.Value;
    private static readonly Lazy<ControlStack> shared = new(() => new ControlStack());

    internal Interpreter TBInterpreter = Interpreter.Shared;
    internal ParserTools parser = ParserTools.Shared;

    internal StackLevelInfo? ForLoopBegin(LValue lValue, short initialVal, short stepVal, short limitVal) {
        if (parser.ScanRegex("^\\s*;") != null) {
        } else {
            //should be at eol()
            parser.SkipSpaces();
            if (!parser.EoL()) {
                throw new RuntimeException("Expected ';' or end of line.");
            }
        }

        var level = new StackLevelInfo(file: TBInterpreter.CurrentFile,
                                       lineNum: parser.LineNumber,
                                       colNum: parser.LinePosition,
                                       srcLine: parser.Line);
        //EndPoint = null; //unknown at this time
        level.ForLValue = lValue;
        level.ForInitial = initialVal;
        level.ForIncrement = stepVal;
        level.ForLimit = limitVal;
        level.Kind = StackEntryKind.For;

        lValue.Value = initialVal;
        if ((stepVal > 0 && initialVal > limitVal) || (stepVal < 0 && initialVal < limitVal)) {
            ForLoopSkip(level); //loop index outside limits, break out of loop immediately
            return null;
        }

        TBStack.Push(level);
        return level;
    }

    private void ForLoopSkip(StackLevelInfo level) {
        var endFor = level.FindForLoopEndPoint();
        if (endFor == null) {
            throw new RuntimeException("Cannot find next statement to exit for loop.");
        } else {
            //DIRTY CODE!  poke a new location into ITBInterpreter and parser.
            if (TBInterpreter.LineNumber == 0) { //interactive
                                                 //TBInterpreter.CurrentLine = ;
                                                 //TBInterpreter.LineNumber = ;
                                                 //TBInterpreter.CurrentLineOrd = ;
                parser.LinePosition = endFor.ColumnPosition;
            } else {
                TBInterpreter.CurrentLine = TBInterpreter.ProgramSource[TBInterpreter.LineLocations[(short)endFor.LineNumber]].src;
                TBInterpreter.LineNumber = endFor.LineNumber;
                TBInterpreter.CurrentLineOrd = TBInterpreter.LineLocations[(short)endFor.LineNumber];
                parser.SetLine(line: TBInterpreter.CurrentLine,
                    linePosition: endFor.ColumnPosition,
                    lineNumber: endFor.LineNumber);
            }
        }
    }

    internal StackLevelInfo ForLoopNext(string varName) {
        StackLevelInfo? lvl;
        while (TBStack.TryPeek(out lvl)) {
            if (lvl.Kind == StackEntryKind.For && string.Equals(lvl.ForLValue!.LVar.VName, varName, StringComparison.InvariantCultureIgnoreCase)) {
                //this is matching level
                //var indexVar = lvl.ForLValue!.LVar;
                var indexVal = lvl.ForLValue!.Value;
                bool loopEnded;
                indexVal += lvl.ForIncrement;
                //VariableStore.Shared.StoreVariable(varName, indexVal);
                lvl.ForLValue!.Value = indexVal;
                if (lvl.ForIncrement > 0) {
                    loopEnded = indexVal > lvl.ForLimit;
                } else {
                    loopEnded = indexVal < lvl.ForLimit;
                }

                if (loopEnded) {
                    ForLoopEnd();
                } else {
                    //DIRTY CODE!  poke a new location into TBInterpreter and parser.  Should add a method to jump to new loc...
                    if (lvl.EntryPoint.LineNumber == 0) { //interactive
                        //TBInterpreter.CurrentLine = ;
                        //TBInterpreter.LineNumber = ;
                        //TBInterpreter.CurrentLineOrd = ;
                        parser.LinePosition = lvl.EntryPoint.ColumnPosition;
                    } else {
                        TBInterpreter.CurrentLine = TBInterpreter.ProgramSource[TBInterpreter.LineLocations[(short)lvl.EntryPoint.LineNumber]].src;
                        TBInterpreter.LineNumber = lvl.EntryPoint.LineNumber;
                        TBInterpreter.CurrentLineOrd = TBInterpreter.LineLocations[(short)lvl.EntryPoint.LineNumber];
                        parser.SetLine(line:TBInterpreter.CurrentLine,
                            linePosition:lvl.EntryPoint.ColumnPosition,
                            lineNumber:lvl.EntryPoint.LineNumber);
                    }

                }
                return lvl;
            }
            _ = TBStack.Pop();
            //continue;
        }
        throw new RuntimeException("Next variable does not match an executing for loop.");
    }

    internal void ForLoopEnd() {
        // just completed "next variablename"; get ready to run first statement after next
        _ = parser.ScanRegex("^\\s*;");
        _ = TBStack.Pop();
    }

    internal StackLevelInfo Gosub(short newLineNumber, int newLineOrd) {
        //push current program point (1st statement after gosub)
        var level = new StackLevelInfo(file: TBInterpreter.CurrentFile,
                                       lineNum: parser.LineNumber,
                                       colNum: parser.LinePosition,
                                       srcLine: parser.Line);
        //EndPoint = null; //unknown at this time
        level.Kind = StackEntryKind.Gosub;
        TBStack.Push(level);
        //stuff new loc into TBInterpreter, parser (just like goto)
        TBInterpreter.JumpToLine(newLineOrd);
        return level;
    }

    internal void Return() {
        var returnPoint = TBStack.Pop();

        //DIRTY CODE!  poke a new location into ITBInterpreter and parser.
        TBInterpreter.CurrentLine = returnPoint.EntryPoint.SrcLine;
        TBInterpreter.LineNumber = returnPoint.EntryPoint.LineNumber;
        if (returnPoint.EntryPoint.LineNumber == 0) {
            TBInterpreter.CurrentLineOrd = -1;
        } else {
            TBInterpreter.CurrentLineOrd = TBInterpreter.LineLocations[returnPoint.EntryPoint.LineNumber];
        }
        parser.SetLine(line: returnPoint.EntryPoint.SrcLine!,
            linePosition: returnPoint.EntryPoint.ColumnPosition,
            lineNumber: returnPoint.EntryPoint.LineNumber);
        
    }
}
