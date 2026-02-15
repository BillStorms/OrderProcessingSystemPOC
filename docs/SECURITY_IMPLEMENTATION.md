# Security Implementation Guide

## Overview

This document outlines the security features implemented in the Order Processing System API.

## 🔒 Implemented Security Features

### 1. Input Validation

**Location:** `OrderService.Service/DTOs/`

All DTOs now include validation attributes:

```csharp
[Required(ErrorMessage = "CustomerId is required")]
[StringLength(100, MinimumLength = 1)]
public string CustomerId { get; set; }

[Range(1, 10000, ErrorMessage = "Quantity must be between 1 and 10000")]
public int Quantity { get; set; }
```

**Validation Rules:**
- Customer ID: Required, 1-100 characters
- Customer Name: Required, 1-200 characters
- Items: At least 1 item required
- Product ID: Required, 1-100 characters
- Quantity: 1-10,000 range

**Testing:**
```bash
# Invalid request (empty items)
curl -X POST http://localhost:8080/api/v1/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"CUST-001","customerName":"Test","items":[]}'
# Returns: 400 Bad Request with validation errors
```

---

### 2. Rate Limiting

**Configuration:** `appsettings.RateLimit.json`

**Default Limits:**
- 10 requests per second
- 100 requests per minute
- 1000 requests per hour

**Whitelisted Endpoints:**
- `/health` (health checks exempt from rate limiting)

**Response:**
- HTTP 429 (Too Many Requests) when limit exceeded
- `Retry-After` header included

**Testing:**
```bash
# Test rate limiting
for i in {1..15}; do 
  curl -w "%{http_code}\n" http://localhost:8080/api/v1/orders/test-id
done
# First 10 return 200/404, remaining return 429
```

**Configuration Override:**
```json
{
  "IpRateLimiting": {
    "GeneralRules": [
      {
        "Endpoint": "POST:/api/*/orders",
        "Period": "1m",
        "Limit": 50
      }
    ]
  }
}
```

---

### 3. Security Headers

**Middleware:** `OrderService.Api/Middleware/SecurityHeadersMiddleware.cs`

**Headers Applied:**

| Header | Value | Purpose |
|--------|-------|---------|
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` | Force HTTPS for 1 year |
| `X-Content-Type-Options` | `nosniff` | Prevent MIME sniffing |
| `X-Frame-Options` | `DENY` | Prevent clickjacking |
| `X-XSS-Protection` | `1; mode=block` | Enable XSS filter |
| `Content-Security-Policy` | `default-src 'self'; ...` | Mitigate XSS/injection |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Control referrer info |
| `Permissions-Policy` | `geolocation=(), camera=(), ...` | Disable browser features |

**Verification:**
```bash
curl -I http://localhost:8080/api/v1/orders/test
# Check response headers
```

---

### 4. CORS Policy

**Configuration:** `appsettings.json`

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:5173",
      "http://localhost:8080"
    ]
  }
}
```

**Policy Features:**
- Explicit origin whitelist
- Credentials allowed
- All methods permitted
- All headers permitted

**Production Recommendations:**
- Use specific origins (no wildcards)
- Disable credentials if not needed
- Restrict methods to required only
- Implement origin validation

---

### 5. API Versioning

**Implementation:** URL segment versioning with fallback options

**Version Readers:**
1. **URL Segment** (Primary): `/api/v1/orders`
2. **Header**: `X-Api-Version: 1.0`
3. **Query String**: `?api-version=1.0`

**Default Version:** 1.0

**Example Requests:**
```bash
# URL versioning (recommended)
curl http://localhost:8080/api/v1/orders

# Header versioning
curl http://localhost:8080/api/orders \
  -H "X-Api-Version: 1.0"

# Query string versioning
curl http://localhost:8080/api/orders?api-version=1.0
```

**Adding a New Version:**
```csharp
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/orders")]
public class OrderControllerV2 : ControllerBase
{
    // V2 implementation
}
```

---

## 📝 Security Response Codes

| Code | Meaning | When Used |
|------|---------|-----------|
| 400 | Bad Request | Invalid input, validation failure |
| 401 | Unauthorized | Missing/invalid authentication (future) |
| 403 | Forbidden | Insufficient permissions (future) |
| 404 | Not Found | Resource doesn't exist |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Unexpected server error |

---

## 🔍 Testing Security Features

### 1. Test Input Validation

```bash
# Missing required field
curl -X POST http://localhost:8080/api/v1/orders \
  -H "Content-Type: application/json" \
  -d '{"customerName":"Test"}'

# Expected: 400 with "CustomerId is required"
```

### 2. Test Rate Limiting

```bash
# Rapid requests
for i in {1..15}; do 
  curl -w "%{http_code} " http://localhost:8080/api/v1/orders/test
done

# Expected: First 10 succeed, last 5 return 429
```

### 3. Test Security Headers

```bash
curl -I http://localhost:8080/api/v1/orders/test | grep -E "X-|Content-Security|Strict-Transport"

# Expected: All security headers present
```

### 4. Test CORS

```bash
curl -X OPTIONS http://localhost:8080/api/v1/orders \
  -H "Origin: http://localhost:3000" \
  -H "Access-Control-Request-Method: POST" \
  -v

# Expected: Access-Control-Allow-Origin header in response
```

### 5. Test API Versioning

```bash
# All three methods should work
curl http://localhost:8080/api/v1/orders
curl http://localhost:8080/api/orders -H "X-Api-Version: 1.0"
curl "http://localhost:8080/api/orders?api-version=1.0"
```

---

## ⚙️ Configuration Reference

### Rate Limiting Configuration

**File:** `appsettings.RateLimit.json`

```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "GeneralRules": [
      {
        "Endpoint": "*",
        "Period": "1s",
        "Limit": 10
      }
    ],
    "EndpointWhitelist": [
      "get:/health"
    ]
  }
}
```

### CORS Configuration

**File:** `appsettings.json`

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000"
    ]
  }
}
```

---

## 🚀 Production Deployment Checklist

- [ ] Update CORS origins to production domains
- [ ] Review and adjust rate limits based on load testing
- [ ] Enable HTTPS enforcement (remove HTTP endpoints)
- [ ] Configure CSP based on actual resource needs
- [ ] Implement authentication (JWT/OAuth2)
- [ ] Add API key validation for service-to-service calls
- [ ] Enable request/response logging for auditing
- [ ] Configure DDoS protection at infrastructure level
- [ ] Set up API gateway for additional security layer
- [ ] Implement certificate pinning for mobile clients

---

## 📚 Related Documentation

- [SECURITY.md](SECURITY.md) - Secrets management and best practices
- [PRE_COMMIT_CHECKLIST.md](PRE_COMMIT_CHECKLIST.md) - Pre-deployment security checks
- [PRESENTATION.md](PRESENTATION.md) - Architecture overview

---

## 🔄 Future Security Enhancements

**Priority 1 (Critical):**
- [ ] JWT authentication
- [ ] Role-based authorization
- [ ] TLS for Kafka connections
- [ ] Dedicated SQL user (least privilege)

**Priority 2 (Important):**
- [ ] API key management
- [ ] Request signing
- [ ] Audit logging
- [ ] Anomaly detection

**Priority 3 (Enhancement):**
- [ ] Mutual TLS between services
- [ ] Azure Key Vault integration
- [ ] OWASP ZAP security scanning
- [ ] Penetration testing

---

## 📞 Security Contact

For security issues or questions:
- Review existing security documentation
- Check application logs for security events
- Monitor rate limiting metrics
- Report security vulnerabilities immediately
