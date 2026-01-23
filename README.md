# OrderProcessingSystemPOC
System name: OrderProcessingSystemPOC
Goal: Demonstrate an event-driven, scalable order processing flow using .NET 8, Kafka, and REST APIs.

The system consists of two main microservices:

Order Service: Synchronous API for order creation and status queries; publishes order events to Kafka.

Fulfillment Service: Asynchronous consumer of order events; integrates with a mocked shipping provider and updates order status.

Both services run in Docker and communicate via Kafka and HTTP.

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

<img width="2120" height="3269" alt="OrderSystem_HLD" src="https://github.com/user-attachments/assets/a6e491b5-b5b0-495b-a37f-d3bd1ae2b711" />

## Component design

### Order service

**Responsibilities:**

- **Order Creation API**
  - **Endpoint:** `POST /api/orders`
  - **Behavior:**
    - Validate payload (customer info, items, quantities).
    - Generate unique `OrderId` (e.g., GUID).
    - Persist initial order record with status `Created` / `Pending`.
    - Publish `OrderCreated` event to Kafka topic `order-events`.
    - Return `201 Created` with `OrderId` and initial status.
  - **Performance target:** Respond within **500 ms (P95)** by:
    - Keeping Kafka publish non-blocking (with short timeout).
    - Avoiding any long-running fulfillment logic in this path.

- **Order Status API**
  - **Endpoint:** `GET /api/orders/{orderId}`
  - **Behavior:**
    - Fetch order from the order store.
    - Return current status and fulfillment details (e.g., shipping tracking, timestamps, error info).

**Internal components:**

- **API Layer (ASP.NET Core):** Controllers, request/response models, validation.
- **Application Layer:**
  - **OrderService** (domain logic: create order, query status).
  - **EventPublisher** (Kafka producer abstraction).
- **Persistence Layer:**
  - Repository for orders (e.g., SQL Server/Postgres or simple in-memory/SQLite for POC).
- **Integration Layer:**
  - Kafka producer client with:
    - Configurable retries.
    - Reasonable timeouts.
    - Circuit breaker/fallback logging when Kafka is unavailable.

---

### Fulfillment service

**Responsibilities:**

- **Kafka Consumer**
  - Subscribes to `order-events` topic.
  - Consumes `OrderCreated` events.
  - Processes messages asynchronously with consumer group for scalability.
  - Ensures at-least-once processing (idempotent updates in order store).

- **Order Fulfillment Workflow**
  - Steps:
    1. Parse `OrderCreated` event.
    2. Load or create fulfillment record for the order.
    3. Call mocked third-party shipping provider.
    4. Update order status (`Processing` → `Shipped` / `Failed`).
    5. Optionally publish `OrderFulfilled` event (future extension).

- **Third-party shipping integration**
  - HTTP client with:
    - **Timeout:** 10 seconds.
    - Retry policy with backoff for transient failures.
    - Clear error handling and logging.
  - Failures **must not** block order acceptance:
    - Orders remain in `Pending`/`Retrying` state.
    - Retries scheduled or re-processed via Kafka.

**Internal components:**

- **Worker/Background Service:**
  - .NET worker service hosting Kafka consumer loop.
- **Fulfillment Orchestrator:**
  - Encapsulates business logic for fulfillment and status transitions.
- **ShippingClient (Mock):**
  - Simulates external API with configurable latency/failure.
- **Persistence:**
  - Shared order store or dedicated fulfillment store (for POC, can reuse same DB).

**Performance target:**

- Orders should be picked up within **5 seconds**:
  - Kafka consumer poll interval tuned appropriately.
  - Sufficient consumer instances for load.
  - Use Kafka buffering to absorb spikes.

---

## Data model and event schema

### Order domain model (simplified)

- **Order**
  - **OrderId:** string (GUID)
  - **CustomerId:** string
  - **CustomerName:** string
  - **Items:** collection of `OrderItem`
  - **Status:** enum (`Created`, `Pending`, `Processing`, `Shipped`, `Failed`)
  - **CreatedAt / UpdatedAt:** timestamps
  - **FulfillmentDetails:** tracking number, shipping status, error messages

- **OrderItem**
  - **ProductId:** string
  - **Quantity:** int
  - **UnitPrice:** decimal 

### Kafka event schema

**Topic:** `order-events`  
**Key:** `OrderId`  
**Value (JSON):**

```json
{
  "eventType": "OrderCreated",
  "orderId": "string",
  "customer": {
    "customerId": "string",
    "name": "string"
  },
  "items": [
    {
      "productId": "string",
      "quantity": 1
    }
  ],
  "createdAt": "2025-01-20T21:09:00Z",
  "metadata": {
    "source": "OrderService",
    "correlationId": "string"
  }
}
```
Design notes:

- **Well-defined schema:** Versioned via `eventType` and optional `schemaVersion`.
- **Loose coupling:** Fulfillment service only depends on event schema, not on Order service internals.
- **Extensibility:** Future events like `OrderFulfilled`, `OrderFailed` can reuse the same topic or separate topics.

---

## Cross-cutting concerns and NFR handling

### Event-driven architecture

- **Asynchronous communication:** All fulfillment logic is triggered by Kafka events, not synchronous API calls.
- **Loose coupling:** Order service doesn’t know about fulfillment implementation; it only publishes events.
- **Schema governance:** Event contracts defined in a shared library or OpenAPI/JSON schema for both services.

---

### Scalability

**Independent scaling:**

- Scale **Order Service** instances behind a load balancer for API throughput.
- Scale **Fulfillment Service** instances (consumer group) to increase parallel processing.

**Kafka buffering:**

- During spikes, Kafka topic stores events; fulfillment catches up as capacity allows.

---

### Resilience

**Kafka failures:**

**Order service:**

- On publish failure, log and optionally mark order as `Pending` with `PublishFailed` flag.
- Use retry with backoff.
  
**Fulfillment service:**

- Consumer retries on transient errors.
- On persistent failures, park messages (DLQ pattern for future extension).

**Third-party failures:**

- Do not block order creation.

**Fulfillment service:**

- Retries shipping calls with backoff.
- If still failing, set status to `Failed` or `Retrying` and log.

---

### Performance

**Order creation (≤ 500 ms P95):**

- Minimal synchronous work: validation, DB insert, Kafka publish.
- No external HTTP calls in this path.

**Fulfillment pickup (≤ 5 seconds):**

- Kafka consumer with short poll interval.
- Adequate consumer instances and partitions.

**Shipping timeout (10 seconds):**

- HTTP client timeout set to 10 seconds.
- Circuit breaker to avoid cascading failures.

---

### Observability

**Logging:**

- Structured logs for each major step (order created, event published, event consumed, shipping called, status updated).

**Metrics (POC-level):**

- Order creation latency.
- Time from `OrderCreated` event to `Shipped`.
- Kafka consumer lag.

---

## Deployment and infrastructure

### Docker and local environment

Docker Compose orchestrates:

- **Order Service** container.
- **Fulfillment Service** container.
- **Kafka + Zookeeper** containers.
- Optional **DB** container (SQL Server, Postgres, or lightweight alternative).

---

## Configuration

### Environment variables:

- Kafka bootstrap servers.
- Topic name (`order-events`).
- DB connection strings.
- Shipping provider base URL and timeout.

### Profiles:

- **Local POC profile** with mocked shipping and simple DB.
- **Future profiles:** dev/test/prod with real integrations and scaling.
