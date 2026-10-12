using ImageGenerator.Forms;

namespace ImageGenerator;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Without these handlers a single exception out of an async void handler (model fetch, update
        // check, generation) kills the whole process with the framework's dialog. Keep the app alive
        // and name the failure instead.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportUnhandled(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
                ReportUnhandled(exception);
        };

        Application.Run(new MainForm());
    }

    private static void ReportUnhandled(Exception exception)
    {
        System.Diagnostics.Debug.WriteLine($"[Program] unhandled exception: {exception}");
        try
        {
            MessageBox.Show(
                "发生了未预期的错误，当前操作已中止，程序会继续运行。"
                + Environment.NewLine + Environment.NewLine
                + $"{exception.GetType().Name}: {exception.Message}",
                "GPT Image Playground",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // Reporting must never throw on top of the original failure.
        }
    }
}
