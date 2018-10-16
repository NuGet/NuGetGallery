// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Status.Table;
using StatusAggregator.Parse;

namespace StatusAggregator.Factory
{
    public class IncidentAffectedComponentPathProvider : IAffectedComponentPathProvider<IncidentEntity>, IAffectedComponentPathProvider<IncidentGroupEntity>
    {
        /// <summary>
        /// <see cref="IncidentEntity"/>s and <see cref="IncidentGroupEntity"/>s should be created using the same path as the <paramref name="input"/>.
        /// </summary>
        public string Get(ParsedIncident input)
        {
            return input.AffectedComponentPath;
        }
    }
}
