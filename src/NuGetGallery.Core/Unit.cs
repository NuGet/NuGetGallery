// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace System
{
    // .NET Framework needs a unit type!
    public class Unit : IEquatable<Unit>
    {
        public static readonly Unit Instance = new Unit();

        private Unit() { }

        public bool Equals(Unit other) => ReferenceEquals(this, other);

        public override bool Equals(object obj) => ReferenceEquals(this, obj);

        public override string ToString() => "<unit>";

        public override int GetHashCode() => 0; // There can only be one!
    }
}
