using System.Xml.Linq;
using Microsoft.VisualBasic.FileIO;

namespace NewPaloAltoTB;



/**
 *   Creating a Windows version of Palo Alto Tiny Basic
 */ 
internal class CommandShell() {

    internal bool Exiting = false;
    internal bool SuppressPrompt = false; //skip prompt during repeated line additions
    internal string CmdLine = "";
    internal ParserTools parser = ParserTools.Shared;
    internal Interpreter TBInterpreter = Interpreter.Shared;

    internal void Run(string[] _) { //args) {
        //to do: process args
        
        //banner
        Console.WriteLine("*** TINY BASIC DOT NET ***");
        Console.WriteLine("All lefts reserved.");

        //com loop
        while (!Exiting) {
            try {
                //prompt
                if (!SuppressPrompt) {
                    Console.Write("\nOK>\n");
                }
                CmdLine = Console.ReadLine() ?? "";
                parser.SetLine(CmdLine, 0, 0);
                //parse CmdLine: command, statement(s), or line editing
                parser.SkipSpaces();
                if (!(TryCommand() || TryEdits() || TryStatements())) {
                    throw new RuntimeException("WHAT?  The command or statement was not understood. Type HELP for command list.");
                }
            } catch (RuntimeException ex) {
                Console.WriteLine(ex.MessageDetail);
                //throw;
                SuppressPrompt = false;
            } catch (Exception ex) {
                Console.WriteLine(ex.Message);
                //throw;
                SuppressPrompt = false;
            }
        }
        Console.WriteLine("Bye!");
    }

    internal enum CmdEnum {
        List,
        Run,
        New,
        Load,
        Save,
        Bye,
        Quit,
        Exit,
        Delete,
        Kill
    }

    internal string[] CommandList = ["List","Run","New","Load","Save","Bye","Quit","Exit","Delete", "Kill"];

    internal bool TryCommand() {
        var rslt = false;
        var cmd = (CmdEnum?)parser.ScanStringTableEntry(CommandList);
        switch (cmd) {
            case CmdEnum.List:
                ListProgram();
                rslt = true;
                break;
            case CmdEnum.Run:
                RunProgram();
                rslt = true;
                break;
            case CmdEnum.New:
                TBInterpreter.ProgramSource.Clear();
                TBInterpreter.LineLocations.Clear();
                rslt = true;
                break;
            case CmdEnum.Load:
                break;
            case CmdEnum.Save:
                break;
            case CmdEnum.Bye:
            case CmdEnum.Quit:
            case CmdEnum.Exit:
                Exiting = true;
                rslt = true;
                break;
            case CmdEnum.Delete:
            case CmdEnum.Kill:
                DeleteLine();
                rslt = true;
                break;
            default:
                break;
        }
         if (rslt) {
            SuppressPrompt = false;
        }
        return rslt;
    }

    internal void DeleteLine() {
        short lineNum;
        parser.SkipSpaces();
        if (parser.ScanShort(out lineNum)) {
            if (parser.EoL()) {
                //delete line if found
                TBInterpreter.DeleteLine(lineNum);
                return;
            }
        }
        throw new RuntimeException("WHAT? Kill/Delete: target line number or range not understood.");
    }

    internal bool TryEdits() {
        short lineNum;
        if (parser.ScanShort(out lineNum)) {
            if (parser.EoL()) {
                //delete line if found
                TBInterpreter.DeleteLine(lineNum);
                SuppressPrompt = false;
            } else {
                //add or replace line of code
                _ = parser.ScanChar(' ');
                TBInterpreter.StoreLine(lineNum, parser.Line[parser.LinePosition..]);
                SuppressPrompt = true;
            }
            return true;
        }
        return false;
    }

    internal bool TryStatements() {
        TBInterpreter.ImmediateLine = parser.Line;
        TBInterpreter.Run(Immediate: true);
        return true;
    }

    internal void ListProgram() {
        var first = 1;
        var last = 0;
        var count = 0;

        foreach (var (linenum, src) in TBInterpreter.ProgramSource) { 
            Console.WriteLine($"{linenum,5:D} {src}");
        }

    }

    internal void RunProgram() {
        TBInterpreter.Run(Immediate: false);
    }
}