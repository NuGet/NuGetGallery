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

## Configuration and execution

Run the job with its environment-specific JSON configuration:

```powershell
.\NuGet.Services.Staging.Promotion.exe -Configuration .\appsettings.json
```

| Section or setting | Purpose |
| --- | --- |
| `GalleryDb` | Gallery SQL connection. |
| `ServiceBus` | Promotion topic connection, topic path, and subscription name. |
| `Promotion` | `PackageStorageConnectionString`, `StagingStorageConnectionString`, and `FlatContainerStorageConnectionString`. |
| `PackageValidationServiceBus` | Symbols-orchestrator topic connection and path. |
| `Email` | Email topic, sender settings, and package/account link templates. |
| `Storage_UseManagedIdentity`, `Storage_ManagedIdentityClientId` | Shared storage authentication settings when using managed identity. |

The job needs Gallery database writes, promotion-subscription consumption and
topic sends, private staging reads and lease writes, public package/content
writes, and sends to the symbols-orchestrator and email topics. Symbol promotion
also requires `Configuration.EnableStagedSymbolPromotion` on the symbols
orchestrator.

## Concurrency and recovery

Instances in multiple regions actively consume the same subscription. Each
message has its own dependency-injection scope and database context; the current
limit is one message per instance, not globally.

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
