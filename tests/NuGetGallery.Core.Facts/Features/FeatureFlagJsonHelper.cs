// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGetGallery.Features
{
    public static class FeatureFlagJsonHelper
    {
        #region Valid JSON

        public static readonly IReadOnlyList<string> ValidJson = new List<string>
        {
            EmptyJson,
            EmptyFlight,
            FormattedFullJson,
            UnformattedFullJson
        };

        public const string EmptyJson = "{}";
        public const string EmptyFlight = @"
{
  ""Flights"": {
    ""A"": {}
  }
}
";

        public const string FormattedFullJson = @"{
  ""Features"": {
    ""NuGetGallery.Typosquatting"": ""Enabled""
  },
  ""Flights"": {
    ""NuGetGallery.TyposquattingFlight"": {
      ""All"": true,
      ""SiteAdmins"": true,
      ""Accounts"": [
        ""a""
      ],
      ""Domains"": [
        ""b""
      ]
    }
  }
}";

        public const string UnformattedFullJson = @"
{
  ""Features"": {
    ""NuGetGallery.Typosquatting"": ""Enabled""
  },

  ""Flights"": {
    ""NuGetGallery.TyposquattingFlight"": {
      ""All"": true,
      ""SiteAdmins"": true,
      ""Accounts"": [""a""],
      ""Domains"": [""b""]
    }
  }
}";

        #endregion

        #region Invalid JSON

        public static readonly IReadOnlyList<string> InvalidJson = new List<string>
        {
            InvalidFeature1,
            InvalidFeature2,
            InvalidFlight1,
            InvalidFlight2,
            InvalidFlight3,
            InvalidFlight4,
            InvalidFlight5
        };

        private const string InvalidFeature1 = @"
{
  ""Features"": [
    ""NuGetGallery.Typosquatting""
  ],
}";

        private const string InvalidFeature2 = @"
{
  ""Features"": {
    ""NuGetGallery.Typosquatting"": ""Bad""
  },
}";


        private const string InvalidFlight1 = @"
{
  ""Flights"": [
    ""NuGetGallery.TyposquattingFlight""
  ]
}";

        private const string InvalidFlight2 = @"
{
  ""Flights"": {
    ""NuGetGallery.TyposquattingFlight"": {
      ""All"": ""bad""
    }
  }
}";

        private const string InvalidFlight3 = @"
{
  ""Flights"": {
    ""NuGetGallery.TyposquattingFlight"": {
      ""SiteAdmins"": ""bad""
    }
  }
}";

        private const string InvalidFlight4 = @"
{
  ""Flights"": {
    ""NuGetGallery.TyposquattingFlight"": {
      ""Accounts"": ""a"",
    }
  }
}";

        private const string InvalidFlight5 = @"
{
  ""Flights"": {
    ""NuGetGallery.TyposquattingFlight"": {
      ""Domains"": ""b""
    }
  }
}";
        #endregion
    }
}
