using System;
using GlassBoard;
using System.Windows;

internal static class SelfTestRunner
{
    [STAThread]
    private static int Main()
    {
        Application application = new Application();
        application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            int result = SelfTest.Run();
            Console.WriteLine("GlassBoard self-test exit code: " + result);
            Console.Out.Flush();
            return result;
        }
        finally
        {
            application.Shutdown();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
