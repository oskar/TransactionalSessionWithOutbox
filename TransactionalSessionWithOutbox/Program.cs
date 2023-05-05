using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NpgsqlTypes;
using NServiceBus;
using NServiceBus.TransactionalSession;

namespace TransactionalSessionWithOutbox;

public static class Program
{
    public static async Task Main()
    {
        const string receiverEndpoint = "Receiver";

        // Configure endpoint
        var configuration = new EndpointConfiguration("Sender");
        configuration.EnableInstallers();
        var transport = configuration.UseTransport<LearningTransport>();
        var storageDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Console.WriteLine("Configuring LearningTransport to use storage: " + storageDirectory);
        transport.StorageDirectory(storageDirectory);
        transport.Transactions(TransportTransactionMode.ReceiveOnly);
        transport.Routing().RouteToEndpoint(typeof(IMyCommand), receiverEndpoint);

        var outbox = configuration.EnableOutbox();

        // If enabled, no message is sent to the receiver
        //outbox.UseTransactionScope(System.Transactions.IsolationLevel.ReadCommitted);

        // If enabled, duplicate key exception occurs but message is sent to the receiver after retry
        //outbox.UsePessimisticConcurrencyControl();

        var persistence = configuration.UsePersistence<SqlPersistence>();
        persistence.EnableTransactionalSession();
        persistence.ConnectionBuilder(() => new NpgsqlConnection("User ID=postgres;Password=postgres;Host=localhost;Port=5432;Database=postgres"));
        var dialect = persistence.SqlDialect<SqlDialect.PostgreSql>();
        dialect.JsonBParameterModifier(parameter => ((NpgsqlParameter)parameter).NpgsqlDbType = NpgsqlDbType.Jsonb);

        // Start endpoint
        var services = new ServiceCollection();
        var startableEndpoint = EndpointWithExternallyManagedContainer.Create(configuration, services);
        var serviceProvider = services.BuildServiceProvider();
        var endpoint = await startableEndpoint.Start(serviceProvider);

        // Open session, send message, commit session
        var transactionalSession = serviceProvider.GetRequiredService<ITransactionalSession>();
        await transactionalSession.Open(new SqlPersistenceOpenSessionOptions());
        await transactionalSession.Send<IMyCommand>(_ => { });
        await transactionalSession.Commit();

        // Waiting less than 5 seconds is too short, no message will be sent
        await Task.Delay(TimeSpan.FromSeconds(10));
        await endpoint.Stop();

        Console.WriteLine(new DirectoryInfo(storageDirectory).GetDirectories(receiverEndpoint).Length == 0
            ? "No receiver directory found (no message was sent)"
            : "Receiver directory found (message was sent)");
    }

    public interface IMyCommand : ICommand { }
}
