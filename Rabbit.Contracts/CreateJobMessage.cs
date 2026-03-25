namespace Rabbit.Contracts;

public sealed record CreateJobMessage(
    Guid JobId,
    int DurationMs,
    DateTime CreatedAtUtc
);