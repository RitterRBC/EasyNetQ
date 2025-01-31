using EasyNetQ.Consumer;
using EasyNetQ.Logging;
using RabbitMQ.Client;

namespace EasyNetQ.Tests.HandlerRunnerTests;

public class When_a_user_handler_is_executed
{
    public When_a_user_handler_is_executed()
    {
        var consumerErrorStrategy = Substitute.For<IConsumerErrorStrategy>();

        var handlerRunner = new HandlerRunner(Substitute.For<ILogger<IHandlerRunner>>(), consumerErrorStrategy);

        var consumer = Substitute.For<IBasicConsumer>();
        channel = Substitute.For<IModel, IRecoverable>();
        consumer.Model.Returns(channel);

        var context = new ConsumerExecutionContext(
            (body, properties, info, _) =>
            {
                deliveredBody = body;
                deliveredProperties = properties;
                deliveredInfo = info;
                return Task.FromResult(AckStrategies.Ack);
            },
            messageInfo,
            messageProperties,
            messageBody
        );

        var handlerTask = handlerRunner.InvokeUserMessageHandlerAsync(context, default)
            .ContinueWith(async x =>
            {
                var ackStrategy = await x;
                return ackStrategy(channel, 42);
            }, TaskContinuationOptions.ExecuteSynchronously)
            .Unwrap();

        if (!handlerTask.Wait(5000))
        {
            throw new TimeoutException();
        }
    }

    private ReadOnlyMemory<byte> deliveredBody;
    private MessageProperties deliveredProperties;
    private MessageReceivedInfo deliveredInfo;

    private readonly MessageProperties messageProperties = new()
    {
        CorrelationId = "correlation_id"
    };

    private readonly MessageReceivedInfo messageInfo = new("consumer_tag", 42, false, "exchange", "routingKey", "queue");
    private readonly byte[] messageBody = Array.Empty<byte>();

    private readonly IModel channel;

    [Fact]
    public void Should_ACK()
    {
        channel.Received().BasicAck(42, false);
    }

    [Fact]
    public void Should_deliver_body()
    {
        deliveredBody.ToArray().Should().BeEquivalentTo(messageBody);
    }

    [Fact]
    public void Should_deliver_info()
    {
        deliveredInfo.Should().BeEquivalentTo(messageInfo);
    }

    [Fact]
    public void Should_deliver_properties()
    {
        deliveredProperties.Should().BeEquivalentTo(messageProperties);
    }
}
