namespace Classroom;

// Entry point is intentionally outside the student's Program.cs.
internal static class Startup
{
    [System.Diagnostics.DebuggerNonUserCode]
    private static void Main()
    {
        try
        {
            ServerHost.Run(global::Program.HandleRequest);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("The server could not start: " + ex.Message);
            Environment.ExitCode = 1;
        }
    }
}
