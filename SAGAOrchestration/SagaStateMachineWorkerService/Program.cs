using MassTransit;
using Microsoft.EntityFrameworkCore;
using SagaStateMachineWorkerService;
using SagaStateMachineWorkerService.Models;
using Shared;
using System.Reflection;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddMassTransit(cfg =>
        {
            cfg.AddSagaStateMachine<OrderStateMachine, OrderStateInstance>().EntityFrameworkRepository(opt =>
            {
                opt.AddDbContext<DbContext, OrderStateDbContext>((provider, builder) =>
                {
                    builder.UseSqlServer(hostContext.Configuration.GetConnectionString("SqlConnection"), m =>
                    {
                        m.MigrationsAssembly(Assembly.GetExecutingAssembly().GetName().Name);
                    });
                });
            });

            cfg.AddBus(provider => Bus.Factory.CreateUsingRabbitMq(configure =>
            {
                configure.Host(hostContext.Configuration.GetConnectionString("RabbitMQ"));
                configure.ReceiveEndpoint(RabbitMQSettings.OrderSaga, e =>
                {
                    e.ConfigureSaga<OrderStateInstance>(provider);
                });
            }));
        });

        //Masstransit v8 s�r�m� sonras�nda bunun kullan�lmas�na gerek kalmad�.
        //services.AddMassTransitHostedService();

        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
