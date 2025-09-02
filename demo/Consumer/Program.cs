using Common;
using Consumer.Receivers;
using EasyMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(ConfigureServices).ConfigureAppConfiguration(builder =>
    {
        builder.Sources.Clear();
        builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
    }).ConfigureLogging((context, logging) =>
    {
        if (context.HostingEnvironment.IsDevelopment())
        {
            logging.AddConsole();
            logging.AddDebug();
        }
        else
            logging.ClearProviders();
    })
    .Build();


void ConfigureServices(HostBuilderContext hostingContext, IServiceCollection services)
{
    var configuration = hostingContext.Configuration;

    services.AddRabbitMq(settings =>
        {
            var config = configuration.GetSection("Rabbit");
            int.TryParse(config["Port"], out var port);
            settings.Host = config["Host"];
            settings.Port = port;
            settings.ExchangeName = config["ApplicationName"];
            settings.VirtualHost = config["VirtualHost"];
            settings.UserName = config["Username"];
            settings.Password = config["Password"];
        }, queues =>
        {
            // add queues 
            queues.Add<MessageModel>(queueName: "message", prefetchCount: 10, retryCount: 5);
            // queues.Add<MessageModel2>(queueName: "message2", prefetchCount: 3, retryCount: 2);
        })
        // add receivers
        .AddReceiver<MessageModel, MessageReceiver>();
    //.AddReceiver<MessageModel2, MessageReceiver2>();
}


Console.WriteLine("consumer start");
await host.RunAsync();
Console.ReadLine();