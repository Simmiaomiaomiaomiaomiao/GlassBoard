using System;
using GlassBoard;

internal static class SelfTestRunner
{
    [STAThread]
    private static int Main()
    {
        try
        {
            int result = SelfTest.Run();
            Console.WriteLine("GlassBoard self-test exit code: " + result);
            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
