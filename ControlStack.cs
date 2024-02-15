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

    internal StackLevelInfo? ForLoopBegin(string varName, short initialVal, short stepVal, short limitVal) {
        if (parser.ScanRegex("^\\s*;") == null) {
        } else {
            //should be at eol()
            parser.SkipSpaces();
            if (!parser.EoL()) {
                throw new RuntimeException("Expected ';' or end of line.");    
            }
        }

        var level = new StackLevelInfo(file: TBInterpreter.CurrentFile,
                                       lineNum: parser.LineNumber ?? 0,
                                       colNum: parser.LinePosition);
        //EndPoint = null; //unknown at this time
        level.ForVariable = varName;
        level.ForInitial = initialVal;
        level.ForIncrement = stepVal;
        level.ForLimit = limitVal;
        level.Kind = StackEntryKind.For;

        VariableStore.Shared.StoreVariable(varName.ToUpperInvariant(), initialVal);
        if ((stepVal > 0 && initialVal > limitVal) || (stepVal < 0 && initialVal < limitVal)) {
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
                    TBInterpreter.CurrentLine = TBInterpreter.Program[TBInterpreter.LineLocations[(short)endFor.LineNumber]].src;
                    TBInterpreter.LineNumber = endFor.LineNumber;
                    TBInterpreter.CurrentLineOrd = TBInterpreter.LineLocations[(short)endFor.LineNumber];
                    parser.SetLine(line:TBInterpreter.CurrentLine,
                        linePosition:endFor.ColumnPosition,
                        lineNumber:endFor.LineNumber);
                }
                return null;
            }
        }

        TBStack.Push(level);
        return level;
    }

    internal StackLevelInfo ForLoopNext(string varName) {
        StackLevelInfo? lvl;
        while (TBStack.TryPeek(out lvl)) {
            if (lvl.Kind == StackEntryKind.For && string.Equals(lvl.ForVariable, varName, StringComparison.InvariantCultureIgnoreCase)) {
                //this is matching level
                var indexVar = VariableStore.Shared.TryGetVariable(varName.ToUpperInvariant(), false);
                if (indexVar == null) {
                    throw new RuntimeException($"Variable '{varName}' not found.");
                }
                var indexVal = indexVar.ShortValue ?? 0;
                bool loopEnded;
                indexVal += lvl.ForIncrement;
                //VariableStore.Shared.StoreVariable(varName, indexVal);
                indexVar.VValue = indexVal;
                if (lvl.ForIncrement > 0) {
                    loopEnded = indexVal > lvl.ForLimit;
                } else {
                    loopEnded = indexVal < lvl.ForLimit;
                }

                if (loopEnded) {
                    _ = parser.ScanRegex("^\\s*;");
                    _ = TBStack.Pop();
                    //ready to run first statement after next
                } else {
                    //DIRTY CODE!  poke a new location into ITBInterpreter and parser.
                    if (lvl.EntryPoint.LineNumber == 0) { //interactive
                        //TBInterpreter.CurrentLine = ;
                        //TBInterpreter.LineNumber = ;
                        //TBInterpreter.CurrentLineOrd = ;
                        parser.LinePosition = lvl.EntryPoint.ColumnPosition;
                    } else {
                        TBInterpreter.CurrentLine = TBInterpreter.Program[TBInterpreter.LineLocations[(short)lvl.EntryPoint.LineNumber]].src;
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
            continue;
        }
        throw new RuntimeException("Next variable does not match an executing for loop.");
    }

    internal StackLevelInfo ForLoopEnd() {
        return null;
    }

    internal StackLevelInfo Gosub() {
        return null;
    }

    internal StackLevelInfo Return() {
        return null;
    }
}
