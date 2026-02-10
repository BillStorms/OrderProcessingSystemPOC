# Order Processing System POC - Project Status

## Executive Summary

**Status:** Core infrastructure operational; end-to-end order fulfillment flow verified ✅

We've successfully built a distributed order processing system demonstrating event-driven architecture with two microservices communicating via Apache Kafka. The system handles order creation, publishes domain events, processes asynchronous fulfillment workflows, and updates order status—all running in Docker Compose with SQL Server persistence.

**Key Achievement:** Full request-response cycle validated: API accepts orders → persists to database → publishes Kafka events → worker consumes events → simulates shipping → updates order status asynchronously.

---

## Implemented Components

### 1. Order Service (API + Domain + Infrastructure)
**Technology:** ASP.NET Core 8, Entity Framework Core, SQL Server, Kafka Producer

**Completed:**
- ✅ Clean architecture with separation of concerns (Api/Application/Domain/Infrastructure layers)
- ✅ RESTful endpoints: `POST /api/orders`, `PATCH /api/orders/{id}/status`
- ✅ Domain modeling with aggregate roots (`Order`, `OrderItem`, `FulfillmentDetails` as owned entities)
- ✅ SQL Server persistence with EF Core including owned entity table-per-hierarchy mapping
- ✅ Kafka event publishing (`OrderCreatedEvent`) on successful order creation
- ✅ Structured JSON logging with Serilog (correlation IDs, trace context)
- ✅ Global exception handling middleware
- ✅ Health checks (TCP-based readiness probes)

**Architectural Patterns Demonstrated:**
- **Event-Driven Design:** Order creation triggers asynchronous `OrderCreatedEvent` published to Kafka topic `order-events`
- **System Boundaries:** Clear separation between order management (write) and fulfillment orchestration (async processing)
- **Async Workflows:** Fire-and-forget event publishing; API responds immediately without waiting for fulfillment

### 2. Fulfillment Service (Background Worker)
**Technology:** .NET 8 Worker Service, Kafka Consumer, HttpClient

**Completed:**
- ✅ Long-running background worker consuming from Kafka topic
- ✅ Event deserialization and processing (`OrderCreatedEvent` → fulfillment workflow)
- ✅ Mock shipping provider simulating external API calls (configurable delays, failure injection)
- ✅ Order status updates via HTTP PATCH to Order Service API
- ✅ Offset commit management ensuring at-least-once delivery semantics
- ✅ Structured logging with correlation ID propagation

**Architectural Patterns Demonstrated:**
- **Event-Driven Design:** Consumer subscribes to domain events; processing decoupled from order creation
- **Async Workflows:** Simulated shipping delay (1-5 seconds) + non-blocking HTTP calls to update order state
- **Failure Modes:** Configurable failure rate (15%) testing error scenarios; resilient consumption (continues on processing errors)
- **Operational Concerns:** 
  - Consumer group (`fulfillment-service-group`) enables horizontal scaling
  - Manual offset commits ensure message durability
  - Restart policies (`restart: on-failure`) handle transient crashes

### 3. Infrastructure & DevOps
**Technology:** Docker, Docker Compose, SQL Server 2022, Apache Kafka (Confluent), Zookeeper

**Completed:**
- ✅ Multi-stage Dockerfiles for both services (build isolation, minimal runtime images)
- ✅ Docker Compose orchestration with service dependencies and health checks
- ✅ SQL Server containerized with persistent volumes (`sqlserver-data`)
- ✅ Kafka + Zookeeper cluster configuration (single-broker, auto-topic creation enabled)
- ✅ Network isolation (`order-processing-network` bridge network)
- ✅ Centralized NuGet package management (`Directory.Packages.props`, `Directory.Build.props`)
- ✅ `.dockerignore` for clean Docker build contexts

**Architectural Patterns Demonstrated:**
- **System Boundaries:** Each service runs in isolated containers; communication via defined contracts (HTTP REST, Kafka messages)
- **Operational Concerns:**
  - Health checks with `depends_on: service_healthy` ensure startup ordering
  - Configurable retry limits and timeouts (`retries: 20`, `timeout: 30s`)
  - Volume mounts for database durability across container restarts

---

## Current State & Known Issues

### ✅ Working
1. **Order Creation Flow:** POST request → DB insert → Kafka publish → 200 response
2. **Event Consumption:** Worker polls Kafka, deserializes events, processes orders
3. **Status Updates:** Worker successfully PATCHes order status back to API (204 responses observed)
4. **Persistence:** Orders stored in SQL Server `OrderServiceDb` with relational integrity

### ⚠️ Schema Evolution Issue (Resolved with Manual Patch)
- **Root Cause:** EF migration file existed but `Fulfillment_*` columns were not applied to the live database
- **Temporary Fix:** Manually added four columns via SQL:
  ```sql
  ALTER TABLE Orders ADD 
    Fulfillment_Carrier NVARCHAR(200) NULL,
    Fulfillment_TrackingNumber NVARCHAR(200) NULL,
    Fulfillment_ShippedAt DATETIME2 NULL,
    Fulfillment_ErrorMessage NVARCHAR(MAX) NULL;
  ```
- **Impact:** Fresh database instances will fail without this manual step

### 🔧 Technical Debt
1. **Migration Management:** Need to consolidate EF migrations or ensure `20260210_InitialCreate` properly includes fulfillment columns in the `Up()` method
2. **Owned Entity Warning:** EF logs warning about `FulfillmentDetails` lacking required properties (may return null if all columns null)
3. **HTTPS Redirection Warning:** `HttpsRedirectionMiddleware` unable to determine HTTPS port in containerized environment (cosmetic; app uses HTTP-only)

---

## Event-Driven Architecture Deep Dive

### Design Principles in Action

#### 1. **Event-Driven Design**
- **Publisher:** `OrderManagementService.CreateOrderAsync()` produces `OrderCreatedEvent` after DB commit
- **Message Broker:** Kafka acts as durable event log with configurable retention
- **Subscriber:** `FulfillmentWorker.ExecuteAsync()` consumes events in a polling loop
- **Decoupling:** Order Service has zero knowledge of fulfillment logic; services communicate via immutable events

**Code Evidence:**
```csharp
// Order Service: Fire-and-forget publish
await _kafkaProducer.ProduceAsync(_topic, new Message<string, string> {
    Key = order.OrderId,
    Value = JsonSerializer.Serialize(orderEvent)
});

// Fulfillment Worker: Event-driven processing
var result = _consumer.Consume(timeout);
var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(result.Message.Value);
await ProcessOrderAsync(orderEvent);
```

#### 2. **Async Workflows**
- **Non-Blocking API:** Order creation returns immediately after persisting + publishing; does not wait for shipment
- **Background Processing:** Worker executes fulfillment logic asynchronously (simulated 1-5s delay)
- **Eventual Consistency:** Order status transitions from `Created` → `Processing` → `Shipped` over time

**Latency Profile:**
- Order creation API response: ~1s (includes DB write + Kafka publish)
- Fulfillment processing: 1-5s simulated shipping delay + HTTP PATCH (<100ms)
- Total order-to-shipped latency: 2-6 seconds (acceptable for non-critical fulfillment)

#### 3. **Failure Modes**
**Implemented Resilience Patterns:**
- **Producer:** Kafka client automatically retries failed publishes (default: `retries=MAX_INT`)
- **Consumer:** Manual offset commit only after successful processing; failures result in message replay
- **Idempotency Gap:** ⚠️ No deduplication logic—duplicate Kafka messages would create duplicate shipments (future improvement: idempotency keys)

**Failure Injection:**
```csharp
// MockShippingProvider simulates 15% failure rate
if (_random.NextDouble() < _failureRate) {
    return new ShipmentResult { 
        Success = false, 
        ErrorMessage = "Simulated carrier rejection" 
    };
}
```

**Observed Behavior:**
- Failed shipments still update order status to `Failed` with error message
- Worker continues processing subsequent messages despite individual failures
- No automatic retries for failed shipments (acceptable for POC; production would use dead-letter queues)

#### 4. **System Boundaries**
**Service Ownership:**
- **Order Service:** Owns order lifecycle, customer data, order items (bounded context: Order Management)
- **Fulfillment Service:** Owns shipment orchestration, carrier integration (bounded context: Fulfillment)

**Communication Contracts:**
- **Async (Kafka):** `OrderCreatedEvent` schema defines the event contract
  ```json
  {
    "eventType": "OrderCreated",
    "orderId": "guid",
    "customer": { "customerId": "string", "name": "string" },
    "items": [{ "productId": "string", "quantity": int }],
    "createdAt": "ISO8601",
    "metadata": { "source": "OrderService", "correlationId": "guid" }
  }
  ```
- **Sync (HTTP):** `PATCH /api/orders/{id}/status` request DTO defines the update contract

**Boundary Enforcement:**
- Fulfillment service does NOT directly access order database (zero shared state)
- All state mutations flow through well-defined API endpoints
- Kafka topic acts as the integration layer preventing tight coupling

#### 5. **Operational Concerns**

**Scalability:**
- **Horizontal Scaling Ready:** Kafka consumer group allows multiple worker instances to share partition load
- **Current Config:** Single partition (implicit); multi-partition topic would enable parallel processing
- **Database Connection Pooling:** EF Core manages connection pool (default: shared pool across requests)

**Latency:**
- **API P99:** ~1s (observed from logs: `Elapsed: 1018ms`)
- **Consumer Lag:** Near-zero (worker processes messages within seconds of publish)
- **Network Latency:** Intra-Docker-network HTTP calls <10ms

**Retries:**
- **Kafka Producer:** Automatic retries with exponential backoff (Confluent client defaults)
- **HTTP Client:** ⚠️ No retry policy configured (future: Polly for transient fault handling)
- **Consumer:** At-least-once delivery via manual offset commits after processing

**Monitoring Gaps (Future Work):**
- No metrics export (Prometheus, Application Insights)
- No distributed tracing (OpenTelemetry)
- No consumer lag alerts

---

## Next Steps (Priority Order)

### 1. **Fix Migration Management** (HIGH PRIORITY)
**Goal:** Ensure fresh database instances work without manual SQL

**Actions:**
- [ ] Update existing migration file to include `Fulfillment_*` columns in `Up()` method
- [ ] OR: Generate new migration capturing current schema delta
- [ ] Test migration on clean database: `docker compose down -v && docker compose up`
- [ ] Document migration workflow in README (local dev: `dotnet ef migrations add`, production: automated on startup)

### 2. **Add Idempotency** (MEDIUM PRIORITY)
**Goal:** Prevent duplicate message processing

**Actions:**
- [ ] Add `ProcessedEventIds` table to track consumed event IDs
- [ ] Check for duplicate `OrderId` + `CorrelationId` before processing
- [ ] Return early if event already processed (skip shipment creation)

### 3. **Implement Retry Policies** (MEDIUM PRIORITY)
**Goal:** Handle transient failures gracefully

**Actions:**
- [ ] Install Polly NuGet package (`Microsoft.Extensions.Http.Polly`)
- [ ] Configure HTTP client with exponential backoff retry policy (3 retries, 2^attempt * 100ms delay)
- [ ] Add circuit breaker for Order Service API calls (open after 5 consecutive failures)

### 4. **Add Observability** (MEDIUM PRIORITY)
**Goal:** Production-ready monitoring and diagnostics

**Actions:**
- [ ] Add OpenTelemetry SDK for distributed tracing (trace order flow across services)
- [ ] Export metrics to Prometheus (Kafka consumer lag, HTTP request duration, DB query time)
- [ ] Add health check endpoint exposing Kafka consumer status
- [ ] Integrate Application Insights or Seq for centralized logging

### 5. **Schema Evolution Hardening** (LOW PRIORITY)
**Goal:** Support backward-compatible event schema changes

**Actions:**
- [ ] Add `schemaVersion` field to `OrderCreatedEvent`
- [ ] Implement versioned event deserializers (handle old + new schema formats)
- [ ] Document breaking vs. non-breaking changes policy

### 6. **Load Testing** (LOW PRIORITY)
**Goal:** Validate scalability assumptions

**Actions:**
- [ ] Use Apache Bench or k6 to simulate 100 req/s order creation
- [ ] Scale fulfillment worker to 3 instances, observe partition rebalancing
- [ ] Measure Kafka throughput and consumer lag under load

---

## Coding Concepts Reinforced

### ✅ Successfully Demonstrated
1. **Event-Driven Design:** Kafka-based pub/sub enabling temporal decoupling
2. **Async Workflows:** Fire-and-forget publishing, background processing, eventual consistency
3. **System Boundaries:** Microservices with clear ownership, contract-based integration
4. **Operational Concerns:** Health checks, structured logging, container orchestration, restart policies

### 🔄 Partially Implemented
1. **Failure Modes:** Simulated failures present, but retry logic incomplete (no Polly policies)
2. **Operational Concerns:** Basic monitoring (logs), but missing metrics/tracing/alerting

### ⏳ Not Yet Addressed
1. **Advanced Failure Handling:** Dead-letter queues, compensating transactions, saga patterns
2. **Security:** Authentication, authorization, message encryption (TLS)
3. **Multi-Environment Config:** Separate dev/staging/prod configurations

---

## Conclusion

The Order Processing System POC successfully demonstrates core distributed systems concepts in a working end-to-end implementation. The architecture is event-driven, resilient to basic failures, and operationally viable for development environments. 

**Primary value delivered:** A concrete, hands-on example of how microservices communicate asynchronously via message brokers, handle eventual consistency, and maintain clear service boundaries—all critical patterns for scalable, modern distributed systems.

**Recommended next action:** Consolidate EF migrations to ensure reproducible database setup, then proceed with idempotency and retry policies to harden production readiness.

---

*Last Updated: February 10, 2026*  
*Document Version: 1.0*
