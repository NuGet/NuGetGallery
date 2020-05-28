// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery
{
    /// <summary>
    /// This class stores a specified user's configuration about what variants of A/B tests they are participating in.
    /// Aside from the <see cref="State"/> and <see cref="SchemaVersion"/> properties, the other properties are about
    /// which bucket number the user falls into for different A/B tests.
    /// 
    /// For each A/B test, an integer in the range [0, 99] is stored. This is a bucket number. An A/B test is
    /// configured to include a specific percentage of these buckets. For example, suppose we want to point 50% of
    /// users to a new preview search experience. If a "PreviewSearch" property has a value of less than 50, that means
    /// this user along with all other bucket values in range [0, 49] will get the new preview experience.
    /// 
    /// The reason a bucket value is stored instead of a simple boolean (i.e. UsesPreviewSearch = true | false) is to
    /// make percentage increase transitions consistent for users. If we start with 2% of users on preview search and
    /// then later want 10% of users, the original 2% should still see the preview search. Storing the bucket value
    /// means that each user's placement on the range of percentage thresholds remains consistent.
    /// </summary>
    public class ABTestEnrollment
    {
        public ABTestEnrollment(ABTestEnrollmentState state, int schemaVersion, int previewSearchBucket, int packageDependentBucket)
        {
            State = state;
            SchemaVersion = schemaVersion;
            PreviewSearchBucket = previewSearchBucket;
            PackageDependentBucket = packageDependentBucket;
        }
        public ABTestEnrollmentState State { get; }
        public int SchemaVersion { get; }
        public int PreviewSearchBucket { get; }
        public int PackageDependentBucket { get; }
    }
}