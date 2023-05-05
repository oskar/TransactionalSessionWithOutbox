# Working baseline

1. Start postgres database for outbox persistence
```
docker-compose up -d
```

2. Run the app
```
dotnet run --project .\TransactionalSessionWithOutbox\
```

3. `Receiver` directory was created

# Reproduce TransactionScope issue

Enable TransactionScope by uncommenting line 31 in `Program.cs`

```csharp
outbox.UseTransactionScope(System.Transactions.IsolationLevel.ReadCommitted);
```

# Reproduce pessimistic concurrency issue

Enable transaction scope by uncommenting line 34 in `Program.cs`

```csharp
outbox.UsePessimisticConcurrencyControl();
```

