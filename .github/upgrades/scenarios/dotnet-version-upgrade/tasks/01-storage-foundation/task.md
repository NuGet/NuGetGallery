# 01-storage-foundation: Refactor Gallery storage without changing behavior

Create the independently deployable storage foundation across `NuGetGallery.Core`, `NuGetGallery.Services`, the legacy web project, jobs, AccountDeleter, shared solutions, and all tests and registration sites. Move `IsAvailableAsync` to `ICoreFileStorageService`; replace the MVC-returning download API with a framework-neutral `DownloadFileResult` that explicitly represents redirect, local-file, and not-found outcomes; propagate that result through package services; and map it to the unchanged MVC response only at controller boundaries. Remove `IFileStorageService` and update every implementation, mock, keyed Autofac registration, Microsoft DI registration, and consumer atomically.

Extract or move the framework-neutral portions of `FileSystemFileStorageService`, `IFileSystemService`, and `FileSystemService` into `NuGetGallery.Core`, leaving only unavoidable legacy root-resolution and `HostingEnvironment.MapPath` behavior in the web adapter. Preserve existing filesystem and Blob behavior, including redirect policy, CDN/query/version handling, conflict semantics, resource lifetimes, status codes, headers, filenames, and content types. Do not add file enumeration, Data Protection packages, key-ring storage, new storage capabilities, or any .NET 10 migration wiring.

**Done when**: A storage-only commit can be cherry-picked onto unmodified `dev`; `NuGetGallery.sln`, `NuGet.Server.Common.sln`, `NuGet.Jobs.sln`, and AccountDeleter's build surface restore and build; targeted storage, package-download, controller, registration, and availability tests pass; and observable HTTP and storage behavior is unchanged.

## Research findings

Confirmed against the repository on 2026-09-08 before source changes:

- `ICoreFileStorageService` is in `src/NuGetGallery.Core/Services/ICoreFileStorageService.cs`; `IFileStorageService` is a thin MVC-dependent extension in `src/NuGetGallery.Services/Storage/IFileStorageService.cs` that adds only `CreateDownloadFileActionResultAsync` and `IsAvailableAsync`.
- The Blob implementation is `src/NuGetGallery.Services/Storage/CloudBlobFileStorageService.cs`. Its download path returns `System.Web.Mvc.RedirectResult`; it preserves redirect-policy validation, optional `AzureCdnHost`, request/blob scheme and port behavior, blob-query precedence, request-query forwarding, and the `packageVersion` query parameter.
- The local implementation and filesystem abstraction are currently compiled directly into the legacy web project from `src/NuGetGallery/Services/{FileSystemFileStorageService,IFileSystemService,FileSystemService}.cs`. Only `IAppConfiguration` lookup and `HostingEnvironment.IsHosted/MapPath` are inherently web-specific. The remaining implementation uses `System.IO` and Gallery.Core storage contracts and can move to `NuGetGallery.Core`.
- The neutral contract will expose `IsAvailableAsync` and `CreateDownloadFileResultAsync` on `ICoreFileStorageService`. `DownloadFileResult` will explicitly distinguish redirect, local-file, and not-found outcomes while carrying the same redirect URI, local path, content type, and download filename data used by MVC today.
- `PackageFileService`, `SymbolPackageFileService`, `IPackageFileService`, and `ISymbolPackageFileService` are the propagation layer. The two download actions in `Controllers/ApiController.cs` are the only MVC response boundaries and will translate the neutral result back to the same `RedirectResult`, `FilePathResult`, or `HttpNotFoundResult`.
- Direct `IFileStorageService` consumers to convert to `ICoreFileStorageService`: `ContentService`, `CertificateService`, `UploadFileService`, `PackageFileService`, and `SymbolPackageFileService`, plus their facts. `StatusService` contains only an obsolete comment reference.
- Registration sites requiring atomic updates are `App_Start/DefaultDependenciesModule.cs` (default and keyed Autofac registrations/constructor parameter resolution), `App_Start/StorageDependent.cs` and its facts, and `AccountDeleter/Job.cs` (Microsoft DI). Existing job registrations already use `ICoreFileStorageService`.
- Tests requiring behavior updates are `CloudBlobFileStorageServiceFacts`, `FileSystemFileStorageServiceFacts`, `PackageFileServiceFacts`, `ApiControllerFacts`, `StorageDependentFacts`, `ContentServiceFacts`, `CertificateServiceFacts`, and `UploadFileServiceFacts`. Existing `Mock<ICoreFileStorageService>` sites do not require setup for the newly added member unless they exercise availability/download behavior.
- Project/solution impact: `NuGetGallery.Core` and `NuGetGallery.Services` are SDK-style and multi-target `net472;netstandard2.1`; the legacy `NuGetGallery.csproj` is non-SDK-style and explicitly lists the three filesystem source files; AccountDeleter targets `net472`. `NuGetGallery.sln`, `NuGet.Server.Common.sln`, and `NuGet.Jobs.sln` already include/reference the shared projects, so no new solution project is needed.
- Validation must use the repository `build.ps1`/Visual Studio MSBuild path for the legacy Gallery surface. Targeted xUnit runs will cover the storage implementations, package propagation, controller mapping, registrations, and availability behavior before the requested solution/build surfaces.

## Implementation pattern

1. Add the neutral result and complete neutral storage contract in `NuGetGallery.Core`.
2. Move the filesystem implementation and abstraction to Core with a resolved root-path constructor; retain legacy `~/` resolution in a small web adapter/resolver.
3. Convert Blob and all consumers/registrations from `IFileStorageService` to `ICoreFileStorageService`, then delete the old interface.
4. Propagate neutral download results through package services and map them to MVC only in `ApiController`.
5. Update tests to assert the neutral outcome data and unchanged MVC boundary results, then run targeted tests and requested builds.
