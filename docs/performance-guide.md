# Performance Optimization Guide

This guide covers performance optimization techniques used in the Book Store project.

## Garbage Collection (GC) Optimization

The API service is configured with optimal garbage collection settings for server workloads.

### Configuration

See [BookStore.ApiService.csproj](file:///Users/antaoalmada/Projects/BookStore/src/BookStore.ApiService/BookStore.ApiService.csproj):

```xml
<PropertyGroup>
  <!-- Server GC: Uses multiple heaps and threads for better throughput -->
  <ServerGarbageCollection>true</ServerGarbageCollection>
  
  <!-- Concurrent GC: Reduces pause times by running GC concurrently -->
  <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
  
  <!-- Retain VM: Keeps virtual memory allocated for better performance -->
  <RetainVMGarbageCollection>true</RetainVMGarbageCollection>
  
  <!-- Dynamic PGO: Profile-guided optimization for hot paths -->
  <TieredCompilation>true</TieredCompilation>
  <TieredCompilationQuickJit>true</TieredCompilationQuickJit>
</PropertyGroup>
```

### Server GC vs Workstation GC

#### Server GC (Enabled)

**How it works:**
- Creates a separate heap and dedicated GC thread for each CPU core
- Performs garbage collection in parallel across all heaps
- Optimized for throughput over latency

**Benefits:**
- ✅ **Higher throughput** - Can handle more requests per second
- ✅ **Better scalability** - Utilizes all available CPU cores
- ✅ **Larger heap sizes** - Can manage more memory efficiently
- ✅ **Ideal for server applications** - ASP.NET Core, APIs, background services

**Trade-offs:**
- ⚠️ Uses more memory (separate heap per core)
- ⚠️ Slightly longer GC pauses (but less frequent)

#### Workstation GC (Default for console apps)

**How it works:**
- Single heap and single GC thread
- Optimized for low latency and UI responsiveness

**When to use:**
- Desktop applications
- Interactive tools
- Memory-constrained environments

### Concurrent GC

**Enabled:** `<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>`

**How it works:**
- Runs most of the Gen2 collection concurrently with application threads
- Application threads can continue allocating objects during GC
- Only brief pauses for critical GC phases

**Benefits:**
- ✅ **Reduced pause times** - Application remains responsive during GC
- ✅ **Better user experience** - Fewer noticeable delays
- ✅ **Improved throughput** - Less time blocked waiting for GC

**Performance Impact:**
- Typical Gen2 pause: 10-50ms (vs 100-500ms without concurrent GC)
- Background GC thread uses ~10-25% of one CPU core

### Retain VM GC

**Enabled:** `<RetainVMGarbageCollection>true</RetainVMGarbageCollection>`

**How it works:**
- Keeps virtual memory pages allocated after GC
- Avoids frequent memory allocation/deallocation from OS
- Memory is decommitted but not released

**Benefits:**
- ✅ **Faster allocations** - No need to request memory from OS
- ✅ **Reduced fragmentation** - More consistent memory layout
- ✅ **Better performance** - Fewer system calls

**When to use:**
- Long-running server applications
- Applications with predictable memory patterns
- Environments with dedicated resources

**Trade-off:**
- Uses more virtual memory (but not physical memory)
- May not be ideal for memory-constrained containers

### Tiered Compilation & Dynamic PGO

**Enabled:**
```xml
<TieredCompilation>true</TieredCompilation>
<TieredCompilationQuickJit>true</TieredCompilationQuickJit>
```

**How it works:**

1. **Tier 0 (Quick JIT):**
   - Methods are compiled quickly with minimal optimizations
   - Application starts faster
   - Low compilation overhead

2. **Tier 1 (Optimized JIT):**
   - Hot methods (frequently called) are recompiled with full optimizations
   - Uses runtime profiling data (Dynamic PGO)
   - Optimizes based on actual usage patterns

**Benefits:**
- ✅ **Faster startup** - Quick JIT gets app running immediately
- ✅ **Better steady-state performance** - Hot paths are fully optimized
- ✅ **Adaptive optimization** - Optimizes based on real workload
- ✅ **Reduced memory** - Only hot methods get full optimization

**Dynamic PGO (Profile-Guided Optimization):**
- Collects runtime profiling data
- Optimizes based on actual code paths taken
- Inlines hot methods
- Devirtualizes interface/virtual calls when possible
- Optimizes branch predictions

## Performance Monitoring

### Aspire Dashboard (Recommended)

**The easiest way to monitor GC performance!** Aspire provides a built-in dashboard with real-time metrics.

#### Access the Dashboard

```bash
# Start the application
aspire run

# Dashboard automatically opens at:
# https://localhost:17238 (or check console output)
```

#### Monitor GC Metrics

The Aspire Dashboard provides comprehensive .NET runtime metrics:

**1. Navigate to Metrics Tab**
   - Select your service (e.g., `apiservice`)
   - View real-time metrics

**2. Key GC Metrics to Monitor**

**Garbage Collection Metrics:**
- `process.runtime.dotnet.gc.collections.count`
  - **Gen 0**: Should be frequent (every few seconds)
  - **Gen 1**: Moderate (every 10-30 seconds)
  - **Gen 2**: Rare (every few minutes)
  - ✅ **Good**: Gen2 collections are infrequent
  - ❌ **Bad**: Frequent Gen2 collections indicate memory pressure

- `process.runtime.dotnet.gc.heap.size`
  - Total heap size across all generations
  - Should stabilize after warmup period
  - With Server GC: Larger but more efficient

- `process.runtime.dotnet.gc.pause.time`
  - **Target**: <50ms for Gen2 collections
  - **Concurrent GC**: Typically 10-30ms
  - **Without Concurrent GC**: 100-500ms
  - Lower is better!

- `process.runtime.dotnet.gc.duration`
  - Time spent in GC as percentage
  - **Target**: <5% of total time
  - **Good**: 1-3%
  - **Warning**: >5% indicates GC pressure

**Memory Metrics:**
- `process.runtime.dotnet.gc.allocations.size`
  - Allocation rate (bytes/second)
  - Lower is better for GC pressure
  - Identify allocation hot spots

- `process.runtime.dotnet.gc.committed_memory.size`
  - Physical memory committed to GC
  - Should be stable under load

**JIT Compilation Metrics:**
- `process.runtime.dotnet.jit.compiled_methods.count`
  - Methods compiled by JIT
  - Should plateau after warmup

- `process.runtime.dotnet.jit.compilation_time`
  - Time spent in JIT compilation
  - With Tiered Compilation: Initial spike, then low

**3. Visualize Performance Impact**

Create custom charts in Aspire Dashboard:

```
Chart 1: GC Pause Times
- Metric: process.runtime.dotnet.gc.pause.time
- Filter: generation=2
- Expected: <50ms with our optimizations

Chart 2: GC Frequency
- Metric: process.runtime.dotnet.gc.collections.count
- Group by: generation
- Expected: Gen0 >> Gen1 >> Gen2

Chart 3: Time in GC
- Metric: process.runtime.dotnet.gc.duration
- Expected: <5%

Chart 4: Heap Size
- Metric: process.runtime.dotnet.gc.heap.size
- Expected: Stable after warmup
```

**4. Compare Before/After**

To see the impact of GC optimizations:

```bash
# 1. Baseline (disable GC settings)
# Comment out GC settings in BookStore.ApiService.csproj
aspire run

# 2. Open Aspire Dashboard
# Note metrics for 5 minutes under load

# 3. Optimized (enable GC settings)
# Uncomment GC settings
aspire run

# 4. Compare in Dashboard
# You should see:
# - Lower GC pause times (-60-80%)
# - Higher throughput (+20-40%)
# - More stable heap size
```

#### Traces Tab

Monitor request performance:

1. **Navigate to Traces**
2. **Filter by service**: `apiservice`
3. **Look for**:
   - Request duration (P50, P95, P99)
   - Database query times
   - External service calls

**Expected improvements with GC optimizations:**
- Lower P99 latency (fewer GC pauses during requests)
- More consistent response times
- Better performance under load

#### Logs Tab

Monitor GC-related logs:

```csharp
// Add GC logging to Program.cs if needed
var gcInfo = GC.GetGCMemoryInfo();
logger.LogInformation(
    "GC: Gen0={Gen0} Gen1={Gen1} Gen2={Gen2} Heap={HeapMB}MB Pause={PauseMs}ms",
    GC.CollectionCount(0),
    GC.CollectionCount(1),
    GC.CollectionCount(2),
    gcInfo.HeapSizeBytes / 1024 / 1024,
    gcInfo.PauseDurations.Length > 0 ? gcInfo.PauseDurations[0].TotalMilliseconds : 0);
```

View in Aspire Dashboard Logs tab with real-time filtering.

#### Structured Logs

Aspire automatically captures structured logs with:
- Correlation IDs
- Trace IDs
- Service names
- Timestamps

Perfect for correlating GC events with request performance!

### Alternative: dotnet-counters (Command Line)

If you prefer command-line monitoring:

```bash
# Install dotnet-counters
dotnet tool install -g dotnet-counters

# Find process ID
aspire run
# Note the apiservice process ID from console

# Monitor GC metrics
dotnet-counters monitor --process-id <pid> --counters System.Runtime

# Look for:
# - gc-heap-size: Should be larger with Server GC
# - gc-pause-time: Should be lower with Concurrent GC  
# - time-in-gc: Should be <5% for healthy application
# - gen-0-gc-count, gen-1-gc-count, gen-2-gc-count
```

### Verify GC Settings

Check that GC settings are applied correctly:

```bash
# Run the application
dotnet run --project src/BookStore.ApiService

# In another terminal, check GC info
dotnet-counters monitor --process-id <pid> --counters System.Runtime

# Look for:
# - gc-heap-size: Should be larger with Server GC
# - gc-pause-time: Should be lower with Concurrent GC
# - time-in-gc: Should be <5% for healthy application
```

### Key Metrics to Monitor

#### GC Metrics

- **GC Pause Time** - Should be <50ms for Gen2 collections
- **GC Frequency** - Gen0/Gen1 should be frequent, Gen2 should be rare
- **Time in GC** - Should be <5% of total CPU time
- **Heap Size** - Should stabilize after warmup

#### Application Metrics

- **Request Throughput** - Requests per second
- **Response Time** - P50, P95, P99 latencies
- **CPU Usage** - Should be <80% under normal load
- **Memory Usage** - Should not grow unbounded

### Load Testing with Aspire Dashboard

The best way to see GC optimizations in action:

#### Step 1: Start Baseline Test

```bash
# 1. Temporarily disable GC optimizations
# Comment out the PropertyGroup in src/BookStore.ApiService/BookStore.ApiService.csproj

# 2. Start Aspire
aspire run

# 3. Open Aspire Dashboard (URL in console output)
# Navigate to Metrics → apiservice

# 4. In another terminal, run load test
# Install wrk: brew install wrk (macOS) or apt-get install wrk (Linux)
wrk -t4 -c100 -d60s http://localhost:5000/api/books/search?query=code

# 5. Watch metrics in Aspire Dashboard for 60 seconds
# Note:
# - GC pause times (process.runtime.dotnet.gc.pause.time)
# - GC frequency (process.runtime.dotnet.gc.collections.count)
# - Time in GC (process.runtime.dotnet.gc.duration)
# - Request latency (in Traces tab)
```

#### Step 2: Test with Optimizations

```bash
# 1. Stop the application (Ctrl+C)

# 2. Re-enable GC optimizations
# Uncomment the PropertyGroup in src/BookStore.ApiService/BookStore.ApiService.csproj

# 3. Start Aspire again
aspire run

# 4. Open Aspire Dashboard

# 5. Run same load test
wrk -t4 -c100 -d60s http://localhost:5000/api/books/search?query=code

# 6. Compare metrics in Dashboard
```

#### Step 3: Analyze Results

**Expected Improvements in Aspire Dashboard:**

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| GC Pause Time (Gen2) | 100-500ms | 10-50ms | **-60-80%** ⚡ |
| Time in GC | 8-15% | 2-5% | **-50-70%** 📈 |
| Gen2 Collections | Frequent | Rare | **-40-60%** 🎯 |
| Request P99 Latency | Higher | Lower | **-10-20%** ⚡ |
| Throughput (req/s) | Baseline | +20-40% | **+20-40%** 🚀 |

**Screenshots to Capture:**
1. GC pause time chart (before/after)
2. GC collections count (before/after)
3. Request latency distribution (Traces tab)
4. Heap size over time

#### Alternative: Use Aspire's Built-in Load Testing

```csharp
// Create a simple load test using HttpClient
// Run from a separate console app or test project

using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

var tasks = Enumerable.Range(0, 100).Select(async _ =>
{
    for (int i = 0; i < 100; i++)
    {
        await client.GetAsync("/api/books/search?query=code");
        await Task.Delay(10); // 10ms between requests
    }
});

await Task.WhenAll(tasks);
```

Watch the results in real-time in Aspire Dashboard!

## Expected Performance Improvements

Based on typical ASP.NET Core applications with these settings:

### Throughput

- **Server GC**: +20-40% more requests/second vs Workstation GC
- **Concurrent GC**: +10-15% throughput (less time blocked)
- **Tiered Compilation**: +15-25% steady-state performance

### Latency

- **Concurrent GC**: -60-80% reduction in GC pause times
- **Server GC**: More consistent latency under load
- **Dynamic PGO**: -10-20% reduction in P99 latency

### Memory

- **Server GC**: Uses ~2-3x more memory than Workstation GC
- **Retain VM**: +10-20% virtual memory usage
- **Overall**: Better memory efficiency under sustained load

## Container Considerations

When running in containers (Docker, Kubernetes), consider:

### Memory Limits

```yaml
# docker-compose.yml or Kubernetes manifest
resources:
  limits:
    memory: "2Gi"  # Set appropriate limit
  requests:
    memory: "1Gi"
```

### GC Heap Limit

.NET automatically respects container memory limits, but you can tune:

```bash
# Set GC heap limit to 75% of container memory
DOTNET_GCHeapHardLimit=0x60000000  # 1.5GB for 2GB container
```

### CPU Limits

Server GC creates one heap per CPU. In containers:

```yaml
resources:
  limits:
    cpu: "2000m"  # 2 CPUs
```

.NET will create 2 GC heaps automatically.

## Advanced Tuning

### GC Heap Count

Override automatic heap count:

```bash
# Force 4 GC heaps regardless of CPU count
DOTNET_GCHeapCount=4
```

### GC LOH Threshold

Tune Large Object Heap threshold:

```bash
# Objects >85KB go to LOH by default
# Increase threshold to reduce LOH fragmentation
DOTNET_GCLOHThreshold=100000  # 100KB
```

### GC Conserve Memory

For memory-constrained environments:

```bash
# More aggressive memory management
DOTNET_GCConserveMemory=9  # 0-9, higher = more aggressive
```

## Monitoring in Production

### Aspire Dashboard (Development & Staging)

**Recommended for development and staging environments:**

Aspire Dashboard provides the best developer experience with:
- ✅ Real-time metrics visualization
- ✅ Distributed tracing
- ✅ Structured logs with correlation
- ✅ No configuration needed
- ✅ Built-in .NET runtime metrics

```bash
# Development
aspire run

# Staging (with Aspire hosting)
# Deploy AppHost to staging environment
# Dashboard URL provided in deployment output
```

### OpenTelemetry (Production)

For production, export metrics to your observability platform:

Already configured in the project via `ServiceDefaults`:

```csharp
// ServiceDefaults automatically configures OpenTelemetry
// Exports to configured OTLP endpoint

// Runtime metrics tracked:
// - process.runtime.dotnet.gc.collections.count
// - process.runtime.dotnet.gc.heap.size  
// - process.runtime.dotnet.gc.pause.time
// - process.runtime.dotnet.gc.duration
// - process.runtime.dotnet.jit.compiled_methods.count
```

**Supported backends:**
- Azure Monitor / Application Insights
- Prometheus + Grafana
- Datadog
- New Relic
- Honeycomb
- Any OTLP-compatible backend

### Application Insights

For Azure deployments:

```csharp
// Program.cs (already configured via ServiceDefaults)
builder.Services.AddApplicationInsightsTelemetry();

// Automatically tracks:
// - GC metrics
// - Memory usage
// - Request performance
// - Dependencies
// - Exceptions
```

View in Azure Portal:
- Performance → Server metrics
- Metrics Explorer → .NET CLR metrics
- Live Metrics for real-time monitoring

### Custom Metrics

```csharp
// Track GC metrics manually
var gcInfo = GC.GetGCMemoryInfo();
logger.LogInformation(
    "GC Info: Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}, Heap={HeapSize}MB",
    GC.CollectionCount(0),
    GC.CollectionCount(1),
    GC.CollectionCount(2),
    gcInfo.HeapSizeBytes / 1024 / 1024);
```

## Best Practices

### DO

✅ Use Server GC for ASP.NET Core applications
✅ Enable Concurrent GC for better responsiveness  
✅ Enable Tiered Compilation for optimal performance
✅ Monitor GC metrics in production
✅ Set appropriate container memory limits
✅ Use `ArrayPool<T>` and `MemoryPool<T>` for frequently allocated buffers
✅ Implement `IDisposable` for large objects
✅ Avoid allocations in hot paths

### DON'T

❌ Use Workstation GC for server applications
❌ Disable Concurrent GC (unless specific requirements)
❌ Ignore GC metrics and warnings
❌ Set container limits too low for Server GC
❌ Create unnecessary allocations
❌ Hold references to large objects longer than needed
❌ Use finalizers unless absolutely necessary

## Troubleshooting

### High GC Pause Times

**Symptom:** GC pauses >100ms

**Solutions:**
1. Enable Concurrent GC (should already be enabled)
2. Reduce heap size by fixing memory leaks
3. Use `GC.TryStartNoGCRegion()` for critical sections
4. Profile with `dotnet-trace` to find allocation hot spots

### High Memory Usage

**Symptom:** Memory grows unbounded

**Solutions:**
1. Check for memory leaks with `dotnet-dump`
2. Review static collections and event handlers
3. Ensure `IDisposable` objects are disposed
4. Use `WeakReference` for caches
5. Enable `GCConserveMemory` mode

### Low Throughput

**Symptom:** Lower than expected requests/second

**Solutions:**
1. Verify Server GC is enabled: `dotnet-counters monitor`
2. Check CPU usage - should be <80%
3. Profile with `dotnet-trace` to find bottlenecks
4. Review async/await usage
5. Check database connection pooling

## Learn More

- [.NET GC Documentation](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/)
- [Server GC vs Workstation GC](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/workstation-server-gc)
- [Dynamic PGO](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/compilation#profile-guided-optimization)
- [Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/performance/performance-best-practices)
- [dotnet-counters](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters)
- [dotnet-trace](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace)
