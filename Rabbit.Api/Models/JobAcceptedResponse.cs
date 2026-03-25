namespace Rabbit.Api.Models;

public sealed record JobAcceptedResponse(
    Guid JobId,
    DateTime AcceptedAtUtc,
    string Message
);