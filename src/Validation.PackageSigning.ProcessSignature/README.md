## Overview

**Subsystem: Validation 📝**

This job manages the validation of a package's signature. A NuGet package (.nupkg) can be signed using a cryptographic
certificate and the signature is embedded in the package file. On nuget.org all packages must have a repository
signature and can optionally have an author signature.

This job is enqueued to at two points in a package's validation, managed by
[the orchestrator](https://github.com/NuGet/NuGet.Jobs/tree/master/src/NuGet.Services.Validation.Orchestrator).

**First**, it is treated as an `IProcessor`, meaning the package can be modified. Any unacceptable repository signature is
stripped. Any author signature is validated. The enqueuer in the orchestrator is
[`PackageSignatureProcessor`](https://github.com/NuGet/NuGet.Jobs/blob/master/src/NuGet.Services.Validation.Orchestrator/PackageSigning/ProcessSignature/PackageSignatureProcessor.cs).
This mode is executed before repository signing to prepare the package for signing and to quickly reject any bad author
signatures.

**Second**, it is treated as an `IValidator`, meaning the package is not allowed to be modified. All of the same validations
as the first mode are run. Additionally, a repository signature is now required. The enqueuer in the orchestrator is
[`PackageSignatureValidator`](https://github.com/NuGet/NuGet.Jobs/blob/master/src/NuGet.Services.Validation.Orchestrator/PackageSigning/ProcessSignature/PackageSignatureValidator.cs).
This mode is executed after the package has been repository signed so that the whole signature, as it will be seen by
package consumers, can be validated.

The first and second mode are distinguished by the `RequireRepositorySignature` boolean property on the message sent by
orchestrator. The first mode has `RequireRepositorySignature` set to `false` and the second mode has it set to `true`.

In general, the signature validation logic depends on the same logic that is run by the NuGet client, e.g.
`nuget.exe verify`. In the document below, these are referred to as "integrity and trustworthiness" but the nitty
gritty details are delegated to the NuGet client APIs.

Similar to most of the other validators, the state of the each validation is persisted in the Validation SQL DB using the
[`IValidatorStateService`](https://github.com/NuGet/NuGet.Jobs/blob/master/src/Validation.Common.Job/Storage/IValidatorStateService.cs).
Orchestrator initializes the validation record after enqueueing the message for this job. This job subsequently updates
the record to declare the validation as a success or failure.

## Multiple Job Instances ✅

Since this job is at its core a Service Bus subscription listener, you can run many instances in parallel. In other
words, this job does not have to be a singleton.

## Algorithm

1. Receive a message from Service Bus requesting a validation for a single package.
1. Load the validation status from the DB.
   1. If the validation status does not exist in the DB or is `ValidationStatus.NotStarted`, retry the message.
   1. If the validation status is not incompliete, complete the message. The validation is done.
1. Download the package from the SAS-enabled URL that orchestrator provided.
1. ❌ Reject ZIP64 packages. These are not supported for package signing.
1. If the package is unsigned, execute [the unsigned package validation](#algorithm-for-unsigned-packages).
1. If the package is signed, execute [the signed package validation](#algorithm-for-signed-packages).
1. If the package is valid and the package has been modified in the validation process, save the new .nupkg URL to the DB.
   - This URL is different than the URL provided by the orchestrator and is enabled with a READ + DELETE SAS token.
1. Save the validation status to the DB.
1. Queue-back to the orchestrator notifying it that the work is done.
   - This is an optimization. Orchestrator is periodically checking the validation record in the DB.

### Algorithm for unsigned packages

1. ❌ If author signing is required by the owner, reject the package.
1. ❌ If `RequireRepositorySignature` is true, reject the package.
1. ✔️ If all of these checks have passed, accept the package.

### Algorithm for signed packages

1. ❌ If the signature is unreadable, reject the package.
1. ❌ If the package has author counter signatures, reject the package.
1. Strip unacceptable repository signatures (e.g. signatures from another repository).
    1. A repository signature is unacceptable is:
       - The V3 service index in the metadata does not match the expected.
       - The signing certificate does not match the expected.
    1. ⚠️ If repository signature matches configuration but does not meet integrity and trustworthiness requirements, dead letter.
    1. If the package is now unsigned, [validate the unsigned package](#algorithm-for-unsigned-packages) and skip the rest of these steps.
1. ❌ If the primary signature is a repository signature and if author signing is required by the owner, reject the package.
1. If the primary signature is an author signature...
    1. If the package is already available, skip these owner-specific certificate checks. Owners can change over time.
    1. ❌ If there is a required signer and the author signature does not match, reject the package.
    1. ❌ If there is no required signer and the author signature does not match any owner, reject the package.
1. ❌ If the primary signature is not a repository or author signature, reject the package.
1. ❔ Validate the integrity and trustworthiness of just the author signature.
1. ❔ Validate the integrity and trustworthiness of the entire signature.
1. ✔️ If all of these checks have passed...
   - Extract signature and certificate information to validation DB, gallery DB, and blob storage.
     - This lays the groundwork for offline certification validation.
     - This populates certificate information in the gallery DB for display purposes.
   - Accept the package.

## Service Bus Message Shape

As with all nuget.org Service Bus messages, the `SchemaName` and `SchemaVersion` property are set on the message in
addition to the JSON message body shown below.

This message is enqueued by the orchestrator to start a signature validation.

```json
{
    "PackageId": "NuGet.Versioning",
    "PackageVersion": "4.3.0",
    "NupkgUri": "https://example.blob.core.windows.net/my-container/23/package.nupkg?READSAS",
    "ValidationId": "97c23446-9e75-4b3b-a0f8-09eb9adc74b3",
    "RequireRepositorySignature": true
}
```

The `NupkgUri` is generated by the orchestrator and is authenticated using a READ SAS token. The signature
processor downloads package to validate using this URL.

The `ValidationId` is an identifier for the specific validation step. This is different than the validation set
ID managed by the orchestrator. There will be two different values for a single package's entire validation: one
validation ID for the first step before repository signing and a different validation ID for the step after repository
signing.

The `RequireRepositorySignature` determines whether the validation will fail if a repository signature is not
present on the package. This essentially toggles between the two different modes describes above.

The `SchemaName` property is set to `SignatureValidationMessageData`. 

## Command-line arguments

```
NuGet.Jobs.Validation.PackageSigning.ProcessSignature.exe
    -Configuration <configuration_filename>
    [-InstanceName <instance_name>]
    [-InstrumentationKey <AI_instrumentation_key>]
    [-HeartbeatIntervalSeconds <seconds>]
```

`-Configuration <configuration_filename>` - the path to the service configuration file

`-InstanceName <instance_name>` - optional name of the instance used in the logs. Will appear in `InstanceName`
property of `customDimensions` object in AI. If not specified will use `NuGet.Jobs.Validation.PackageSigning.ProcessSignature`.

`-InstrumentationKey <AI_instrumentation_key>` - AI instrumentation key to send logs to. If not specified will only log
to console.

`-HeartbeatIntervalSeconds <seconds>` - optional heartbeat send interval. If specified will override the default AI
setting.

## Running the job

The easiest way to run the job if you are on the nuget.org server team is to use the DEV environment resources. This can
be done by pointing the executable to the internal DEV configuration via the `-Configuration` command-line
parameter.

Since this job is not a singleton, you can run it in parallel with the instances that are already running in the DEV
environment. However, any enqueued message may end up on the other running instances so you can consider shutting them
off temporarily while you are testing locally.

The job will wait for Service Bus messages for 24 hours before terminating.

### Prerequisites

- **Gallery DB**. This can be initialized locally with the [NuGetGallery README](https://github.com/NuGet/NuGetGallery/blob/master/README.md).
- **Validation DB**. This can be initialized locally with the [ValidationEntitiesContext](https://github.com/NuGet/ServerCommon/blob/master/src/NuGet.Services.Validation/Entities/ValidationEntitiesContext.cs).
- **ProcessSignature Service Bus**. This must be a Service Bus connection with the "Listen" permissions.
- **Orchestrator Service Bus**. This must be a Service Bus connection with the "Send" permissions. This is used for queue-back.
- **Validation Storage**. This is used for storing certificate files.
