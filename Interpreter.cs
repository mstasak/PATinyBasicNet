

using System.Collections.Generic;
using System.Linq;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.PortableExecutable;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Runtime.Intrinsics.X86;
using System.ComponentModel.Design;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;
using System.Runtime.Intrinsics.Arm;

namespace NewPaloAltoTB;

/**
 *   Creating a Windows version of Palo Alto Tiny Basic
 */
internal partial class Interpreter {
    internal int LineNumber;
    internal int CurrentLineOrd;
    internal int NewLineOrd = -1;
    internal string ImmediateLine = "";
    //internal int LinePosition;
    internal string? CurrentLine;
    //internal short LineLabel;
    internal bool OutputSwitch = true;
    internal List<(int linenum, string src)> ProgramSource = [];
    internal Dictionary<int, int> LineLocations = []; //looks up ordinal position of a basic line# in Program
    internal ParserTools parser = ParserTools.Shared;
    internal Expression ExpressionService = Expression.Shared;
    internal bool StopRequested = false;
    internal bool ImmediateMode = false;
    internal string CurrentFile => ImmediateMode ? "" : "FILE"; //in future will contain actual filename.bas path
    internal static Interpreter Shared => shared.Value;
    private static readonly Lazy<Interpreter> shared = new(() => new Interpreter());

    /// <summary>
    /// Delete a given line from the program buffer (by vb line#).
    /// To do: implement deletion of range of lines.
    /// </summary>
    /// <param name="lineNumber"></param>
    /// <returns></returns>
    internal bool DeleteLine(short lineNumber) {
        int ordinalLinePos;
        if (LineLocations.TryGetValue(lineNumber, out ordinalLinePos)) {
            ProgramSource.RemoveAt(ordinalLinePos);
            LineLocations.Remove(lineNumber);
            foreach (var kv in LineLocations.Where(e => e.Key > lineNumber)) {
                LineLocations[kv.Key] -= 1;
            }
            return true;
        } else {
            return false;
        }
    }

    /// <summary>
    /// Add or update a line in the program buffer.
    /// </summary>
    /// <param name="lineNumber"></param>
    /// <param name="lineContents"></param>
    internal void StoreLine(short lineNumber, string lineContents) {
        //insert or update line
        var ListPosition = LineLocations.Where(e => e.Key >= lineNumber).AsQueryable().FirstOrDefault(new KeyValuePair<int, int>(-1, (short)-1));
        if (ListPosition.Key == lineNumber) {
            //replace found
            ProgramSource[ListPosition.Value] = (lineNumber, lineContents);
        } else if (ListPosition.Key == -1) {
            //at end
            LineLocations[lineNumber] = ProgramSource.Count;
            ProgramSource.Add((lineNumber, lineContents));
        } else {
            //insert before found
            ProgramSource.Insert(ListPosition.Value, (lineNumber, lineContents));
            foreach (var kv in LineLocations.Where(e => e.Key > lineNumber)) {
                LineLocations[kv.Key] += 1;
            }
        }
    }

    /* internal void CrLf() {
    //    OutChar('\r');
    //    OutChar('\n');
    //} */

    /* internal void OutChar(char c) {
    //    if (OutputSwitch == false) {
    //        return;
    //    }

    //    Console.Write(c);
    //    if (c == '\r') {
    //        Console.Write('\n');
    //    }
    //} */

    internal bool RunLine(string line, int lineNumber) {
        StopRequested = false;
        NewLineOrd = -1;
        parser.SetLine(line: line, linePosition: 0, lineNumber: lineNumber);
        //TODO: figure out how to handle 1st return in case like '100 GOSUB 2000; GOSUB 3000'
        var statementSucceeded = true;
        while (statementSucceeded && !parser.EoL() && NewLineOrd == -1) {
            statementSucceeded = RunStatement(immediateMode: lineNumber == 0);
            _ = parser.ScanRegex("^\\s*;");
            if (StopRequested) {
                break;
            }
        }
        return statementSucceeded;
    }

    internal bool Run(bool Immediate) {
        var rslt = true;
        ImmediateMode = Immediate;
        StopRequested = false;
        CurrentLineOrd = Immediate ? -1 : 0;
        NewLineOrd = -1;
        try {
            while (true) {
                if (CurrentLineOrd >= 0) {
                    (LineNumber, CurrentLine) = ProgramSource[CurrentLineOrd];
                } else {
                    (LineNumber, CurrentLine) = (0, ImmediateLine);
                }
                var oldCurrentLineOrd = CurrentLineOrd;
                rslt = RunLine(CurrentLine, LineNumber);
                if (LineNumber == 0) {
                    break;
                }
                int.Min(1, 2);
                if (NewLineOrd == -1) {
                    //advance to next line
                    CurrentLineOrd++;
                    if (CurrentLineOrd >= ProgramSource.Count) {
                        break;
                    }
                } else {
                    // RunLine caused a new next line, i.e. GoTo, GoSub, Next, Return or similar.
                    // (certain statements, when run, will directly change the current line and lineposition)
                    // so do nothing to NewLineOrd. This is to jump into mid-line, after a ';'.
                    CurrentLineOrd = NewLineOrd;
                }
                if (StopRequested) {
                    break;
                }
            }
            //for now, retain these - may want some debug dump or retry/resume command
            //LineNumber = 0;
            //LinePosition = 0;
        } catch (RuntimeException ex) {
            Console.WriteLine(ex.MessageDetail + "\\n" + ex.ToString());
            throw;
        } catch (Exception ex) {
            Console.WriteLine(ex.Message + "\\n" + ex.ToString());
            throw;
        }
        return rslt;
    }

    internal enum StatementCode {
        LetStatement,
        InputStatement,
        PrintStatement,
        RemStatement,
        WaitStatement,
        StopStatement,
        //OutStatement,
        //PokeStatement,
        IfStatement,
        ForStatement,
        NextStatement,
        GotoStatement,
        GosubStatement,
        ReturnStatement,
        /* future possibles:
         dim, global, sub, function, import/inherit/etc to access caller-scoped vars
        graphics subs/funcs
        file subs/funcs
        debug tools (trace on/off, dump vars, watch vars, break, save state?)
         */
    }

    internal bool RunStatement(bool immediateMode) {
        var rslt = false;
        var whichStmt = parser.ScanStringTableEntry([
            "LET",
            "INPUT",
            "PRINT",
            "REM",
            "WAIT",
            "STOP",
            //"OUT",
            //"POKE",
            "IF",
            "FOR",
            "NEXT",
            "GOTO",
            "GOSUB",
            "RETURN",

        ]);
        switch ((StatementCode?)whichStmt) {
            case StatementCode.LetStatement:
                rslt = RunAssignmentStatement();
                break;
            case StatementCode.InputStatement:
                break;
            case StatementCode.PrintStatement:
                rslt = RunPrintStatement();
                break;
            case StatementCode.RemStatement:
                parser.LinePosition = parser.Line.Length;
                rslt = true;
                break;
            case StatementCode.WaitStatement:
                rslt = RunWaitStatement();
                break;
            case StatementCode.StopStatement:
                StopRequested = true;
                rslt = true;
                break;
            case StatementCode.IfStatement:
                rslt = RunIfStatement();
                break;
            case StatementCode.ForStatement:
                rslt = RunForStatement();
                break;
            case StatementCode.NextStatement:
                rslt = RunNextStatement();
                break;
            case StatementCode.GotoStatement:
                rslt = RunGotoStatement();
                break;
            case StatementCode.GosubStatement:
                rslt = RunGosubStatement();
                break;
            case StatementCode.ReturnStatement:
                rslt = RunReturnStatement();
                break;
            default:
                rslt = RunAssignmentStatement();
                break;
        }
        return rslt;
    }

    internal bool RunAssignmentStatement() {
        var rslt = true;
        while (true) {
            if (!RunOneAssignment()) {
                rslt = false;
                break;
            }
            if (parser.ScanRegex("^\\s*,\\s*") == null) {
                break;
            }
        }
        return rslt;
    }

    internal bool RunOneAssignment() {
        var rslt = true;
        var oldPos = parser.LinePosition;
        var VName = parser.ScanName();
        if (VName == null) {
            return false;
        }
        VName = VName.ToUpperInvariant();
        if (parser.ScanRegex("^\\s*=") == null) {
            parser.LinePosition = oldPos;
            return false;
        }
        short value;
        if (ExpressionService.TryEvaluateExpr(out value)) {
            VariableStore.Shared.StoreVariable(VName, value);
            return true;
        } else {
            throw new RuntimeException("Expression expected.");
        }
    }

    private int PrintNumWidth;
    private bool DidPrintSomething;
    internal bool RunPrintStatement() {
        // PRINT (EXPR | #FORMAT | '/"STRINGLITERAL'/") [,another] [,?]
        var rslt = true;
        PrintNumWidth = 6; //changed by #formatexpression arguments
        DidPrintSomething = false; //when true, a trailing comma is OK
        while (true) {
            if (TryPrintFormat() || TryPrintStringLiteral() || TryPrintNumber()) {
                if (TrySkipComma()) {
                    //ready for next term, just repeat do loop
                } else {
                    //no comma so no more terms expected, do output final crlf
                    Console.WriteLine();
                    break;
                }
            } else {
                if (DidPrintSomething) {
                    //we only get here with Print term [,term...], (trailing ',' AFTER printing something)
                    break;
                } else {
                    //trailing ',' without printing anything (or only processing format terms)!
                    throw new RuntimeException("Print statement will not print anything!");
                }
            }
        }
        return rslt;
    }

    internal bool TryPrintFormat() {
        parser.SkipSpaces();
        if (parser.ScanString("#")) {
            short newWidth;
            if (ExpressionService.TryEvaluateExpr(out newWidth)) {
                PrintNumWidth = newWidth;
                return true;
            }
        }
        return false;
    }
    internal bool TryPrintNumber() {
        short pValue;
        if (ExpressionService.TryEvaluateExpr(out pValue)) {
            Console.Write(pValue.ToString().PadLeft(PrintNumWidth)); // pValue.ToString($"{PrintNumWidth}:D"));
            DidPrintSomething = true;
            return true;
        } else {
            return false;
        }
    }
    internal bool TryPrintStringLiteral() {
        var sValue = parser.ScanStringLiteral();
        if (sValue != null) {
            Console.Write(sValue);
            DidPrintSomething = true;
            return true;
        }
        return false;
    }

    internal bool TrySkipComma() {
        parser.SkipSpaces();
        return parser.ScanString(",");
    }

    internal bool RunWaitStatement() {
        var prompt = parser.ScanStringLiteral() ?? "Press a key...";
        Console.WriteLine(prompt);
        Console.ReadKey();
        return true;
    }
    internal bool RunIfStatement() {
        var rslt = false;
        short cond;
        if (ExpressionService.TryEvaluateExpr(out cond)) {
            rslt = true;
            _ = parser.ScanString("THEN"); // THEN is optional
            if (cond == 0) {
                parser.LinePosition = parser.Line.Length; //if condition is false, ignore rest of line    
            }
        } else {
            throw new RuntimeException("Invalid If expression.");
        }
        return rslt;
    }
    internal bool RunForStatement() {
        //var = expr [down]to expr [step expr]
        var varName = parser.ScanName();
        if (varName == null) {
            throw new RuntimeException("For loop variable name not found.");
        }
        if (parser.ScanRegex("^\\s*=") == null) {
            throw new RuntimeException("For loop syntax: expected '='.");
        }
        short initValue;
        if (!ExpressionService.TryEvaluateExpr(out initValue)) {
            throw new RuntimeException("For loop: error in initial value expression.");
        }
        if (parser.ScanRegex("^\\s*to") == null) {
            throw new RuntimeException("For loop syntax: expected \" TO \".");
        }
        short limitValue;
        if (!ExpressionService.TryEvaluateExpr(out limitValue)) {
            throw new RuntimeException("For loop: error in limit value expression.");
        }
        short stepValue = 1;
        if (parser.ScanRegex("^\\s*step") != null) {
            if (!ExpressionService.TryEvaluateExpr(out stepValue)) {
                throw new RuntimeException("For loop: error in step value expression.");
            }
        } else {
            if (limitValue < initValue) {
                stepValue = -1;    
            }
        }
        //got a valid for statement!
        ControlStack.Shared.ForLoopBegin(varName: varName, initialVal: initValue, stepVal: stepValue, limitVal: limitValue);

        return true;
    }
    internal bool RunNextStatement() {
        var varName = parser.ScanName();
        if (varName == null) {
            throw new RuntimeException("Expected: for loop variable name.");
        }
        ControlStack.Shared.ForLoopNext(varName: varName);
        return true;
    }
    internal bool RunGotoStatement() {
        var rslt = false;
        short newLineNum;
        if (ExpressionService.TryEvaluateExpr(out newLineNum)) {
            int newOrd;
            if (LineLocations.TryGetValue(newLineNum, out newOrd)) {
                NewLineOrd = newOrd; //run loop will transfer to this line
                parser.LinePosition = parser.Line.Length; //ignore rest of line (could complain if not empty or REM...)
                rslt = true;
            } else {
                throw new RuntimeException($"Goto target line {newLineNum} not found.");
            }
        } else {
            throw new RuntimeException("Goto target line number/expression not understood.");
        }

        return rslt;
    }
    internal bool RunGosubStatement() {
        var rslt = false;
        short newLineNum;
        if (ExpressionService.TryEvaluateExpr(out newLineNum)) {
            int newOrd;
            if (LineLocations.TryGetValue(newLineNum, out newOrd)) {
                NewLineOrd = newOrd; //run loop will transfer to this line
                //parser.LinePosition = parser.Line.Length; //ignore rest of line (could complain if not empty or REM...)
                parser.ScanRegex("^\\s*;"); //skip statement separator
                ControlStack.Shared.Gosub(newLineNum, newOrd); //push return address onto control stack
                rslt = true;
            } else {
                throw new RuntimeException($"Gosubtarget line {newLineNum} not found.");
            }
        } else {
            throw new RuntimeException("Gosub target line number/expression not understood.");
        }

        return rslt;
    }
    internal bool RunReturnStatement() {
        ControlStack.Shared.Return();
        return true;
    }

    internal void JumpToLine(int newLineOrder) {
        LineNumber = ProgramSource[newLineOrder].linenum;
        CurrentLineOrd = newLineOrder;
        CurrentLine = ProgramSource[newLineOrder].src;
        parser.SetLine(CurrentLine, 0, LineNumber);
    }
}


/*
       JP   TN1       ;DIGIT. S SAYS OVERFLOW
QHOW   PUSH D         ;*** ERROR: "HOW?" ***
AHOW   LXI  D,HOW
       JMP  ERROR
HOW    .ASCII   "HOW?\r"
OK     .ASCII   "OK\r"
WHAT   .ASCII   "WHAT?\r"
SORRY  .ASCII   "SORRY\r"
;*
;**************************************************************
;*
;* *** MAIN ***
;*
;* THIS IS THE MAIN LOOP THAT COLLECTS THE TINY BASIC PROGRAM
;* AND STORES IT IN THE MEMORY.
;*
;* AT START, IT PRINTS OUT "(CR)OK(CR)", AND INITIALIZES THE
;* STACK AND SOME OTHER INTERNAL VARIABLES.  THEN IT PROMPTS
;* ">" AND READS A LINE.  IFF THE LINE STARTS WITH A NON-ZERO
;* NUMBER, THIS NUMBER IS THE LINE NUMBER.  THE LINE NUMBER
;* (IN 16 BIT BINARY) AND THE REST OF THE LINE (INCLUDING CR)
;* IS STORED IN THE MEMORY.  IFF A LINE WITH THE SAME LINE
;* NUMBER IS ALREDY THERE, IT IS REPLACED BY THE NEW ONE.  IF
;* THE REST OF THE LINE CONSISTS OF A 0DHONLY, IT IS NOT STORED
;* AND ANY EXISTING LINE WITH THE SAME LINE NUMBER IS DELETED.
;*
;* AFTER A LINE IS INSERTED, REPLACED, OR DELETED, THE PROGRAM
;* LOOPS BACK AND ASK FOR ANOTHER LINE.  THIS LOOP WILL BE
;* TERMINATED WHEN IT READS A LINE WITH ZERO OR NO LINE
;* NUMBER; AND CONTROL IS TRANSFERED TO "DIRCT".
;*
;* TINY BASIC PROGRAM SAVE AREA STARTS AT THE MEMORY LOCATION
;* LABELED "TXTBGN" AND ENDED AT "TXTEND".  WE ALWAYS FILL THIS
;* AREA STARTING AT "TXTBGN", THE UNFILLED PORTION IS POINTED
;* BY THE CONTENT OF A MEMORY LOCATION LABELED "TXTUNF".
;*
;* THE MEMORY LOCATION "CURRNT" POINTS TO THE LINE NUMBER
;* THAT IS CURRENTLY BEING INTERPRETED.  WHILE WE ARE IN
;* THIS LOOP OR WHILE WE ARE INTERPRETING A DIRECT COMMAND
;* (SEE NEXT SECTION), "CURRNT" SHOULD POINT TO A 0.
;*
RSTART LXI  SP,STACK  ;SET STACK POINTER
ST1    CALL CRLF      ;AND JUMP TO HERE
       LXI  D,OK      ;DE->STRING
       SUB  A         ;A=0
       CALL PRTSTG    ;PRINT STRING UNTIL 0DH
       LXI  H,ST2+1   ;LITERAL 0
       SHLD CURRNT    ;CURRNT->LINE # = 0
ST2    LXI  H,0
       SHLD LOPVAR
       SHLD STKGOS
ST3    MVI  A,'>'     ;PROMPT '>' AND
       CALL GETLN     ;READ A LINE
       PUSH D         ;DE->END OF LINE
ST3A   LXI  D,BUFFER  ;DE->BEGINNING OF LINE
       CALL TSTNUM    ;TESt IFF IT IS A NUMBER
       RST  5
       MOV  A,H       ;HL=VALUE OF THE # OR
       ORA  L         ;0 IFF NO # WAS FOUND
       POP  B         ;BC->END OF LINE
       JZ   DIRECT
       DCX  D         ;BACKUP DE AND SAVE
       MOV  A,H       ;VALUE OF LINE # THERE
       STAX D
       DCX  D
       MOV  A,L
       STAX D
       PUSH B         ;BC,DE->BEGIN, END
       PUSH D
       MOV  A,C
       SUB  E
       PUSH PSW       ;A=# OF BYTES IN LINE
       CALL FNDLN     ;FIND THIS LINE IN SAVE
       PUSH D         ;AREA, DE->SAVE AREA
       JNZ  ST4       ;NZ:NOT FOUND, INSERT
       PUSH D         ;Z:FOUND, DELETE IT
       CALL FNDNXT    ;FIND NEXT LINE
;*                                       DE->NEXT LINE
       POP  B         ;BC->LINE TO BE DELETED
       LHLD TXTUNF    ;HL->UNFILLED SAVE AREA
       CALL MVUP      ;MOVE UP TO DELETE
       MOV  H,B       ;TXTUNF->UNFILLED AREA
       MOV  L,C
       SHLD TXTUNF    ;UPDATE
ST4    POP  B         ;GET READY TO INSERT
       LHLD TXTUNF    ;BUT FIRT CHECK IF
       POP  PSW       ;THE LENGTH OF NEW LINE
       PUSH H         ;IS 3 (LINE # AND CR)
       CPI  3         ;THEN DO NOT INSERT
       JZ   RSTART    ;MUST CLEAR THE STACK
       ADD  L         ;COMPUTE NEW TXTUNF
       MOV  L,A
       MVI  A,0
       ADC  H
       MOV  H,A       ;HL->NEW UNFILLED AREA
ST4A   LXI  D,TXTEND  ;CHECK TO SEE IF THERE
       RST  4         ;IS ENOUGH SPACE
       JNC  QSORRY    ;SORRY, NO ROOM FOR IT
       SHLD TXTUNF    ;OK, UPDATE TXTUNF
       POP  D         ;DE->OLD UNFILLED AREA
       CALL MVDOWN
       POP  D         ;DE->BEGIN, HL->END
       POP  H
       CALL MVUP      ;MOVE NEW LINE TO SAVE
       JMP  ST3       ;AREA
;*
;**************************************************************
;*
;* *** TABLES *** DIRECT *** & EXEC ***
;*
;* THIS SECTION OF THE CODE TESTS A STRING AGAINST A TABLE.
;* WHEN A MATCH IS FOUND, CONTROL IS TRANSFERED TO THE SECTION
;* OF CODE ACCORDING TO THE TABLE.
;*
;* AT 'EXEC', DE SHOULD POINT TO THE STRING AD HL SHOULD POINT
;* TO THE TABLE-1.  AT 'DIRECT', DE SHOULD POINT TO THE STRING,
;* HL WILL BE SET UP TO POINT TO TAB1-1, WHICH IS THE TABLE OF
;* ALL DIRECT AND STATEMENT COMMANDS.
;*
;* A '.' IN THE STRING WILL TERMINATE THE TEST AND THE PARTIAL
;* MATCH WILL BE CONSIDERED AS A MATCH.  E.G., 'P.', 'PR.',
;* 'PRI.', 'PRIN.', OR 'PRINT' WILL ALL MATCH 'PRINT'.
;*
;* THE TABLE CONSISTS OF ANY NUMBER OF ITEMS.  EACH ITEM
;* IS A STRING OF CHARACTERS WITH BIT 7 SET TO 0 AND
;* A JUMP ADDRESS STORED HI-LOW WITH BIT 7 OF THE HIGH
;* BYTE SET TO 1.
;*
;* END OF TABLE IS AN ITEM WITH A JUMP ADDRESS ONLY.  IFF THE
;* STRING DOES NOT MATCH ANY OF THE OTHER ITEMS, IT WILL
;* MATCH THIS NULL ITEM AS DEFAULT.
;*
TAB1   =    *
;DIRECT COMMANDS
       .byte "LIST",   <LIST   | $80, >LIST
       .byte "RUN",    <RUN    | $80, >RUN
       .byte "NEW",    <NEW    | $80, >NEW
       ;.byte "LOAD",   <DLOAD  | $80, >DLOAD
       ;.byte "SAVE",   <DSAVE  | $80, >DSAVE
       ;.byte "BYE",    $80,           $0      ;GO BACK TO CPM
TAB2   =     *                                ;DIRECT/STATEMENT
       .byte "NEXT",   <NEXT   | $80, >NEXT
       .byte "LET",    <LET    | $80, >LET
       .byte "OUT",    <OUTCMD | $80, >OUTCMD
       .byte "POKE",   <POKE   | $80, >POKE
       .byte "WAIT",   <WAITCM | $80, >WAITCM
       .byte "IF",     <IFF    | $80, >IFF
       .byte "GOTO",   <GOTO   | $80, >GOTO
       .byte "GOSUB",  <GOSUB  | $80, >GOSUB
       .byte "RETURN", <RETURN | $80, >RETURN
       .byte "REM",    <REM    | $80, >REM
       .byte "FOR",    <FOR    | $80, >FOR
       .byte "INPUT",  <INPUT  | $80, >INPUT
       .byte "PRINT",  <PRINT  | $80, >PRINT
       .byte "STOP",   <STOP   | $80, >STOP
       .byte           <DEFLT  | $80, >DEFLT
       .byte  "YOU CAN ADD MORE"              ;COMMANDS BUT
                                              ;REMEMBER TO MOVE
                                              ;DEFAULT DOWN.
TAB5   =  *                                   ;"TO" IN "FOR"
       .byte   "TO",   <FR1    | $80, >FR1
       .byte           <QWHAT  | $80, >QWHAT
TAB6   = *                                    ;"STEP" IN "FOR"
       .byte   "STEP", <FR2    | $80, >FR2
       .byte           <FR3    | $80, >FR3
;*
DIRECT LXI  H,TAB1-1  ;*** DIRECT ***
;*
EXEC   =    *         ;*** EXEC ***
EX0    RST  5         ;IGNORE LEADING BLANKS
       PUSH D         ;SAVE POINTER
EX1    LDAX D         ;IFF FOUND '.' IN STRING
       INX  D         ;BEFORE ANY MISMATCH
       CPI  '.'       ;WE DECLARE A MATCH
       JZ   EX3
       INX  H         ;HL->TABLE
       CMP  M         ;IFF MATCH, TEST NEXT
       JZ   EX1
       MVI  A,$7F     ;ELSE, SEE IFF BIT 7
       DCX  D         ;OF TABLE IS SET, WHICH
       CMP  M         ;IS THE JUMP ADDR. (HI)
       JC   EX5       ;C:YES, MATCHED
EX2    INX  H         ;NC:NO, FIND JUMP ADDR.
       CMP  M
       JNC  EX2
       INX  H         ;BUMP TO NEXT TAB. ITEM
       POP  D         ;RESTORE STRING POINTER
       JMP  EX0       ;TEST AGAINST NEXT ITEM
EX3    MVI  A,$7F     ;PARTIAL MATCH, FIND
EX4    INX  H         ;JUMP ADDR., WHICH IS
       CMP  M         ;FLAGGED BY BIT 7
       JNC  EX4
EX5    MOV  A,M       ;LOAD HL WITH THE JUMP
       INX  H         ;ADDRESS FROM THE TABLE
       MOV  L,M
       ANI  $7F       ;MASK OFF BIT 7
       MOV  H,A
       POP  PSW       ;CLEAN UP THE GABAGE
       PCHL           ;AND WE GO DO IT
;*
;**************************************************************
;*
;* WHAT FOLLOWS IS THE CODE TO EXECUTE DIRECT AND STATEMENT
;* COMMANDS.  CONTROL IS TRANSFERED TO THESE POINTS VIA THE
;* COMMAND TABLE LOOKUP CODE OF 'DIRECT' AND 'EXEC' IN LAST
;* SECTION.  AFTER THE COMMAND IS EXECUTED, CONTROL IS
;* TANSFERED TO OTHER SECTIONS AS FOLLOWS:
;*
;* FOR 'LIST', 'NEW', AND 'STOP': GO BACK TO 'RSTART'
;* FOR 'RUN': GO EXECUTE THE FIRST STORED LINE IFF ANY; ELSE
;* GO BACK TO 'RSTART'.
;* FOR 'GOTO' AND 'GOSUB': GO EXECUTE THE TARGET LINE.
;* FOR 'RETURN' AND 'NEXT': GO BACK TO SAVED RETURN LINE.
;* FOR ALL OTHERS: IFF 'CURRNT' -> 0, GO TO 'RSTART', ELSE
;* GO EXECUTE NEXT COMMAND.  (THIS IS DONE IN 'FINISH'.)
;*
;**************************************************************
;*
;* *** NEW *** STOP *** RUN (& FRIENDS) *** & GOTO ***
;*
;* 'NEW(CR)' SETS 'TXTUNF' TO POINT TO 'TXTBGN'
;*
;* 'STOP(CR)' GOES BACK TO 'RSTART'
;*
;* 'RUN(CR)' FINDS THE FIRST STORED LINE, STORE ITS ADDRESS (IN
;* 'CURRNT'), AND START EXECUTE IT.  NOTE THAT ONLY THOSE
;* COMMANDS IN TAB2 ARE LEGAL FOR STORED PROGRAM.
;*
;* THERE ARE 3 MORE ENTRIES IN 'RUN':
;* 'RUNNXL' FINDS NEXT LINE, STORES ITS ADDR. AND EXECUTES IT.
;* 'RUNTSL' STORES THE ADDRESS OF THIS LINE AND EXECUTES IT.
;* 'RUNSML' CONTINUES THE EXECUTION ON SAME LINE.
;*
;* 'GOTO EXPR(CR)' EVALUATES THE EXPRESSION, FIND THE TARGET
;* LINE, AND JUMP TO 'RUNTSL' TO DO IT.
;* 'DLOAD' LOADS A NAMED PROGRAM FROM DISK.
;* 'DSAVE' SAVES A NAMED PROGRAM ON DISK.
;* 'FCBSET' SETS UP THE FILE CONTROL BLOCK FOR SUBSEQUENT DISK I/O.
;*
NEW    CALL ENDCHK    ;*** NEW(CR) ***
       LXI  H,TXTBGN
       SHLD TXTUNF
;*
STOP   CALL ENDCHK    ;*** STOP(CR) ***
       JMP RSTART
;*
RUN    CALL ENDCHK    ;*** RUN(CR) ***
       LXI  D,TXTBGN  ;FIRST SAVED LINE
;*
RUNNXL LXI  H,0       ;*** RUNNXL ***
       CALL FNDLNP    ;FIND WHATEVER LINE #
       JC   RSTART    ;C:PASSED TXTUNF, QUIT
;*
RUNTSL XCHG           ;*** RUNTSL ***
       SHLD CURRNT    ;SET 'CURRNT'->LINE #
       XCHG
       INX  D         ;BUMP PASS LINE #
       INX  D
;*
RUNSML CALL CHKIO     ;*** RUNSML ***
       LXI  H,TAB2-1  ;FIND COMMAND IN TAB2
       JMP  EXEC      ;AND EXECUTE IT
;*
GOTO   RST  3         ;*** GOTO EXPR ***
       PUSH D         ;SAVE FOR ERROR ROUTINE
       CALL ENDCHK    ;MUST FIND A 0DH
       CALL FNDLN     ;FIND THE TARGET LINE
       JNZ  AHOW      ;NO SUCH LINE #
       POP  PSW       ;CLEAR THE "PUSH DE"
       JMP  RUNTSL    ;GO DO IT
;CPM    =  5         ;DISK PARAMETERS
;FCB    =  $5C
;SETDMA =  26
;OPEN   =  15
;READD  =  20
;WRITED =  21
;CLOSE  =  16
;MAKE   =  22
;DELETE =  19
;*
;DLOAD  RST  5         ;IGNORE BLANKS
;       PUSH H         ;SAVE H
;       CALL FCBSET    ;SET UP FILE CONTROL BLOCK
;       PUSH D         ;SAVE THE REST
;       PUSH B
;       LXI  D,FCB     ;GET FCB ADDRESS
;       MVI  C,OPEN    ;PREPARE TO OPEN FILE
;       CALL CPM       ;OPEN IT
;       CPI  $FF       ;IS IT THERE?
;       JZ   QHOW      ;NO, SEND ERROR
;       XRA  A         ;CLEAR A
;       STA  FCB+32    ;START AT RECORD 0
;       LXI  D,TXTUNF  ;GET BEGINNING
;LOAD   PUSH D         ;SAVE DMA ADDRESS
;       MVI  C,SETDMA  ;
;       CALL CPM       ;SET DMA ADDRESS
;       MVI  C,READD   ;
;       LXI  D,FCB
;       CALL CPM       ;READ SECTOR
;       CPI  1         ;DONE?
;       JC   RDMORE    ;NO, READ MORE
;       JNZ  QHOW      ;BAD READ
;       MVI  C,CLOSE
;       LXI  D,FCB
;       CALL CPM       ;CLOSE FILE
;       POP  D         ;THROW AWAY DMA ADD.
;       POP  B         ;GET OLD REGISTERS BACK
;       POP  D
;       POP  H
;       RST  6         ;FINISH
;RDMORE POP  D         ;GET DMA ADDRESS
;       LXI  H,$80     ;GET 128
;       DAD  D         ;ADD 128 TO DMA ADD.
;       XCHG           ;PUT IT BACK IN D
;       JMP  LOAD      ;AND READ SOME MORE
;;*
;DSAVE  RST  5         ;IGNORE BLANKS
;       PUSH H         ;SAVE H
;       CALL FCBSET    ;SETUP FCB
;       PUSH D
;       PUSH B         ;SAVE OTHERS
;       LXI  D,FCB
;       MVI  C,DELETE
;       CALL CPM       ;ERASE FILE IF IT EXISTS
;       LXI  D,FCB
;       MVI  C,MAKE
;       CALL CPM       ;MAKE A NEW ONE
;       CPI  $FF      ;IS THERE SPACE?
;       JZ   QHOW      ;NO, ERROR
;       XRA  A         ;CLEAR A
;       STA  FCB+32    ;START AT RECORD 0
;       LXI  D,TXTUNF  ;GET BEGINNING
;SAVE   PUSH D         ;SAVE DMA ADDRESS
;       MVI  C,SETDMA  ;
;       CALL CPM       ;SET DMA ADDRESS
;       MVI  C,WRITED
;       LXI  D,FCB
;       CALL CPM       ;WRITE SECTOR
;       ORA  A         ;SET FLAGS
;       JNZ  QHOW      ;IF NOT ZERO, ERROR
;       POP  D         ;GET DMA ADD. BACK
;       LDA  TXTUNF+1  ;AND MSB OF LAST ADD.
;       CMP  D         ;IS D SMALLER?
;       JC   SAVDON    ;YES, DONE
;       JNZ  WRITMOR   ;DONT TEST E IF NOT EQUAL
;       LDA  TXTUNF    ;IS E SMALLER?
;       CMP  E
;       JC   SAVDON    ;YES, DONE
;WRITMOR LXI  H,$80
;       DAD  D         ;ADD 128 TO DMA ADD.
;       XCHG           ;GET IT BACK IN D
;       JMP  SAVE      ;WRITE SOME MORE
;SAVDON MVI  C,CLOSE
;       LXI  D,FCB
;       CALL CPM       ;CLOSE FILE
;       POP  B         ;GET REGISTERS BACK
;       POP  D
;       POP  H
;       RST  6         ;FINISH
;;*
;FCBSET LXI  H,FCB     ;GET FILE CONTROL BLOCK ADDRESS
;       MVI  M,0       ;CLEAR ENTRY TYPE
;FNCLR  INX  H         ;NEXT LOCATION
;       MVI  M,' '     ;CLEAR TO SPACE
;       MVI  A,<(FCB+8)
;       CMP  L         ;DONE?
;       JNZ  FNCLR     ;NO, DO IT AGAIN
;       INX  H         ;NEXT
;       MVI  M,'T'     ;SET FILE TYPE TO 'TBI'
;       INX  H
;       MVI  M,'B'
;       INX  H
;       MVI  M,'I'
;EXRC   INX  H         ;CLEAR REST OF FCB
;       MVI  M,0
;       MVI  A,<(FCB+15)
;       CMP  L         ;DONE?
;       JNZ  EXRC      ;NO, CONTINUE
;       LXI  H,FCB+1   ;GET FILENAME START
;FN     LDAX D         ;GET CHARACTER
;       CPI  $0D       ;IS IT A 'CR'
;       RZ             ;YES, DONE
;       CPI  '!'       ;LEGAL CHARACTER?
;       JC   QWHAT     ;NO, SEND ERROR
;       CPI  '['       ;AGAIN
;       JNC  QWHAT     ;DITTO
;       MOV  M,A        ;SAVE IT IN FCB
;       INX  H         ;NEXT
;       INX  D
;       MVI  A,<(FCB+9)
;       CMP  L         ;LAST?
;       JNZ  FN        ;NO, CONTINUE
;       RET            ;TRUNCATE AT 8 CHARACTERS
;*
;*************************************************************
;*
;* *** LIST *** & PRINT ***
;*
;* LIST HAS TWO FORMS:
;* 'LIST(CR)' LISTS ALL SAVED LINES
;* 'LIST #(CR)' START LIST AT THIS LINE #
;* YOU CAN STOP THE LISTING BY CONTROL C KEY
;*
;* PRINT COMMAND IS 'PRINT ....;' OR 'PRINT ....(CR)'
;* WHERE '....' IS A LIST OF EXPRESIONS, FORMATS, BACK-
;* ARROWS, AND STRINGS.  THESE ITEMS ARE SEPERATED BY COMMAS.
;*
;* A FORMAT IS A POUND SIGN FOLLOWED BY A NUMBER.  IT CONTROLSs
;* THE NUMBER OF SPACES THE VALUE OF A EXPRESION IS GOING TO
;* BE PRINTED.  IT STAYS EFFECTIVE FOR THE REST OF THE PRINT
;* COMMAND UNLESS CHANGED BY ANOTHER FORMAT.  IFF NO FORMAT IS
;* SPECIFIED, 6 POSITIONS WILL BE USED.
;*
;* A STRING IS QUOTED IN A PAIR OF SINGLE QUOTES OR A PAIR OF
;* DOUBLE QUOTES.
;*
;* A BACK-ARROW MEANS GENERATE A (CR) WITHOUT (LF)
;*
;* A (CRLF) IS GENERATED AFTER THE ENTIRE LIST HAS BEEN
;* PRINTED OR IFF THE LIST IS A NULL LIST.  HOWEVER IFF THE LIST
;* ENDED WITH A COMMA, NO (CRL) IS GENERATED.
;*
LIST   CALL TSTNUM    ;TEST IFF THERE IS A #
       CALL ENDCHK    ;IFF NO # WE GET A 0
       CALL FNDLN     ;FIND THIS OR NEXT LINE
LS1    JC   RSTART    ;C:PASSED TXTUNF
       CALL PRTLN     ;PRINT THE LINE
       CALL CHKIO     ;STOP IFF HIT CONTROL-C
       CALL FNDLNP    ;FIND NEXT LINE
       JMP  LS1       ;AND LOOP BACK
;*
PRINT  MVI  C,6       ;C = # OF SPACES
       RST  1         ;IFF NULL LIST & ";"
      .byte   0o73
      .byte   6
       CALL CRLF      ;GIVE CR-LF AND
       JMP  RUNSML    ;CONTINUE SAME LINE
PR2    RST  1         ;IFF NULL LIST (CR)
      .byte   '\r'
      .byte   6
       CALL CRLF      ;ALSO GIVE CR-LF &
       JMP  RUNNXL    ;GO TO NEXT LINE
PR0    RST  1         ;ELSE IS IT FORMAT?
      .byte   '#'
      .byte   5
       RST  3         ;YES, EVALUATE EXPR.
       MOV  C,L       ;AND SAVE IT IN C
       JMP  PR3       ;LOOK FOR MORE TO PRINT
PR1    CALL QTSTG     ;OR IS IT A STRING?
       JMP  PR8       ;IFF NOT, MUST BE EXPR.
PR3    RST  1         ;IFF ",", GO FIND NEXT
      .byte   ','
      .byte   6
       CALL FIN       ;IN THE LIST.
       JMP  PR0       ;LIST CONTINUES
PR6    CALL CRLF      ;LIST ENDS
       RST  6
PR8    RST  3         ;EVALUATE THE EXPR
       PUSH B
       CALL PRTNUM    ;PRINT THE VALUE
       POP  B
       JMP  PR3       ;MORE TO PRINT?
;*
;**************************************************************
;*
;* *** GOSUB *** & RETURN ***
;*
;* 'GOSUB EXPR;' OR 'GOSUB EXPR (CR)' IS LIKE THE 'GOTO'
;* COMMAND, EXCEPT THAT THE CURRENT TEXT POINTER, STACK POINTER
;* ETC. ARE SAVE SO THAT EXECUTION CAN BE CONTINUED AFTER THE
;* SUBROUTINE 'RETURN'.  IN ORDER THAT 'GOSUB' CAN BE NESTED
;* (AND EVEN RECURSIVE), THE SAVE AREA MUST BE STACKED.
;* THE STACK POINTER IS SAVED IN 'STKGOS'. THE OLD 'STKGOS' IS
;* SAVED IN THE STACK.  IFF WE ARE IN THE MAIN ROUTINE, 'STKGOS'
;* IS ZERO (THIS WAS DONE BY THE "MAIN" SECTION OF THE CODE),
;* BUT WE STILL SAVE IT AS A FLAG FORr NO FURTHER 'RETURN'S.
;*
;* 'RETURN(CR)' UNDOS EVERYHING THAT 'GOSUB' DID, AND THUS
;* RETURN THE EXCUTION TO THE COMMAND AFTER THE MOST RECENT
;* 'GOSUB'.  IFF 'STKGOS' IS ZERO, IT INDICATES THAT WE
;* NEVER HAD A 'GOSUB' AND IS THUS AN ERROR.
;*
GOSUB  CALL PUSHA     ;SAVE THE CURRENT "FOR"
       RST  3         ;PARAMETERS
       PUSH D         ;AND TEXT POINTER
       CALL FNDLN     ;FIND THE TARGET LINE
       JNZ  AHOW      ;NOT THERE. SAY "HOW?"
       LHLD CURRNT    ;FOUND IT, SAVE OLD
       PUSH H         ;'CURRNT' OLD 'STKGOS'
       LHLD STKGOS
       PUSH H
       LXI  H,0       ;AND LOAD NEW ONES
       SHLD LOPVAR
       DAD  SP
       SHLD STKGOS
       JMP  RUNTSL    ;THEN RUN THAT LINE
RETURN CALL ENDCHK    ;THERE MUST BE A 0DH
       LHLD STKGOS    ;OLD STACK POINTER
       MOV  A,H       ;0 MEANS NOT EXIST
       ORA  L
       JZ   QWHAT     ;SO, WE SAY: "WHAT?"
       SPHL           ;ELSE, RESTORE IT
       POP  H
       SHLD STKGOS    ;AND THE OLD 'STKGOS'
       POP  H
       SHLD CURRNT    ;AND THE OLD 'CURRNT'
       POP  D         ;OLD TEXT POINTER
       CALL POPA      ;OLD "FOR" PARAMETERS
       RST  6         ;AND WE ARE BACK HOME
;*
;**************************************************************
;*
;* *** FOR *** & NEXT ***
;*
;* 'FOR' HAS TWO FORMS:
;* 'FOR VAR=EXP1 TO EXP2 STEP EXP1' AND 'FOR VAR=EXP1 TO EXP2'
;* THE SECOND FORM MEANS THE SAME THING AS THE FIRST FORM WITH
;* EXP1=1.  (I.E., WITH A STEP OF +1.)
;* TBI WILL FIND THE VARIABLE VAR. AND SET ITS VALUE TO THE
;* CURRENT VALUE OF EXP1.  IT ALSO EVALUATES EXPR2 AND EXP1
;* AND SAVE ALL THESE TOGETHER WITH THE TEXT POINTERr ETC. IN
;* THE 'FOR' SAVE AREA, WHICH CONSISTS OF 'LOPVAR', 'LOPINC',
;* 'LOPLMT', 'LOPLN', AND 'LOPPT'.  IFF THERE IS ALREADY SOME-
;* THING IN THE SAVE AREA (THIS IS INDICATED BY A NON-ZERO
;* 'LOPVAR'), THEN THE OLD SAVE AREA IS SAVED IN THE STACK
;* BEFORE THE NEW ONE OVERWRITES IT.
;* TBI WILL THEN DIG IN THE STACK AND FIND OUT IFF THIS SAME
;* VARIABLE WAS USED IN ANOTHER CURRENTLY ACTIVE 'FOR' LOOP.
;* IFF THAT IS THE CASE THEN THE OLD 'FOR' LOOP IS DEACTIVATED.
;* (PURGED FROM THE STACK..)
;*
;* 'NEXT VAR' SERVES AS THE LOGICAL (NOT NECESSARILLY PHYSICAL)
;* END OF THE 'FOR' LOOP.  THE CONTROL VARIABLE VAR. IS CHECKED
;* WITH THE 'LOPVAR'.  IFF THEY ARE NOT THE SAME, TBI DIGS IN
;* THE STACK TO FIND THE RIGHTt ONE AND PURGES ALL THOSE THAT
;* DID NOT MATCH.  EITHER WAY, TBI THEN ADDS THE 'STEP' TO
;* THAT VARIABLE AND CHECK THE RESULT WITH THE LIMIT.  IFF IT
;* IS WITHIN THE LIMIT, CONTROL LOOPS BACK TO THE COMMAND
;* FOLLOWING THE 'FOR'.  IFF OUTSIDE THE LIMIT, THE SAVE ARER
;* IS PURGED AND EXECUTION CONTINUES.
;*
FOR    CALL PUSHA     ;SAVE THE OLD SAVE AREA
       CALL SETVAL    ;SET THE CONTROL VAR.
       DCX  H         ;HL IS ITS ADDRESS
       SHLD LOPVAR    ;SAVE THAT
       LXI  H,TAB5-1  ;USE 'EXEC' TO LOOK
       JMP  EXEC      ;FOR THE WORD 'TO'
FR1    RST  3         ;EVALUATE THE LIMIT
       SHLD LOPLMT    ;SAVE THAT
       LXI  H,TAB6-1  ;USE 'EXEC' TO LOOK
       JMP  EXEC      ;FOR THE WORD 'STEP'
FR2    RST  3         ;FOUND IT, GET STEP
       JMP  FR4
FR3    LXI  H,1       ;NOT FOUND, SET TO 1
FR4    SHLD LOPINC    ;SAVE THAT TOO
FR5    LHLD CURRNT    ;SAVE CURRENT LINE #
       SHLD LOPLN
       XCHG           ;AND TEXT POINTER
       SHLD LOPPT
       LXI  B,10      ;DIG INTO STACK TO
       LHLD LOPVAR    ;FIND 'LOPVAR'
       XCHG
       MOV  H,B
       MOV  L,B       ;HL=0 NOW
       DAD  SP        ;HERE IS THE STACK
      .byte   0o76
FR7    DAD  B         ;EACH LEVEL IS 10 DEEP
       MOV  A,M       ;GET THAT OLD 'LOPVAR'
       INX  H
       ORA  M
       JZ   FR8       ;0 SAYS NO MORE IN IT
       MOV  A,M
       DCX  H
       CMP  D         ;SAME AS THIS ONE?
       JNZ  FR7
       MOV  A,M       ;THE OTHER HALF?
       CMP  E
       JNZ  FR7
       XCHG           ;YES, FOUND ONE
       LXI  H,0o0
       DAD  SP        ;TRY TO MOVE SP
       MOV  B,H
       MOV  C,L
       LXI  H,0o12
       DAD  D
       CALL MVDOWN    ;AND PURGE 10 WORDS
       SPHL           ;IN THE STACK
FR8    LHLD LOPPT     ;JOB DONE, RESTORE DE
       XCHG
       RST  6         ;AND CONTINUE
;*
NEXT   RST  7         ;GET ADDRESS OF VAR.
       JC   QWHAT     ;NO VARIABLE, "WHAT?"
       SHLD VARNXT    ;YES, SAVE IT
NX0    PUSH D         ;SAVE TEXT POINTER
       XCHG
       LHLD LOPVAR    ;GET VAR. IN 'FOR'
       MOV  A,H
       ORA  L         ;0 SAYS NEVER HAD ONE
       JZ   AWHAT     ;SO WE ASK: "WHAT?"
       RST  4         ;ELSE WE CHECK THEM
       JZ   NX3       ;OK, THEY AGREE
       POP  D         ;NO, LET'S SEE
       CALL POPA      ;PURGE CURRENT LOOP
       LHLD VARNXT    ;AND POP ONE LEVEL
       JMP  NX0       ;GO CHECK AGAIN
NX3    MOV  E,M       ;COME HERE WHEN AGREED
       INX  H
       MOV  D,M       ;DE=VALUE OF VAR.
       LHLD LOPINC
       PUSH H
       DAD  D         ;ADD ONE STEP
       XCHG
       LHLD LOPVAR    ;PUT IT BACK
       MOV  M,E
       INX  H
       MOV  M,D
       LHLD LOPLMT    ;HL->LIMIT
       POP  PSW       ;OLD HL
       ORA  A
       JP   NX1       ;STEP > 0
       XCHG
NX1    CALL CKHLDE    ;COMPARE WITH LIMIT
       POP  D         ;RESTORE TEXT POINTER
       JC   NX2       ;OUTSIDE LIMIT
       LHLD LOPLN     ;WITHIN LIMIT, GO
       SHLD CURRNT    ;BACK TO THE SAVED
       LHLD LOPPT     ;'CURRNT' AND TEXT
       XCHG           ;POINTER
       RST  6
NX2    CALL POPA      ;PURGE THIS LOOP
       RST  6
;*
;**************************************************************
;*
;* *** REM *** IFF *** INPUT *** & LET (& DEFLT) ***
;*
;* 'REM' CAN BE FOLLOWED BY ANYTHING AND IS IGNORED BY TBI.
;* TBI TREATS IT LIKE AN 'IF' WITH A FALSE CONDITION.
;*
;* 'IF' IS FOLLOWED BY AN EXPR. AS A CONDITION AND ONE OR MORE
;* COMMANDS (INCLUDING OUTHER 'IF'S) SEPERATED BY SEMI-COLONS.
;* NOTE THAT THE WORD 'THEN' IS NOT USED.  TBI EVALUATES THE
;* EXPR. IFF IT IS NON-ZERO, EXECUTION CONTINUES.  IFF THE
;* EXPR. IS ZERO, THE COMMANDS THAT FOLLOWS ARE IGNORED AND
;* EXECUTION CONTINUES AT THE NEXT LINE.
;*
;* 'IPUT' COMMAND IS LIKE THE 'PRINT' COMMAND, AND IS FOLLOWED
;* BY A LIST OF ITEMS.  IFF THE ITEM IS A STRING IN SINGLE OR
;* DOUBLE QUOTES, OR IS A BACK-ARROW, IT HAS THE SAME EFFECT AS
;* IN 'PRINT'.  IFF AN ITEM IS A VARIABLE, THIS VARIABLE NAME IS
;* PRINTED OUT FOLLOWED BY A COLON.  THEN TBI WAITS FOR AN
;* EXPR. TO BE TYPED IN.  THE VARIABLE ISs THEN SET TO THE
;* VALUE OF THIS EXPR.  IFF THE VARIABLE IS PROCEDED BY A STRING
;* (AGAIN IN SINGLE OR DOUBLE QUOTES), THE STRING WILL BE
;* PRINTED FOLLOWED BY A COLON.  TBI THEN WAITS FOR INPUT EXPR.
;* AND SET THE VARIABLE TO THE VALUE OF THE EXPR.
;*
;* IFF THE INPUT EXPR. IS INVALID, TBI WILL PRINT "WHAT?",
;* "HOW?" OR "SORRY" AND REPRINT THE PROMPT AND REDO THE INPUT.
;* THE EXECUTION WILL NOT TERMINATE UNLESS YOU TYPE CONTROL-C.
;* THIS IS HANDLED IN 'INPERR'.
;*
;* 'LET' IS FOLLOWED BY A LIST OF ITEMS SEPERATED BY COMMAS.
;* EACH ITEM CONSISTS OF A VARIABLE, AN EQUAL SIGN, AND AN EXPR.
;* TBI EVALUATES THE EXPR. AND SET THE VARIBLE TO THAT VALUE.
;* TB WILL ALSO HANDLE 'LET' COMMAND WITHOUT THE WORD 'LET'.
;* THIS IS DONE BY 'DEFLT'.
;*
REM    LXI  H,0o0     ;*** REM ***
      .byte   0o76
;*
IFF    RST  3         ;*** IFF ***
       MOV  A,H       ;IS THE EXPR.=0?
       ORA  L
       JNZ  RUNSML    ;NO, CONTINUE
       CALL FNDSKP    ;YES, SKIP REST OF LINE
       JNC  RUNTSL
       JMP  RSTART
;*
INPERR LHLD STKINP    ;*** INPERR ***
       SPHL           ;RESTORE OLD SP
       POP  H         ;AND OLD 'CURRNT'
       SHLD CURRNT
       POP  D         ;AND OLD TEXT POINTER
       POP  D         ;REDO INPUT
;*
INPUT  =    *         ;*** INPUT ***
IP1    PUSH D         ;SAVE IN CASE OF ERROR
       CALL QTSTG     ;IS NEXT ITEM A STRING?
       JMP  IP2       ;NO
       RST  7         ;YES. BUT FOLLOWED BY A
       JC   IP4       ;VARIABLE?   NO.
       JMP  IP3       ;YES.  INPUT VARIABLE
IP2    PUSH D         ;SAVE FOR 'PRTSTG'
       RST  7         ;MUST BE VARIABLE NOW
       JC   QWHAT     ;"WHAT?" IT IS NOT?
       LDAX D         ;GET READY FOR 'RTSTG'
       MOV  C,A
       SUB  A
       STAX D
       POP  D
       CALL PRTSTG    ;PRINT STRING AS PROMPT
       MOV  A,C       ;RESTORE TEXT
       DCX  D
       STAX D
IP3    PUSH D         ;SAVE IN CASE OF ERROR
       XCHG
       LHLD CURRNT    ;ALSO SAVE 'CURRNT'
       PUSH H
       LXI  H,IP1     ;A NEGATIVE NUMBER
       SHLD CURRNT    ;AS A FLAG
       LXI  H,0        ;SAVE SP TOO
       DAD  SP
       SHLD STKINP
       PUSH D         ;OLD HL
       MVI  A,0o72     ;PRINT THIS TOO
       CALL GETLN     ;AND GET A LINE
IP3A   LXI  D,BUFFER  ;POINTS TO BUFFER
       RST  3         ;EVALUATE INPUT
       NOP            ;CAN BE 'CALL ENDCHK'
       NOP
       NOP
       POP  D         ;OK, GET OLD HL
       XCHG
       MOV  M,E       ;SAVE VALUE IN VAR.
       INX  H
       MOV  M,D
       POP  H         ;GET OLD 'CURRNT'
       SHLD CURRNT
       POP  D         ;AND OLD TEXT POINTER
IP4    POP  PSW       ;PURGE JUNK IN STACK
       RST  1         ;IS NEXT CH. ','?
      .byte   ','
      .byte   3
       JMP  IP1       ;YES, MORE ITEMS.
IP5    RST  6
;*
DEFLT  LDAX D         ;*** DEFLT ***
       CPI  $0D       ;EMPTY LINE IS OK
       JZ   LT1       ;ELSE IT IS 'LET'
;*
LET    CALL SETVAL    ;*** LET ***
       RST  1         ;SET VALUE TO VAR.
      .byte   ','
      .byte   3
       JMP  LET       ;ITEM BY ITEM
LT1    RST  6         ;UNTIL FINISH
;*
RND    CALL PARN      ;*** RND(EXPR) ***
       MOV  A,H       ;EXPR MUST BE +
       ORA  A
       JM   QHOW
       ORA  L         ;AND NON-ZERO
       JZ   QHOW
       PUSH D         ;SAVE BOTH
       PUSH H
       LHLD RANPNT    ;GET MEMORY AS RANDOM
       LXI  D,LSTROM  ;NUMBER
       RST  4
       JC   RA1       ;WRAP AROUND IFF LAST
       LXI  H,START
RA1    MOV  E,M
       INX  H
       MOV  D,M
       SHLD RANPNT
       POP  H
       XCHG
       PUSH B
       CALL DIVIDE    ;RND(N)=MOD(M,N)+1
       POP  B
       POP  D
       INX  H
       RET
;*
ABS    CALL PARN      ;*** ABS(EXPR) ***
       CALL CHKSGN    ;CHECK SIGN
       MOV  A,H       ;NOTE THAT -32768
       ORA  H         ;CANNOT CHANGE SIGN
       JM   QHOW      ;SO SAY: "HOW?"
       RET
SIZE   LHLD TXTUNF    ;*** SIZE ***
       PUSH D         ;GET THE NUMBER OF FREE
       XCHG           ;BYTES BETWEEN 'TXTUNF'
SIZEA  LXI  H,VARBGN  ;AND 'VARBGN'
       CALL SUBDE
       POP  D
       RET
;*
;*********************************************************
;*
;*   *** OUT *** INP *** WAIT *** POKE *** PEEK *** & USR
;*
;*  OUT I,J(,K,L)
;*
;*  OUTPUTS EXPRESSION 'J' TO PORT 'I', AND MAY BE REPEATED
;*  AS IN DATA 'L' TO PORT 'K' AS MANY TIMES AS NEEDED
;*  THIS COMMAND MODIFIES ;*  THIS COMMAND MODIFIES
;*  THIS COMMAND MODIFY'S A SMALL SECTION OF CODE LOCATED
;*  JUST ABOVE ADDRESS 2K
;*
;*  INP (I)
;*
;*  THIS FUNCTION RETURNS DATA READ FROM INPUT PORT 'I' AS
;*  IT'S VALUE.
;*  IT ALSO MODIFIES CODE JUST ABOVE 2K.
;*
;*  WAIT I,J,K
;*
;*  THIS COMMAND READS THE STATUS OF PORT 'I', EXCLUSIVE OR'S
;*  THE RESULT WITH 'K' IF THERE IS ONE, OR IF NOT WITH 0,
;*  AND'S WITH 'J' AND RETURNS WHEN THE RESULT IS NONZERO.
;*  ITS MODIFIED CODE IS ALSO ABOVE 2K.
;*
;*  POKE I,J(,K,L)
;*
;*  THIS COMMAND WORKS LIKE OUT EXCEPT THAT IT PUTS DATA 'J'
;*  INTO MEMORY LOCATION 'I'.
;*
;*  PEEK (I)
;*
;*  THIS FUNCTION WORKS LIKE INP EXCEPT IT GETS IT'S VALUE
;*  FROM MEMORY LOCATION 'I'.
;*
;*  USR (I(,J))
;*
;*  USR CALLS A MACHINE LANGUAGE SUBROUTINE AT LOCATION 'I'
;*  IF THE OPTIONAL PARAMETER 'J' IS USED ITS VALUE IS PASSED
;*  IN H&L.  THE VALUE OF THE FUNCTION SHOULD BE RETURNED IN H&L.
;*
;************************************************************
;*
OUTCMD RST  3
       MOV  A,L
       STA  OUTIO + 1
       RST  1
      .byte   ','
      .byte   $2F
       RST  3
       MOV  A,L
       CALL OUTIO
       RST  1
      .byte   ','
      .byte   $03
       JMP  OUTCMD
       RST  6
WAITCM RST  3
       MOV  A,L
       STA  WAITIO + 1
       RST  1
      .byte   ','
      .byte   $1B
       RST  3
       PUSH H
       RST  1
      .byte   ','
      .byte   $7
       RST  3
       MOV  A,L
       POP  H
       MOV  H,A
       JMP  * + 2
       MVI  H,0
       JMP  WAITIO
INP    CALL PARN
       MOV  A,L
       STA  INPIO + 1
       MVI  H,0
       JMP  INPIO
       JMP  QWHAT
POKE   RST  3
       PUSH H
       RST  1
      .byte   ','
      .byte   $12
       RST  3
       MOV  A,L
       POP  H
       MOV  M,A
       RST  1
      .byte   ',',$03
       JMP  POKE
       RST 6
PEEK   CALL PARN
       MOV  L,M
       MVI  H,0
       RET
       JMP  QWHAT
USR    PUSH B
       RST  1
      .byte   '(',28    ;QWHAT
       RST  3          ;EXPR
       RST  1
      .byte   ')',7      ;PASPARM
       PUSH D
       LXI  D,USRET
       PUSH D
       PUSH H
       RET             ;CALL USR ROUTINE
PASPRM RST  1
      .byte   ',',14
       PUSH H
       RST  3
       RST  1
      .byte   ')',9
       POP  B
       PUSH D
       LXI  D,USRET
       PUSH D
       PUSH B
       RET             ;CALL USR ROUTINE
USRET  POP  D
       POP  B
       RET
       JMP  QWHAT
;*
;**************************************************************
;*
;* *** DIVIDE *** SUBDE *** CHKSGN *** CHGSGN *** & CKHLDE ***
;*
;* 'DIVIDE' DIVIDES HL BY DE, RESULT IN BC, REMAINDER IN HL
;*
;* 'SUBDE' SUBTRACTS DE FROM HL
;*
;* 'CHKSGN' CHECKS SIGN OF HL.  IFF +, NO CHANGE.  IFF -, CHANGE
;* SIGN AND FLIP SIGN OF B.
;*
;* 'CHGSGN' CHNGES SIGN OF HL AND B UNCONDITIONALLY.
;*
;* 'CKHLE' CHECKS SIGN OF HL AND DE.  IFF DIFFERENT, HL AND DE
;* ARE INTERCHANGED.  IFF SAME SIGN, NOT INTERCHANGED.  EITHER
;* CASE, HL DE ARE THEN COMPARED TO SET THE FLAGS.
;*
DIVIDE PUSH H         ;*** DIVIDE ***
       MOV  L,H       ;DIVIDE H BY DE
       MVI  H,0
       CALL DV1
       MOV  B,C       ;SAVE RESULT IN B
       MOV  A,L       ;(REMAINDER+L)/DE
       POP  H
       MOV  H,A
DV1    MVI  C,$ff     ;RESULT IN C
DV2    INR  C         ;DUMB ROUTINE
       CALL SUBDE     ;DIVIDE BY SUBTRACT
       JNC  DV2       ;AND COUNT
       DAD  D
       RET
;*
SUBDE  MOV  A,L       ;*** SUBDE ***
       SUB  E         ;SUBTRACT DE FROM
       MOV  L,A       ;HL
       MOV  A,H
       SBB  D
       MOV  H,A
       RET
;*
CHKSGN MOV  A,H       ;*** CHKSGN ***
       ORA  A         ;CHECK SIGN OF HL
       RP             ;IFF -, CHANGE SIGN
;*
CHGSGN MOV  A,H       ;*** CHGSGN ***
       CMA            ;CHANGE SIGN OF HL
       MOV  H,A
       MOV  A,L
       CMA
       MOV  L,A
       INX  H
       MOV  A,B       ;AND ALSO FLIP B
       XRI  0o200
       MOV  B,A
       RET
;*
CKHLDE MOV  A,H
       XRA  D         ;SAME SIGN?
       JP   CK1       ;YES, COMPARE
       XCHG           ;NO, XCH & COMP
CK1    RST  4
       RET
;*
;**************************************************************
;*
;* *** SETVAL *** FIN *** ENDCHK *** & ERROR (& FRIENDS) ***
;*
;* "SETVAL" EXPECTS A VARIABLE, FOLLOWED BY AN EQUAL SIGN AND
;* THEN AN EXPR.  IT EVALUATES THE EXPR. AND SET THE VARIABLE
;* TO THAT VALUE.
;*
;* "FIN" CHECKS THE END OF A COMMAND.  IFF IT ENDED WITH ";",
;* EXECUTION CONTINUES.  IFF IT ENDED WITH A CR, IT FINDS THE
;* NEXT LINE AND CONTINUE FROM THERE.
;*
;* "ENDCHK" CHECKS IFF A COMMAND IS ENDED WITH CR.  THIS IS
;* REQUIRED IN CERTAIN COMMANDS. (GOTO, RETURN, AND STOP ETC.)
;*
;* "ERROR" PRINTS THE STRING POINTED BY DE (AND ENDS WITH CR).
;* IT THEN PRINTS THE LINE POINTED BY 'CURRNT' WITH A "?"
;* INSERTED AT WHERE THE OLD TEXT POINTER (SHOULD BE ON TOP
;* O THE STACK) POINTS TO.  EXECUTION OF TB IS STOPPED
;* AND TBI IS RESTARTED.  HOWEVER, IFF 'CURRNT' -> ZERO
;* (INDICATING A DIRECT COMMAND), THE DIRECT COMMAND IS NOT
;*  PRINTED.  AND IFF 'CURRNT' -> NEGATIVE # (INDICATING 'INPUT'
;* COMMAND, THE INPUT LINE IS NOT PRINTED AND EXECUTION IS
;* NOT TERMINATED BUT CONTINUED AT 'INPERR'.
;*
;* RELATED TO 'ERROR' ARE THE FOLLOWING:
;* 'QWHAT' SAVES TEXT POINTER IN STACK AND GET MESSAGE "WHAT?"
;* 'AWHAT' JUST GET MESSAGE "WHAT?" AND JUMP TO 'ERROR'.
;* 'QSORRY' AND 'ASORRY' DO SAME KIND OF THING.
;* 'QHOW' AND 'AHOW' IN THE ZERO PAGE SECTION ALSO DO THIS
;*
SETVAL RST  7         ;*** SETVAL ***
       JC   QWHAT     ;"WHAT?" NO VARIABLE
       PUSH H         ;SAVE ADDRESS OF VAR.
       RST  1         ;PASS "=" SIGN
      .byte   '='
      .byte   0o10
       RST  3         ;EVALUATE EXPR.
       MOV  B,H       ;VALUE IN BC NOW
       MOV  C,L
       POP  H         ;GET ADDRESS
       MOV  M,C       ;SAVE VALUE
       INX  H
       MOV  M,B
       RET
SV1    JMP  QWHAT     ;NO "=" SIGN
;*
FIN    RST  1         ;*** FIN ***
      .byte   0o73
      .byte   4
       POP  PSW       ;";", PURGE RET ADDR.
       JMP  RUNSML    ;CONTINUE SAME LINE
FI1    RST  1         ;NOT ";", IS IT CR?
      .byte   '\r'
      .byte   4
       POP  PSW       ;YES, PURGE RET ADDR.
       JMP  RUNNXL    ;RUN NEXT LINE
FI2    RET            ;ELSE RETURN TO CALLER
;*
ENDCHK RST  5         ;*** ENDCHK ***
       CPI  13        ;END WITH CR?
       RZ             ;OK, ELSE SAY: "WHAT?"
;*
QWHAT  PUSH D         ;*** QWHAT ***
AWHAT  LXI  D,WHAT    ;*** AWHAT ***
ERROR  SUB  A         ;*** ERROR ***
       CALL PRTSTG    ;PRINT 'WHAT?', 'HOW?'
       POP  D         ;OR 'SORRY'
       LDAX D         ;SAVE THE CHARACTER
       PUSH PSW       ;AT WHERE OLD DE ->
       SUB  A         ;AND PUT A 0 THERE
       STAX D
       LHLD CURRNT    ;GET CURRENT LINE #
       PUSH H
       MOV  A,M       ;CHECK THE VALUE
       INX  H
       ORA  M
       POP  D
       JZ   RSTART    ;IFF ZERO, JUST RERSTART
       MOV  A,M       ;IFF NEGATIVE,
       ORA  A
       JM   INPERR    ;REDO INPUT
       CALL PRTLN     ;ELSE PRINT THE LINE
       DCX  D         ;UPTO WHERE THE 0 IS
       POP  PSW       ;RESTORE THE CHARACTER
       STAX D
       MVI  A,'?'     ;PRINTt A "?"
       RST  2
       SUB  A         ;AND THE REST OF THE
       CALL PRTSTG    ;LINE
       JMP  RSTART
QSORRY PUSH D         ;*** QSORRY ***
ASORRY LXI  D,SORRY   ;*** ASORRY ***
       JMP  ERROR
;*
;**************************************************************
;*
;* *** GETLN *** FNDLN (& FRIENDS) ***
;*
;* 'GETLN' READS A INPUT LINE INTO 'BUFFER'.  IT FIRST PROMPT
;* THE CHARACTER IN A (GIVEN BY THE CALLER), THEN IT FILLS THE
;* THE BUFFER AND ECHOS.  IT IGNORES LF'S AND NULLS, BUT STILL
;* ECHOS THEM BACK.  RUB-OUT IS USED TO CAUSE IT TO DELETE
;* THE LAST CHARATER (IFF THERE IS ONE), AND ALT-MOD IS USED TO
;* CAUSE IT TO DELETE THE WHOLE LINE AND START IT ALL OVER.
;* 0DHSIGNALS THE END OF A LINE, AND CAUE 'GETLN' TO RETURN.
;*
;* 'FNDLN' FINDS A LINE WITH A GIVEN LINE # (IN HL) IN THE
;* TEXT SAVE AREA.  DE IS USED AS THE TEXT POINTER.  IFF THE
;* LINE IS FOUND, DE WILL POINT TO THE BEGINNING OF THAT LINE
;* (I.E., THE LOW BYTE OF THE LINE #), AND FLAGS ARE NC & Z.
;* IFF THAT LINE IS NOT THERE AND A LINE WITH A HIGHER LINE #
;* IS FOUND, DE POINTS TO THERE AND FLAGS ARE NC & NZ.  IFF
;* WE REACHED THE END OF TEXT SAVE ARE AND CANNOT FIND THE
;* LINE, FLAGS ARE C & NZ.
;* 'FNDLN' WILL INITIALIZE DE TO THE BEGINNING OF THE TEXT SAVE
;* AREA TO START THE SEARCH.  SOME OTHER ENTRIES OF THIS
;* ROUTINE WILL NOT INITIALIZE DE AND DO THE SEARCH.
;* 'FNDLNP' WILL START WITH DE AND SEARCH FOR THE LINE #.
;* 'FNDNXT' WILL BUMP DE BY 2, FIND A 0DHAND THEN START SEARCH.
;* 'FNDSKP' USE DE TO FIND A CR, AND THEN STRART SEARCH.
;*
GETLN  RST  2         ;*** GETLN ***
       LXI  D,BUFFER  ;PROMPT AND INIT
GL1    CALL CHKIO     ;CHECK KEYBOARD
       JZ   GL1       ;NO INPUT, WAIT
       CPI  '?'       ;DELETE LST CHARACTER?
       JZ   GL3       ;YES
       CPI  10        ;IGNORE LF
       JZ   GL1
       ORA  A         ;IGNORE NULL
       JZ   GL1
       CPI  0o134     ;DELETE THE WHOLE LINE?
       JZ   GL4       ;YES
       STAX D         ;ELSE, SAVE INPUT
       INX  D         ;AND BUMP POINTER
       CPI  13        ;WAS IT CR?
       JNZ  GL2       ;NO
       MVI  A,10      ;YES, GET LINE FEED
       RST  2         ;CALL OUTC & LINE FEED
       RET            ;WE'VE GOT A LINE
GL2    MOV  A,E       ;MORE FREE ROOM?
       CPI  >BUFEND
       JNZ  GL1       ;YES, GET NEXT INPUT
GL3    MOV  A,E       ;DELETE LAST CHARACTER
       CPI  >BUFFER   ;BUT DO WE HAVE ANY?
       JZ   GL4       ;NO, REDO WHOLE LINE
       DCX  D         ;YES, BACKUP POINTER
       MVI  A,'_'     ;AND ECHO A BACK-SPACE
       RST  2
       JMP  GL1       ;GO GET NEXT INPUT
GL4    CALL CRLF      ;REDO ENTIRE LINE
       MVI  A,0o136    ;CR, LF & UP-ARROW
       JMP  GETLN
;*
FNDLN  MOV  A,H       ;*** FNDLN ***
       ORA  A         ;CHECK SIGN OF HL
       JM   QHOW      ;IT CANNT BE -
       LXI  D,TXTBGN  ;INIT. TEXT POINTER
;*
FNDLNP =    *         ;*** FNDLNP ***
FL1    PUSH H         ;SAVE LINE #
       LHLD TXTUNF    ;CHECK IFF WE PASSED END
       DCX  H
       RST  4
       POP  H         ;GET LINE # BACK
       RC             ;C,NZ PASSED END
       LDAX D         ;WE DID NOT, GET BYTE 1
       SUB  L         ;IS THIS THE LINE?
       MOV  B,A       ;COMPARE LOW ORDER
       INX  D
       LDAX D         ;GET BYTE 2
       SBB  H         ;COMPARE HIGH ORDER
       JC   FL2       ;NO, NOT THERE YET
       DCX  D         ;ELSE WE EITHER FOUND
       ORA  B         ;IT, OR IT IS NOT THERE
       RET            ;NC,Z:FOUND; NC,NZ:NO
;*
FNDNXT =    *         ;*** FNDNXT ***
       INX  D         ;FIND NEXT LINE
FL2    INX  D         ;JUST PASSED BYTE 1 & 2
;*
FNDSKP LDAX D         ;*** FNDSKP ***
       CPI  13        ;TRY TO FIND 0DH
       JNZ  FL2       ;KEEP LOOKING
       INX  D         ;FOUND CR, SKIP OVER
       JMP  FL1       ;CHECK IFF END OF TEXT
;*
;*************************************************************
;*
;* *** PRTSTG *** QTSTG *** PRTNUM *** & PRTLN ***
;*
;* 'PRTSTG' PRINTS A STRING POINTED BY DE.  IT STOPS PRINTING
;* AND RETURNS TO CAL ER WHEN EITHER A 0DHIS PRINTED OR WHEN
;* THE NEXT BYTE IS THE SAME AS WHAT WAS IN A (GIVEN BY THE
;* CALLER).  OLD A IS STORED IN B, OLD B IS LOST.
;*
;* 'QTSTG' LOOKS FOR A BACK-ARROW, SINGLE QUOTE, OR DOUBLE
;* QUOTE.  IFF NONE OF THESE, RETURN TO CALLER.  IFF BACK-ARROW,
;* OUTPUT A 0DHWITHOUT A LF.  IFF SINGLE OR DOUBLE QUOTE, PRINT
;* THE STRING IN THE QUOTE AND DEMANDS A MATCHING UNQUOTE.
;* AFTER THE PRINTING THE NEXT 3 BYTES OF THE CALLER IS SKIPPED
;* OVER (USUALLY A JUMP INSTRUCTION).
;*
;* 'PRTNUM' PRINTS THE NUMBER IN HL.  LEADING BLANKS ARE ADDED
;* IFF NEEDED TO PAD THE NUMBER OF SPACES TO THE NUMBER IN C.
;* HOWEVER, IFF THE NUMBER OF DIGITS IS LARGER THAN THE # IN
;* C, ALL DIGITS ARE PRINTED ANYWAY.  NEGATIVE SIGN IS ALSO
;* PRINTED AND COUNTED IN, POSITIVE SIGN IS NOT.
;*
;* 'PRTLN' PRINSrA SAVED TEXT LINE WITH LINE # AND ALL.
;*
PRTSTG MOV  B,A       ;*** PRTSTG ***
PS1    LDAX D         ;GET A CHARACTERr
       INX  D         ;BUMP POINTER
       CMP  B         ;SAME AS OLD A?
       RZ             ;YES, RETURN
       RST  2         ;ELSE PRINT IT
       CPI  13        ;WAS IT A CR?
       JNZ  PS1       ;NO, NEXT
       RET            ;YES, RETURN
;*
QTSTG  RST  1         ;*** QTSTG ***
      .byte   '"'
      .byte   0o17
       MVI  A,0o42    ;IT IS A "
QT1    CALL PRTSTG    ;PRINT UNTIL ANOTHER
       CPI  13        ;WAS LAST ONE A CR?
       POP  H         ;RETURN ADDRESS
       JZ   RUNNXL    ;WAS CR, RUN NEXT LINE
QT2    INX  H         ;SKIP 3 BYTES ON RETURN
       INX  H
       INX  H
       PCHL           ;RETURN
QT3    RST  1         ;IS IT A ' ?
      .byte   '\''
      .byte   5
       MVI  A,'\''    ;YES, DO SAME
       JMP  QT1       ;AS IN "
QT4    RST  1         ;IS IT BACK-ARROW?
      .byte   0o137
      .byte   0o10
       MVI  A,0o215   ;YES, 0DH WITHOUT LF!!
       RST  2         ;DO IT TWICE TO GIVE
       RST  2         ;TTY ENOUGH TIME
       POP  H         ;RETURN ADDRESS
       JMP  QT2
QT5    RET            ;NONE OF ABOVE
;*
PRTNUM PUSH D         ;*** PRTNUM ***
       LXI  D,0o12    ;DECIMAL
       PUSH D         ;SAVE AS A FLAG
       MOV  B,D       ;B=SIGN
       DCR  C         ;C=SPACES
       CALL CHKSGN    ;CHECK SIGN
       JP   PN1       ;NO SIGN
       MVI  B,'='     ;B=SIGN
       DCR  C         ;'-' TAKES SPACE
PN1    PUSH B         ;SAVE SIGN & SPACE
PN2    CALL DIVIDE    ;DEVIDE HL BY 10
       MOV  A,B       ;RESULT 0?
       ORA  C
       JZ   PN3       ;YES, WE GOT ALL
       XTHL           ;NO, SAVE REMAINDER
       DCR  L         ;AND COUNT SPACE
       PUSH H         ;HL IS OLD BC
       MOV  H,B       ;MOVE RESULT TO BC
       MOV  L,C
       JMP  PN2       ;AND DIVIDE BY 10
PN3    POP  B         ;WE GOT ALL DIGITS IN
PN4    DCR  C         ;THE STACK
       MOV  A,C       ;LOOK AT SPACE COUNT
       ORA  A
       JM   PN5       ;NO LEADING BLANKS
       MVI  A,' '     ;LEADING BLANKS
       RST  2
       JMP  PN4       ;MORE?
PN5    MOV  A,B       ;PRINT SIGN
       RST  2         ;MAYBE - OR NULL
       MOV  E,L       ;LAST REMAINDER IN E
PN6    MOV  A,E       ;CHECK DIGIT IN E
       CPI  10        ;10 IS FLAG FOR NO MORE
       POP  D
       RZ             ;IFF SO, RETURN
       ADI  0o60      ;ELSE CONVERT TO ASCII
       RST  2         ;AND PRINT THE DIGIT
       JMP  PN6       ;GO BACK FOR MORE
;*
PRTLN  LDAX D         ;*** PRTLN ***
       MOV  L,A       ;LOW ORDER LINE #
       INX  D
       LDAX D         ;HIGH ORDER
       MOV  H,A
       INX  D
       MVI  C,4       ;PRINT 4 DIGIT LINE #
       CALL PRTNUM
       MVI  A,' '     ;FOLLOWED BY A BLANK
       RST  2
       SUB  A         ;AND THEN THE TEXT
       CALL PRTSTG
       RET
;*
;**************************************************************
;*
;* *** MVUP *** MVDOWN *** POPA *** & PUSHA ***
;*
;* 'MVUP' MOVES A BLOCK UP FROM HERE DE-> TO WHERE BC-> UNTIL
;* DE = HL
;*
;* 'MVDOWN' MOVES A BLOCK DOWN FROM WHERE DE-> TO WHERE HL->
;* UNTIL DE = BC
;*
;* 'POPA' RESTORES THE 'FOR' LOOP VARIABLE SAVE AREA FROM THE
;* STACK
;*
;* 'PUSHA' STACKS THE 'FOR' LOOP VARIABLE SAVE AREA INTO THE
;* STACK
;*
MVUP   RST  4         ;*** MVUP ***
       RZ             ;DE = HL, RETURN
       LDAX D         ;GET ONE BYTE
       STAX B         ;MOVE IT
       INX  D         ;INCREASE BOTH POINTERS
       INX  B
       JMP  MVUP      ;UNTIL DONE
;*
MVDOWN MOV  A,B       ;*** MVDOWN ***
       SUB  D         ;TEST IFF DE = BC
       JNZ  MD1       ;NO, GO MOVE
       MOV  A,C       ;MAYBE, OTHER BYTE?
       SUB  E
       RZ             ;YES, RETURN
MD1    DCX  D         ;ELSE MOVE A BYTE
       DCX  H         ;BUT FIRST DECREASE
       LDAX D         ;BOTH POINTERS AND
       MOV  M,A       ;THEN DO IT
       JMP  MVDOWN    ;LOOP BACK
;*
POPA   POP  B         ;BC = RETURN ADDR.
       POP  H         ;RESTORE LOPVAR, BUT
       SHLD LOPVAR    ;=0 MEANS NO MORE
       MOV  A,H
       ORA  L
       JZ   PP1       ;YEP, GO RETURN
       POP  H         ;NOP, RESTORE OTHERS
       SHLD LOPINC
       POP  H
       SHLD LOPLMT
       POP  H
       SHLD LOPLN
       POP  H
       SHLD LOPPT
PP1    PUSH B         ;BC = RETURN ADDR.
       RET
;*
PUSHA  LXI  H,STKLMT  ;*** PUSHA ***
       CALL CHGSGN
       POP  B         ;BC=RETURN ADDRESS
       DAD  SP        ;IS STACK NEAR THE TOP?
       JNC  QSORRY    ;YES, SORRY FOR THAT.
       LHLD LOPVAR    ;ELSE SAVE LOOP VAR.S
       MOV  A,H       ;BUT IFF LOPVAR IS 0
       ORA  L         ;THAT WILL BE ALL
       JZ   PU1
       LHLD LOPPT     ;ELSE, MORE TO SAVE
       PUSH H
       LHLD LOPLN
       PUSH H
       LHLD LOPLMT
       PUSH H
       LHLD LOPINC
       PUSH H
       LHLD LOPVAR
PU1    PUSH H
       PUSH B         ;BC = RETURN ADDR.
       RET
;*
;**************************************************************
;*
;* *** OUTC *** & CHKIO ****!
;* THESE ARE THE ONLY I/O ROUTINES IN TBI.
;* 'OUTC' IS CONTROLLED BY A SOFTWARE SWITCH 'OCSW'.  IFF OCSW=0
;* 'OUTC' WILL JUST RETURN TO THE CALLER.  IFF OCSW IS NOT 0,
;* IT WILL OUTPUT THE BYTE IN A.  IFF THAT IS A CR, A LF IS ALSO
;* SEND OUT.  ONLY THE FLAGS MAY BE CHANGED AT RETURN, ALL REG.
;* ARE RESTORED.
;*
;* 'CHKIO' CHECKS THE INPUT.  IFF NO INPUT, IT WILL RETURN TO
;* THE CALLER WITH THE Z FLAG SET.  IFF THERE IS INPUT, Z FLAG
;* IS CLEARED AND THE INPUT BYTE IS IN A.  HOWERER, IFF THE
;* INPUT IS A CONTROL-O, THE 'OCSW' SWITCH IS COMPLIMENTED, AND
;* Z FLAG IS RETURNED.  IFF A CONTROL-C IS READ, 'CHKIO' WILL
;* RESTART TBI AND DO NOT RETURN TO THE CALLER.
;*
;*                 OUTC   PUSH AF        THIS IS AT LOC. 10
;*                        LD   A,OCSW    CHECK SOFTWARE SWITCH
;*                        IOR  A
OC2    JNZ  OC3       ;IT IS ON
       POP  PSW       ;IT IS OFF
       RET            ;RESTORE AF AND RETURN
OC3    POP  PSW       ;GET OLD A BACK
       PUSH B         ;SAVE B ON STACK
;       PUSH D         ;AND D
;       PUSH H         ;AND H TOO
       MOV  B,A
       PUSH B
       OUT  2
       CPI  13        ;WAS IT A 'CR'?
       JNZ  DONE      ;NO, DONE
       ;MVI  E,'\n'   ;GET LINEFEED
       ;MVI  C,2      ;AND CONOUT AGAIN
       ;CALL CPM      ;CALL CPM
       MVI  A,10
       OUT  2
DONE   POP B          ;GET CHARACTER BACK
       MOV  A,B
;IDONE  POP  H         ;GET H BACK
;       POP  D         ;AND D
IDONE  POP  B         ;AND B TOO
       RET            ;DONE AT LAST
CHKIO  PUSH B         ;SAVE B ON STACK
;       PUSH D         ;AND D
;       PUSH H         ;THEN H
       MOV  B,A
       PUSH B
       ;MVI  C,11      ;GET CONSTANT WORD
       ;CALL CPM       ;CALL THE BDOS
 CI0:  IN   2
       CPI  0         ;SET FLAGS
       JZ   DONE      ;IF NOT READY RESTORE AND RETURN
;CI1    MVI  C,1       ;GET CONIN WORD
;       CALL CPM       ;CALL THE BDOS
CI1    CPI  $0F       ;IS IT CONTROL-O?
       JNZ  CI2       ;NO, MORE CHECKING
       LDA  OCSW      ;CONTROL-O  FLIP OCSW
       CMA            ;ON TO OFF, OFF TO ON
       STA  OCSW      ;AND PUT IT BACK
       JMP  CI0       ;AND GET ANOTHER CHARACTER
CI2    CPI  3         ;IS IT CONTROL-C?
       POP  B         ;DISCARD SAVED A VALUE
       JNZ  IDONE     ;RETURN AND RESTORE IF NOT
       JMP  RSTART    ;YES, RESTART TBI
LSTROM =    *         ;ALL ABOVE CAN BE ROM
OUTIO  OUT  $FF
       RET
WAITIO IN   $FF
       XRA  H
       ANA  L
       JZ   WAITIO
       RST  6
INPIO  IN   $FF
       MOV  L,A
       RET
;OUTCAR .byte  0       ;OUTPUT CHAR. STORAGE
OCSW   .byte  $FF     ;SWITCH FOR OUTPUT
CURRNT .word  0       ;POINTS TO CURRENT LINE
STKGOS .word  0       ;SAVES SP IN 'GOSUB'
VARNXT .word  0       ;TEMPORARY STORAGE
STKINP .word  0       ;SAVES SP IN 'INPUT'
LOPVAR .word  0       ;'FOR' LOOP SAVE AREA
LOPINC .word  0       ;INCREMENT
LOPLMT .word  0       ;LIMIT
LOPLN  .word  0       ;LINE NUMBER
LOPPT  .word  0       ;TEXT POINTER
RANPNT .word START    ;RANDOM NUMBER POINTER
TXTUNF .word TXTBGN   ;->UNFILLED TEXT AREA
TXTBGN .storage 1     ;TEXT SAVE AREA BEGINS
MSG1  .byte   $7F,$7F,$7F,"SHERRY BROTHERS TINY BASIC VER. 3.1",$0D
INIT   MVI  A,$FF
       STA  OCSW      ;TURN ON OUTPUT SWITCH
       MVI  A,$0C     ;GET FORM FEED
       RST  2         ;SEND TO CRT
PATLOP SUB  A         ;CLEAR ACCUMULATOR
       LXI  D,MSG1    ;GET INIT MESSAGE
       CALL PRTSTG    ;SEND IT
LSTRAM LDA  7         ;GET FBASE FOR TOP
       STA  RSTART+2
       DCR  A         ;DECREMENT FOR OTHER POINTERS
       STA  SS1A+2    ;AND FIX THEM TOO
       STA  TV1A+2
       STA  ST3A+2
       STA  ST4A+2
       STA  IP3A+2
       STA  SIZEA+2
       STA  GETLN+3
       STA  PUSHA+2
       LXI  H,ST1     ;GET NEW START JUMP
       SHLD START+1   ;AND FIX IT
       JMP  ST1
;	RESTART TABLE
;	.org	$0A50
;RSTBL:
;       XTHL           ;*** TSTC OR RST 1 ***
;       RST  5         ;IGNORE BLANKS AND
;       CMP  M         ;TEST CHARACTER
;       JMP  TC1       ;REST OF THIS IS AT TC1
;;*
;CRLF:	=    $0E       ;EXECUTE TIME LOCATION OF THIS INSTRUCTION.
;	MVI  A,'\r'    ;*** CRLF ***
;;*
;       PUSH PSW       ;*** OUTC OR RST 2 ***
;       LDA  OCSW      ;PRINT CHARACTER ONLY
;       ORA  A         ;IFF OCSW SWITCH IS ON
;       JMP  OC2       ;REST OF THIS IS AT OC2
;;*
;       CALL EXPR2     ;*** EXPR OR RST 3 ***
;       PUSH H         ;EVALUATE AN EXPRESION
;       JMP  EXPR1     ;REST OF IT IS AT EXPR1
;      .byte   'W'
;;*
;       MOV  A,H       ;*** COMP OR RST 4 ***
;       CMP  D         ;COMPARE HL WITH DE
;       RNZ            ;RETURN CORRECT C AND
;       MOV  A,L       ;Z FLAGS
;       CMP  E         ;BUT OLD A IS LOST
;       RET
;      .byte   "AN"
;;*
;SS1:	=	$28	;EXECUTE TIME LOCATION OF THIS INSTRUCTION.
;       LDAX D         ;*** IGNBLK/RST 5 ***
;       CPI  ' '       ;IGNORE BLANKS
;       RNZ            ;IN TEXT (WHERE DE->)
;       INX  D         ;AND RETURN THE FIRST
;       JMP  SS1       ;NON-BLANK CHAR. IN A
;;*
;       POP  PSW       ;*** FINISH/RST 6 ***
;       CALL FIN       ;CHECK END OF COMMAND
;       JMP  QWHAT     ;PRINT "WHAT?" IFF WRONG
;      .byte   'G'
;;*
;       RST  5         ;*** TSTV OR RST 7 ***
;       SUI  0o100      ;TEST VARIABLES
;       RC             ;C:NOT A VARIABLE
;       JMP  TSTV1     ;JUMP AROUND RESERVED AREA

       .org   $0F00
TXTEND = *            ;TEXT SAVE AREA ENDS
VARBGN .storage   2*27      ;VARIABLE @(0)
       .storage   1         ;EXTRA BYTE FOR BUFFER
BUFFER .storage   80        ;INPUT BUFFER
BUFEND = *            ;BUFFER ENDS
       .storage   40        ;EXTRA BYTES FOR STACK
STKLMT =  *         ;TOP LIMIT FOR STACK
       .org  $2000
STACK  =  *         ;STACK STARTS HERE
       .end
*/
