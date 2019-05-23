// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Services.AzureSearch
{
    /// <summary>
    /// A mutable version of <see cref="HijackDocumentChanges"/>. The booleans in this class are nullable so that we can
    /// track the "undetermined" state. Once the booleans are set to true or false, they cannot be changed again. This
    /// helps protect against bugs in the code that calls
    /// <see cref="ApplyChange(SearchFilters, HijackIndexChangeType)"/> in an inconsistent manner. Technically, the
    /// <see cref="Delete"/> and <see cref="UpdateMetadata"/> do not need to be nullable because there is no code path
    /// that can set them explicitly to false. However it's better to be consistent with the latest booleans and employ
    /// the same strategy. Null booleans can be assumed by the caller to be false.
    /// </summary>
    internal class MutableHijackDocumentChanges : IEquatable<MutableHijackDocumentChanges>
    {
        public MutableHijackDocumentChanges()
        {
        }

        public MutableHijackDocumentChanges(
            bool? delete,
            bool? updateMetadata,
            bool? latestStableSemVer1,
            bool? latestSemVer1,
            bool? latestStableSemVer2,
            bool? latestSemVer2)
        {
            Delete = delete;
            UpdateMetadata = updateMetadata;
            LatestStableSemVer1 = latestStableSemVer1;
            LatestSemVer1 = latestSemVer1;
            LatestStableSemVer2 = latestStableSemVer2;
            LatestSemVer2 = latestSemVer2;
        }

        public bool? Delete { get; private set; }
        public bool? UpdateMetadata { get; private set; }
        public bool? LatestStableSemVer1 { get; private set; }
        public bool? LatestSemVer1 { get; private set; }
        public bool? LatestStableSemVer2 { get; private set; }
        public bool? LatestSemVer2 { get; private set; }

        public void ApplyChange(SearchFilters searchFilters, HijackIndexChangeType changeType)
        {
            bool latest;
            switch (changeType)
            {
                case HijackIndexChangeType.Delete:
                    Guard.Assert(
                        Delete != false,
                        "The hijack document has already been set to not delete.");
                    Guard.Assert(
                        UpdateMetadata != true,
                        "The hijack document has already been set to update metadata.");
                    Delete = true;
                    foreach (var eachSearchFilters in MutableIndexChanges.AllSearchFilters)
                    {
                        SetLatest(eachSearchFilters, latest: null);
                    }
                    return;
                case HijackIndexChangeType.UpdateMetadata:
                    Guard.Assert(
                        UpdateMetadata != false,
                        "The hijack document has already been set to not update metadata.");
                    Guard.Assert(
                        Delete != true,
                        "The hijack document has already been set to delete so metadata can't be updated.");
                    UpdateMetadata = true;
                    return;
                case HijackIndexChangeType.SetLatestToFalse:
                    latest = false;
                    break;
                case HijackIndexChangeType.SetLatestToTrue:
                    latest = true;
                    break;
                default:
                    throw new NotImplementedException($"The hijack index change type '{changeType}' is not supported.");
            }

            Guard.Assert(
                Delete != true,
                "The hijack document has already been set to delete so the latest value can't be updated.");
            SetLatest(searchFilters, latest);
        }

        public bool? GetLatest(SearchFilters searchFilters)
        {
            switch (searchFilters)
            {
                case SearchFilters.Default:
                    return LatestStableSemVer1;
                case SearchFilters.IncludePrerelease:
                    return LatestSemVer1;
                case SearchFilters.IncludeSemVer2:
                    return LatestStableSemVer2;
                case SearchFilters.IncludePrereleaseAndSemVer2:
                    return LatestSemVer2;
                default:
                    throw new NotImplementedException($"The search filters '{searchFilters}' is not supported.");
            }
        }

        private void SetLatest(SearchFilters searchFilters, bool? latest)
        {
            switch (searchFilters)
            {
                case SearchFilters.Default:
                    LatestStableSemVer1 = latest;
                    break;
                case SearchFilters.IncludePrerelease:
                    LatestSemVer1 = latest;
                    break;
                case SearchFilters.IncludeSemVer2:
                    LatestStableSemVer2 = latest;
                    break;
                case SearchFilters.IncludePrereleaseAndSemVer2:
                    LatestSemVer2 = latest;
                    break;
                default:
                    throw new NotImplementedException($"The search filters '{searchFilters}' is not supported.");
            }
        }

        public HijackDocumentChanges Solidify()
        {
            return new HijackDocumentChanges(
                Delete ?? false,
                UpdateMetadata ?? false,
                LatestStableSemVer1 ?? false,
                LatestSemVer1 ?? false,
                LatestStableSemVer2 ?? false,
                LatestSemVer2 ?? false);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MutableHijackDocumentChanges);
        }

        /// <summary>
        /// This was generated using Visual Studio.
        /// </summary>
        public bool Equals(MutableHijackDocumentChanges document)
        {
            return document != null &&
                   Delete == document.Delete &&
                   UpdateMetadata == document.UpdateMetadata &&
                   EqualityComparer<bool?>.Default.Equals(LatestStableSemVer1, document.LatestStableSemVer1) &&
                   EqualityComparer<bool?>.Default.Equals(LatestSemVer1, document.LatestSemVer1) &&
                   EqualityComparer<bool?>.Default.Equals(LatestStableSemVer2, document.LatestStableSemVer2) &&
                   EqualityComparer<bool?>.Default.Equals(LatestSemVer2, document.LatestSemVer2);
        }

        public static bool operator ==(MutableHijackDocumentChanges a, MutableHijackDocumentChanges b)
        {
            if (ReferenceEquals(a, null))
            {
                return ReferenceEquals(b, null);
            }

            return a.Equals(b);
        }

        public static bool operator !=(MutableHijackDocumentChanges a, MutableHijackDocumentChanges b)
        {
            return !(a == b);
        }

        /// <summary>
        /// This was generated using Visual Studio.
        /// </summary>
        public override int GetHashCode()
        {
            var hashCode = -1628679267;
            hashCode = hashCode * -1521134295 + Delete.GetHashCode();
            hashCode = hashCode * -1521134295 + UpdateMetadata.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<bool?>.Default.GetHashCode(LatestStableSemVer1);
            hashCode = hashCode * -1521134295 + EqualityComparer<bool?>.Default.GetHashCode(LatestSemVer1);
            hashCode = hashCode * -1521134295 + EqualityComparer<bool?>.Default.GetHashCode(LatestStableSemVer2);
            hashCode = hashCode * -1521134295 + EqualityComparer<bool?>.Default.GetHashCode(LatestSemVer2);
            return hashCode;
        }
    }
}
