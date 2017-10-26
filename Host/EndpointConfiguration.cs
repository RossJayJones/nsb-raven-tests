using System;
using System.Linq;
using log4net.Config;
using Microsoft.Practices.Unity;
using NServiceBus;
using NServiceBus.Log4Net;
using NServiceBus.Logging;
using NServiceBus.Persistence;
using Raven.Client;
using Raven.Client.Document;

namespace Host
{
    public class EndpointConfiguration : IConfigureThisEndpoint, AsA_Server
    {
        public void Customize(BusConfiguration configuration)
        {
            XmlConfigurator.Configure();
            LogManager.Use<Log4NetFactory>();

            var container = BootstrapApplication();

            configuration.EndpointName("raven-tests");

            configuration.UseTransport<MsmqTransport>();
            configuration.UseSerialization<JsonSerializer>();

            var store = container.Resolve<IDocumentStore>();

            if (store == null)
            {
                throw new NullReferenceException("Store cannot be null");
            }

            var persistence = configuration.UsePersistence<RavenDBPersistence>().SetDefaultDocumentStore(store).DoNotSetupDatabasePermissions();
            persistence.UseSharedSession(() => container.Resolve<IDocumentSession>());

            configuration.UseContainer<UnityBuilder>(customisations => { customisations.UseExistingContainer(container); });

            var transactions = configuration.Transactions();
            transactions.DoNotWrapHandlersExecutionInATransactionScope();
            transactions.DisableDistributedTransactions();
            configuration.RijndaelEncryptionService();
            var conventions = configuration.Conventions();
            conventions.DefiningEventsAs(type => IsMessage(type, ".Events"));
            conventions.DefiningMessagesAs(type => IsMessage(type, ".Messages", ".Commands"));
        }

        private static bool IsMessage(Type type, params string[] check)
        {
            if (string.IsNullOrEmpty(type.Namespace))
            {
                return false;
            }

            return (type.Namespace.Contains("Trackmatic.") || type.Namespace.Contains("Tracking.")) && check.Any(x => type.Namespace.Contains(x));
        }

        private IUnityContainer BootstrapApplication()
        {
            var container = new UnityContainer();
            container.RegisterType<IDocumentStore>(new ContainerControlledLifetimeManager(), new InjectionFactory(c =>
            {
                var store = new DocumentStore
                {
                    EnlistInDistributedTransactions = false,
                    Conventions =
                    {
                        ShouldCacheRequest = url => true,
                        ShouldAggressiveCacheTrackChanges = false
                    },
                    ConnectionStringName = "raven-db"
                };
                store.Initialize();
                return store;
            }));
            return container;
        }
    }
}
