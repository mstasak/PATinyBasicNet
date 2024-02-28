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

    internal CodeInterpreter Interpreter = CodeInterpreter.Shared;
    internal CodeParser Parser = CodeParser.Shared;

    internal StackLevelInfo? ForLoopBegin(LValue lValue, short initialVal, short stepVal, short limitVal) {
        if (Parser.ScanRegex("^\\s*;") != null) {
        } else {
            //should be at eol()
            Parser.SkipSpaces();
            if (!Parser.EoL()) {
                throw new RuntimeException("Expected ';' or end of line.");
            }
        }

        var level = new StackLevelInfo(file: Interpreter.CurrentFile,
                                       lineNum: Parser.LineNumber,
                                       colNum: Parser.LinePosition,
                                       srcLine: Parser.Line);
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
            //DIRTY CODE!  poke a new location into Interpreter and Parser.
            if (Interpreter.LineNumber == 0) { //interactive
                                               //Interpreter.CurrentLine = ;
                                               //Interpreter.LineNumber = ;
                                               //Interpreter.CurrentLineOrd = ;
                Parser.LinePosition = endFor.ColumnPosition;
            } else {
                Interpreter.CurrentLine = Interpreter.ProgramSource[Interpreter.LineLocations[(short)endFor.LineNumber]].src;
                Interpreter.LineNumber = endFor.LineNumber;
                Interpreter.CurrentLineOrd = Interpreter.LineLocations[(short)endFor.LineNumber];
                Parser.SetLine(line: Interpreter.CurrentLine,
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
                    //DIRTY CODE!  poke a new location into Interpreter and Parser.  Should add a method to jump to new loc...
                    if (lvl.EntryPoint.LineNumber == 0) { //interactive
                        //Interpreter.CurrentLine = ;
                        //Interpreter.LineNumber = ;
                        //Interpreter.CurrentLineOrd = ;
                        Parser.LinePosition = lvl.EntryPoint.ColumnPosition;
                    } else {
                        Interpreter.CurrentLine = Interpreter.ProgramSource[Interpreter.LineLocations[(short)lvl.EntryPoint.LineNumber]].src;
                        Interpreter.LineNumber = lvl.EntryPoint.LineNumber;
                        Interpreter.CurrentLineOrd = Interpreter.LineLocations[(short)lvl.EntryPoint.LineNumber];
                        Parser.SetLine(line: Interpreter.CurrentLine,
                            linePosition: lvl.EntryPoint.ColumnPosition,
                            lineNumber: lvl.EntryPoint.LineNumber);
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
        _ = Parser.ScanRegex("^\\s*;");
        _ = TBStack.Pop();
    }

    internal StackLevelInfo Gosub(short newLineNumber, int newLineOrd) {
        //push current program point (1st statement after gosub)
        var level = new StackLevelInfo(file: Interpreter.CurrentFile,
                                       lineNum: Parser.LineNumber,
                                       colNum: Parser.LinePosition,
                                       srcLine: Parser.Line);
        //EndPoint = null; //unknown at this time
        level.Kind = StackEntryKind.Gosub;
        TBStack.Push(level);
        //stuff new loc into Interpreter, Parser (just like goto)
        Interpreter.JumpToLine(newLineOrd);
        return level;
    }

    internal void Return() {
        var returnPoint = TBStack.Pop();

        //DIRTY CODE!  poke a new location into Interpreter and Parser.
        Interpreter.CurrentLine = returnPoint.EntryPoint.SrcLine;
        Interpreter.LineNumber = returnPoint.EntryPoint.LineNumber;
        if (returnPoint.EntryPoint.LineNumber == 0) {
            Interpreter.CurrentLineOrd = -1;
        } else {
            Interpreter.CurrentLineOrd = Interpreter.LineLocations[returnPoint.EntryPoint.LineNumber];
        }
        Parser.SetLine(line: returnPoint.EntryPoint.SrcLine!,
            linePosition: returnPoint.EntryPoint.ColumnPosition,
            lineNumber: returnPoint.EntryPoint.LineNumber);

    }
}
