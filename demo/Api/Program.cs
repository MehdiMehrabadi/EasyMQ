using Common;
using EasyMQ;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddRabbitMq(settings =>
{
    var configuration = builder.Configuration.GetSection("Rabbit");
    int.TryParse(configuration["Port"], out var port);
    settings.Host = configuration["Host"];
    settings.Port = port;
    settings.ExchangeName = configuration["ApplicationName"];
    settings.VirtualHost = configuration["VirtualHost"];
    settings.UserName = configuration["Username"];
    settings.Password = configuration["Password"];
  
}, queues =>
{
    queues.Add<MessageModel>(queueName: "message");
 // queues.Add<MessageModel2>(queueName: "message2");

});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();

app.Run();
