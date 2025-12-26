using Consumer.Config;
using Consumer.Consumers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
    options.ServicesStopConcurrently = true;
});

builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection(nameof(RabbitMqSettings)));

builder.Services.AddHostedService<BatchOmsOrderCreatedConsumer>();
builder.Services.AddHostedService<BatchOmsOrderStatusChangedConsumer>();

builder.Services.AddHttpClient<Client>(c => c.BaseAddress = new Uri(builder.Configuration["HttpClient:Oms:BaseAddress"]));

var app = builder.Build();
await app.RunAsync();