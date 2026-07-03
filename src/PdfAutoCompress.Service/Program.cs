using PdfAutoCompress.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "PdfAutoCompress");
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
