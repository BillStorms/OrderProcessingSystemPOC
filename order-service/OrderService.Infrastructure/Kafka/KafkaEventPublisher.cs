using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using OrderService.Application.Events;
using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.Kafka;

public class KafkaEventPublisher : IEventPublisher
{
  private readonly IProducer<string, string> _producer;
  private readonly string _topic;

  public KafkaEventPublisher(IConfiguration config)
  {
    var settings = new ProducerConfig
    {
      BootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092"
    };

    _producer = new ProducerBuilder<string, string>(settings).Build();
    _topic = config["Kafka:Topic"] ?? "order-events";
  }

  public async Task PublishOrderCreatedAsync(OrderCreatedEvent evt)
  {
    var json = JsonSerializer.Serialize(evt);

    await _producer.ProduceAsync(_topic, new Message<string, string>
    {
      Key = evt.OrderId,
      Value = json
    });
  }
}
