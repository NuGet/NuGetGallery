// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery;

public sealed class GalleryHostOptions
{
    public const string SectionName = "GalleryHost";

    public string LocalPath { get; set; } = "/_local";
    public string HealthPath { get; set; } = "/_health";
    public string ReadinessPath { get; set; } = "/_ready";

    public static bool IsValid(GalleryHostOptions options)
    {
        if (options == null)
        {
            return false;
        }

        var paths = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        return IsValidPath(options.LocalPath, paths)
            && IsValidPath(options.HealthPath, paths)
            && IsValidPath(options.ReadinessPath, paths);
    }

    private static bool IsValidPath(string path, HashSet<string> paths)
    {
        return !string.IsNullOrWhiteSpace(path)
            && path.StartsWith("/", System.StringComparison.Ordinal)
            && paths.Add(path);
    }
}
