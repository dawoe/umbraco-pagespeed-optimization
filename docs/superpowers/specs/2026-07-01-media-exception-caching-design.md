# Media Exception Caching — Design

## Context

`OptimizedImageUrlGenerator` (`src/code/Umbraco.Community.PagespeedOptimizer.Infrastructure/OptimizedImageUrlGenerator.cs`) currently applies a single global image quality and a global "force WebP" setting to every image URL it generates. The media exception feature (see `2026-06-30-media-exceptions-ef-repository-design.md` and `2026-07-01-media-exception-workspace-view-design.md`) lets editors override quality/WebP per media item via the back-office, persisted in the `PageSpeedOptimizer_MediaExceptions` table through `IMediaExceptionRepository`.

`OptimizedImageUrlGenerator` does not yet read these exceptions. This design adds that read path, with caching so the generator never hits the database on the image-rendering hot path.

## Goals

- `OptimizedImageUrlGenerator` applies a media item's `MediaException` (quality/`ForceWebp`) when one exists for the image being rendered, instead of the global defaults.
- The exception data is cached using Umbraco's isolated cache (`AppCaches.IsolatedCaches`) and is not re-read from the database on every image URL generation.
- The cache is invalidated and rebuilt when exceptions are created/updated/deleted via the back-office workspace view, using an `ICacheRefresher`.
- No `IServiceScopeFactory.CreateScope()` scope-juggling and no `GetAwaiter().GetResult()` sync-over-async on the image-generation hot path.

## Key constraint: URL, not MediaKey

`ImageUrlGenerationOptions.ImageUrl` (Umbraco core) is a plain relative URL string (e.g. `/media/1234/photo.jpg`) — `GetImageUrl()` never receives a `MediaKey`. Exceptions are stored keyed by `MediaKey`. So the cache must be a `Dictionary<string ImageUrl, MediaException>`, built by resolving each exception's `MediaKey` to its current media URL.

## Key constraint: singleton hot path, async-only persistence

- `OptimizedImageUrlGenerator` is registered as a **Singleton** (`UmbracoBuilderExtensions.AddOptimizedImageUrlGenerator`), created once from the root provider.
- `IEFCoreScopeProvider<T>` is itself a **Singleton** (`AddUnique` defaults to `ServiceLifetime.Singleton`) and hands out independent EF Core scopes on demand — so `MediaExceptionRepository` can safely be registered as Singleton too. This matches precedent in Umbraco core itself (e.g. `DatabaseCacheRepository`, `ExternalLoginRepository` are Singletons that perform DB I/O).
- `IPublishedUrlProvider` (used to resolve a `MediaKey` to a URL) is also registered via `AddUnique` — i.e. Singleton — but it reads an **ambient** `UmbracoContext` via `IUmbracoContextAccessor`. That ambient context only exists during an active HTTP request. Any code that calls `IPublishedUrlProvider` outside a request (startup, background notification handlers) must wrap the call in `IUmbracoContextFactory.EnsureUmbracoContext()` to fabricate one.
- `IEfCoreScope<T>` only exposes `ExecuteWithContextAsync` — there is no synchronous query path. Since `IImageUrlGenerator.GetImageUrl()` is a synchronous method, any database read triggered directly from it would require blocking on async work (`GetAwaiter().GetResult()`).

**Resolution:** cache rebuilds never happen inline within `GetImageUrl()`. They only run from genuinely asynchronous entry points (an app-startup notification handler and a cache-invalidation notification handler). `GetImageUrl()` only ever performs a synchronous, non-blocking dictionary lookup against whatever is currently cached. If nothing has been built yet (e.g. the brief window right after startup before the first rebuild completes), it falls back to the global settings — no exception applied — until the next successful rebuild.

## Components

### `IMediaExceptionCache` (Core) / `MediaExceptionCache` (Infrastructure, Singleton)

```csharp
public interface IMediaExceptionCache
{
    MediaException? GetByImageUrl(string imageUrl);
    Task RebuildAsync();
}
```

- `GetByImageUrl`: synchronous, non-blocking. Reads the cached `Dictionary<string, MediaException>` (key comparer `OrdinalIgnoreCase`) from `AppCaches.IsolatedCaches.GetOrCreate<MediaException>()`. Returns `null` on a cache miss (no rebuild is triggered from here) or when no entry matches the URL.
- `RebuildAsync`:
  1. Loads all rows via `IMediaExceptionRepository.GetAllAsync()`.
  2. For each row, resolves `MediaKey` → URL via `IPublishedUrlProvider.GetMediaUrl(mediaKey, UrlMode.Relative)`, wrapped in `IUmbracoContextFactory.EnsureUmbracoContext()`. Rows whose media no longer resolves (e.g. deleted media) are skipped.
  3. Builds a complete new dictionary off to the side, then swaps it into the isolated cache in one atomic `Insert` — readers never observe a partially-built map.
  4. Any exception during the rebuild (e.g. DB unreachable) is caught and logged by the caller (see notification handlers below); the previous dictionary, if any, keeps serving reads until the next successful rebuild.

### `OptimizedImageUrlGenerator` (updated)

Gains a constructor dependency on `IMediaExceptionCache`. In `GetImageUrl`, after determining `options.ImageUrl`, it calls `mediaExceptionCache.GetByImageUrl(options.ImageUrl)`. If a `MediaException` is found:
- Its `Quality` is used instead of `settings.DefaultImageQuality`.
- Its `ForceWebp` is used instead of `settings.ForceWebP` when deciding whether to inject the `format=webp` further-option.

The presence of a `MediaException` row is what "Override image optimizations" means in the back-office UI — there is no separate enabled flag on the entity.

### Rebuild triggers (async only)

- `RebuildOnStartupHandler : INotificationAsyncHandler<UmbracoApplicationStartedNotification>` — calls `RebuildAsync()` once at boot, so the cache is warm before the first request in the common case.
- `RebuildOnMediaExceptionCacheRefreshHandler : INotificationAsyncHandler<MediaExceptionCacheRefresherNotification>` — calls `RebuildAsync()` whenever the refresher below fires.

Both handlers catch and log exceptions from `RebuildAsync()` rather than letting them propagate — a failed rebuild must not crash startup or the notification pipeline; it just means the cache keeps serving stale (or empty) data until the next trigger.

### `MediaExceptionCacheRefresher` (Infrastructure, new `Cache/` folder)

```csharp
public sealed class MediaExceptionCacheRefresher : CacheRefresherBase<MediaExceptionCacheRefresherNotification>
{
    public static readonly Guid UniqueId = Guid.Parse("317F1A0A-1131-4F2B-A9E8-6287C49C6B38");
    public override Guid RefresherUniqueId => UniqueId;
    public override string Name => "Media Exception Cache Refresher";

    public override void RefreshAll() => base.RefreshAll(); // publishes MediaExceptionCacheRefresherNotification
    public override void Refresh(int id) => throw new NotSupportedException();
    public override void Refresh(Guid id) => throw new NotSupportedException();
    public override void Remove(int id) => throw new NotSupportedException();
}
```

A small paired `MediaExceptionCacheRefresherNotification` class mirrors Umbraco's own per-refresher notification types (e.g. `ApplicationCacheRefresherNotification`). The refresher itself does not touch the isolated cache directly — publishing the notification is what triggers the async rebuild handler above. This is a full-rebuild-only strategy: id/Guid/remove-based partial refresh is not supported, since the exception list is expected to be small and a full rebuild is cheap.

Registered via `builder.CacheRefreshers().Append<MediaExceptionCacheRefresher>()` in `UmbracoBuilderExtensions`.

### `IMediaExceptionRepository` / `MediaExceptionRepository` (registration change)

Registration changes from `AddScoped` to `AddSingleton` in `UmbracoBuilderExtensions`. No code change to the repository itself is required — it already uses `IEFCoreScopeProvider<PageSpeedOptimizerDbContext>.CreateScope()` per call, which is safe from a Singleton.

### `IMediaExceptionService` (Core) / `MediaExceptionService` (Infrastructure, Singleton)

Thin wrapper mirroring `IMediaExceptionRepository`'s CRUD surface (`CreateAsync`, `UpdateAsync`, `DeleteAsync`, `GetAsync`, `GetAllAsync`, `GetByMediaKeyAsync`). After each successful `CreateAsync`/`UpdateAsync`/`DeleteAsync`, it calls `IDistributedCache.RefreshAll(MediaExceptionCacheRefresher.UniqueId)`. Read methods pass straight through to the repository.

### `MediaExceptionManagementApiController` (updated)

Depends on `IMediaExceptionService` instead of `IMediaExceptionRepository` directly, for all endpoints — keeping the controller a thin HTTP wrapper with a single collaborator.

## Data Flow

**Write:** Workspace view save → controller → `MediaExceptionService` → repository writes to DB → `DistributedCache.RefreshAll(MediaExceptionCacheRefresher.UniqueId)` → (single server: invoked in-process immediately; load-balanced: via Umbraco's configured server messenger, same as any built-in cache refresher) → `MediaExceptionCacheRefresher.RefreshAll()` publishes `MediaExceptionCacheRefresherNotification` → async handler calls `MediaExceptionCache.RebuildAsync()` → new dictionary atomically swapped into the isolated cache.

**Read:** `OptimizedImageUrlGenerator.GetImageUrl()` → `MediaExceptionCache.GetByImageUrl(url)` → synchronous dictionary lookup, always non-blocking, always safe to call from a Singleton on any thread.

## Error Handling

- Stale `MediaKey` (media deleted after the exception was created) → the row is skipped when building the dictionary; not fatal.
- DB unreachable during a background rebuild → caught and logged in the notification handler; the previous cached dictionary (or an empty result, if none exists yet) continues to serve reads.
- `GetImageUrl()` itself never throws due to caching — a cache miss is treated as "no override," not an error.

## Testing

Unit tests in `Umbraco.Community.PagespeedOptimizer.Infrastructure.Tests`:
- `MediaExceptionCache`: `RebuildAsync` builds the expected dictionary from repository data, skips rows with unresolvable `MediaKey`s, and performs an atomic swap (readers never see a partial dictionary); `GetByImageUrl` returns `null` on miss and before any rebuild has completed.
- `OptimizedImageUrlGenerator`: when a matching `MediaException` exists, its `Quality`/`ForceWebp` take precedence over the global `ImageOptimizationSettings`; falls back to global settings when no exception matches.
- `MediaExceptionCacheRefresher`: `RefreshAll()` publishes `MediaExceptionCacheRefresherNotification`.
- `MediaExceptionService`: each successful write (`Create`/`Update`/`Delete`) calls `IDistributedCache.RefreshAll` with `MediaExceptionCacheRefresher.UniqueId`.
