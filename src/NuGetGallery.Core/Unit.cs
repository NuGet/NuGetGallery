// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    // .NET Framework needs a unit type!
    public class Unit : IEquatable<Unit>
    {
        public static readonly Unit Instance = new Unit();

        private Unit() { }

        public bool Equals(Unit other)
        {
            return ReferenceEquals(this, other);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj);
        }

        public override string ToString()
        {
            return "<unit>";
        }

        public override int GetHashCode()
        {
            return 0; // There can only be one!
        }
    }
}
