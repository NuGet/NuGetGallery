# Staging promotion worker

`NuGet.Services.Staging.Promotion` publishes validated staged packages and
dispatches staged symbol packages for ingestion. It consumes accepted promotion
requests from the `staging-promotion/package-promotion` Service Bus subscription.
Requests identify a staging group, parent package attempt, or symbol package
attempt and carry the promotion ID used to reject stale deliveries.

## Promotion flow

1. **Acceptance:** Gallery checks promotion eligibility, records the accepted
   artifacts as `Promoting`, and sends the request. Staging validation happens
   before acceptance, not in this worker.
2. **Group dispatch:** A group request fans out to its accepted artifacts.
   Symbols paired with a staged parent wait until that parent is published.
   Children publish independently; a group is not an atomic publication.
3. **Parent publication:** The worker checks the current attempt and ownership,
   copies the validated nupkg to public package storage, extracts embedded
   license/readme files to flat-container storage, and transactionally updates
   package metadata and status to `Available`. The staged listed/unlisted intent
   is preserved. Accepted paired symbols are dispatched after publication.
4. **Symbol publication:** The worker sends an ingestion-only request to the
   symbols orchestrator with a stable tracking ID. That orchestrator owns symbol
   ingestion, snupkg publication, and completion; this worker does not ingest PDBs.
5. **Completion:** Promotion results use the existing email pipeline. Successful
   staging records are removed and private-file cleanup is queued for
   `Gallery.Maintenance`. Group finalization waits for all accepted artifacts,
   removes successful members, retains failed members, and keeps the group.

Published parents enter the normal catalog/V3 pipeline. Catalog generation,
registration, and search indexing are separate jobs, not part of promotion.

Gallery accepts a group in one database transaction, updating attempts in batches
of at most 100 instead of one SQL round trip per artifact. Every group/attempt
write checks its original row version; a conflict rolls back the entire
acceptance and no work is dispatched. Attempt snapshots are untracked; the group
uses the existing repository save and transaction APIs, so dispatch-failure
recovery retains normal EF concurrency handling without tracking reconciliation.
Individual acceptance and resend paths are unchanged. No schema change is required.

Parent promotion reads the final validated blob's stored SHA-512 hash and length
without downloading its contents. Validation already calculates that hash after
package processing and stores it as blob metadata, which the copy to staging
preserves. The metadata read must match `ValidatedBlobETag`; upload metadata and
the package's pre-validation database hash are not reused. Stored hash metadata
is required: missing metadata, changed/missing blobs, storage failures, and invalid
metadata fail the delivery. Promotion never downloads a package just to hash it.
The immediate-validation development path saves a separate validated blob with
its hash metadata before returning Ready, preserving the original upload and
capturing the validated ETag after metadata is written. File-system storage does
not support this blob-metadata contract; staging uses Azure Blob Storage or
Azurite. Embedded license/readme extraction still opens the validated package
when needed, and publication lease checks are unchanged.

## Publication timing

`StagingPromotion.AcceptedToGallerySeconds` measures time from promotion
acceptance to committed Gallery publication. The parent worker and symbol
publication service emit it after the publication transaction succeeds, with
`ArtifactType` (`Package` or `Symbols`) as the only dimension.

For hourly publication counts and average duration:

```kusto
customMetrics
| where timestamp >= ago(24h)
| where name == "StagingPromotion.AcceptedToGallerySeconds"
| summarize Publications = sum(valueCount),
    AverageSeconds = sum(valueSum) / sum(valueCount)
    by ArtifactType = tostring(customDimensions.ArtifactType), bin(timestamp, 1h)
| order by timestamp desc
```

Query the promotion worker and symbols orchestrator in one environment. If they
use separate Application Insights resources, replace `customMetrics` with a
`union` of their `app("resource").customMetrics` tables. Counts reflect recorded
latency samples, not an authoritative count of all publications.

The start time is persisted as nullable UTC `PromotionAcceptedAt` with the
accepted state. Groups and their accepted artifacts share one timestamp, as do
parents and paired symbols. Resends preserve it. Missing timestamps or
future timestamps produce a warning and no duration sample; publication proceeds.
Successful cleanup deletes the temporary staging records, not permanent history.

### Optional V3 diagnosis

`StagingPromotion.Accepted` and `StagingPromotion.GalleryPublished` JSON traces
identify individual promotions; the publication trace includes `PromotionAcceptedAt`.
For V3 origin timing, correlate it by package ID/version with Catalog2Dnx's existing
`Commit: {Id}/{Version}` logs after Gallery publication, scoped to one destination
feed. Matching is best-effort: missing logs, deletion, or reprocessing can affect
the match. It does not establish CDN downloadability, search/registration readiness,
or PDB readiness.

### Worker diagnostics

The Service Bus processor also emits:

| Metric | Measurement / dimensions |
| --- | --- |
| `StagingPromotion.MessageDeliveryLagSeconds` | Scheduled enqueue time to delivery; includes retry delays. `MessageType`. |
| `StagingPromotion.MessageEnqueueLagSeconds` | Scheduled enqueue time to actual enqueue. `MessageType`. |
| `StagingPromotion.MessageHandlerDurationSeconds` | Per-delivery processing through completion acknowledgement. `MessageType`, `Handled`. |
| `StagingPromotion.MessageLockLost` | Lock-loss count. `MessageType`. |

`Handled=True` means consumed, not necessarily published. Promotion/package IDs
are not metric dimensions. These metrics cover promotion messages only.
Every send refreshes the enqueue timestamp, including resends.

## Configuration and execution

Run the job with its environment-specific JSON configuration:

```powershell
.\NuGet.Services.Staging.Promotion.exe -Configuration .\appsettings.json
```

| Section or setting | Purpose |
| --- | --- |
| `GalleryDb` | Gallery SQL connection. |
| `ServiceBus` | Promotion topic connection, topic path, and subscription name. |
| `ServiceBus.MaxConcurrentCalls` | Maximum concurrent promotion message handlers per process. Defaults to `1`; must be greater than zero. |
| `Promotion` | `PackageStorageConnectionString`, `StagingStorageConnectionString`, and `FlatContainerStorageConnectionString`. |
| `PackageValidationServiceBus` | Symbols-orchestrator topic connection and path. |
| `Email` | Email topic, sender settings, and package/account link templates. |
| `Storage_UseManagedIdentity`, `Storage_ManagedIdentityClientId` | Shared storage authentication settings when using managed identity. |

The job needs Gallery database writes, promotion-subscription consumption and
topic sends, private staging reads and lease writes, public package/content
writes, and sends to the symbols-orchestrator and email topics. The symbols
orchestrator needs `Configuration.PackageStorageConnectionString` for promotion.

## Concurrency and recovery

Instances in multiple regions actively consume the same subscription. Each
message has its own dependency-injection scope and database context.
`ServiceBus.MaxConcurrentCalls` sets the limit per instance, not globally.
For example, set it to `8` to allow up to eight handlers in each process.
Omitting the setting keeps the default of one; zero or negative values fail
startup. Configuration changes require restarting the job. This setting does
not change symbols-orchestrator, ingestion, or downstream V3 concurrency.

Parent and symbol requests share a distributed lease keyed by staging package
identity. Group dispatch has a separate group lease, allowing different children
to proceed concurrently. Lease blobs live under `promotion-leases/` in the
private `staging` container; all instances must share that staging account.
Leases last 60 seconds and renew every 20 seconds. Parent publication checks
ownership before copying, after copying, and before its database transition.

Missing or inactive attempts are ignored. Contention, processing exceptions, and
lease loss leave messages retryable; a crashed worker's lease expires. Redelivery
resumes unfinished follow-up work or ignores a completed attempt. Ineligible
artifacts are recorded as promotion failures.

Publication retries forward: identical public package copies are reusable and
license/readme extraction can repeat. Failed or ambiguous SQL commits never
trigger deletion of copied public files. Storage and SQL are not one transaction,
so public files may remain after failure; the database determines completion.
