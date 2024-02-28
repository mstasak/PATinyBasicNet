using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Microsoft.VisualBasic.FileIO;

namespace NewPaloAltoTB;

/**
 *   Creating a Windows version of Palo Alto Tiny Basic
 */ 

/// <summary>
/// Run a command prompt, from which the user can edit lines of source code, issue commands
/// like Load, Save, Run, or execute immediate statements
/// </summary>
internal class CommandShell() {

    internal bool Exiting = false;
    internal bool SuppressPrompt = false; //skip prompt during repeated line additions
    internal string CommandLine = "";
    internal CodeParser Parser = CodeParser.Shared;
    internal CodeInterpreter Interpreter = CodeInterpreter.Shared;

    /// <summary>
    /// Runs a command loop, which prompts the user, accepts an input line,
    /// and tries to process it as a command, edit command, or statement(s)
    /// to be executed.
    /// </summary>
    /// <param name="_"></param>
    internal void RunCommandLoop(string[] _) { //args) {
        //to do: process args
        
        //banner
        Console.WriteLine("*** TINY BASIC DOT NET ***");
        Console.WriteLine("All wrongs and lefts reserved.");

        //com loop
        while (!Exiting) {
            try {
                //prompt
                if (!SuppressPrompt) {
                    Console.Write("\nOK>\n");
                }
                CommandLine = Console.ReadLine() ?? throw new RuntimeException("EOF on console input.");
                Parser.SetLine(line: CommandLine, lineNumber: 0, linePosition: 0);
                Parser.SkipSpaces();
                if (!(TryCommand() || TryEdit() || TryStatements())) {
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

    internal enum Commands {
        List,
        Run,
        New,
        Load,
        Save,
        Bye,
        Quit,
        Exit,
        Delete,
        Kill,
        Help,
        Dump
    }

    internal string[] CommandList = ["List","Run","New","Load","Save","Bye","Quit","Exit","Delete", "Kill", "Help", "Dump"];

    internal bool TryCommand() {
        var rslt = false;
        var cmd = (Commands?)Parser.ScanStringTableEntry(CommandList);
        switch (cmd) {
            case Commands.List:
                ListProgram();
                rslt = true;
                break;
            case Commands.Run:
                RunProgram();
                rslt = true;
                break;
            case Commands.New:
                Interpreter.ProgramSource.Clear();
                Interpreter.LineLocations.Clear();
                Variable.VariableStore.Clear();
                rslt = true;
                break;
            case Commands.Load:
                break;
            case Commands.Save:
                break;
            case Commands.Bye:
            case Commands.Quit:
            case Commands.Exit:
                Exiting = true;
                rslt = true;
                break;
            case Commands.Delete:
            case Commands.Kill:
                DeleteLine();
                rslt = true;
                break;
            case Commands.Help:
                //hmm - drill down by typing keywords?  Prev/Next browse 10-20 pages of static content?
                //try to present a picklist with console mouse clicking to nav?
                rslt = true;
                break;
            case Commands.Dump:
                DumpVariables();
                rslt = true;
                break;
            default:
                // rslt = false;
                break;
        }
         if (rslt) {
            SuppressPrompt = false;
        }
        return rslt;
    }

    internal void DumpVariables() {
        Console.WriteLine("Variables:");
        foreach (var (key, var) in Variable.VariableStore.OrderBy(e => e.Value.VType).ThenBy(e => e.Key)) {
            Console.WriteLine($"{key.PadRight(10)} = {var.ShortValue}");
        }
    }

    internal void DeleteLine() {
        int lineNum;
        Parser.SkipSpaces();
        if (Parser.ScanInt(out lineNum)) {
            if (Parser.EoL()) {
                //delete line if found
                Interpreter.DeleteLine(lineNum);
                return;
            }
        }
        throw new RuntimeException("WHAT? Kill/Delete: target line number or range not understood.");
    }

    internal bool TryEdit() {
        short lineNum;
        if (Parser.ScanShort(out lineNum)) {
            if (Parser.EoL()) {
                //delete line if found
                Interpreter.DeleteLine(lineNum);
                SuppressPrompt = false;
            } else {
                //add or replace line of code
                _ = Parser.ScanChar(' ');
                Interpreter.StoreLine(lineNum, Parser.Line[Parser.LinePosition..]);
                SuppressPrompt = true;
            }
            return true;
        }
        return false;
    }

    internal bool TryStatements() {
        Interpreter.ImmediateLine = Parser.Line;
        Interpreter.Run(Immediate: true);
        return true;
    }

    internal void ListProgram() {

        var count = 0;
        var listParams = Parser.ScanLineRange();
        foreach (var (linenum, src) in Interpreter.ProgramSource
            .Where (e => {
                return (e.linenum >= listParams.low || listParams.low <= 0) &&
                       (e.linenum <= listParams.high || listParams.high <= 0) &&
                       (count < listParams.lineCount || listParams.lineCount <= 0) &&
                       (listParams.search == "" || e.src.Contains(listParams.search,StringComparison.InvariantCultureIgnoreCase));
            })) { 
            Console.WriteLine($"{linenum,5:D} {src}");
            count++;
        }

    }

    internal void RunProgram() {
        Interpreter.Run(Immediate: false);
    }
}