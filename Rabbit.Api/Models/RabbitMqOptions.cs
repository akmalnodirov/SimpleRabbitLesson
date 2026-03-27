using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using RabbitMQ.Client;
using System.Buffers.Text;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Rabbit.Api.Models;

//This class is a configuration model.
//Its purpose is to hold all the RabbitMQ - related settings in one place, so the
//rest of your code does not hardcode broker address, credentials, exchange name, queue name,
//and routing key directly inside business logic.
//In other words, instead of writing things like "localhost", "guest", "demo.jobs.queue" in many places,
//you centralize them in one object. Then.NET can fill this object from appsettings.json.
//So conceptually, this class answers two groups of questions:

//First group: How do we connect to RabbitMQ?

//HostName
//Port
//UserName
//Password

//Second group: Once connected, where do messages go?

//ExchangeName
//QueueName
//RoutingKey

//That is why this class exists.

public sealed class RabbitMqOptions
{

    //This is the hostname of the RabbitMQ broker.
    //The broker is the RabbitMQ server itself, the process that accepts connections,
    //stores messages in queues, routes them through exchanges, and delivers them to consumers.
    //When your API wants to publish a message, it must connect to a RabbitMQ server somewhere.
    //HostName tells the client where that server is.

    //Examples:

    //"localhost" means RabbitMQ is running on the same machine
    //"rabbitmq" might be the Docker container name on a Docker network
    //"10.0.0.5" might be the IP address of a server
    //"rabbit.company.internal" might be a DNS name in production

    //Why "localhost" here?
    //Because for local development, RabbitMQ is often run on your own
    //machine or exposed locally via Docker port mapping
    //If Docker maps container port 5672 to your machine,
    //then from your application’s perspective, RabbitMQ can be reached at localhost.
    //So HostName is not about queues or exchanges.It is purely about network location of the broker.
    public string HostName { get; set; } = "localhost";


    //This is the TCP port used for RabbitMQ AMQP connections.
    //RabbitMQ usually exposes:

    //5672 for AMQP client traffic
    //15672 for management UI

    //This is a very important distinction.
    //Your application code uses 5672, because it is talking to RabbitMQ as a client over the AMQP protocol.
    //Your browser uses 15672, because that is the HTTP management interface.

    //So when you set: Port = 5672
    //you are saying:
    //“My application should connect to the RabbitMQ broker over the standard messaging port.”
    //If this port is wrong, the application may fail to connect even if RabbitMQ is running.
    //So HostName + Port together define the network address:

    //host = where
    //port = which endpoint on that machine
    public int Port { get; set; } = 5672;



    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";


//Now we move to messaging topology.

//This is the name of the exchange where the producer publishes messages.
//This is one of the most important RabbitMQ concepts, so let’s make it very clear.
//In RabbitMQ, a producer usually does not send directly to a queue in the general AMQP model.
//Instead, the producer publishes a message to an exchange.
//Then the exchange decides where that message goes.
//So the flow is:

//Producer → Exchange → Queue → Consumer

//That is why ExchangeName exists.
//It identifies the exchange that your application intends to publish into.

//What is an exchange?
//An exchange is a routing component inside RabbitMQ.

//Its job is to:

//receive messages from producers
//inspect routing metadata
//decide which queue or queues should receive the message

//So an exchange is not a storage place.
//That is very important.
//An exchange does not keep messages for consumers long-term like a queue does.
//A queue is where messages wait.
//An exchange is more like a traffic controller or dispatcher.
//It accepts a message and immediately tries to route it according to its rules and bindings.

//Why does RabbitMQ use exchanges?
//Because RabbitMQ is designed to separate:

//message production
//message routing
//message consumption

//Without exchanges, a producer would need to know exactly which queue every message should go to.
//That becomes rigid and tightly coupled.
//With exchanges, the producer only says:
//“Here is a message, and here is its routing key.”
//The broker topology then decides:

//which queue gets it
//whether multiple queues get it
//whether nobody gets it

//This makes the system much more flexible.

//Why not just publish directly to a queue?

//Good question.

//In RabbitMQ, there is a way to appear as if you are publishing directly to a queue, but internally that still goes through an exchange: the default exchange.

//So even “direct to queue” is not truly bypassing the exchange concept. RabbitMQ still routes the message via an exchange mechanism.

//Using your own named exchange gives you more control because:

//you can route one message to multiple queues
//you can route based on patterns
//you can replace or add consumers without changing producer code too much
//you can design clearer messaging topology

//So if you define your own exchange, you are moving from a very simple setup into a more explicit and scalable messaging design.

//What does ExchangeName actually represent?

//This property holds the name of the exchange object inside RabbitMQ.

//Example:

//ExchangeName = "demo.jobs.exchange"

//That means your code expects there to be an exchange with that exact name.

//Usually your code will either:

//declare it if it does not exist
//or publish to it assuming it already exists

//So this is not just an arbitrary string. It identifies a real RabbitMQ object.

//If the exchange name is wrong:

//the producer may publish to the wrong place
//or publishing may fail if the exchange does not exist and the broker does not allow implicit behavior

//So naming matters.

//What kind of thing is an exchange in practice?

//Think of an exchange as a post office sorting center.

//A producer hands over a package.

//The exchange looks at the label:

//maybe exact destination
//maybe a category
//maybe a pattern
//maybe broadcast to all

//Then it routes the package to one or more mailboxes, which are the queues.

//Consumers do not take messages from the exchange.
//They take messages from the queues.

//So the exchange is a middle layer between producer and consumer.

//Exchanges do not work alone

//This is extremely important.

//An exchange by itself is useless unless it has bindings.

//A binding is a relationship between:

//an exchange
//and a queue
//often with some routing condition

//So the real picture is:

//Producer → Exchange → Binding rule → Queue → Consumer

//If there is no binding:

//the exchange may receive the message
//but have nowhere to send it

//That can mean the message is dropped, unless special handling is configured.

//So when you think about exchanges, never think only about the exchange object itself. Always think about:

//exchange type
//routing key
//queue bindings
//binding keys

//These all work together.

//Exchange types

//You asked how to know what type of exchange you are using, and when and why each type is used.

//This is the core part.

//RabbitMQ has four main exchange types:

//direct
//fanout
//topic
//headers

//Each one changes the routing behavior.

//1. Direct exchange

//A direct exchange routes a message based on an exact match between:

//the message’s routing key
//and the queue’s binding key

//Example:

//Producer publishes:

//routing key = jobs.create

//Queue binding:

//binding key = jobs.create

//This is an exact match, so the message goes to that queue.

//If another queue is bound with:

//binding key = jobs.update

//then it does not receive the jobs.create message.

//When do we use direct exchange?

//Use it when:

//routing rules are simple and explicit
//you want exact categories
//you know the event types clearly
//you want predictable one-to-one or selective one-to-many routing
//Typical examples
//jobs.create
//jobs.delete
//email.send
//invoice.generated
//Why use it?

//Because it is simple, clear, and easy to reason about.

//For many business systems, direct exchange is the first and most practical choice.

//In your case

//If you have:

//exchange name
//routing key like jobs.create
//one queue bound with exact key jobs.create

//then you are most likely using a direct exchange.

//2. Fanout exchange

//A fanout exchange ignores routing keys and sends the message to all bound queues.

//So if a message is published to a fanout exchange:

//Queue A gets it
//Queue B gets it
//Queue C gets it

//as long as all of them are bound to that exchange.

//When do we use fanout?

//Use it when you want broadcast behavior.

//One published message should go everywhere.

//Typical examples
//notify many services about the same event
//logging to multiple destinations
//sending the same event to analytics, audit, and notification services
//pub/sub style event broadcasting
//Why use it?

//Because the producer does not need to care about specific routing keys. It simply says:

//“Everyone listening to this exchange should receive this.”

//Example scenario

//A user registers.

//You publish one user.registered event to a fanout exchange.

//Then:

//email service sends welcome email
//analytics service records signup
//audit service stores event history
//CRM integration syncs customer

//One event, many consumers.

//Downside

//It is less selective. Every bound queue gets the message.

//So fanout is powerful when broadcast is the goal, but wasteful if only one or a few consumers should receive the message.

//3. Topic exchange

//A topic exchange routes messages based on pattern matching in routing keys.

//Routing keys are usually dot-separated words, for example:

//jobs.create
//jobs.update
//users.registered
//payments.failed.us

//Bindings can include wildcards:

//* = exactly one word
//# = zero or more words
//Examples

//Binding key:

//jobs.*

//matches:

//jobs.create
//jobs.update

//but not:

//jobs.create.high

//Binding key:

//jobs.#

//matches:

//jobs.create
//jobs.update
//jobs.create.high
//jobs.anything.at.all
//When do we use topic exchange?

//Use it when:

//routing needs hierarchy or patterns
//you have many related message categories
//exact match is too limited
//you want flexible subscriptions
//Typical examples
//event-driven architectures
//domain events
//logging systems by severity and region
//multi-tenant or multi-module apps

//Examples:

//order.created
//order.cancelled
//payment.failed
//payment.failed.eu
//payment.failed.us

//Then consumers can subscribe broadly or narrowly:

//payment.*
//payment.failed.*
//#.failed
//Why use it?

//Because it gives much more flexibility than direct exchange without forcing full broadcast like fanout.

//Downside

//It is more complex. You must design routing keys carefully.

//4. Headers exchange

//A headers exchange routes messages based on message headers instead of the routing key.

//So instead of matching:

//routing key = jobs.create

//it checks headers like:

//department = finance
//priority = high
//format = pdf
//When do we use headers exchange?

//Usually much less often.

//Use it when routing depends on multiple metadata fields rather than one string routing key.

//Why is it less common?

//Because direct and topic exchanges are usually simpler and enough for most systems.

//Headers exchange can be useful, but it is more specialized and often unnecessary in normal application messaging.

//How do I know which exchange type I am using?

//Excellent question.

//You know the exchange type by how the exchange is declared.

//Somewhere in your code, if you are explicitly creating the exchange, you will have something like:

//await channel.ExchangeDeclareAsync(
//    exchange: options.ExchangeName,
//    type: ExchangeType.Direct,
//    durable: true);

//or:

//type: ExchangeType.Fanout

//or:

//type: ExchangeType.Topic

//That type parameter is the answer.

//So if you want to know what kind of exchange you are using, check where you call:

//ExchangeDeclare
//or ExchangeDeclareAsync

//That is where the exchange type is chosen.

//What if I never declare an exchange?

//Then one of two things is probably happening:

//Case 1: you are using the default exchange

//If you publish with:

//exchange: ""

//that means you are using RabbitMQ’s built-in default exchange.

//This default exchange behaves like a direct exchange.

//It routes messages to a queue whose name exactly matches the routing key.

//Example:

//routing key = demo.jobs.queue
//queue name = demo.jobs.queue

//Then the message goes to that queue.

//This is why people sometimes think they are publishing directly to a queue.

//But technically they are still publishing to an exchange: the unnamed default one.

//Case 2: exchange already exists outside your code

//Maybe:

//it was created manually in UI
//created by another service
//created during startup elsewhere

//Then your code may only publish to it, without declaring it itself.

//In that case, you would inspect RabbitMQ Management UI to see its type.

//How to see the exchange type in RabbitMQ UI

//Open RabbitMQ Management UI.

//Usually:

//http://localhost:15672

//Then:

//go to Exchanges
//find your exchange by name
//open it

//There you can see:

//exchange name
//type
//durability
//bindings
//incoming/outgoing routing relationships

//So the UI is one of the easiest ways to confirm:

//whether the exchange exists
//what type it is
//what queues are bound to it
//What is the default exchange?

//This is very important because many beginners use it without realizing it.

//RabbitMQ has a built-in exchange with:

//empty string as its name: ""
//direct-like behavior

//Its rule is:

//route to the queue whose name exactly matches the routing key

//So if you publish like this:

//channel.BasicPublish(
//    exchange: "",
//    routingKey: "demo.jobs.queue",
//    ...)

//and there is a queue named demo.jobs.queue, the message will go there.

//That is why simple examples often look like they publish straight to the queue.

//But the exchange still exists.It is just implicit.

//Why would I use my own named exchange instead of the default one?

//Because your own exchange gives you:

//clear architecture
//explicit routing design
//ability to evolve routing later
//multiple queues for the same message
//less hard-coding to queue names

//The default exchange is fine for simple demos and very direct producer-to-consumer workflows.

//A named exchange is better when:

//you want cleaner topology
//you want scalability
//you want multiple consumers
//you want flexibility in future routing
//Exchange durability

//An exchange can also be:

//durable
//non-durable

//A durable exchange survives broker restart.

//A non-durable exchange disappears when RabbitMQ restarts.

//This is similar in concept to durable queues.

//So exchange design is not only about type; it is also about lifecycle and persistence of topology.

//Exchange does not guarantee message delivery by itself

//Another important conceptual point:

//Even if the producer publishes to an exchange, that does not automatically mean the message will safely end up in a queue.

//The message can be lost if:

//the exchange does not exist
//there is no matching binding
//queues are transient and disappear
//messages are not persistent
//broker or consumer configuration is incomplete

//So exchange is a routing point, not a full guarantee of durable delivery.

//Reliable delivery depends on multiple pieces working together:

//exchange exists
//queue exists
//binding exists
//queue/message durability is configured correctly
//acknowledgements are handled correctly
//When to choose each exchange type

//Here is the practical decision model.

//Use direct exchange when:
//you want exact routing
//event categories are known and finite
//one message type should go only to certain queues
//you want simple and predictable rules

//This is often the best starting point.

//Use fanout exchange when:
//one event should be broadcast everywhere
//you want pub/sub behavior
//all subscribers should get the same message

//Use it for announcements and event broadcasting.

//Use topic exchange when:
//routing categories are hierarchical
//you need wildcard subscriptions
//multiple services care about related but different subgroups of events

//This is common in larger event-driven systems.

//Use headers exchange when:
//routing depends on multiple metadata attributes
//routing key is not enough
//you have a special case requiring header-based logic

//This is the least common choice in normal application design.

//What exchange type are you most likely using?

//Based on your configuration:

//public string ExchangeName { get; set; } = "demo.jobs.exchange";
//public string QueueName { get; set; } = "demo.jobs.queue";
//public string RoutingKey { get; set; } = "jobs.create";

//This strongly suggests one of these:

//Most likely

//A direct exchange, where:

//exchange = demo.jobs.exchange
//queue = demo.jobs.queue
//binding key = jobs.create
//routing key = jobs.create
//Possibly

//A topic exchange, but only if you intentionally plan to use wildcard-style routing later.

//Because jobs.create also looks like a topic-style routing key.

//But unless you are using wildcard bindings, it behaves basically like an exact match anyway.

//So if this is a demo jobs pipeline, the most natural guess is:
//direct exchange

//How routing actually happens

//Let’s make it concrete.

//Suppose you declare:

//exchange: demo.jobs.exchange
//type: direct
//queue: demo.jobs.queue
//binding key: jobs.create

//Then producer publishes:

//exchange = demo.jobs.exchange
//routingKey = jobs.create

//RabbitMQ receives the message at the exchange.

//The exchange checks all bindings.

//If it finds a queue bound with:

//jobs.create

//it sends the message there.

//If there is no matching binding, the message goes nowhere.

//That is the routing process.

//One message can go to multiple queues

//Another very important idea:

//An exchange can route one message to more than one queue.

//Example with direct exchange:

//If Queue A and Queue B are both bound with jobs.create, then both receive the message.

//So direct exchange is not always one message to one queue.

//It is:

//one message
//to all queues whose binding key exactly matches

//That is why exchange-based routing is more powerful than “send directly to one queue.”

//Exchange names are just identifiers, but naming matters

//This:

//"demo.jobs.exchange"

//is just a string name.

//RabbitMQ does not care if you call it:

//x1
//main
//demo.jobs.exchange

//But humans do care.

//Good names help you:

//inspect UI faster
//understand topology
//debug easier
//avoid confusion between queue and exchange names

//Your naming convention is actually good because it makes the object’s purpose obvious.

//The deeper architectural value of exchanges

//Exchanges help decouple producer and consumer.

//Without exchanges, a producer might know too much:

//exact queue name
//how many consumers exist
//how routing is organized

//With exchanges, the producer only needs to know:

//exchange name
//routing key

//That means later you can:

//add more queues
//add more consumers
//change bindings
//split processing responsibilities

//without necessarily rewriting producer code.
//That is one of the biggest reasons message brokers are useful in distributed systems.

//Compact mental model
//If we want a short internal picture, use this:

//Producer creates message
//Exchange decides where it should go
//Binding defines routing rule
//Queue stores the message
//Consumer processes it

//And exchange type defines how the decision is made.
//In our class, what does ExchangeName really mean?
//So now, in the fullest sense:

//"public string ExchangeName { get; set; } = "demo.jobs.exchange";" means:
//This application is configured to publish messages into a RabbitMQ exchange named demo.jobs.exchange,
//which acts as the routing hub between producers and queues.The exchange itself does not store messages;
//instead, it uses its configured type and bindings to determine which queue or queues
//should receive each published message.
    public string ExchangeName { get; set; } = "demo.jobs.exchange";
    public string QueueName { get; set; } = "demo.jobs.queue";
    public string RoutingKey { get; set; } = "jobs.create";
}