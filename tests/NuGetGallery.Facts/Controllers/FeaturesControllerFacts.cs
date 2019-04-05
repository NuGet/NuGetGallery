// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGet.Services.FeatureFlags;
using NuGetGallery.Areas.Admin.Controllers;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Configuration;
using NuGetGallery.Features;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class FeaturesControllerFacts
    {
        public class TheIndexMethod : FeaturesControllerFactsBase
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReturnsViewWithModel(bool hasLastUpdated)
            {
                // Arrange
                SetupGetModel(hasLastUpdated);

                // Act
                var result = await GetController<FeaturesController>().Index();

                // Assert
                var model = ResultAssert.IsView<FeatureFlagsViewModel>(result);
                AssertFlags(Flags, model, hasLastUpdated, RefreshInterval, ContentId);
            }
        }

        public class TheAddFeatureMethod : FeatureBase
        {
            public static ModifyFeatureFlagsFeatureViewModel ValidDisabledModel =
                new ModifyFeatureFlagsFeatureViewModel
                {
                    Name = "MyNewDisabledFeature",
                    Status = FeatureStatus.Disabled,
                    ContentId = "a"
                };

            public static ModifyFeatureFlagsFeatureViewModel ValidEnabledModel =
                new ModifyFeatureFlagsFeatureViewModel
                {
                    Name = "MyNewEnabledFeature",
                    Status = FeatureStatus.Disabled,
                    ContentId = "b"
                };

            public static FeatureFlags GetValidFlags(ModifyFeatureFlagsFeatureViewModel model)
            {
                var features = Flags.Features.ToDictionary(f => f.Key, f => f.Value);
                features.Add(model.Name, model.Status);

                return new FeatureFlags(
                    features,
                    Flags.Flights);
            }

            public static object[] GetValidModelDataSet(ModifyFeatureFlagsFeatureViewModel model) =>
                MemberDataHelper.AsData(
                        model,
                        GetValidFlags(model));

            public static IEnumerable<ModifyFeatureFlagsFeatureViewModel> ValidModels = 
                new[] { ValidDisabledModel, ValidEnabledModel };

            public static IEnumerable<object[]> ValidModelsWithLastUpdated_Data
            {
                get
                {
                    foreach (var hasLastUpdated in new[] { false, true })
                    {
                        foreach (var model in ValidModels)
                        {
                            yield return MemberDataHelper.AsData(hasLastUpdated, model, GetValidFlags(model));
                        }
                    }
                }
            }

            public static IEnumerable<object[]> ReturnsViewWithFailureForExisting_Data =>
                MemberDataHelper
                    .Combine(
                        MemberDataHelper.BooleanDataSet(),
                        MemberDataHelper.EnumDataSet<FeatureStatus>())
                    .ToList();

            [Theory]
            [MemberData(nameof(ReturnsViewWithFailureForExisting_Data))]
            public Task ReturnsViewWithFailureForExisting(bool hasLastUpdated, FeatureStatus status)
            {
                var model = new ModifyFeatureFlagsFeatureViewModel
                {
                    Name = Feature1Name,
                    Status = status,
                    ContentId = "c"
                };

                return AssertFailure(
                    hasLastUpdated,
                    model,
                    GetTryAddExistingErrorMessage(model));
            }

            protected override Task<ActionResult> InvokeAsync(FeaturesController controller, ModifyFeatureFlagsFeatureViewModel model)
            {
                return controller.AddFeature(model);
            }
        }

        public class TheEditFeatureMethod : FeatureBase
        {
            public static ModifyFeatureFlagsFeatureViewModel ValidEditFeature1Model =
                new ModifyFeatureFlagsFeatureViewModel
                {
                    Name = Feature1Name,
                    Status = FeatureStatus.Enabled,
                    ContentId = "a"
                };

            public static ModifyFeatureFlagsFeatureViewModel ValidEditFeature2Model =
                new ModifyFeatureFlagsFeatureViewModel
                {
                    Name = Feature2Name,
                    Status = FeatureStatus.Disabled,
                    ContentId = "b"
                };

            public static FeatureFlags GetValidFlags(ModifyFeatureFlagsFeatureViewModel model)
            {
                var features = Flags.Features.ToDictionary(f => f.Key, f => f.Value);
                features[model.Name] = model.Status;

                return new FeatureFlags(
                    features,
                    Flags.Flights);
            }

            public static object[] GetValidModelDataSet(ModifyFeatureFlagsFeatureViewModel model) =>
                MemberDataHelper.AsData(
                        model,
                        GetValidFlags(model));

            public static IEnumerable<ModifyFeatureFlagsFeatureViewModel> ValidModels =
                new[] { ValidEditFeature1Model, ValidEditFeature2Model };

            public static IEnumerable<object[]> ValidModelsWithLastUpdated_Data
            {
                get
                {
                    foreach (var hasLastUpdated in new[] { false, true })
                    {
                        foreach (var model in ValidModels)
                        {
                            yield return MemberDataHelper.AsData(hasLastUpdated, model, GetValidFlags(model));
                        }
                    }
                }
            }

            public static IEnumerable<object[]> ReturnsViewWithFailureForExisting_Data =>
                MemberDataHelper.Combine(
                    MemberDataHelper.BooleanDataSet(),
                    MemberDataHelper.EnumDataSet<FeatureStatus>());

            [Theory]
            [MemberData(nameof(ReturnsViewWithFailureForExisting_Data))]
            public Task ReturnsViewWithFailureForMissing(bool hasLastUpdated, FeatureStatus status)
            {
                var model = new ModifyFeatureFlagsFeatureViewModel
                {
                    Name = "NotAFeature",
                    Status = status,
                    ContentId = "c"
                };

                return AssertFailure(
                    hasLastUpdated,
                    model,
                    GetTryEditMissingErrorMessage(model));
            }

            protected override Task<ActionResult> InvokeAsync(FeaturesController controller, ModifyFeatureFlagsFeatureViewModel model)
            {
                return controller.EditFeature(model);
            }
        }

        public class TheDeleteFeatureMethod : FeatureBase
        {
            public static ModifyFeatureFlagsFeatureViewModel ValidDeleteFeature1Model =
                new ModifyFeatureFlagsFeatureViewModel
                {
                    Name = Feature1Name,
                    ContentId = "a"
                };

            public static ModifyFeatureFlagsFeatureViewModel ValidDeleteFeature2Model =
                new ModifyFeatureFlagsFeatureViewModel
                {
                    Name = Feature2Name,
                    ContentId = "b"
                };

            public static FeatureFlags GetValidFlags(ModifyFeatureFlagsFeatureViewModel model)
            {
                var features = Flags.Features.ToDictionary(f => f.Key, f => f.Value);
                features.Remove(model.Name);

                return new FeatureFlags(
                    features,
                    Flags.Flights);
            }

            public static object[] GetValidModelDataSet(ModifyFeatureFlagsFeatureViewModel model) =>
                MemberDataHelper.AsData(
                        model,
                        GetValidFlags(model));

            public static IEnumerable<ModifyFeatureFlagsFeatureViewModel> ValidModels =
                new[] { ValidDeleteFeature1Model, ValidDeleteFeature2Model };

            public static IEnumerable<object[]> ValidModelsWithLastUpdated_Data
            {
                get
                {
                    foreach (var hasLastUpdated in new[] { false, true })
                    {
                        foreach (var model in ValidModels)
                        {
                            yield return MemberDataHelper.AsData(hasLastUpdated, model, GetValidFlags(model));
                        }
                    }
                }
            }

            public static IEnumerable<object[]> ReturnsViewWithFailureForExisting_Data =>
                MemberDataHelper.Combine(
                    MemberDataHelper.BooleanDataSet(),
                    MemberDataHelper.EnumDataSet<FeatureStatus>());

            [Theory]
            [MemberData(nameof(ReturnsViewWithFailureForExisting_Data))]
            public Task ReturnsViewWithFailureForMissing(bool hasLastUpdated, FeatureStatus status)
            {
                var model = new ModifyFeatureFlagsFeatureViewModel
                {
                    Name = "MissingFeature",
                    Status = status,
                    ContentId = "c"
                };

                return AssertFailure(
                    hasLastUpdated,
                    model,
                    GetTryDeleteMissingErrorMessage(model));
            }

            protected override Task<ActionResult> InvokeAsync(FeaturesController controller, ModifyFeatureFlagsFeatureViewModel model)
            {
                return controller.DeleteFeature(model);
            }
        }

        public abstract class FeatureBase : ModifyMethodBase<ModifyFeatureFlagsFeatureViewModel>
        {
            protected override string PrettyName => "feature";

            // If there was validation for features, a test for it would go here.
        }

        public abstract class FlightBase : ModifyMethodBase<ModifyFeatureFlagsFlightViewModel>
        {
            public const string ExistingAccount = "account";

            public FlightBase()
            {
                GetMock<UserService>()
                    .Setup(x => x.FindByUsername(ExistingAccount, false))
                    .Returns(new User(ExistingAccount));
            }

            protected override string PrettyName => "flight";

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public Task ReturnsViewWithValidationFailure(bool hasLastUpdated)
            {
                var missingAccount1 = "account1";
                var missingAccount2 = "account2";

                var flightName = "flighty-flight";
                var model = new ModifyFeatureFlagsFlightViewModel
                {
                    Name = flightName,
                    Accounts = new[] { missingAccount1, ExistingAccount, missingAccount2 }
                };

                return AssertFailure(
                    hasLastUpdated,
                    model,
                    $"Some accounts specified by the flight '{flightName}' ({string.Join(", ", new[] { missingAccount1, missingAccount2 })}) do not exist. A flight cannot specify accounts that do not exist.");
            }
        }

        public abstract class ModifyMethodBase<TModify> : FeaturesControllerFactsBase
            where TModify : class, IModifyFeatureFlagsViewModel
        {
            protected abstract string PrettyName { get; }
            protected abstract Task<ActionResult> InvokeAsync(FeaturesController controller, TModify model);

            [Theory]
            [MemberData("ValidModelsWithLastUpdated_Data")]
            public async Task ReturnsViewWithChange(
                bool hasLastUpdated,
                TModify model,
                FeatureFlags flags)
            {
                // Arrange
                SetupGetModel(hasLastUpdated);

                var controller = GetController<FeaturesController>();

                GetMock<IEditableFeatureFlagStorageService>()
                    .Setup(x => x.TrySaveAsync(
                        It.Is<FeatureFlags>(f => DoFlagsMatch(flags, f)),
                        model.ContentId))
                    .ReturnsAsync(FeatureFlagSaveResult.Ok);

                // Act
                var result = await InvokeAsync(controller, model);

                // Assert
                AssertRedirectToIndex(result);
                AssertSuccessMessage(controller);
            }

            protected static IEnumerable<object[]> ReturnsViewWithSaveError_TypeAndMessageData
            {
                get
                {
                    yield return MemberDataHelper.AsData(
                        FeatureFlagSaveResultType.Conflict,
                        "Your changes were not applied as the feature flags were modified by someone else. Please reload the page and try again.");
                    yield return MemberDataHelper.AsData(
                        (FeatureFlagSaveResultType)99,
                        "Unknown save result 'NuGetGallery.Features.FeatureFlagSaveResult': message.");
                }
            }

            [Theory]
            [MemberData("ValidModelsWithLastUpdated_Data")]
            public Task ReturnsViewWithSaveErrorConflict(
                bool hasLastUpdated,
                TModify validModel,
                FeatureFlags flags)
            {
                return ReturnsViewWithSaveErrorConflict(
                    hasLastUpdated,
                    validModel,
                    FeatureFlagSaveResultType.Conflict,
                    "Your changes were not applied as the feature flags were modified by someone else. Please reload the page and try again.");
            }

            [Theory]
            [MemberData("ValidModelsWithLastUpdated_Data")]
            public Task ReturnsViewWithSaveErrorUnknown(
                bool hasLastUpdated,
                TModify validModel,
                FeatureFlags flags)
            {
                return ReturnsViewWithSaveErrorConflict(
                    hasLastUpdated,
                    validModel,
                    (FeatureFlagSaveResultType)99,
                    "Unknown save result 'NuGetGallery.Features.FeatureFlagSaveResult': message.");
            }

            private async Task ReturnsViewWithSaveErrorConflict(
                bool hasLastUpdated,
                TModify validModel,
                FeatureFlagSaveResultType resultType,
                string errorMessage)
            {
                // Arrange
                SetupGetModel(hasLastUpdated);

                var controller = GetController<FeaturesController>();

                GetMock<IEditableFeatureFlagStorageService>()
                    .Setup(x => x.TrySaveAsync(
                        It.IsAny<FeatureFlags>(),
                        validModel.ContentId))
                    .ReturnsAsync(new FeatureFlagSaveResult(resultType, "message"));

                // Act
                var result = await InvokeAsync(controller, validModel);

                // Assert
                AssertRedirectToIndex(result);
                AssertErrorMessage(controller, errorMessage);
            }

            [Theory]
            [MemberData("ValidModelsWithLastUpdated_Data")]
            public Task ReturnsViewWithModelErrors(
                bool hasLastUpdated,
                TModify model,
                FeatureFlags flags) // I have kept this parameter to reduce the need to make another data function without it.
            {
                var modelState = GetController<FeaturesController>().ModelState;
                modelState.AddModelError("key1", "error1");
                modelState.AddModelError("key2", "error2");

                return AssertFailure(
                    hasLastUpdated,
                    model,
                    "The model submitted was invalid: error1 error2");
            }

            [Theory]
            [MemberData("ValidModelsWithLastUpdated_Data")]
            public Task ReturnsViewWithModelErrorWithNoMessage(
                bool hasLastUpdated,
                TModify model,
                FeatureFlags flags) // I have kept this parameter to reduce the need to make another data function without it.
            {
                var modelState = GetController<FeaturesController>().ModelState;
                modelState.AddModelError("key", string.Empty);

                return AssertFailure(
                    hasLastUpdated,
                    model,
                    "The model submitted was invalid.");
            }

            protected string GetTryAddExistingErrorMessage(TModify model) =>
                $"The {PrettyName} '{model.Name}' already exists. You cannot add a {PrettyName} that already exists.";

            protected string GetTryEditMissingErrorMessage(TModify model) =>
                $"The {PrettyName} '{model.Name}' does not exist. You cannot edit a {PrettyName} that does not exist.";

            protected string GetTryDeleteMissingErrorMessage(TModify model) =>
                $"The {PrettyName} '{model.Name}' does not exist. You cannot delete a {PrettyName} that does not exist.";

            protected async Task AssertFailure(
                bool hasLastUpdated,
                TModify model,
                string expectedMessage)
            {
                // Arrange
                SetupGetModel(hasLastUpdated);

                var controller = GetController<FeaturesController>();

                // Act
                var result = await InvokeAsync(controller, model);

                // Assert
                AssertRedirectToIndex(result);
                AssertErrorMessage(controller, expectedMessage);
            }

            protected void AssertSuccessMessage(FeaturesController controller)
            {
                AssertTempData(controller, "Message", 
                    $"Your feature flags have been saved! It may take up to {RefreshInterval.TotalSeconds} seconds for this change to propagate everywhere.");
                AssertTempData(controller, "ErrorMessage", null);
            }

            protected void AssertErrorMessage(FeaturesController controller, string expectedErrorMessage)
            {
                AssertTempData(controller, "Message", null);
                AssertTempData(controller, "ErrorMessage", expectedErrorMessage);
            }

            protected void AssertTempData(FeaturesController controller, string fieldName, string expected)
            {
                Assert.Equal(expected, controller.TempData[fieldName]);
            }

            protected void AssertRedirectToIndex(ActionResult result)
            {
                ResultAssert.IsRedirectToRoute(result, new { action = nameof(FeaturesController.Index) });
            }
        }

        public class FeaturesControllerFactsBase : TestContainer
        {
            public const string ContentId = "contentId";
            public static TimeSpan RefreshInterval = new TimeSpan(0, 5, 0);

            public const string Feature1Name = "Feature1";
            public const FeatureStatus Feature1Status = FeatureStatus.Disabled;

            public const string Feature2Name = "Feature2";
            public const FeatureStatus Feature2Status = FeatureStatus.Enabled;

            public const string Flight1Name = "Flight1";
            public static Flight Flight1Value = new Flight(false, false, null, null);

            public const string Flight2Name = "Flight2";
            public static Flight Flight2Value = new Flight(true, true, new[] { "a", "b" }, new[] { "domain1.com", "domain2.com" });

            public static FeatureFlags Flags = new FeatureFlags(
                new Dictionary<string, FeatureStatus>
                {
                    { Feature1Name, Feature1Status },
                    { Feature2Name, Feature2Status }
                },
                new Dictionary<string, Flight>
                {
                    { Flight1Name, Flight1Value },
                    { Flight2Name, Flight2Value }
                });

            protected void SetupGetModel(bool hasLastUpdated)
            {
                GetMock<IEditableFeatureFlagStorageService>()
                    .Setup(x => x.GetReferenceAsync())
                    .ReturnsAsync(new FeatureFlagReference(Flags, ContentId));

                GetMock<IFeatureFlagCacheService>()
                    .Setup(x => x.GetRefreshTimeOrNull())
                    .Returns(hasLastUpdated ? (DateTimeOffset?)new DateTimeOffset(2019, 4, 4, 1, 1, 1, TimeSpan.Zero) : null);

                GetMock<IAppConfiguration>()
                    .Setup(x => x.FeatureFlagsRefreshInterval)
                    .Returns(RefreshInterval);
            }
        }

        public static bool DoFlagsMatch(FeatureFlags first, FeatureFlags second)
        {
            try
            {
                AssertFlags(first, second);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void AssertFlags(
            FeatureFlags expectedFlags,
            FeatureFlags actualFlags)
        {
            Assert.Equal(expectedFlags.Features.Count, actualFlags.Features.Count);
            Assert.Equal(expectedFlags.Flights.Count, actualFlags.Flights.Count);

            foreach (var feature in actualFlags.Features)
            {
                Assert.True(expectedFlags.Features.ContainsKey(feature.Key));
                Assert.Equal(expectedFlags.Features[feature.Key], feature.Value);
            }

            foreach (var flight in actualFlags.Flights)
            {
                Assert.True(expectedFlags.Flights.ContainsKey(flight.Key));
                AssertFlight(expectedFlags.Flights[flight.Key], flight.Value);
            }
        }

        public static void AssertFlags(
            FeatureFlags flags,
            FeatureFlagsViewModel model,
            bool hasLastUpdated,
            TimeSpan refreshInterval,
            string contentId)
        {
            if (hasLastUpdated)
            {
                Assert.True(model.TimeSinceLastRefresh > TimeSpan.Zero);
            }
            else
            {
                Assert.Null(model.TimeSinceLastRefresh);
            }

            Assert.Equal(refreshInterval, model.RefreshInterval);
            Assert.Equal(contentId, model.ContentId);

            AssertFlags(flags, model);
        }

        public static void AssertFlags(FeatureFlags flags, FeatureFlagsViewModel model)
        {
            Assert.Equal(flags.Features.Count, model.Features.Count);
            Assert.Equal(flags.Flights.Count, model.Flights.Count);

            foreach (var feature in model.Features)
            {
                Assert.True(flags.Features.ContainsKey(feature.Name));
                Assert.Equal(flags.Features[feature.Name], feature.Status);
            }

            foreach (var flight in model.Flights)
            {
                Assert.True(flags.Flights.ContainsKey(flight.Name));
                AssertFlight(flags.Flights[flight.Name], flight);
            }
        }

        public static void AssertFlight(Flight expected, Flight actual)
        {
            Assert.Equal(expected.All, actual.All);
            Assert.Equal(expected.SiteAdmins, actual.SiteAdmins);
            Assert.Equal(expected.Accounts, actual.Accounts);
            Assert.Equal(expected.Domains, actual.Domains);
        }

        public static void AssertFlight(Flight expected, FeatureFlagsFlightViewModel actual)
        {
            Assert.Equal(expected.All, actual.All);
            Assert.Equal(expected.SiteAdmins, actual.SiteAdmins);
            Assert.Equal(expected.Accounts, actual.Accounts);
            Assert.Equal(expected.Domains, actual.Domains);
        }
    }
}
