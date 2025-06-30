// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class PackageType
        : IEntity
    {
        public Package Package { get; set; }
        public int PackageKey { get; set; }

        /// <remarks>
        ///     Has a max length of 512. Is indexed. Db column is nvarchar(512).
        /// </remarks>
        public string Name { get; set; }

        public string Version { get; set; }

        /// <remarks>
        ///     Has a max length of 20,000. Db column is nvarchar(max).
        /// </remarks>
        public string CustomData { get; set; }

        [Key]
        public int Key { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is PackageType other)
            {
                return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase) &
                    string.Equals(Version, other.Version, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + (Name?.ToLowerInvariant().GetHashCode() ?? 0);
            hash = hash * 31 + (Version?.ToLowerInvariant().GetHashCode() ?? 0);
            return hash;
        }
    }
}
