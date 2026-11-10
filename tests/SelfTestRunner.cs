using System;
using System.Windows;
using GlassBoard;

internal static class SelfTestRunner
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args != null && args.Length > 0 && string.Equals(args[0], "--headless", StringComparison.OrdinalIgnoreCase))
            return SelfTest.RunHeadless();
        Application application = new Application();
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            int result = SelfTest.Run();
            Console.WriteLine("GlassBoard self-test exit code: " + result);
            Console.Out.Flush();
            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Console.Error.Flush();
            return 1;
        }
        finally
        {
            application.Shutdown();
        }
    }
}
