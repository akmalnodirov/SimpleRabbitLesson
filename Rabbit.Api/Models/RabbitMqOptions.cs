using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Rabbit.Api.Models;

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "demo.jobs.exchange";
    public string QueueName { get; set; } = "demo.jobs.queue";
    public string RoutingKey { get; set; } = "jobs.create";
}


//Why these fields exist
//HostName, Port, UserName, Password

//These are for connecting to the broker.

//RabbitMQ clients connect to the broker over AMQP, and ConnectionFactory uses
//those values to establish the connection.

//ExchangeName

//Where the API will publish messages.

//Important:
//the producer publishes to an exchange, not directly “to the queue” in the AMQP model.

//QueueName

//Where the Worker will consume from.

//RoutingKey

//How the direct exchange decides where to route the message.

//With a direct exchange, routing depends on exact binding key match.