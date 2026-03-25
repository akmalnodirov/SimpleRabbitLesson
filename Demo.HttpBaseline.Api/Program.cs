using Demo.HttpBaseline.Api.Models;
using System.Diagnostics;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOpenApi();
        builder.Services.AddSingleton<MetricsStore>();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.MapPost("/jobs", async (CreateJobRequest request, MetricsStore metricsStore, ILogger<Program> logger, CancellationToken cancellationToken) =>
        {
            if (request.DurationMs <= 0)
            {
                return Results.BadRequest(new
                {
                    error = "durationMs must be greater than 0"
                });
            }

            var jobId = Guid.NewGuid();
            var startedAtUtc = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            metricsStore.RequestStarted();

            logger.LogInformation(
                "Job started. JobId: {JobId}, DurationMs: {DurationMs}, StartedAtUtc: {StartedAtUtc}",
                jobId,
                request.DurationMs,
                startedAtUtc);

            try
            {
                // Simulate slow work inside the HTTP request path
                await Task.Delay(request.DurationMs, cancellationToken);

                stopwatch.Stop();

                var finishedAtUtc = DateTime.UtcNow;

                metricsStore.RequestCompleted(stopwatch.ElapsedMilliseconds);

                logger.LogInformation(
                    "Job completed. JobId: {JobId}, ElapsedMs: {ElapsedMs}, FinishedAtUtc: {FinishedAtUtc}",
                    jobId,
                    stopwatch.ElapsedMilliseconds,
                    finishedAtUtc);

                return Results.Ok(new CreateJobResponse(
                    JobId: jobId,
                    StartedAtUtc: startedAtUtc,
                    FinishedAtUtc: finishedAtUtc,
                    ElapsedMs: stopwatch.ElapsedMilliseconds,
                    Message: "Job completed inside HTTP request pipeline"
                ));
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                metricsStore.RequestFailed();

                logger.LogWarning(
                    "Job cancelled. JobId: {JobId}, ElapsedMs: {ElapsedMs}",
                    jobId,
                    stopwatch.ElapsedMilliseconds);

                return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                metricsStore.RequestFailed();

                logger.LogError(
                    ex,
                    "Job failed. JobId: {JobId}, ElapsedMs: {ElapsedMs}",
                    jobId,
                    stopwatch.ElapsedMilliseconds);

                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        });

        app.MapGet("/metrics", (MetricsStore metricsStore) =>
        {
            var snapshot = metricsStore.GetSnapshot();

            return Results.Ok(snapshot);
        });

        app.Run();
    }
}

public sealed record CreateJobRequest(int DurationMs);

public sealed record CreateJobResponse(
    Guid JobId,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    long ElapsedMs,
    string Message
);