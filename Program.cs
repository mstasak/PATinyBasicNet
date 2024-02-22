/**
 *   Creating a Windows version of Palo Alto Tiny Basic
 */

// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");

namespace NewPaloAltoTB;

public class Program {
    public static int Main(string[] args) {
        CommandShell Shell = new();
        Shell.RunCommandLoop(args);
        return 0;
    }
}
