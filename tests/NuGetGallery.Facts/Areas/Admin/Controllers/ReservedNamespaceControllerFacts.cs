// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Moq;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.TestUtils;
using Xunit;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class ReservedNamespaceControllerFacts
    {
        [Fact]
        public void CtorThrowsIfReservedNamespaceServiceNull()
        {
            // Arrange.
            var packageRegistrations = new Mock<IEntityRepository<PackageRegistration>>();

            // Act & Assert.
            Assert.Throws<ArgumentNullException>(() => new ReservedNamespaceController(null, packageRegistrations.Object));
        }

        [Theory]
        [InlineData(null, 0, 0)]
        [InlineData("", 0, 0)]
        [InlineData("microsoft., jquery.extentions., abc.", 2, 1)]
        [InlineData("all., new., name., , spaces,;,", 0, 4)]
        public void SearchFindsMatchingPrefixes(string query, int foundCount, int notFoundCount)
        {
            // Arrange.
            var namespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
            var reservedNamespaceService = new TestableReservedNamespaceService(reservedNamespaces: namespaces);
            var packageRegistrations = new Mock<IEntityRepository<PackageRegistration>>();
            var controller = new ReservedNamespaceController(reservedNamespaceService, packageRegistrations.Object);

            // Act.
            JsonResult jsonResult = controller.SearchPrefix(query);

            // Assert
            dynamic data = jsonResult.Data;
            var resultModelList = data.Prefixes as IEnumerable<ReservedNamespaceResultModel>;
            var found = resultModelList.Where(r => r.isExisting);
            var notFound = resultModelList.Where(r => !r.isExisting);
            Assert.Equal(foundCount, found.Count());
            Assert.Equal(notFoundCount, notFound.Count());
        }


        [Theory]
        [InlineData("Zzz.", "")]
        [InlineData("Microsoft.", "")]
        [InlineData("Microsoft.Zzz.", "Microsoft.*")]
        [InlineData("microsoft.zzz.", "Microsoft.*")]
        [InlineData("Microsoft.AspNet.", "Microsoft.*")]
        [InlineData("Microsoft.AspNet.Zzz", "Microsoft.*, Microsoft.Aspnet.*")]
        [InlineData("jquery.", "")]
        [InlineData("jquery.Extentions.Zzz", "jquery.Extentions.*")]
        public void IncludesParentNamespaces(string query, string parents)
        {
            // Arrange.
            var namespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
            var reservedNamespaceService = new TestableReservedNamespaceService(reservedNamespaces: namespaces);
            var packageRegistrations = new Mock<IEntityRepository<PackageRegistration>>();
            var controller = new ReservedNamespaceController(reservedNamespaceService, packageRegistrations.Object);

            // Act.
            JsonResult jsonResult = controller.SearchPrefix(query);

            // Assert
            dynamic data = jsonResult.Data;
            var resultModelList = data.Prefixes as IEnumerable<ReservedNamespaceResultModel>;
            var match = Assert.Single(resultModelList);
            Assert.Equal(parents, string.Join(", ", match.parents));
        }

        [Fact]
        public async Task AddNamespaceDoesNotReturnSuccessForInvalidNamespaces()
        {
            // Arrange.
            var namespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
            var newNamespace = namespaces.First();
            var reservedNamespaceService = new TestableReservedNamespaceService(reservedNamespaces: namespaces);
            var packageRegistrations = new Mock<IEntityRepository<PackageRegistration>>();
            var controller = new ReservedNamespaceController(reservedNamespaceService, packageRegistrations.Object);

            // Act & Assert.
            JsonResult result = await controller.AddNamespace(newNamespace);
            dynamic data = result.Data;
            Assert.False(data.success);
        }

        [Theory]
        [InlineData("abc.", false, true)]
        [InlineData("abc", false, false)]
        [InlineData("microsoft.aspnet.mvc.", false, true)]
        [InlineData("microsoft.aspnet.extention.", true, true)]
        public async Task AddNamespaceSuccessfullyAddsNewNamespaces(string value, bool isSharedNamespace, bool isPrefix)
        {
            // Arrange.
            var namespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
            var newNamespace = new ReservedNamespace(value, isSharedNamespace, isPrefix);
            var reservedNamespaceService = new TestableReservedNamespaceService(reservedNamespaces: namespaces);
            var packageRegistrations = new Mock<IEntityRepository<PackageRegistration>>();
            var controller = new ReservedNamespaceController(reservedNamespaceService, packageRegistrations.Object);

            // Act & Assert.
            JsonResult result = await controller.AddNamespace(newNamespace);
            dynamic data = result.Data;
            Assert.True(data.success);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("abc")]
        public async Task RemoveNamespaceDoesNotReturnSuccessForInvalidNamespaces(string value)
        {
            // Arrange.
            var namespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
            var invalidNamespace = new ReservedNamespace();
            invalidNamespace.Value = value;
            var reservedNamespaceService = new TestableReservedNamespaceService(reservedNamespaces: namespaces);
            var packageRegistrations = new Mock<IEntityRepository<PackageRegistration>>();
            var controller = new ReservedNamespaceController(reservedNamespaceService, packageRegistrations.Object);

            // Act & Assert.
            JsonResult result = await controller.RemoveNamespace(invalidNamespace);
            dynamic data = result.Data;
            Assert.False(data.success);
        }

        [Theory]
        [InlineData("microsoft.")]
        [InlineData("jquery")]
        [InlineData("jQuery.Extentions.")]
        public async Task RemoveNamespaceSuccesfullyDeletesNamespace(string value)
        {
            // Arrange.
            var namespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
            var existingNamespace = namespaces.FirstOrDefault(rn => rn.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
            var reservedNamespaceService = new TestableReservedNamespaceService(reservedNamespaces: namespaces);
            var packageRegistrations = new Mock<IEntityRepository<PackageRegistration>>();
            var controller = new ReservedNamespaceController(reservedNamespaceService, packageRegistrations.Object);

            // Act & Assert.
            JsonResult result = await controller.RemoveNamespace(existingNamespace);
            dynamic data = result.Data;
            Assert.True(data.success);
        }

        [Theory]
        [InlineData(null, "test1")]
        [InlineData("", "test1")]
        [InlineData("   ", "test1")]
        [InlineData("non.existent.namespace.", "test1")]
        [InlineData("microsoft.", null)]
        [InlineData("microsoft.", "")]
        [InlineData("microsoft.", "   ")]
        [InlineData("microsoft.", "nonexistentuser")]
        public async Task AddOwnerDoesNotReturnSuccessForInvalidData(string value, string username)
        {
            // Arrange.
            var namespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
            var allUsers = ReservedNamespaceServiceTestData.GetTestUsers();
            var existingNamespace = namespaces.FirstOrDefault(rn => rn.Value.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? new ReservedNamespace();
            existingNamespace.Value = value;
            var reservedNamespaceService = new TestableReservedNamespaceService(reservedNamespaces: namespaces, users: allUsers);
            var packageRegistrations = new Mock<IEntityRepository<PackageRegistration>>();
            var controller = new ReservedNamespaceController(reservedNamespaceService, packageRegistrations.Object);

            // Act & Assert.
            JsonResult result = await controller.AddOwner(existingNamespace, username);
            dynamic data = result.Data;
            Assert.False(data.success);
        }

        [Theory]
        [InlineData("microsoft.", "test1")]
        [InlineData("jquery", "test1")]
        [InlineData("jQuery.Extentions.", "test1")]
        public async Task AddOwnerSuccessfullyAddsOwnerToReservedNamespace(string value, string username)
        {
            // Arrange.
            var namespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
            var allUsers = ReservedNamespaceServiceTestData.GetTestUsers();
            var existingNamespace = namespaces.FirstOrDefault(rn => rn.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
            var reservedNamespaceService = new TestableReservedNamespaceService(reservedNamespaces: namespaces, users: allUsers);
            var packageRegistrations = new Mock<IEntityRepository<PackageRegistration>>();
            var controller = new ReservedNamespaceController(reservedNamespaceService, packageRegistrations.Object);

            // Act & Assert.
            JsonResult result = await controller.AddOwner(existingNamespace, username);
            dynamic data = result.Data;
            Assert.True(data.success);
        }

        [Theory]
        [InlineData(null, "test1")]
        [InlineData("", "test1")]
        [InlineData("   ", "test1")]
        [InlineData("non.existent.namespace.", "test1")]
        [InlineData("microsoft.", null)]
        [InlineData("microsoft.", "")]
        [InlineData("microsoft.", "   ")]
        [InlineData("microsoft.", "nonexistentuser")]
        [InlineData("microsoft.", "test1")]
        public async Task RemoveOwnerDoesNotReturnSuccessForInvalidData(string value, string username)
        {
            // Arrange.
            var namespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
            var allUsers = ReservedNamespaceServiceTestData.GetTestUsers();
            var existingNamespace = namespaces.FirstOrDefault(rn => rn.Value.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? new ReservedNamespace();
            existingNamespace.Value = value;
            var reservedNamespaceService = new TestableReservedNamespaceService(reservedNamespaces: namespaces, users: allUsers);
            var packageRegistrations = new Mock<IEntityRepository<PackageRegistration>>();
            var controller = new ReservedNamespaceController(reservedNamespaceService, packageRegistrations.Object);

            // Act & Assert.
            JsonResult result = await controller.RemoveOwner(existingNamespace, username);
            dynamic data = result.Data;
            Assert.False(data.success);
        }

        [Theory]
        [InlineData("microsoft.")]
        [InlineData("jquery")]
        [InlineData("jQuery.Extentions.")]
        public async Task RemoveOwnerSuccessfullyRemovesOwnerToReservedNamespace(string value)
        {
            // Arrange.
            var namespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
            var allUsers = ReservedNamespaceServiceTestData.GetTestUsers();
            var testUser = allUsers.First();
            var existingNamespace = namespaces.FirstOrDefault(rn => rn.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
            existingNamespace.Owners.Add(testUser);
            var reservedNamespaceService = new TestableReservedNamespaceService(reservedNamespaces: namespaces, users: allUsers);
            var packageRegistrations = new Mock<IEntityRepository<PackageRegistration>>();
            var controller = new ReservedNamespaceController(reservedNamespaceService, packageRegistrations.Object);

            // Act & Assert.
            JsonResult result = await controller.RemoveOwner(existingNamespace, testUser.Username);
            dynamic data = result.Data;
            Assert.True(data.success);
        }

        [Fact]
        public void FindsReservedNamespacesStartingWithValue()
        {
            // Arrange.
            var namespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
            var allUsers = ReservedNamespaceServiceTestData.GetTestUsers();
            var reservedNamespaceService = new TestableReservedNamespaceService(reservedNamespaces: namespaces, users: allUsers);
            var packageRegistrations = new Mock<IEntityRepository<PackageRegistration>>();
            var controller = new ReservedNamespaceController(reservedNamespaceService, packageRegistrations.Object);

            // Act
            var result = controller.FindNamespacesByPrefix("m");

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ReservedNamespaceViewModel>(viewResult.Model);
            Assert.Equal(2, model.ReservedNamespaces.Count);
            Assert.Equal("Microsoft.", model.ReservedNamespaces[0].Value);
            Assert.Equal("Microsoft.Aspnet.", model.ReservedNamespaces[1].Value);
        }

        [Fact]
        public void FindsPackageRegistrationsStartingWithValue()
        {
            // Arrange.
            var namespaces = ReservedNamespaceServiceTestData.GetTestNamespaces();
            var allUsers = ReservedNamespaceServiceTestData.GetTestUsers();
            var reservedNamespaceService = new TestableReservedNamespaceService(reservedNamespaces: namespaces, users: allUsers);
            var packageRegistrations = new Mock<IEntityRepository<PackageRegistration>>();
            packageRegistrations
                .Setup(x => x.GetAll())
                .Returns(() => new[] { new PackageRegistration { Id = "foo" }, new PackageRegistration { Id = "bar" } }.AsQueryable());
            var controller = new ReservedNamespaceController(reservedNamespaceService, packageRegistrations.Object);

            // Act
            var result = controller.FindPackagesByPrefix("f");

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ReservedNamespaceViewModel>(viewResult.Model);
            Assert.Equal("foo", Assert.Single(model.PackageRegistrations).Id);
        }
    }
}
