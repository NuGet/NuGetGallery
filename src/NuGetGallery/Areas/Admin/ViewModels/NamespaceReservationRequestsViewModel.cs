// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public sealed class NamespaceReservationRequestsViewModel
    {
        public string FromDate { get; set; }
        public string ToDate { get; set; }
        public int Page { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasResults { get; set; }
        public IReadOnlyList<NamespaceReservationRequestRowViewModel> Requests { get; set; }
            = Array.Empty<NamespaceReservationRequestRowViewModel>();
    }

    public sealed class NamespaceReservationRequestRowViewModel
    {
        public int Key { get; set; }
        public string Namespace { get; set; }
        public string SubmittedBy { get; set; }
        public string RequestedOwnersJson { get; set; }
        public string Justification { get; set; }
        public DateTime CreatedTimestamp { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public DateTime? CompletedTimestamp { get; set; }

        public IReadOnlyList<string> RequestedOwners
        {
            get
            {
                if (string.IsNullOrWhiteSpace(RequestedOwnersJson))
                {
                    return Array.Empty<string>();
                }

                try
                {
                    return JArray.Parse(RequestedOwnersJson)
                        .Select(owner => owner is JObject value && value["Username"]?.Type == JTokenType.String
                            && !string.IsNullOrWhiteSpace((string)value["Username"])
                            ? (string)value["Username"] : "Unknown owner")
                        .ToArray();
                }
                catch (JsonException)
                {
                    return new[] { "Owner information unavailable" };
                }
            }
        }
    }
}