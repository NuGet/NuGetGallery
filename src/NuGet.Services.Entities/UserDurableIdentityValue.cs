// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Services.Entities
{
    /// <summary>
    /// Represents a relationship between a user and a durable identity value.
    /// </summary>
    public class UserDurableIdentityValue : IEntity
    {
        public int Key { get; set; }

        public int DurableIdentityValueKey { get; set; }

        public virtual DurableIdentityValue DurableIdentityValue { get; set; }

        public int UserKey { get; set; }

        public virtual User User { get; set; }
    }
}
