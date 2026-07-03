using PdfAutoCompress.Core;

Console.WriteLine("PDF Auto-Compress — CLI watcher");
Console.WriteLine("--------------------------------");

AppConfig config = AppConfig.Load();
using var watcher = new PdfWatcher();
watcher.Log += Console.WriteLine;

string? error = watcher.Start(config);
if (error != null)
{
    Console.Error.WriteLine(error);
    return 1;
}

Console.WriteLine("Press Ctrl+C to stop.");

using var quit = new ManualResetEventSlim(false);
Console.CancelKeyPress += (_, e) => { e.Cancel = true; quit.Set(); };
quit.Wait();

watcher.Stop();
Console.WriteLine("Stopped.");
return 0;
