// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch
{
    public class HijackIndexChange : IEquatable<HijackIndexChange>
    {
        private HijackIndexChange(NuGetVersion version, HijackIndexChangeType type)
        {
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Type = type;
        }

        public static HijackIndexChange UpdateMetadata(NuGetVersion version)
        {
            return new HijackIndexChange(version, HijackIndexChangeType.UpdateMetadata);
        }

        public static HijackIndexChange Delete(NuGetVersion version)
        {
            return new HijackIndexChange(version, HijackIndexChangeType.Delete);
        }

        public static HijackIndexChange SetLatestToFalse(NuGetVersion version)
        {
            return new HijackIndexChange(version, HijackIndexChangeType.SetLatestToFalse);
        }

        public static HijackIndexChange SetLatestToTrue(NuGetVersion version)
        {
            return new HijackIndexChange(version, HijackIndexChangeType.SetLatestToTrue);
        }

        /// <summary>
        /// The package version affected.
        /// </summary>
        public NuGetVersion Version { get; }

        /// <summary>
        /// The type of the document change.
        /// </summary>
        public HijackIndexChangeType Type { get; }

        public override bool Equals(object obj)
        {
            return Equals(obj as HijackIndexChange);
        }

        public bool Equals(HijackIndexChange change)
        {
            return change != null &&
                   Version == change.Version &&
                   Type == change.Type;
        }

        /// <summary>
        /// This was generated using Visual Studio.
        /// </summary>
        public override int GetHashCode()
        {
            var hashCode = 1834694972;
            hashCode = hashCode * -1521134295 + EqualityComparer<NuGetVersion>.Default.GetHashCode(Version);
            hashCode = hashCode * -1521134295 + Type.GetHashCode();
            return hashCode;
        }
    }
}
