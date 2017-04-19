// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;

namespace NuGetGallery
{
    public abstract class MinClientVersionPolicy : SecurityPolicy
    {
        private Version _minClientVersion;

        public MinClientVersionPolicy()
        {
        }

        public MinClientVersionPolicy(Version minClientVersion)
            : this()
        {
            MinClientVersion = minClientVersion;
        }

        [JsonIgnore]
        [NotMapped]
        public Version MinClientVersion
        {
            get
            {
                return _minClientVersion;
            }
            set
            {
                _minClientVersion = value;
                StateChanged = true;
            }
        }

        [JsonProperty("v")]
        [NotMapped]
        public string MinClientVersionString
        {
            get
            {
                return $"{_minClientVersion.Major}.{_minClientVersion.Minor}.{_minClientVersion.Build}";
            }
            set
            {
                MinClientVersion = Version.Parse(value);
            }
        }

        protected override void UpdateState(SecurityPolicy policy)
        {
            var subPolicy = policy as MinClientVersionPolicy;
            if (subPolicy != null)
            {
                _minClientVersion = subPolicy.MinClientVersion;
            }
        }
    }
}
