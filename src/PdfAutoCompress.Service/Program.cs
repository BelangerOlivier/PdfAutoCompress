using PdfAutoCompress.Service;

var builder = Host.CreateApplicationBuilder(args);

// Run as a Windows Service when installed; falls back to a console host otherwise.
builder.Services.AddWindowsService(options => options.ServiceName = "PdfAutoCompress");
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
