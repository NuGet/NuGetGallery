// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public class ABTestEnrollmentFactory : IABTestEnrollmentFactory
    {
        private const int SchemaVersion1 = 1;
        private const int SchemaVersion2 = 2;

        private static readonly RNGCryptoServiceProvider _secureRng = new RNGCryptoServiceProvider();
        private static readonly ThreadLocal<byte[]> _bytes = new ThreadLocal<byte[]>(() => new byte[sizeof(ulong)]);

        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<ABTestEnrollmentFactory> _logger;

        public ABTestEnrollmentFactory(
            ITelemetryService telemetryService,
            ILogger<ABTestEnrollmentFactory> logger)
        {
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ABTestEnrollment Initialize()
        {

            //(else ... schemaversion  == 2)
                //If v1 does not exist 
               var enrollment = new ABTestEnrollment(
                    ABTestEnrollmentState.FirstHit,
                    SchemaVersion2,
                    previewSearchBucket: GetRandomWholePercentage(),
                    packageDependentBucket: GetRandomWholePercentage());

            _telemetryService.TrackABTestEnrollmentInitialized(
                enrollment.SchemaVersion,
                enrollment.PreviewSearchBucket,
                enrollment.PackageDependentBucket); // Maybe add pdbucket or make another one??

            return enrollment;
        }

        /// <summary>
        /// Returns a random integer in range [1, 100].
        /// </summary>
        private static int GetRandomWholePercentage()
        {
            _secureRng.GetBytes(_bytes.Value);
            var value = BitConverter.ToUInt64(_bytes.Value, 0);

            // Note that this is very slightly biased towards values 1 through 16 because the maximum value of an
            // unsigned 64-bit integer is not evenly divisible by 100 and therefore remainders (of mod operator) are
            // more likely than others. This is okay since the possible range of this integer type is so much larger
            // than 100 that the bias will be unnoticeable for us. We could mitigate this by rejecting unfavorable
            // numbers before the mod operation but that's too much work.
            return (int)(value % 100) + 1;
        }

        public string Serialize(ABTestEnrollment enrollment)
        {
            if (enrollment.SchemaVersion != SchemaVersion2)
            {
                throw new NotImplementedException($"Serializing schema version {enrollment.SchemaVersion} is not implemented.");
            }

            var deserialized2 = new StateVersion2
            {
                SchemaVersion = SchemaVersion2,
                PreviewSearchBucket = enrollment.PreviewSearchBucket,
                PackageDependentBucket = enrollment.PackageDependentBucket,

            };
            return JsonConvert.SerializeObject(deserialized2);


        }

        public bool TryDeserialize(string serialized, out ABTestEnrollment enrollment)
        {
            enrollment = null;
            if (string.IsNullOrWhiteSpace(serialized))
            {
                return false;
            }

            return (TryDeserializeStateVer2(serialized, out enrollment) || TryDeserializeStateVer1(serialized, out enrollment));


            /* IN CASE THINGS GO HAYWIRE REVERT BACK TO THIS VERSION
            try
            {
                var v1 = JsonConvert.DeserializeObject<StateVersion1>(serialized);
                if (v1 == null
                    || v1.SchemaVersion != SchemaVersion1
                    || IsNotPercentage(v1.PreviewSearchBucket))
                {
                    return false;
                }

                enrollment = new ABTestEnrollment(
                    ABTestEnrollmentState.Active,
                    v1.SchemaVersion,
                    v1.PreviewSearchBucket);

                return true;
            }
            catch (JsonException)
            {
                return false;
            }

         */
        }

        private bool TryDeserializeStateVer1(string serialized, out ABTestEnrollment enrollment)
        {
            enrollment = null;
            try
            {
                var v1 = JsonConvert.DeserializeObject<StateVersion1>(serialized);
                if (v1 == null
                    || v1.SchemaVersion != SchemaVersion1
                    || IsNotPercentage(v1.PreviewSearchBucket))
                {
                    return false;
                }

                enrollment = new ABTestEnrollment(
                    ABTestEnrollmentState.Upgraded,
                    SchemaVersion2,
                    v1.PreviewSearchBucket,
                    packageDependentBucket: GetRandomWholePercentage()); // What is the point of making this here
                //TO DO Add telememtry for this case
                return true;
            }
            catch (JsonException)
            {
                return false;
            }

        }

        private bool TryDeserializeStateVer2(string serialized, out ABTestEnrollment enrollment)
        {
            enrollment = null;
            try
            {
                var v2 = JsonConvert.DeserializeObject<StateVersion2>(serialized);
                if (v2 == null
                    || v2.SchemaVersion != SchemaVersion2
                    || IsNotPercentage(v2.PreviewSearchBucket)
                    || IsNotPercentage(v2.PackageDependentBucket))
                {
                    return false;
                }

                enrollment = new ABTestEnrollment(
                    ABTestEnrollmentState.Active,
                    v2.SchemaVersion,
                    v2.PreviewSearchBucket,
                    v2.PackageDependentBucket); // What is the point of making this here

                return true;
            }
            catch (JsonException)
            {
                return false;
            }

        }

        private static bool IsNotPercentage(int input)
        {
            return input < 1 || input > 100;
        }

        private class StateVersion1
        {
            [JsonProperty("v", Required = Required.Always)]
            public int SchemaVersion { get; set; }

            [JsonProperty("ps", Required = Required.Always)]
            public int PreviewSearchBucket { get; set; }
        }

        private class StateVersion2
        {
            [JsonProperty("v", Required = Required.Always)]
            public int SchemaVersion { get; set; }

            [JsonProperty("ps", Required = Required.Always)]
            public int PreviewSearchBucket { get; set; }

            [JsonProperty("pd", Required = Required.Always)]
            public int PackageDependentBucket { get; set; }
        }
    }
}