// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Metadata.Catalog.Helpers;
using NuGet.Services.Metadata.Catalog.Monitoring;
using NuGet.Services.Metadata.Catalog.Persistence;
using NuGetGallery;
using Xunit;

namespace NgTests
{
    public class PackageMonitoringStatusAccessConditionHelperTests
    {
        [Fact]
        public void FromContentReturnsEmptyIfNoETag()
        {
            var content = new StringStorageContent("content");
            PackageMonitoringStatusTestUtility.AssertAccessCondition(
                AccessConditionWrapper.GenerateEmptyCondition(),
                PackageMonitoringStatusAccessConditionHelper.FromContent(content));
        }

        [Fact]
        public void FromContentReturnsEmptyIfNullETag()
        {
            var content = new StringStorageContentWithETag("content", null);
            PackageMonitoringStatusTestUtility.AssertAccessCondition(
                AccessConditionWrapper.GenerateEmptyCondition(),
                PackageMonitoringStatusAccessConditionHelper.FromContent(content));
        }

        [Fact]
        public void FromContentReturnsMatchIfETag()
        {
            var eTag = "etag";
            var content = new StringStorageContentWithETag("content", eTag);
            PackageMonitoringStatusTestUtility.AssertAccessCondition(
                AccessConditionWrapper.GenerateIfMatchCondition(eTag),
                PackageMonitoringStatusAccessConditionHelper.FromContent(content));
        }

        public static IEnumerable<object[]> UpdateFromExistingUpdatesExistingStatus_Data
        {
            get
            {
                foreach (var previousState in Enum.GetValues(typeof(PackageState)).Cast<PackageState>())
                {
                    foreach (var accessCondition in
                        new[]
                        {
                            AccessConditionWrapper.GenerateIfNotExistsCondition(),
                            AccessConditionWrapper.GenerateIfMatchCondition("howdy"),
                            AccessConditionWrapper.GenerateEmptyCondition()
                        })
                    {
                        foreach (var newState in Enum.GetValues(typeof(PackageState)).Cast<PackageState>())
                        {
                            yield return new object[] { previousState, accessCondition, newState };
                        }
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(UpdateFromExistingUpdatesExistingStatus_Data))]
        public void UpdateFromExistingUpdatesExistingStatus(PackageState previousState, IAccessCondition accessCondition, PackageState newState)
        {
            // Arrange
            var feedPackageIdentity = new FeedPackageIdentity("howdy", "3.4.6");

            var existingStatus = PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                feedPackageIdentity.Id,
                feedPackageIdentity.Version,
                PackageMonitoringStatusTestUtility.GetTestResultFromPackageState(previousState));

            existingStatus.AccessCondition = accessCondition;

            var newStatus = PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                feedPackageIdentity.Id,
                feedPackageIdentity.Version,
                PackageMonitoringStatusTestUtility.GetTestResultFromPackageState(newState));

            // Act
            PackageMonitoringStatusAccessConditionHelper.UpdateFromExisting(newStatus, existingStatus);

            // Assert
            foreach (var state in Enum.GetValues(typeof(PackageState)).Cast<PackageState>())
            {
                PackageMonitoringStatusTestUtility.AssertAccessCondition(
                    state == previousState ? accessCondition : AccessConditionWrapper.GenerateIfNotExistsCondition(),
                    newStatus.ExistingState[state]);
            }
        }

        public static IEnumerable<object[]> UpdateFromExistingUpdatesExistingStatusWhenNull_Data
        {
            get
            {
                foreach (var newState in Enum.GetValues(typeof(PackageState)).Cast<PackageState>())
                {
                    yield return new object[] { newState };
                }
            }
        }

        [Theory]
        [MemberData(nameof(UpdateFromExistingUpdatesExistingStatusWhenNull_Data))]
        public void UpdateFromExistingUpdatesExistingStatusWhenNull(PackageState newState)
        {
            // Arrange
            var feedPackageIdentity = new FeedPackageIdentity("howdy", "3.4.6");

            var newStatus = PackageMonitoringStatusTestUtility.CreateStatusWithPackageValidationResult(
                feedPackageIdentity.Id,
                feedPackageIdentity.Version,
                PackageMonitoringStatusTestUtility.GetTestResultFromPackageState(newState));

            // Act
            PackageMonitoringStatusAccessConditionHelper.UpdateFromExisting(newStatus, null);

            // Assert
            foreach (var state in Enum.GetValues(typeof(PackageState)).Cast<PackageState>())
            {
                PackageMonitoringStatusTestUtility.AssertAccessCondition(
                    AccessConditionWrapper.GenerateIfNotExistsCondition(),
                    newStatus.ExistingState[state]);
            }
        }

        [Fact]
        public void FromUnknownReturnsEmptyCondition()
        {
            PackageMonitoringStatusTestUtility.AssertAccessCondition(
                AccessConditionWrapper.GenerateEmptyCondition(),
                PackageMonitoringStatusAccessConditionHelper.FromUnknown());
        }
    }
}
