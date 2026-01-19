# OrderProcessingSystemPOC
A simple order processing system. POC Design

## Functional Requirements

### Order Creation
- The system shall accept order creation requests via REST API
- The system shall validate order data (customer info, items, quantities)
- The system shall assign a unique order ID to each order
- The system shall publish order events to Kafka when orders are created

### Order Fulfillment
- The system shall consume order events from Kafka
- The system shall process orders asynchronously
- The system shall integrate with a third-party shipping provider (mocked)
- The system shall update order status based on fulfillment progress

### Order Status Queries
- The system shall provide REST API to query order status
- The system shall return current order state and fulfillment details

## Non-Functional Requirements

### Event-Driven Architecture
- Services shall communicate asynchronously via Kafka
- Services shall be loosely coupled
- Event schema shall be well-defined

### Scalability
- Services shall be independently scalable
- Kafka shall handle message buffering during load spikes

### Resilience
- Services shall handle Kafka connection failures gracefully
- Third-party integration failures shall not block order acceptance
- Services shall implement retry mechanisms

### Performance SLA
- Order creation API shall respond within 500ms (P95)
- Orders shall be picked up by fulfillment service within 5 seconds
- Third-party shipping integration shall timeout after 10 seconds

## Technical Requirements

### Technology Stack
- **Language**: .NET 8.0 (C#)
- **Messaging**: Apache Kafka
- **API Framework**: ASP.NET Core Web API
- **Serialization**: JSON
- **Container Platform**: Docker

### Microservices
1. **Order Service**: Accepts orders via REST API, publishes to Kafka
2. **Fulfillment Service**: Consumes orders from Kafka, integrates with shipping provider

### Infrastructure
- Kafka cluster with at least 1 topic: `order-events`
- Zookeeper for Kafka coordination
- Docker Compose for local development
