// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.FunctionalTests.ErrorHandling
{
    /// <summary>
    /// Represents a pair of <see cref="EndpointType"/> and <see cref="SimulatedErrorType"/>. Also contains logic on
    /// building a relative URL for this pair.
    /// </summary>
    public class SimulatedErrorRequest : IEquatable<SimulatedErrorRequest>
    {
        public SimulatedErrorRequest(EndpointType endpointType, SimulatedErrorType simulatedErrorType)
        {
            EndpointType = endpointType;
            SimulatedErrorType = simulatedErrorType;
        }

        public EndpointType EndpointType { get; }
        public SimulatedErrorType SimulatedErrorType { get; }

        public string GetRelativePath()
        {
            string path;
            switch (EndpointType)
            {
                case EndpointType.Pages:
                    path = "/pages/simulate-error";
                    break;
                case EndpointType.Api:
                    path = "/api/simulate-error";
                    break;
                case EndpointType.OData:
                    path = "/api/v1/SimulateError()";
                    break;
                default:
                    throw new NotImplementedException("The endpoint type is not supported.");
            }

            return $"{path}?type={SimulatedErrorType}";
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SimulatedErrorRequest);
        }

        public bool Equals(SimulatedErrorRequest other)
        {
            return other != null
                && EndpointType == other.EndpointType
                && SimulatedErrorType == other.SimulatedErrorType;
        }

        public override int GetHashCode()
        {
            var hashCode = -73651023;
            hashCode = hashCode * -1521134295 + EndpointType.GetHashCode();
            hashCode = hashCode * -1521134295 + SimulatedErrorType.GetHashCode();
            return hashCode;
        }

        public override string ToString() => GetRelativePath();
    }
}