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
  
---
# System Components
This document describes the key components of the Order Processing System POC.

## Components

### 1. Order Service
**Type**: Microservice (ASP.NET Core Web API)

**Responsibilities**:
- Expose REST API endpoints for order management
- Validate incoming order requests
- Generate unique order IDs
- Publish order events to Kafka
- Maintain in-memory order state
- Provide order status query endpoint

**Technology**:
- ASP.NET Core 8.0 Web API
- Confluent.Kafka (Kafka producer client)
- Hosted on port 5001

**API Endpoints**:
- `POST /api/orders` - Create new order
- `GET /api/orders/{id}` - Get order status

**Event Publishing**:
- Topic: `order-events`
- Event Type: `OrderCreated`
- Format: JSON

---

### 2. Fulfillment Service
**Type**: Microservice (Background Worker Service)

**Responsibilities**:
- Consume order events from Kafka
- Process orders asynchronously
- Integrate with mock third-party shipping provider
- Update order fulfillment status
- Expose REST API for health checks and metrics

**Technology**:
- .NET 8.0 Worker Service
- ASP.NET Core 8.0 Web API (for status endpoint)
- Confluent.Kafka (Kafka consumer client)
- Hosted on port 5002

**Event Consumption**:
- Topic: `order-events`
- Consumer Group: `fulfillment-service`
- Auto-commit: false (manual commit after processing)

**API Endpoints**:
- `GET /api/fulfillment/health` - Health check
- `GET /api/fulfillment/orders/{id}` - Get fulfillment status

---

### 3. Apache Kafka
**Type**: Message Broker

**Responsibilities**:
- Reliable message delivery between services
- Event persistence and replay capability
- Decoupling of producer and consumer services

**Configuration**:
- Broker: localhost:9092
- Topics: `order-events` (1 partition, replication factor 1)
- Message retention: 7 days
- Cleanup policy: delete

---

### 4. Apache Zookeeper
**Type**: Coordination Service

**Responsibilities**:
- Kafka cluster coordination
- Metadata management
- Leader election for Kafka partitions

**Configuration**:
- Port: 2181
- Data directory: /var/lib/zookeeper

---

### 5. Mock Shipping Provider
**Type**: Simulated External Dependency

**Responsibilities**:
- Simulate third-party shipping API
- Return mock tracking numbers
- Simulate API latency and occasional failures

**Implementation**:
- Embedded in Fulfillment Service
- Simulated HTTP delay (100-500ms)
- 10% random failure rate to test resilience

**Mock API Response**:
```json
{
  "trackingNumber": "SHIP-{random}",
  "estimatedDelivery": "2024-01-25T10:00:00Z",
  "carrier": "MockCarrier",
  "status": "confirmed"
}
```

---

## Data Models

### Order
```json
{
  "orderId": "string (UUID)",
  "customerId": "string",
  "customerName": "string",
  "items": [
    {
      "productId": "string",
      "productName": "string",
      "quantity": "integer",
      "price": "decimal"
    }
  ],
  "totalAmount": "decimal",
  "status": "string (Pending/Processing/Fulfilled/Failed)",
  "createdAt": "datetime",
  "updatedAt": "datetime"
}
```

### OrderCreatedEvent
```json
{
  "eventId": "string (UUID)",
  "eventType": "OrderCreated",
  "timestamp": "datetime",
  "orderId": "string (UUID)",
  "customerId": "string",
  "customerName": "string",
  "items": "array",
  "totalAmount": "decimal"
}
```

### FulfillmentStatus
```json
{
  "orderId": "string (UUID)",
  "status": "string (Pending/Shipped/Delivered/Failed)",
  "trackingNumber": "string",
  "carrier": "string",
  "estimatedDelivery": "datetime",
  "updatedAt": "datetime"
}
```

---
- Zookeeper for Kafka coordination
- Docker Compose for local development
