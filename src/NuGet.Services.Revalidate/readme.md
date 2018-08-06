# The Revalidate Job

This job enqueues packages revalidation as fast as possible without affecting the
health of NuGet's ingestion pipeline. It does so in two phases:

1. Build Preinstalled Packages phase - the job builds a JSON file of  packages that
are installed by .NET SDK and Visual Studio
2. Initialization phase - the job determines which packages should be revalidated.
3. Revalidation phase - packages are enqueued for revalidations

These phases MUST be completed in order.

# The Build Preinstalled Packages phase

This phase should run be at development time before the job is deployed:

```
NuGet.Services.Revalidate.exe ^
    -Configuration "C:\Path\to\job\Settings\dev.json" ^
    -RebuildPreinstalledSet "C:\Path\to\job\Initialization\PreinstalledPackages.json" ^
    -Once
```

# The Initialization Phase

To initialize the job, run:

```
NuGet.Services.Revalidate.exe ^
    -Configuration "C:\Path\to\job\Settings\dev.json" ^
    -Initialize
    -VerifyInitialization
    -Once
```

This will figure which packages should be revalidated, and the order that packages
should be revalidated. Packages are prioritized by:

1. Packages owned by the `Microsoft` account
2. Packages installed by Visual Studio or by the .NET Core SDK
3. All remaining packages

Pending package revalidations are stored in the `PackageRevalidations`
table, in order of priority.

# The Revalidation Phase

To enqueue revalidations, run:

```
NuGet.Services.Revalidate.exe ^
    -Configuration "C:\Path\to\job\Settings\dev.json" ^
    -Initialize
```