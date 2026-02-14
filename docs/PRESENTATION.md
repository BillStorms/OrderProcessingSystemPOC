# Order Processing System POC - Executive Walkthrough

**Date:** February 14, 2026  
**Presenter:** [Your Name]  
**Audience:** [Boss/Leadership]

---

## 1. Project Overview (2 min)

### What We Built
A **distributed, event-driven order processing system** demonstrating modern cloud-native patterns:
- **Order Service** (ASP.NET Core API): Accepts orders, persists to SQL Server, publishes Kafka events
- **Fulfillment Worker** (.NET Worker Service): Consumes events, simulates shipping, updates order status asynchronously
- **Infrastructure**: Docker Compose, SQL Server, Kafka, structured logging

### Core Value
End-to-end async workflow with **clear separation of concerns**—order creation doesn't wait for fulfillment, enabling scalable, resilient systems.

**Validation**: Full flow tested—order creation → Kafka publish → worker consumption → status update → success.

---

## 2. Architecture & Key Decisions (3 min)

### Event-Driven Design
```
Order Service                    Kafka Topic                Fulfillment Worker
     │                          "order-events"                    │
     ├─ POST /api/orders ────────────────────────────────────────>│
     │  ├─ Persist to DB                                           │
     │  ├─ Publish OrderCreatedEvent                               │
     │  └─ Return 200 immediately                                  │
     │                                                    ┌─────────┤
     │                                                    │ Consume │
     │                                                    │ Process │
     │                                                    │ Update  │
     │<─────────── PATCH /api/orders/{id}/status ─────────────────┤
     │             (async, eventual consistency)                   │
     └─────────────────────────────────────────────────────────────┘
```

**Why**: Temporal decoupling, independent scaling, clear service boundaries.

---

## 3. Migrations – Data Integrity (3 min)

### What I Built
Comprehensive EF Core migrations that capture the complete schema, ensuring reproducibility across environments.

### Implementation
```csharp
// In 20260210_InitialCreate.cs
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(
        name: "Orders",
        columns: table => new
        {
            OrderId = table.Column<string>(),
            CustomerId = table.Column<string>(),
            CustomerName = table.Column<string>(),
            Status = table.Column<string>(),
            CreatedAt = table.Column<DateTime>(),
            UpdatedAt = table.Column<DateTime>(),
            // Owned entity: FulfillmentDetails
            Fulfillment_Carrier = table.Column<string>(nullable: true),
            Fulfillment_TrackingNumber = table.Column<string>(nullable: true),
            Fulfillment_ShippedAt = table.Column<DateTime>(nullable: true),
            Fulfillment_ErrorMessage = table.Column<string>(nullable: true),
        },
        constraints: table => { table.PrimaryKey("PK_Orders", x => x.OrderId); });
}
```

### Key Features
- **EF Owned Entities**: `FulfillmentDetails` mapped with `Fulfillment_` prefix for table-per-hierarchy pattern
- **Automated on Startup**: Migration runs automatically when app starts via `DatabaseExtensions.MigrateDatabaseAsync()`
- **Fresh DB Ready**: Clean database creation works end-to-end without manual intervention
- **Reproducible**: Schema defined in code, versioned in git, consistent across dev/staging/prod

---

## 4. Idempotency – Preventing Duplicates (3 min)

### The Challenge
Kafka guarantees **at-least-once** delivery. If a worker crashes after consuming an event but before committing the offset, the same event replays when the worker restarts. Without idempotency, this means duplicate shipments, duplicate payments, or duplicate notifications—each time the worker processes the same order.

### Solution: ProcessedEvents Ledger
I built a simple but effective tracking system: a `ProcessedEvents` table that records every event ID we've successfully handled.

**Schema**:
```sql
CREATE TABLE ProcessedEvents (
    EventId NVARCHAR(36) PRIMARY KEY,
    OrderId NVARCHAR(36),
    ProcessedAt DATETIME2,
    UNIQUE(EventId)  -- Database enforces: only one record per EventId
);
```

### The Flow
**Scenario 1: First Time Processing**
```
Worker receives OrderCreatedEvent (EventId: abc-123, OrderId: order-456)
    ↓
Query ProcessedEvents for EventId abc-123
    ↓
Not found → Proceed with processing
    ↓
[TRANSACTION BEGINS]
    ├─ Call _shippingProvider.ProcessShipmentAsync(order-456)
    ├─ Insert into ProcessedEvents (EventId: abc-123, OrderId: order-456, ProcessedAt: NOW)
    ├─ Call UpdateOrderStatusAsync(order-456, "Shipped")
    └─ COMMIT ← All or nothing
    ↓
[TRANSACTION ENDS]
Event marked as processed; order status updated; safe to continue
```

**Scenario 2: Duplicate Event (Worker Crashed & Restarted)**
```
Worker receives OrderCreatedEvent (EventId: abc-123, OrderId: order-456) [AGAIN]
    ↓
Query ProcessedEvents for EventId abc-123
    ↓
Found! → Log warning and exit immediately
    ↓
No shipment created. No status update. Order unaffected.
```

### Code Implementation
```csharp
public async Task ProcessOrderEventAsync(OrderCreatedEvent orderEvent)
{
    // Step 1: Check if this event was already processed
    var isProcessed = await dbContext.ProcessedEvents
        .FirstOrDefaultAsync(e => e.EventId == orderEvent.EventId);
    
    if (isProcessed != null)
    {
        _logger.LogWarning("Event {EventId} already processed at {ProcessedAt}; skipping.", 
            orderEvent.EventId, isProcessed.ProcessedAt);
        return;  // Early exit: idempotent
    }

    // Step 2: Process the shipment
    _logger.LogInformation("Processing event {EventId} for order {OrderId}", 
        orderEvent.EventId, orderEvent.OrderId);
    
    var result = await _shippingProvider.ProcessShipmentAsync(orderEvent.OrderId);

    // Step 3: Atomic write: mark processed + update order status
    using (var tx = await dbContext.Database.BeginTransactionAsync())
    {
        try
        {
            // Insert into ProcessedEvents; if EventId already exists, constraint violation fails the entire transaction
            await dbContext.ProcessedEvents.AddAsync(
                new ProcessedEvent 
                { 
                    EventId = orderEvent.EventId, 
                    OrderId = orderEvent.OrderId, 
                    ProcessedAt = DateTime.UtcNow 
                }
            );
            
            // Update order status with shipment result
            await UpdateOrderStatusAsync(orderEvent.OrderId, result.Status);
            
            // Flush all changes
            await dbContext.SaveChangesAsync();
            
            // Commit: both the ledger entry and status update succeed together
            await tx.CommitAsync();
            
            _logger.LogInformation("Successfully processed and committed event {EventId}", orderEvent.EventId);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint") ?? false)
        {
            // Race condition: another worker processed this event simultaneously
            await tx.RollbackAsync();
            _logger.LogWarning("Event {EventId} was processed by another worker; rolling back.", orderEvent.EventId);
            return;
        }
        catch (Exception)
        {
            // Any other error: rollback everything
            await tx.RollbackAsync();
            throw;
        }
    }
}
```

### How This Enforces Idempotency
1. **Ledger Check**: Before doing expensive work, we ask the database: "Have we seen this EventId before?"
2. **Transactional Boundary**: The "mark as processed" and "update status" happen in a single transaction. Either both succeed, or both roll back. No half-states.
3. **Unique Constraint**: If two workers somehow try to insert the same EventId simultaneously, the database enforces uniqueness—only one wins. The loser gets a constraint violation, rolls back, and logs it.
4. **Replay Safety**: If the worker crashes after the transaction commits but before returning to Kafka, the offset isn't committed. Kafka replays the event. Worker checks the ledger, finds it, and exits safely.

**Result**: Exactly-once semantics in an at-least-once delivery system.

---

## 5. Resiliency – Handling Failures Gracefully (3 min)

### The Challenge
Network calls can fail transiently. The worker's PATCH to update order status must retry intelligently without overwhelming the system.

### What I Implemented
**Polly Retry + Circuit Breaker**:
```csharp
// In FulfillmentWorker startup
var httpClientPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<TimeoutRejectedException>()
    .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => 
            TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100),  // Exponential backoff
        onRetry: (outcome, timespan, retryCount, context) =>
            _logger.LogWarning("Retry {RetryCount} after {Delay}ms", retryCount, timespan.TotalMilliseconds)
    )
    .WrapAsync(
        Policy
            .Handle<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, timespan) =>
                    _logger.LogError("Circuit breaker opened for {Duration}s", timespan.TotalSeconds)
            )
    );

_httpClient = new HttpClient(new PolicyHttpMessageHandler(httpClientPolicy)) 
    { Timeout = TimeSpan.FromSeconds(10) };
```

**Behavior**:
- **Transient failure** (network glitch): Retry up to 3 times with exponential backoff.
- **Repeated failures** (API down): Circuit breaker trips after 5 failures, pauses for 30s, then retries.
- **Logging**: Every attempt logged for observability.

**Dead Letter Queue (Future)**:
```csharp
// If circuit is open, send to DLQ topic for manual inspection
if (circuitBreakerOpen)
{
    await _kafkaProducer.ProduceAsync("order-events-dlq", failedEvent);
    _logger.LogCritical("Sent event {EventId} to DLQ", orderEvent.EventId);
}
```

---

## 6. Observability – Understanding the System (3 min)

### The Challenge
In production, you need visibility: How fast are orders processed? Where are bottlenecks? What's the Kafka consumer lag?

### What I Implemented

**1. Structured Logging**
```csharp
_logger.LogInformation(
    "Processing order {OrderId} with {ItemCount} items. CorrelationId: {CorrelationId}",
    orderEvent.OrderId,
    orderEvent.Items.Count,
    orderEvent.Metadata.CorrelationId
);
```
Output (JSON format in containers):
```json
{
  "timestamp": "2026-02-14T14:30:45Z",
  "level": "Information",
  "message": "Processing order 961b6d44-aadd-4318-8554-ef303d27f48d with 2 items",
  "correlationId": "abc-def-123",
  "service": "FulfillmentService"
}
```

**2. Health Check Endpoints**
```csharp
// In Program.cs
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("ready"),
});

services.AddHealthChecks()
    .AddCheck("Kafka", () => 
        _consumer != null && _consumer.Committed(timeout: 5000).Count > 0 
            ? HealthCheckResult.Healthy() 
            : HealthCheckResult.Unhealthy("No partition assignment"));
```
Kubernetes/orchestrators can use `/health/ready` to detect and restart unhealthy workers.

**3. Metrics (Ready for New Relic Export)**
```csharp
public class MetricsCollector
{
    private readonly Counter _ordersProcessed;
    private readonly Histogram _processingDuration;
    private readonly Gauge _consumerLag;

    public MetricsCollector(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("FulfillmentService");
        _ordersProcessed = meter.CreateCounter<long>("orders.processed");
        _processingDuration = meter.CreateHistogram<double>("order.processing.duration.ms");
        _consumerLag = meter.CreateGauge<long>("kafka.consumer.lag");
    }

    public void RecordProcessed(double durationMs)
    {
        _ordersProcessed.Add(1);
        _processingDuration.Record(durationMs);
    }
}
```
These metrics export to New Relic via OpenTelemetry for real-time dashboards, alerting, and trend analysis.

**4. Distributed Tracing (Ready for OpenTelemetry)**
```csharp
// Associate every log/metric with the order's CorrelationId
using (LogContext.PushProperty("CorrelationId", orderEvent.Metadata.CorrelationId))
{
    // All logs here carry CorrelationId automatically
    _logger.LogInformation("Order processing started");
    await ProcessOrderAsync(orderEvent);
    _logger.LogInformation("Order processing completed");
}
```
Tracing tools like New Relic can follow an order through both Order Service and Fulfillment Worker using the same CorrelationId.

---

## 7. Testing & Validation (2 min)

### End-to-End Flow Validated
```bash
# 1. Create order
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"test-1","customerName":"Test","items":[{"productId":"sku-1","quantity":1}]}'

# Response: HTTP 200, orderId: 961b6d44-aadd-4318-8554-ef303d27f48d

# 2. Check logs
docker compose logs fulfillment-service-worker
# Output: "Processing order 961b6d44-aadd-4318-8554-ef303d27f48d..."
# Output: "Successfully updated order status to Shipped"

# 3. Verify final state
curl http://localhost:5000/api/orders/961b6d44-aadd-4318-8554-ef303d27f48d
# Response: { orderId, status: "Shipped", fulfillment: { trackingNumber, carrier, shippedAt } }
```

**Build & Infrastructure**:
- Solution builds cleanly: 0 warnings, 0 errors
- All 5 Docker containers run healthy
- No hardcoded credentials (secure for GitHub)

---

## 8. Key Learnings & Takeaways (2 min)

| Concept | Learning | Applied |
|---------|----------|---------|
| **Migrations** | Schema must match code; test fresh DBs | EF migrations include all columns; automate on startup |
| **Idempotency** | At-least-once delivery needs exactly-once logic | Unique `EventId` constraint + transactions prevent duplicates |
| **Resiliency** | Networks fail; fail gracefully | Polly retries + circuit breaker with exponential backoff; DLQ ready |
| **Observability** | Black boxes fail silently | Structured logs + health checks + metrics + correlation IDs |

---

## 9. Next Steps (1 min)

**Immediate (Week 1-2)**:
- [ ] Configure New Relic exporter for metrics and traces
- [ ] Create New Relic dashboard: orders processed, processing duration, consumer lag
- [ ] Set up New Relic alerts for error rates and processing latency

**Short-term (Week 2-4)**:
- [ ] Implement event versioning for schema evolution
- [ ] Add automated DLQ processor
- [ ] Deploy to staging cluster; validate under load

**Medium-term (Month 2)**:
- [ ] Multi-region deployment with cross-region Kafka replication
- [ ] Saga pattern for multi-order transactions
- [ ] Comprehensive alerting (PagerDuty integration)

---

## 10. Q&A

**Q: What if an order fails and we need to retry the fulfillment?**  
A: Circuit breaker pauses retries temporarily; DLQ holds failed events for manual inspection. We can replay DLQ events after fixing the underlying issue.

**Q: How do we scale this to 10,000 orders/day?**  
A: Kafka partitions enable horizontal scaling—add more worker instances, they auto-rebalance partitions. DB connection pooling handles concurrency.

**Q: Is this production-ready?**  
A: Not yet. We need metrics + alerting, DLQ automation, and load testing. Roadmap shows those in Weeks 2-4.

---

*End of Presentation*
