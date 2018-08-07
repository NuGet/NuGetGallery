// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace ArchivePackages
{
    public class PackageRef
    {
        public PackageRef(string id, string version, string hash)
        {
            Id = id;
            Version = version;
            Hash = hash;
        }

        public PackageRef(string id, string version, string hash, DateTime lastEdited)
            : this(id, version, hash)
        {
            LastEdited = lastEdited;
        }

        public PackageRef(string id, string version, string hash, DateTime lastEdited, DateTime published)
            : this(id, version, hash, lastEdited)
        {
            Published = published;
        }

        public string Id { get; set; }

        public string Version { get; set; }

        public string Hash { get; set; }

        public DateTime? LastEdited { get; set; }

        public DateTime? Published { get; set; }
    }
}
