// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class ReservedNamespaceListItemViewModel
    {
        private readonly IReadOnlyList<int> _requestedOwnerKeys;

        public string Value { get; }

        public bool IsPublic { get; }

        public bool IsPrefix { get; }

        public IEnumerable<User> Owners { get; }

        public string Status { get; }

        public string RejectionReason { get; }

        public ReservedNamespaceListItemViewModel(ReservedNamespace reservedNamespace)
        {
            Value = reservedNamespace.Value;
            IsPublic = reservedNamespace.IsSharedNamespace;
            IsPrefix = reservedNamespace.IsPrefix;
            Owners = reservedNamespace.Owners;
            Status = "Reserved";
        }

        public ReservedNamespaceListItemViewModel(NamespaceReservationRequest request, IEnumerable<User> knownOwners)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            Value = request.Namespace;
            IsPrefix = false;
            IsPublic = false;
            Status = request.Status == "Approved" || request.Status == "Rejected" ? request.Status : "Pending";
            // Only the saved, customer-facing rejection explanation belongs in this table.
            RejectionReason = Status == "Rejected" && !string.IsNullOrWhiteSpace(request.Reason) ? request.Reason : null;

            var snapshots = ReadRequestedOwners(request.RequestedOwnersJson);
            _requestedOwnerKeys = snapshots.Select(owner => owner.Key).Distinct().ToList();
            var ownersByKey = (knownOwners ?? Enumerable.Empty<User>())
                .Where(owner => owner != null && owner.Key > 0)
                .GroupBy(owner => owner.Key)
                .ToDictionary(group => group.Key, group => group.First());
            Owners = snapshots.Select(owner => ownersByKey.TryGetValue(owner.Key, out var knownOwner) ? knownOwner : owner).ToList();
        }

        internal bool IsFulfilledBy(ICollection<ReservedNamespace> reservedNamespaces)
        {
            // An assessment decision is not a reservation. Require both actual private
            // patterns, each owned by every original owner, even if display names changed.
            if (_requestedOwnerKeys == null || _requestedOwnerKeys.Count == 0 || string.IsNullOrWhiteSpace(Value))
            {
                return false;
            }

            var ownedReservations = reservedNamespaces.Where(reservation =>
                !reservation.IsSharedNamespace
                && reservation.Owners != null
                && _requestedOwnerKeys.All(key => reservation.Owners.Any(owner => owner != null && owner.Key == key)));

            return ownedReservations.Any(reservation => !reservation.IsPrefix && string.Equals(reservation.Value, Value, StringComparison.OrdinalIgnoreCase))
                && ownedReservations.Any(reservation => reservation.IsPrefix && string.Equals(reservation.Value, Value + ".", StringComparison.OrdinalIgnoreCase));
        }

        private static IReadOnlyList<User> ReadRequestedOwners(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<User>();
            }

            try
            {
                var entries = JArray.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                var owners = new List<User>();
                foreach (var entry in entries)
                {
                    // Never accept a partial snapshot: dropping an invalid owner could
                    // incorrectly make another owner's reservation appear fulfilled.
                    if (!(entry is JObject owner)
                        || owner["Key"]?.Type != JTokenType.Integer
                        || !int.TryParse(owner["Key"].ToString(), out var key)
                        || key <= 0
                        || owner["Username"]?.Type != JTokenType.String
                        || string.IsNullOrWhiteSpace(owner["Username"].Value<string>()))
                    {
                        return Array.Empty<User>();
                    }

                    owners.Add(new User { Key = key, Username = owner["Username"].Value<string>() });
                }

                return owners;
            }
            catch (JsonException)
            {
                return Array.Empty<User>();
            }
        }

        public string GetPattern()
        {
            var namespaceValue = Value;
            if (IsPrefix)
            {
                namespaceValue += "*";
            }

            return namespaceValue;
        }
    }
}