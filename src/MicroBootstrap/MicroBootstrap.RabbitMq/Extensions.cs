using System;
using MicroBootstrap.Jaeger.Tracers;
using MicroBootstrap.RabbitMq.Processors;
using MicroBootstrap.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTracing;
using RawRabbit;
using RawRabbit.Common;
using RawRabbit.Configuration;
using RawRabbit.Enrichers.MessageContext;
using RawRabbit.Instantiation;

namespace MicroBootstrap.RabbitMq
{
    public static class Extensions
    {
        public static IBusSubscriber UseRabbitMq(this IApplicationBuilder app)
            => new BusSubscriber(app);

        private const string SectionName = "rabbitMq";
        private const string RegistryName = "messageBrokers.rabbitMq";

        internal static string GetMessageName(this object message)
            => message.GetType().Name.ToSnakeCase().ToLowerInvariant();

        public static IServiceCollection AddRabbitMq(this IServiceCollection serviceCollection,
            string sectionName = SectionName,
            string redisSectionName = "redis",
            Func<IRabbitMqPluginRegister, IRabbitMqPluginRegister> plugins = null)
        {
            if (string.IsNullOrWhiteSpace(sectionName))
            {
                sectionName = SectionName;
            }

            var options = serviceCollection.GetOptions<RabbitMqOptions>(sectionName);
            var redisOptions = serviceCollection.GetOptions<RedisOptions>(redisSectionName);
            return AddRabbitMq(serviceCollection, options, plugins,
                b => serviceCollection.AddRedis().AddRedis(redisOptions ?? new RedisOptions()));
        }

        public static IServiceCollection AddRabbitMq(this IServiceCollection serviceCollection,
            RabbitMqOptions options,
            Func<IRabbitMqPluginRegister, IRabbitMqPluginRegister> plugins,
            Action<IServiceCollection> registerRedis)
        {
            serviceCollection.AddSingleton(options);
            serviceCollection.AddSingleton<RawRabbitConfiguration>(options);

            serviceCollection.AddTransient<IBusPublisher, BusPublisher>();
            if (options.MessageProcessor?.Enabled == true)
            {
                switch (options.MessageProcessor.Type?.ToLowerInvariant())
                {
                    case "redis":
                        registerRedis(serviceCollection);
                        serviceCollection.AddTransient<IMessageProcessor, RedisMessageProcessor>();
                        break;
                    default:
                        serviceCollection.AddTransient<IMessageProcessor, InMemoryMessageProcessor>();
                        break;
                }
            }
            else
            {
                serviceCollection.AddSingleton<IMessageProcessor, EmptyMessageProcessor>();
            }

            ConfigureBus(serviceCollection, plugins);

            return serviceCollection;
        }

        private static void ConfigureBus(IServiceCollection serviceCollection, Func<IRabbitMqPluginRegister,
            IRabbitMqPluginRegister> plugins = null)
        {
            serviceCollection.AddSingleton<IInstanceFactory>(serviceProvider =>
            {
                var options = serviceProvider.GetService<RabbitMqOptions>();
                var configuration = serviceProvider.GetService<RawRabbitConfiguration>();
                var namingConventions = new CustomNamingConventions(options);
                var tracer = serviceProvider.GetService<ITracer>();
                var register = plugins?.Invoke(new RabbitMqPluginRegister(serviceProvider));

                return RawRabbitFactory.CreateInstanceFactory(new RawRabbitOptions
                {
                    DependencyInjection = ioc =>
                    {
                        register?.Register(ioc);
                        ioc.AddSingleton(options);
                        ioc.AddSingleton(configuration);
                        ioc.AddSingleton<INamingConventions>(namingConventions);
                    },
                    Plugins = clientBuilder =>
                    {
                        register?.Register(clientBuilder);
                        clientBuilder.UseAttributeRouting()
                            .UseRetryLater()
                            .UpdateRetryInfo()
                            .UseMessageContext<CorrelationContext>()
                            .UseContextForwarding();

                        if (options.MessageProcessor?.Enabled == true)
                        {
                            clientBuilder.ProcessUniqueMessages();
                        }
                    }
                });
            });
            serviceCollection.AddTransient(context => context.GetService<IInstanceFactory>().Create());
        }


        private static IClientBuilder ProcessUniqueMessages(this IClientBuilder clientBuilder)
        {
            clientBuilder.Register(c => c.Use<ProcessUniqueMessagesMiddleware>());

            return clientBuilder;
        }

        private static IClientBuilder UpdateRetryInfo(this IClientBuilder clientBuilder)
        {
            clientBuilder.Register(c => c.Use<RetryStagedMiddleware>());

            return clientBuilder;
        }
    }
}