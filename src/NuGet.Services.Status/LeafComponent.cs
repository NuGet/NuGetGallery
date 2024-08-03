// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Status
{
    /// <summary>
    /// A <see cref="Component"/> that has no children.
    /// </summary>
    public class LeafComponent : Component
    {
        public override ComponentStatus Status { get; set; }

        public LeafComponent(
            string name,
            string description)
            : base(name, description)
        {
        }
    }
}
