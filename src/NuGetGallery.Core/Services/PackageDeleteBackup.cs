// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Stores the pre-delete state and backup references needed to undo an admin soft deletion of one package version.
    /// </summary>
    public class PackageDeleteBackup
    {
        public const int CurrentSchemaVersion = 1;

        // Version of this JSON format.
        [JsonProperty(Required = Required.Always)]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        [JsonProperty(Required = Required.Always)]
        public int PackageDeleteKey { get; set; }

        [JsonProperty(Required = Required.Always)]
        public int PackageKey { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string PackageId { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string NormalizedVersion { get; set; }

        // Must match the retained deletion's timestamp; not a separate capture time.
        [JsonProperty(Required = Required.Always)]
        public DateTime DeletedOnUtc { get; set; }

        // State before deletion; listing is independent of availability.
        [JsonProperty(Required = Required.Always)]
        public PackageStatus PackageStatusKey { get; set; }

        [JsonProperty(Required = Required.Always)]
        public bool Listed { get; set; }

        // Actual SHA512 of the public file; existing backup conventions determine its path. Null if absent.
        [JsonProperty(Required = Required.AllowNull)]
        public string PublicBackupHash { get; set; }

        // Actual SHA512 of the private validation file, which may differ from the public file. Null if absent.
        [JsonProperty(Required = Required.AllowNull)]
        public string ValidationBackupHash { get; set; }

        // Excludes already-deleted rows; empty when this deletion has no symbols to restore.
        [JsonProperty(Required = Required.Always)]
        public List<SymbolPackageDeleteBackup> Symbols { get; set; } = new List<SymbolPackageDeleteBackup>();

        // Null for embedded or absent READMEs; retained metadata determines whether external text is required.
        [JsonProperty(Required = Required.AllowNull)]
        public string ExternalReadmeMarkdown { get; set; }

        // Deprecation rows that recommended this package version; empty when none existed.
        [JsonProperty(Required = Required.Always)]
        public List<int> IncomingDeprecationKeys { get; set; } = new List<int>();
    }

    public class SymbolPackageDeleteBackup
    {
        [JsonProperty(Required = Required.Always)]
        public int SymbolPackageKey { get; set; }

        // Pre-delete state: Available, Validating or FailedValidation.
        [JsonProperty(Required = Required.Always)]
        public PackageStatus SymbolPackageStatusKey { get; set; }

        // Actual SHA512 of this row's public symbol file; use existing symbol backup paths. Null if absent.
        [JsonProperty(Required = Required.AllowNull)]
        public string PublicBackupHash { get; set; }

        // Actual SHA512 of this row's private validation symbol file. Null if absent.
        [JsonProperty(Required = Required.AllowNull)]
        public string ValidationBackupHash { get; set; }
    }
}