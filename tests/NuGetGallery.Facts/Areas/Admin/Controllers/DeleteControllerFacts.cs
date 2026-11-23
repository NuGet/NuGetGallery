// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Moq;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Filters;
using NuGetGallery.Framework;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class DeleteControllerFacts
    {
        public class TheSearchMethod : FactsBase
        {
            [Fact]
            public void DoesNotMakeDuplicateQueries()
            {
                // Arrange
                _query = "NuGet.Versioning" + Environment.NewLine + "  NUGET.VERSIONING   ";

                // Act
                var result = _target.Search(_query);

                // Assert
                _packageService.Verify(
                    x => x.FindPackageRegistrationById("NuGet.Versioning"),
                    Times.Once);
            }

            [Fact]
            public void DoesNotReturnDuplicatePackages()
            {
                // Arrange
                _packages.Add(_packages[0]);

                // Act
                var result = _target.Search(_query);

                // Assert
                var jsonResult = Assert.IsType<JsonResult>(result);
                var searchResults = Assert.IsType<List<PackageSearchResult>>(jsonResult.Data);
                var uniqueIdentities = searchResults.Select(p => $"{p.PackageId} {p.PackageVersionNormalized}").Distinct();
                Assert.Equal(searchResults.Count, uniqueIdentities.Count());
            }

            [Fact]
            public void UsesVersionSpecificIdIfAvailable()
            {
                // Arrange
                _packages[0].Id = "nuget.versioning";

                // Act
                var result = _target.Search(_query);

                // Assert
                var jsonResult = Assert.IsType<JsonResult>(result);
                var searchResults = Assert.IsType<List<PackageSearchResult>>(jsonResult.Data);
                Assert.Equal("NuGet.Versioning", searchResults[0].PackageId);
                Assert.Equal("4.3.0", searchResults[0].PackageVersionNormalized);
                Assert.Equal("nuget.versioning", searchResults[1].PackageId);
                Assert.Equal("4.4.0", searchResults[1].PackageVersionNormalized);
            }
        }

        public class TheUndoMethod : FactsBase
        {
            [Fact]
            public async Task PassesReasonAndCallerToTheBatchService()
            {
                var request = CreateUndoRequest();
                request.Reason = new string('a', 1000);
                ValidateRequest(request);
                _batch.Setup(x => x.UndoAsync(request.Packages, request.Reason, "admin"))
                    .ReturnsAsync(new UndoSoftDeleteBatchResult(request.Packages
                        .Select(package => new UndoSoftDeletePackageResult(package)).ToList()));

                var result = GetJson(await _target.Undo(request));

                Assert.True(_target.ModelState.IsValid);
                Assert.True(result.Value<bool>("Success"));
                Assert.Equal("Successfully restored the selected packages. Feed and search updates may take time to appear.", result.Value<string>("Message"));
                Assert.Equal(2, result["Results"].Count());
                Assert.All(result["Results"], entry => Assert.True(entry.Value<bool>("Success")));
                _batch.Verify(x => x.UndoAsync(request.Packages, request.Reason, "admin"), Times.Once);
                _packageService.VerifyNoOtherCalls();
                _packageDeleteService.VerifyNoOtherCalls();
            }

            [Theory]
            [InlineData(null)]
            [InlineData(1001)]
            public async Task RejectsInvalidReasons(int? length)
            {
                var request = CreateUndoRequest();
                request.Reason = length.HasValue ? new string('a', length.Value) : null;
                ValidateRequest(request);

                var result = GetJson(await _target.Undo(request));

                Assert.False(result.Value<bool>("Success"));
                Assert.Contains("Reason", result.Value<string>("Message"));
                _batch.VerifyNoOtherCalls();
                _telemetryService.Verify(x => x.TraceException(It.IsAny<Exception>()), Times.Never);
            }

            [Fact]
            public async Task RejectsANullRequest()
            {
                var result = GetJson(await _target.Undo(null));

                Assert.False(result.Value<bool>("Success"));
                Assert.Equal("The undo request is invalid.", result.Value<string>("Message"));
                _batch.VerifyNoOtherCalls();
                _telemetryService.Verify(x => x.TraceException(It.IsAny<Exception>()), Times.Never);
            }

            [Fact]
            public async Task ReportsRequestErrors()
            {
                var request = CreateUndoRequest();
                var exception = new UserSafeException("A package version cannot be selected more than once.");
                _batch.Setup(x => x.UndoAsync(request.Packages, request.Reason, "admin")).ThrowsAsync(exception);

                var result = GetJson(await _target.Undo(request));

                Assert.False(result.Value<bool>("Success"));
                Assert.Equal(exception.Message, result.Value<string>("Message"));
                _telemetryService.Verify(x => x.TraceException(exception), Times.Once);
            }

            [Fact]
            public async Task UndoFormatsEveryFailureAndTracesOriginalExceptions()
            {
                var request = CreateUndoRequest();
                request.Packages.Add(new UndoPackageRequest { Id = "Other.Package", Version = "2.0.0" });
                var exception = new UserSafeException("Reflow failed.");
                var missingRecovery = new UserSafeException("Recovery data is unavailable.");
                _batch.Setup(x => x.UndoAsync(request.Packages, request.Reason, "admin"))
                    .ReturnsAsync(new UndoSoftDeleteBatchResult(new[]
                    {
                        new UndoSoftDeletePackageResult(request.Packages[0]),
                        new UndoSoftDeletePackageResult(request.Packages[1], exception),
                        new UndoSoftDeletePackageResult(request.Packages[2], missingRecovery)
                    }));

                var result = GetJson(await _target.Undo(request));

                Assert.False(result.Value<bool>("Success"));
                Assert.Contains("Completed undo for 1 of 3 packages.", result.Value<string>("Message"));
                Assert.Contains("Failed to restore: NuGet.Versioning/4.3.0, Other.Package/2.0.0", result.Value<string>("Message"));
                var entries = result["Results"].ToList();
                Assert.True(entries[0].Value<bool>("Success"));
                Assert.False(entries[1].Value<bool>("Success"));
                Assert.Equal(exception.Message, entries[1].Value<string>("Message"));
                Assert.Equal(missingRecovery.Message, entries[2].Value<string>("Message"));
                _batch.Verify(x => x.UndoAsync(request.Packages, request.Reason, "admin"), Times.Once);
                _telemetryService.Verify(x => x.TraceException(exception), Times.Once);
                _telemetryService.Verify(x => x.TraceException(missingRecovery), Times.Once);
                _packageService.VerifyNoOtherCalls();
            }
        }

        [Fact]
        public void UndoRequiresPostElevationAndAntiForgery()
        {
            var method = typeof(DeleteController).GetMethod(nameof(DeleteController.Undo));
            Assert.True(Attribute.IsDefined(method, typeof(HttpPostAttribute)));
            Assert.True(Attribute.IsDefined(method, typeof(ElevatedAdminActionAttribute)));
            Assert.True(Attribute.IsDefined(method, typeof(ValidateAntiForgeryTokenAttribute)));
        }

        public abstract class FactsBase : TestContainer
        {
            protected string _query;
            protected readonly List<Package> _packages;
            protected readonly Mock<IPackageService> _packageService;
            protected readonly Mock<IPackageDeleteService> _packageDeleteService;
            protected readonly Mock<IUndoSoftDeleteBatchService> _batch;
            protected readonly Mock<ITelemetryService> _telemetryService;
            protected readonly Mock<HttpContextBase> _httpContextBase;
            protected readonly DeleteController _target;

            public FactsBase()
            {
                _query = "NuGet.Versioning";
                _packages = new List<Package>()
                {
                    new Package
                    {
                        Key = 2,
                        PackageRegistration = new PackageRegistration
                        {
                            Id = "NuGet.Versioning",
                            Owners = new[]
                            {
                                new User { Username = "microsoft" },
                                new User { Username = "nuget" },
                            }
                        },
                        NormalizedVersion = "4.4.0",
                    },
                    new Package
                    {
                        Key = 1,
                        PackageRegistration = new PackageRegistration
                        {
                            Id = "NuGet.Versioning",
                            Owners = new[]
                            {
                                new User { Username = "microsoft" },
                                new User { Username = "nuget" },
                            }
                        },
                        NormalizedVersion = "4.3.0",
                    },
                };

                _packageService = new Mock<IPackageService>();
                _packageService
                    .Setup(x => x.FindPackageRegistrationById(It.IsAny<string>()))
                    .Returns(() => new PackageRegistration { Packages = _packages });

                _packageDeleteService = new Mock<IPackageDeleteService>();
                _batch = new Mock<IUndoSoftDeleteBatchService>(MockBehavior.Strict);

                _telemetryService = new Mock<ITelemetryService>();

                _httpContextBase = new Mock<HttpContextBase>();
                _httpContextBase.SetupGet(x => x.User).Returns(new GenericPrincipal(new GenericIdentity("admin"), new[] { "Admins" }));

                _target = new DeleteController(_packageService.Object, _packageDeleteService.Object, _batch.Object, _telemetryService.Object);

                TestUtility.SetupHttpContextMockForUrlGeneration(_httpContextBase, _target);
            }

            protected UndoPackagesRequest CreateUndoRequest()
            {
                return new UndoPackagesRequest
                {
                    Reason = "Restore accidentally deleted packages",
                    Packages = _packages.Select(p => new UndoPackageRequest
                    {
                        Id = p.PackageRegistration.Id,
                        Version = p.NormalizedVersion
                    }).ToList()
                };
            }

            protected void ValidateRequest(UndoPackagesRequest request)
            {
                var validationResults = new List<ValidationResult>();
                Validator.TryValidateObject(request, new ValidationContext(request), validationResults, validateAllProperties: true);
                foreach (var result in validationResults)
                {
                    foreach (var memberName in result.MemberNames)
                    {
                        _target.ModelState.AddModelError(memberName, result.ErrorMessage);
                    }
                }
            }

            protected static JObject GetJson(ActionResult result)
            {
                return JObject.FromObject(Assert.IsType<JsonResult>(result).Data);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _target?.Dispose();
                    base.Dispose(disposing);
                }
            }
        }
    }
}
