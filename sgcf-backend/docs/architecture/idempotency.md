# Idempotency

## Overview

Endpoints that create or mutate resources honor an `Idempotency-Key` header to prevent duplicate
processing caused by network retries or double-submissions on the frontend.

## Header

| Header            | Format   | Example                                |
|-------------------|----------|----------------------------------------|
| `Idempotency-Key` | UUID v4  | `550e8400-e29b-41d4-a716-446655440000` |

The key must be generated **client-side**, once per submit attempt. A retry of the same operation
reuses the same key; a genuinely new operation generates a fresh key.

## Endpoints that honor Idempotency-Key

| Method | Path                                              | Notes                                    |
|--------|---------------------------------------------------|------------------------------------------|
| POST   | `/api/v1/contratos`                               | Creates a new financing contract         |
| POST   | `/api/v1/contratos/{id}/simular-antecipacao`      | Saves simulation result (when requested) |
| POST   | `/api/v1/contratos/{id}/garantias`                | Adds a guarantee to a contract           |

## Behavior

- If a request arrives with an `Idempotency-Key` already seen within the TTL window, the server
  returns the cached response from the first successful execution without re-processing.
- If the first request is still in flight when a duplicate arrives, the server returns `409 Conflict`.
- Keys expire after **24 hours**.
- The key is stored in-process (`IMemoryCache`) in the current implementation. A distributed cache
  (Redis) should be used before horizontal scaling.

## Client responsibility

1. Generate a UUID v4 before each form submission.
2. Include it as `Idempotency-Key: <uuid>` in the request headers.
3. On retry (timeout, network error), reuse the same key.
4. On intentional re-submission (user clicks "Submit" again after reading a result), generate a new key.
