using Rabbit.Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

Console.WriteLine("=== Rabbit.Worker starting ===");

var factory = new ConnectionFactory
{
    HostName = "localhost",
    Port = 5672,
    UserName = "guest",
    Password = "guest"
};

var connection = await factory.CreateConnectionAsync();
var channel = await connection.CreateChannelAsync();

const string queueName = "demo.jobs.queue";

// Ensure queue exists (idempotent — same declaration as the API)
await channel.QueueDeclareAsync(
    queue: queueName,
    durable: true,
    exclusive: false,
    autoDelete: false);

// Fair dispatch: worker takes 1 message at a time
await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

Console.WriteLine($"Waiting for messages on '{queueName}'. Press Ctrl+C to exit.");
Console.WriteLine();

var consumer = new AsyncEventingBasicConsumer(channel);

consumer.ReceivedAsync += async (sender, ea) =>
{
    var body = ea.Body.ToArray();
    var json = Encoding.UTF8.GetString(body);
    var message = JsonSerializer.Deserialize<CreateJobMessage>(json);

    if (message is null)
    {
        Console.WriteLine("[WARN] Received null message, skipping.");
        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        return;
    }

    var pickedUpAtUtc = DateTime.UtcNow;
    var queueWaitTime = pickedUpAtUtc - message.CreatedAtUtc;

    Console.WriteLine($"[PICKED UP] JobId: {message.JobId}");
    Console.WriteLine($"    API accepted at:   {message.CreatedAtUtc:HH:mm:ss.fff}");
    Console.WriteLine($"    Worker picked up:  {pickedUpAtUtc:HH:mm:ss.fff}");
    Console.WriteLine($"    Queue wait time:   {queueWaitTime.TotalMilliseconds:F0} ms");
    Console.WriteLine($"    Simulating work:   {message.DurationMs} ms ...");

    var stopwatch = Stopwatch.StartNew();

    await Task.Delay(message.DurationMs);

    stopwatch.Stop();

    var finishedAtUtc = DateTime.UtcNow;
    var totalEndToEnd = finishedAtUtc - message.CreatedAtUtc;

    Console.WriteLine($"[COMPLETED] JobId: {message.JobId}");
    Console.WriteLine($"    Work elapsed:      {stopwatch.ElapsedMilliseconds} ms");
    Console.WriteLine($"    Total end-to-end:  {totalEndToEnd.TotalMilliseconds:F0} ms");
    Console.WriteLine();

    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
};

await channel.BasicConsumeAsync(
    queue: queueName,
    autoAck: false,
    consumer: consumer);

// Keep the app alive until Ctrl+C
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Shutting down gracefully...");
}

await channel.DisposeAsync();
await connection.DisposeAsync();

Console.WriteLine("=== Rabbit.Worker stopped ===");