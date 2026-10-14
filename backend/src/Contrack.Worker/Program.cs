using Contrack.Infrastructure;
using Contrack.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<HeartbeatWorker>();

builder.Build().Run();
