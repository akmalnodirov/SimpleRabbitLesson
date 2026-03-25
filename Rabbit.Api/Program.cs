using Microsoft.Extensions.Options;
using Rabbit.Api.Models;
using Rabbit.Contracts;
using RabbitMQ.Client;

namespace Rabbit.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOpenApi();

        builder.Services.Configure<RabbitMqOptions>(
            builder.Configuration.GetSection("RabbitMq"));

        builder.Services.AddSingleton<IConnectionFactory>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

            return new ConnectionFactory
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName,
                Password = options.Password
            };
        });

        builder.Services.AddSingleton<RabbitMqPublisher>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapPost("/jobs", async (
            CreateJobRequest request,
            RabbitMqPublisher publisher,
            ILogger<Program> logger,
            CancellationToken cancellationToken) =>
        {
            if (request.DurationMs <= 0)
            {
                return Results.BadRequest(new
                {
                    error = "durationMs must be greater than 0"
                });
            }

            var acceptedAtUtc = DateTime.UtcNow;

            var message = new CreateJobMessage(
                JobId: Guid.NewGuid(),
                DurationMs: request.DurationMs,
                CreatedAtUtc: acceptedAtUtc
            );

            await publisher.PublishAsync(message, cancellationToken);

            logger.LogInformation(
                "Job accepted and published. JobId: {JobId}, DurationMs: {DurationMs}, AcceptedAtUtc: {AcceptedAtUtc}",
                message.JobId,
                message.DurationMs,
                acceptedAtUtc);

            return Results.Ok(new JobAcceptedResponse(
                JobId: message.JobId,
                AcceptedAtUtc: acceptedAtUtc,
                Message: "Job accepted and published to RabbitMQ"
            ));
        });

        app.Run();
    }
}