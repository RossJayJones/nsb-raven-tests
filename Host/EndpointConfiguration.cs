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


            configuration.EndpointName("raven-tests");

            configuration.UseTransport<MsmqTransport>();
            configuration.UseSerialization<JsonSerializer>();

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

            var persistence = configuration.UsePersistence<RavenDBPersistence>().SetDefaultDocumentStore(store).DoNotSetupDatabasePermissions();
            persistence.UseSharedSession(() => store.OpenSession());

            var container = new UnityContainer();
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
    }
}
