using System;

namespace RevitGit.Harness
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            CliCommand command = null;
            try
            {
                command = CliParser.Parse(args);
                return (int)new HarnessApplication(Console.Out).Execute(command);
            }
            catch (CliUsageException exception)
            {
                Console.Error.WriteLine("ERROR: " + exception.Message);
                Console.Error.WriteLine("Run 'RevitGit.Harness.exe help' for usage.");
                return (int)ExitCode.InvalidArguments;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("RESULT: ERROR");
                Console.Error.WriteLine(FormatException(exception));
                if (command != null && command.Verbose) Console.Error.WriteLine(exception);
                return (int)ExitCode.InfrastructureFailure;
            }
        }

        private static string FormatException(Exception exception)
        {
            var result = exception.Message;
            while (exception.InnerException != null)
            {
                exception = exception.InnerException;
                result += " -> " + exception.Message;
            }
            return result;
        }
    }
}
