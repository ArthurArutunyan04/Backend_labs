using System.Text.Json;
using Dapper;
using DefaultNamespace;
using DefaultNamespace.Interfaces;
using DefaultNamespace.Repositories;
using FluentValidation;
using WebApp.BLL.Services;
using WebApp.Config;
using WebApp.Jobs;
using WebApp.Validators;

var builder = WebApplication.CreateBuilder(args);

DefaultTypeMap.MatchNamesWithUnderscores = true;

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderItemRepository, OrderItemRepository>();
builder.Services.AddScoped<OrderService>();

builder.Services.AddScoped<UnitOfWork>();

builder.Services.Configure<DbSettings>(builder.Configuration.GetSection(nameof(DbSettings)));


builder.Services.AddScoped<IAuditLogOrderRepository, AuditLogOrderRepository>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection(nameof(RabbitMqSettings)));
builder.Services.AddScoped<RabbitMqService>();


builder.Services.AddValidatorsFromAssemblyContaining(typeof(Program));
builder.Services.AddScoped<ValidatorFactory>(); 

builder.Services.AddControllers().AddJsonOptions(options => 
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

builder.Services.AddSwaggerGen();


builder.Services.AddHostedService<OrderGenerator>();



var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

Migrations.Program.Main([]); 

app.Run();