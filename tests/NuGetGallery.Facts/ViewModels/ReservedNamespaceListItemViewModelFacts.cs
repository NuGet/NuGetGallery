// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class ReservedNamespaceListItemViewModelFacts
    {
        [Theory]
        [InlineData(false, false, "Contoso")]
        [InlineData(true, true, "Contoso*")]
        public void ExistingConstructorPreservesOwnersAndPattern(bool isPublic, bool isPrefix, string pattern)
        {
            var organization = new Organization { Key = 1, Username = "Contoso" };
            var reservation = new ReservedNamespace("Contoso", isPublic, isPrefix);
            reservation.Owners.Add(organization);

            var model = new ReservedNamespaceListItemViewModel(reservation);

            Assert.Equal("Reserved", model.Status);
            Assert.Null(model.RejectionReason);
            Assert.Equal("Contoso", model.Value);
            Assert.Equal(isPublic, model.IsPublic);
            Assert.Equal(isPrefix, model.IsPrefix);
            Assert.Equal(pattern, model.GetPattern());
            Assert.Same(reservation.Owners, model.Owners);
            Assert.Same(organization, Assert.Single(model.Owners));
        }

        [Theory]
        [InlineData("Pending", "Pending")]
        [InlineData("Approved", "Approved")]
        [InlineData("Rejected", "Rejected")]
        [InlineData("Reserved", "Pending")]
        [InlineData("approved", "Pending")]
        [InlineData("Unknown", "Pending")]
        [InlineData("", "Pending")]
        [InlineData(null, "Pending")]
        public void RequestConstructorRestrictsStatusAndUsesBasePattern(string status, string expected)
        {
            const string reason = "Provide more evidence of your connection to this namespace. <strong>Details</strong> & links.";
            var request = new NamespaceReservationRequest { Namespace = "Contoso.Tools", Status = status, Reason = reason };

            var model = new ReservedNamespaceListItemViewModel(request, null);

            Assert.Equal(expected, model.Status);
            Assert.Equal(expected == "Rejected" ? reason : null, model.RejectionReason);
            Assert.Equal("Contoso.Tools", model.Value);
            Assert.Equal("Contoso.Tools", model.GetPattern());
            Assert.False(model.IsPublic);
            Assert.False(model.IsPrefix);
            Assert.Empty(model.Owners);
        }

        [Fact]
        public void RequestOwnersUseCanonicalInstancesByKeyAndDisplayOnlySnapshotsOtherwise()
        {
            var user = new User { Key = 1, Username = "RenamedUser" };
            var organization = new Organization { Key = 2, Username = "RenamedOrganization" };
            var request = new NamespaceReservationRequest
            {
                RequestedOwnersJson = "[{\"Key\":1,\"Username\":\"OldUser\"},{\"Key\":2,\"Username\":\"OldOrganization\"},{\"Key\":3,\"Username\":\"Snapshot\"}]"
            };
            var originalJson = request.RequestedOwnersJson;

            var model = new ReservedNamespaceListItemViewModel(request, new[]
            {
                user, organization, new User { Key = 4, Username = "Snapshot" }
            });

            var owners = model.Owners.ToList();
            Assert.Equal(3, owners.Count);
            Assert.Same(user, owners[0]);
            Assert.Equal("RenamedUser", owners[0].Username);
            Assert.Same(organization, owners[1]);
            Assert.Equal("RenamedOrganization", owners[1].Username);
            Assert.IsType<User>(owners[2]);
            Assert.Equal(3, owners[2].Key);
            Assert.Equal("Snapshot", owners[2].Username);
            Assert.Equal(originalJson, request.RequestedOwnersJson);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MissingRejectionReasonDoesNotInventAnExplanation(string reason)
        {
            var request = new NamespaceReservationRequest { Status = "Rejected", Reason = reason };

            var model = new ReservedNamespaceListItemViewModel(request, null);

            Assert.Equal("Rejected", model.Status);
            Assert.Null(model.RejectionReason);
        }

        [Fact]
        public void RejectsNullRequest()
        {
            Assert.Equal("request", Assert.Throws<ArgumentNullException>(() => new ReservedNamespaceListItemViewModel(null, null)).ParamName);
        }
    }
}