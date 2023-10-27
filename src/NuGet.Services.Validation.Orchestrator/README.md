## Overview

**Subsystem: Validation 📝**

This job manages ("orchestrates") the validation of packages (.nupkg files) and symbol packages (.snupkg files) before
they become available for consumption on nuget.org.

The most notable validations that occur on a package before they are available are malware scanning and (in the case of
packages but not symbol packages) repository signing. The orchestrator delegates the actual work of validation steps to
asynchronous, downstream *validators* but manages the high level validation state and ensures that the package or symbol
package has its `PackageStatusKey` property eventually move from the `Validating` state to the `Available` or
`FailedValidation` state.

Each validation step is managed by a *validator* which has a contract with the orchestrator so that the orchestrator can
start a validation, check the status of a validation, and clean up a validation. A validator is just another job that
focuses on a specific validation step.

Orchestrator has configuration the defines which validators can run in parallel and which validators must run before
others. In other words, there is a dependency graph of validators which the orchestrator knows about and respects.

Some validators are also *processors*. A processor is just a validator that can also modify the package. The main
example of this is a job that applies a repository signature to the package. Processors cannot run in parallel since
orchestrator could end up with two different versions of the package.

The job is given work via an Azure Service Bus topic/subscription. This Service Bus topic is enqueued to by the
[NuGetGallery](https://github.com/NuGet/NuGetGallery) when a new package or symbol package is published or an admin
requests a revalidation.

Another party (e.g. the NuGetGallery) is responsible for starting a validation set for a given package but the
orchestrator is responsible for ensuring that the validations run to completion and has the responsibility of notifying
any subsequent system of the result.

There are two "versions" of the orchestrator. The two share much of the code but have separate running instances,
validators, and configuration.

1. Package orchestrator: [PackageValidationMessageHandler](https://github.com/NuGet/NuGet.Jobs/blob/main/src/NuGet.Services.Validation.Orchestrator/PackageValidationMessageHandler.cs)
2. Symbols orchestrator: [SymbolValidationMessageHandler](https://github.com/NuGet/NuGet.Jobs/blob/main/src/NuGet.Services.Validation.Orchestrator/SymbolValidationMessageHandler.cs) 

## Multiple Job Instances ✅

Since this job is at its core a Service Bus subscription listener, you can run many instances in parallel. In other
words, this job does not have to be a singleton.

## Algorithm

1. Receive a message from Service Bus requesting a validation for a single package. 
1. Check if a validation set exists for the provided tracking ID.
   1. If it does not exist, create one and initialize all validator states to "not started"
1. Find any in-progress validations and see if they are done yet.
   1. If any failed, stop doing any further validations.
   1. If any succeeded, start non-started validations that now have their dependency validations completed.
1. Start any non-started validations that have all dependency validations completed.
1. If any validations are still in-progress, re-enqueue and schedule a new Service Bus message so that
   the validation set can be checked again soon.
1. If a validation failed, mark the package as `FailedValidation` and send an email to the owner(s).
1. If all validations succeeded, mark the package as `Available` and send an email to the owner(s).
   1. Note that part of this successful end result is updating the package's `LastEdited` time, which triggers
      the [Db2Catalog job](https://github.com/NuGet/NuGet.Jobs/blob/main/src/Ng/Jobs/Db2CatalogJob.cs)
      to begin processing it in V3.

## List of Validators

The easiest way to find validators is to find types implementing [`INuGetValidator`](https://github.com/NuGet/NuGet.Jobs/blob/main/src/Validation.Common.Job/Validation/INuGetValidator.cs) or [`INuGetProcessor`](https://github.com/NuGet/NuGet.Jobs/blob/main/src/Validation.Common.Job/Validation/INuGetProcessor.cs), but this is the list as of October 16th, 2023.

### Package Orchestrator

- Process Signature (in "processor" mode)
  - `INuGetProcessor`: [`PackageSignatureProcessor`](https://github.com/NuGet/NuGet.Jobs/blob/main/src/NuGet.Services.Validation.Orchestrator/PackageSigning/ProcessSignature/PackageSignatureProcessor.cs)
  - Job implementation: [Validation.PackageSigning.ProcessSignature](https://github.com/NuGet/NuGet.Jobs/tree/main/src/Validation.PackageSigning.ProcessSignature)
- Validate Certificate
  - `INuGetValidator`: [`PackageCertificatesValidator`](https://github.com/NuGet/NuGet.Jobs/blob/main/src/NuGet.Services.Validation.Orchestrator/PackageSigning/ValidateCertificate/PackageCertificatesValidator.cs)
  - Job implementation: [Validation.PackageSigning.ValidateCertificate](https://github.com/NuGet/NuGet.Jobs/tree/main/src/Validation.PackageSigning.ValidateCertificate)
- Scan and Sign
  - `INuGetProcessor`: [`ScanAndSignProcessor`](https://github.com/NuGet/NuGet.Jobs/blob/main/src/NuGet.Services.Validation.Orchestrator/PackageSigning/ScanAndSign/ScanAndSignProcessor.cs)
  - Job implementation: **Closed-source**, due to integration with Microsoft malware scanning and package signing services.
- Process Signature (in "validator" mode)
  - `INuGetValidator`: [`PackageSignatureValidator`](https://github.com/NuGet/NuGet.Jobs/blob/main/src/NuGet.Services.Validation.Orchestrator/PackageSigning/ProcessSignature/PackageSignatureValidator.cs)
  - Job implementation: [Validation.PackageSigning.ProcessSignature](https://github.com/NuGet/NuGet.Jobs/tree/main/src/Validation.PackageSigning.ProcessSignature)

### Symbols Orchestrator

- Symbol Scan
  - `INuGetValidator`: [`SymbolScanValidator`](https://github.com/NuGet/NuGet.Jobs/blob/main/src/NuGet.Services.Validation.Orchestrator/Symbols/SymbolScanValidator.cs)
  - Job implementation: **Closed-source**, due to integration with Microsoft malware scanning service.
- Symbols Validator
  - `INuGetValidator`: [`SymbolsValidator`](https://github.com/NuGet/NuGet.Jobs/blob/main/src/NuGet.Services.Validation.Orchestrator/Symbols/SymbolsValidator.cs)
  - Job implementation: [Validation.Symbols](https://github.com/NuGet/NuGet.Jobs/tree/main/src/Validation.Symbols)
- Symbols Ingester
  - `INuGetValidator`: [`SymbolsIngester`](https://github.com/NuGet/NuGet.Jobs/blob/main/src/NuGet.Services.Validation.Orchestrator/Symbols/SymbolsIngester.cs)
  - Job implementation: **Closed-source**, due to integration with Microsoft symbol server.

## Service Bus Message Shapes

There are two kinds of messages that come to the orchestrator.

As with all nuget.org Service Bus messages, the `SchemaName` and `SchemaVersion` property are set on the message in
addition to the JSON message body shown below.

### `ProcessValidationSet` type

This message is enqueued by NuGetGallery as well as by the orchestrator itself to re-check an in-progress validation set.

```json
{
    "PackageId": "NuGet.Versioning",
    "PackageVersion": "4.3",
    "PackageNormalizedVersion": "4.3.0",
    "ValidationTrackingId": "14b4c1b8-40e2-4d60-9db7-4b7195e807f5",
    "ValidatingType": 0,
    "EntityKey": 123
}
```

The `ValidationTrackingId` is generated by NuGetGallery to refer to the specific validation attempt (i.e. a validation
set).

The `ValidatingType` is either 0 or 1. 0 refers to a package (.nupkg) and 1 refers to a symbols package (.snupkg).

The `SchemaName` property is set to `PackageValidationMessageData`. 

### `CheckValidator` type

This message is enqueued by a validator when it is done with its step. This is called a "queue-back" and is a
performance optimization. If these messages did not exist at all, orchestrator would still eventually notice that each
validation is complete because it is enqueueing a `ProcessValidationSet` message to itself over al over to check the
state of the entire validation set.

```json
{
    "ValidationId": "3fa83d31-3b44-4ffd-bfb8-02a9f5155af6"
}
```

The `ValidationId` identifies a specific validation step and is generated when the validation set is created.

The `SchemaName` property is set to `PackageValidationCheckValidatorMessageData`.

## Command-line arguments

```
NuGet.Services.Validation.Orchestrator.exe
    -Configuration <configuration_filename>
    [-InstanceName <instance_name>]
    [-InstrumentationKey <AI_instrumentation_key>]
    [-HeartbeatIntervalSeconds <seconds>]
    [-Validate]
```

`-Configuration <configuration_filename>` - the path to the service configuration file

`-InstanceName <instance_name>` - optional name of the instance used in the logs. Will appear in `InstanceName`
property of `customDimensions` object in AI. If not specified will use `NuGet.Services.Validation.Orchestrator`.

`-InstrumentationKey <AI_instrumentation_key>` - AI instrumentation key to send logs to. If not specified will only log
to console.

`-HeartbeatIntervalSeconds <seconds>` - optional heartbeat send interval. If specified will override the default AI
setting.

`-Validate <true | false>` - optional switch to perform some additional checks on the configuration without actually
processing any messages.

## Running the job

The easiest way to run the job if you are on the nuget.org server team is to use the DEV environment resources. This can
be done by pointing the orchestrator executable to the internal DEV configuration via the `-Configuration` command-line
parameter.

Since this job is not a singleton, you can run it in parallel with the instances that are already running in the DEV
environment. However, any enqueued message may end up on the other running instances so you can consider shutting them
off temporarily while you are testing locally.

The job will wait for Service Bus messages for 24 hours before terminating.

### Prerequisites

- **Gallery DB**. This can be initialized locally with the [NuGetGallery README](https://github.com/NuGet/NuGetGallery/blob/main/README.md).
- **Validation storage**. This is a connection string so that orchestrator can read and write the .nupkg/.snupkg files.
- **Orchestrator Service Bus**. This must be a Service Bus connection with both "Listen" and "Send" permissions.
- **Validation DB**. This can be initialized locally with the [ValidationEntitiesContext](https://github.com/NuGet/ServerCommon/blob/main/src/NuGet.Services.Validation/Entities/ValidationEntitiesContext.cs).
- Multiple **Validator-specific Service Bus** connections. These are Service Bus topics for each individual validator.
   - Validators must be running as well for validations to complete.
- **Flat Container storage**. This is so that orchestrator can write license files.
- **Email Service Bus**. This is for enqueueing to the email sending job.