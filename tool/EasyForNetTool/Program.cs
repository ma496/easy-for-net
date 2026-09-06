namespace EasyForNetTool;

using System.Reflection;
using EasyForNetTool.Generator;
using EasyForNetTool.Parsing;

/// <summary>
/// Main entry point for the EasyForNet CLI tool.
/// </summary>
internal class Program
{
    /// <summary>
    /// Processes command-line arguments, parses them and invokes the appropriate code generator.
    /// </summary>
    static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                Console.WriteLine($"EasyForNet Tool Version v{Helpers.GetVersion()}");
                Console.WriteLine("-------------");
                Console.WriteLine("\nUsage:");
                ShowHelp();
                return 0;
            }
            if (args.Length == 1 && (args[0] == "--help" || args[0] == "-h"))
            {
                Console.WriteLine("Usage:");
                ShowHelp();
                return 0;
            }
            if (args.Length == 2 && (args[1] == "--help" || args[1] == "-h"))
            {
                ShowHelp(args[0]);
                return 0;
            }
            if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
            {
                Console.WriteLine($"EasyForNet Tool Version v{Helpers.GetVersion()}");
                return 0;
            }

            var argument = new Parser().Parse(args);
            await new CodeGenerator().Generate(argument);
            return 0;
        }
        catch (UserFriendlyException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            if (!Helpers.IsProduction())
                Console.WriteLine($"{ex.Message}\n{ex.StackTrace}");
            else
                Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
        catch (TargetInvocationException ex)
        {
            if (ex.InnerException == null || !(ex.InnerException is UserFriendlyException))
                throw;
            Console.ForegroundColor = ConsoleColor.Red;
            if (!Helpers.IsProduction())
                Console.WriteLine($"{ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}");
            else
                Console.WriteLine($"Error: {ex.InnerException?.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    /// <summary>
    /// Displays usage information for the specified command or all available commands.
    /// </summary>
    static void ShowHelp(string? command = null)
    {
        Console.WriteLine("  use like this");
        Console.WriteLine("  efn {command} {options}");
        var arguments = ArgumentInfo.Arguments().Where(a => command == null || (a.Name == command || a.ShortName == command));
        foreach (var arg in arguments)
        {
            Console.WriteLine();
            Console.WriteLine("  command");
            Console.WriteLine($"  {arg.Name}, {arg.ShortName}, {arg.Description}");
            Console.WriteLine("  options");
            foreach (var opt in arg.Options.Where(x => !x.IsInternal))
            {
                Console.WriteLine($"    {opt.Name}, {opt.ShortName}, Required: {opt.Required}, {opt.Description}");
            }
        }
    }
}
