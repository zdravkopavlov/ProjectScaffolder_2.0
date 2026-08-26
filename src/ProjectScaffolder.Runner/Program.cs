namespace ProjectScaffolder.Runner;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args) => RunnerApplication.RunAsync(args).GetAwaiter().GetResult();
}
