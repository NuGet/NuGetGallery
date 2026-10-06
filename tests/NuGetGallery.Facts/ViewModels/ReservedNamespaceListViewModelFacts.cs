// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using Xunit;

namespace NuGetGallery.ViewModels
{
    public class ReservedNamespaceListViewModelFacts
    {
        [Fact]
        public void ExistingConstructorPreservesReservedRowsAndOwnerCollections()
        {
            var reservation = CreateReservation("Contoso.", true, 1);

            var model = new ReservedNamespaceListViewModel(new[] { reservation });

            var row = Assert.Single(model.ReservedNamespaces);
            Assert.Equal("Reserved", row.Status);
            Assert.Equal("Contoso.*", row.GetPattern());
            Assert.Same(reservation.Owners, row.Owners);
        }

        [Fact]
        public void MergesAllOutstandingStatusesAfterActualReservations()
        {
            var requests = new[] { CreateRequest("Pending"), CreateRequest("Approved"), CreateRequest("Rejected") };
            var reservation = CreateReservation("Unrelated", false, 1, 2);

            var rows = new ReservedNamespaceListViewModel(new[] { reservation }, requests).ReservedNamespaces.ToList();

            Assert.Equal(new[] { "Reserved", "Pending", "Approved", "Rejected" }, rows.Select(row => row.Status));
            Assert.Equal(new[] { "Unrelated", "Contoso", "Contoso", "Contoso" }, rows.Select(row => row.Value));
        }

        [Theory]
        [InlineData("Pending")]
        [InlineData("Approved")]
        [InlineData("Rejected")]
        [InlineData("Reserved")]
        public void SuppressesFulfilledRequestOnlyAndRetainsBothActualRows(string status)
        {
            var reservations = new[]
            {
                CreateReservation("contoso", false, 1, 2, 3),
                CreateReservation("CONTOSO.", true, 1, 2, 4)
            };
            var canonicalOwner = new Organization { Key = 2, Username = "Renamed" };

            var rows = new ReservedNamespaceListViewModel(reservations, new[] { CreateRequest(status) }, new[] { canonicalOwner })
                .ReservedNamespaces.ToList();

            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.Equal("Reserved", row.Status));
            Assert.Same(reservations[0].Owners, rows[0].Owners);
            Assert.Same(reservations[1].Owners, rows[1].Owners);
        }

        [Theory]
        [InlineData("none")]
        [InlineData("exactOnly")]
        [InlineData("prefixOnly")]
        [InlineData("loosePrefix")]
        [InlineData("parentPrefix")]
        [InlineData("childPrefix")]
        [InlineData("wrongExactName")]
        [InlineData("wrongExactKind")]
        [InlineData("wrongPrefixKind")]
        [InlineData("missingExactOwner")]
        [InlineData("missingPrefixOwner")]
        [InlineData("splitOwners")]
        [InlineData("otherOwners")]
        [InlineData("emptyOwners")]
        [InlineData("nullOwners")]
        [InlineData("sharedExact")]
        [InlineData("sharedPrefix")]
        public void IncompleteOrUnrelatedReservationsCannotHideRequest(string scenario)
        {
            var exact = CreateReservation("Contoso", false, 1, 2);
            var prefix = CreateReservation("Contoso.", true, 1, 2);
            var reservations = new List<ReservedNamespace> { exact, prefix };
            switch (scenario)
            {
                case "none":
                    reservations.Clear();
                    break;
                case "exactOnly":
                    reservations.Remove(prefix);
                    break;
                case "prefixOnly":
                    reservations.Remove(exact);
                    break;
                case "loosePrefix":
                    prefix.Value = "Contoso";
                    break;
                case "parentPrefix":
                    prefix.Value = "Cont";
                    break;
                case "childPrefix":
                    prefix.Value = "Contoso.Tools.";
                    break;
                case "wrongExactName":
                    exact.Value = "Contoso.Tools";
                    break;
                case "wrongExactKind":
                    exact.IsPrefix = true;
                    break;
                case "wrongPrefixKind":
                    prefix.IsPrefix = false;
                    break;
                case "missingExactOwner":
                    exact.Owners.Remove(exact.Owners.Last());
                    break;
                case "missingPrefixOwner":
                    prefix.Owners.Remove(prefix.Owners.Last());
                    break;
                case "splitOwners":
                    exact.Owners.Remove(exact.Owners.Last());
                    prefix.Owners.Remove(prefix.Owners.First());
                    break;
                case "otherOwners":
                    exact.Owners = new[] { new User { Key = 3, Username = "Alice" } };
                    prefix.Owners = exact.Owners;
                    break;
                case "emptyOwners":
                    exact.Owners.Clear();
                    break;
                case "nullOwners":
                    prefix.Owners = null;
                    break;
                case "sharedExact":
                    exact.IsSharedNamespace = true;
                    break;
                case "sharedPrefix":
                    prefix.IsSharedNamespace = true;
                    break;
            }

            var rows = new ReservedNamespaceListViewModel(reservations, new[] { CreateRequest("Approved") }).ReservedNamespaces.ToList();

            Assert.Equal(reservations.Count + 1, rows.Count);
            Assert.All(rows.Take(reservations.Count), row => Assert.Equal("Reserved", row.Status));
            Assert.Equal("Approved", rows.Last().Status);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("not json")]
        [InlineData("null")]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("[null]")]
        [InlineData("[{}]")]
        [InlineData("[{\"Key\":1}]")]
        [InlineData("[{\"Username\":\"Alice\"}]")]
        [InlineData("[{\"Key\":0,\"Username\":\"Alice\"}]")]
        [InlineData("[{\"Key\":-1,\"Username\":\"Alice\"}]")]
        [InlineData("[{\"Key\":2147483648,\"Username\":\"Alice\"}]")]
        [InlineData("[{\"Key\":\"1\",\"Username\":\"Alice\"}]")]
        [InlineData("[{\"Key\":1.0,\"Username\":\"Alice\"}]")]
        [InlineData("[{\"Key\":1,\"Username\":null}]")]
        [InlineData("[{\"Key\":1,\"Username\":\" \"}]")]
        [InlineData("[{\"Key\":1,\"Username\":42}]")]
        [InlineData("[{\"Key\":1,\"Key\":2,\"Username\":\"Alice\"}]")]
        [InlineData("[{\"Key\":1,\"Username\":\"Alice\"},{}]")]
        [InlineData("[{\"Key\":1,\"Username\":\"Alice\"},")]
        public void InvalidOwnerSnapshotFailsClosedWithoutBreakingOtherRows(string json)
        {
            var invalid = CreateRequest("Approved");
            invalid.RequestedOwnersJson = json;
            var valid = CreateRequest("Rejected");
            valid.Namespace = "Other";
            var reservations = new[] { CreateReservation("Contoso", false, 1, 2), CreateReservation("Contoso.", true, 1, 2) };

            var rows = new ReservedNamespaceListViewModel(reservations, new[] { invalid, valid }, reservations[0].Owners)
                .ReservedNamespaces.ToList();

            Assert.Equal(new[] { "Reserved", "Reserved", "Approved", "Rejected" }, rows.Select(row => row.Status));
            Assert.Empty(rows[2].Owners);
            Assert.Equal(2, rows[3].Owners.Count());
        }

        private static NamespaceReservationRequest CreateRequest(string status)
        {
            return new NamespaceReservationRequest
            {
                Namespace = "Contoso",
                Status = status,
                RequestedOwnersJson = "[{\"Key\":1,\"Username\":\"Alice\"},{\"Key\":2,\"Username\":\"Contoso\"}]"
            };
        }

        private static ReservedNamespace CreateReservation(string value, bool isPrefix, params int[] ownerKeys)
        {
            var reservation = new ReservedNamespace(value, false, isPrefix);
            foreach (var key in ownerKeys)
            {
                reservation.Owners.Add(new User { Key = key, Username = "Owner" + key });
            }

            return reservation;
        }
    }
}