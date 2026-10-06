// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace NuGetGallery;

public class LaunchSettingsFacts
{
    [Fact]
    public void DevelopmentProfileResolvesToLegacyFileStorageBase()
    {
        string launchSettingsPath = Path.Combine(AppContext.BaseDirectory, "launchSettings.json");
        using JsonDocument launchSettings = JsonDocument.Parse(File.ReadAllText(launchSettingsPath));
        JsonElement profile = launchSettings.RootElement
            .GetProperty("profiles")
            .GetProperty("NuGetGallery.net10");
        Assert.Equal("Project", profile.GetProperty("commandName").GetString());
        Assert.False(profile.TryGetProperty("workingDirectory", out _));

        JsonElement environmentVariables = profile.GetProperty("environmentVariables");
        Assert.Equal("Development", environmentVariables.GetProperty("ASPNETCORE_ENVIRONMENT").GetString());
        Assert.Equal("localhost", environmentVariables.GetProperty("Kestrel__Certificates__Default__Subject").GetString());
        Assert.Equal("My", environmentVariables.GetProperty("Kestrel__Certificates__Default__Store").GetString());
        Assert.Equal("LocalMachine", environmentVariables.GetProperty("Kestrel__Certificates__Default__Location").GetString());

        string storageLocation = environmentVariables.GetProperty("DataProtection__StorageLocation").GetString();
        Assert.Equal("../NuGetGallery/App_Data/Files", storageLocation);
        Assert.False(Path.IsPathRooted(storageLocation));

        string hostProjectDirectory = GetHostProjectDirectory();
        string resolvedStorageLocation = Path.GetFullPath(storageLocation, hostProjectDirectory);
        string sourceDirectory = Directory.GetParent(hostProjectDirectory)?.FullName
            ?? throw new InvalidOperationException("The host project directory has no parent.");
        string expectedLegacyBase = Path.GetFullPath(
            Path.Combine(sourceDirectory, "NuGetGallery", "App_Data", "Files"));

        Assert.Equal(expectedLegacyBase, resolvedStorageLocation, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetHostProjectDirectory()
    {
        DirectoryInfo directory = new(Path.GetDirectoryName(typeof(LaunchSettingsFacts).Assembly.Location));
        while (directory != null)
        {
            string hostProjectDirectory = Path.Combine(directory.FullName, "src", "NuGetGallery.net10");
            if (File.Exists(Path.Combine(hostProjectDirectory, "NuGetGallery.net10.csproj")))
            {
                return hostProjectDirectory;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The host project directory could not be resolved.");
    }
}
