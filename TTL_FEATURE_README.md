# TTL Feature for Retry Count Persistence

This document explains the new TTL (Time To Live) feature added to EasyMQ for persisting retry count data in Redis or Memory.
## What is TTL and Why Use It for Retry Count?

When processing messages with RabbitMQ, sometimes a message fails and needs to be retried. To avoid infinite retry loops and to implement backoff strategies, EasyMQ tracks how many times each message has been retried. This retry count must be stored somewhere—either in memory or in a persistent store like Redis.

**TTL (Time To Live)** is a mechanism that automatically removes stored data after a certain period. By associating a TTL with each message's retry count, you ensure that retry tracking data does not accumulate forever. This is especially important in high-throughput systems or when using persistent stores like Redis.

## How TTL Works with RabbitMQ Retry Count in EasyMQ

- **When a message is retried**, EasyMQ increments its retry count and stores it (in Redis or memory).
- **TTL is applied** to this retry count entry. After the TTL expires, the retry count is automatically deleted.
- **If the message is processed successfully** or dead-lettered, the retry count is also removed immediately.
- **You can configure TTL** globally (for all queues) or per-queue, giving you flexibility based on your application's needs.

## Benefits

- **Automatic cleanup**: No need to manually remove old retry counts.
- **Resource efficiency**: Prevents memory or Redis from filling up with obsolete retry data.
- **Flexible configuration**: Set different TTLs for different queues or use a sensible default.

## Summary Table

| Storage Type      | How TTL is Applied                | What Happens After TTL Expires         |
|-------------------|-----------------------------------|----------------------------------------|
| Redis             | Key expires via Redis TTL         | Retry count is deleted automatically   |
| In-Memory (Cache) | Entry expires via MemoryCache TTL | Retry count is deleted automatically   |

## Overview

The TTL feature allows you to configure how long retry count data persists in Redis when using the `RedisErrorCounter`. This ensures that retry count information doesn't accumulate indefinitely and automatically expires after the configured time period.

## New Parameters

### AddRabbitMq Method

The `AddRabbitMq` method now accepts an optional `defaultTtl` parameter:

```csharp
services.AddRabbitMq(
    settings => { /* message manager configuration */ },
    queues => { /* queue configuration */ },
    defaultTtl: TimeSpan.FromHours(1) // Optional: default TTL for all queues
);
```

### QueueSettings.Add Method

The `Add<T>` method in `QueueSettings` now accepts an optional `ttl` parameter:

```csharp
queues.Add<MessageModel>(
    queueName: "message",
    prefetchCount: 10,
    retryCount: 3,
    ttl: TimeSpan.FromHours(2) // Optional: TTL for this specific queue
);
```

## TTL Behavior

### Redis Implementation
- When using `RedisErrorCounter`, the TTL is applied to Redis keys using `StringSetAsync` with expiration
- Retry count data automatically expires after the specified TTL
- If no TTL is specified, the default TTL from `AddRabbitMq` is used
- If no default TTL is specified, retry count data persists indefinitely

### In-Memory Implementation
- When using `InMemoryErrorCounter`, the TTL is applied using `MemoryCacheEntryOptions`
- Retry count data automatically expires after the specified TTL
- If no TTL is specified, retry count data persists until manually removed

## Usage Examples

### Basic Usage with Default TTL

```csharp
services.AddRabbitMq(
    settings => { /* configuration */ },
    queues => {
        queues.Add<MessageModel>("message", retryCount: 3);
    },
    defaultTtl: TimeSpan.FromHours(1)
);
```

### Per-Queue TTL Configuration

```csharp
services.AddRabbitMq(
    settings => { /* configuration */ },
    queues => {
        queues.Add<MessageModel>("high-priority", retryCount: 5, ttl: TimeSpan.FromHours(2));
        queues.Add<MessageModel>("low-priority", retryCount: 2, ttl: TimeSpan.FromMinutes(30));
    },
    defaultTtl: TimeSpan.FromHours(1)
);
```

### No TTL (Indefinite Persistence)

```csharp
services.AddRabbitMq(
    settings => { /* configuration */ },
    queues => {
        queues.Add<MessageModel>("message", retryCount: 3);
    }
    // No defaultTtl specified - retry count data persists indefinitely
);
```

## Benefits

1. **Automatic Cleanup**: Retry count data automatically expires, preventing Redis memory accumulation
2. **Flexible Configuration**: Different TTL values can be set for different queues based on business requirements
3. **Consistent Behavior**: Both Redis and In-Memory implementations support TTL
4. **Backward Compatibility**: Existing code continues to work without changes

## Implementation Details

- TTL is applied when calling `UpdateTryCountAsync` in the `Listener` class
- The TTL value is extracted from the queue configuration
- If no TTL is specified for a queue, the default TTL from the service configuration is used
- TTL values are stored as `TimeSpan` objects for precise control over expiration times 