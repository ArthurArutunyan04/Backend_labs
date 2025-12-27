using Dapper;
using FluentValidation;
using System.Text.Json;
using WebApp.BLL.Services;
using WebApp.Config;
using WebApp.DAL;
using WebApp.DAL.Interfaces;
using WebApp.DAL.Repositories;
using WebApp.Jobs;
using WebApp.Migrations;
using WebApp.Validators;


var builder = WebApplication.CreateBuilder(args);
DefaultTypeMap.MatchNamesWithUnderscores = true;

builder.Services.AddHostedService<OrderGenerator>();

builder.Services.AddScoped<UnitOfWork>();


builder.Services.Configure<DbSettings>(builder.Configuration.GetSection(nameof(DbSettings)));
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection(nameof(KafkaSettings)));



builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderItemRepository, OrderItemRepository>();
builder.Services.AddScoped<OrderService>();

builder.Services.AddSingleton<KafkaProducer>(); // Добавлена зависимость Kafka

builder.Services.AddScoped<IAuditLogOrderRepository, AuditLogOrderRepository>();
builder.Services.AddScoped<AuditLogOrderService>();

builder.Services.AddValidatorsFromAssemblyContaining(typeof(Program));
builder.Services.AddScoped<ValidatorFactory>();


builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});
//builder.Services.AddControllers();

builder.Services.AddSwaggerGen();

//builder.Services.AddHostedService<OrderGenerator>();


var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();


app.MapControllers();


MyCompany.Migrations.Program.Main([]);


app.Run();