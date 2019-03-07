// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NuGet.Frameworks;

namespace NuGet.Services.Entities
{
    public class PackageFramework
        : IEntity, IEquatable<PackageFramework>
    {
        private string _targetFramework;

        public Package Package { get; set; }

        [StringLength(256)]
        public string TargetFramework
        {
            get { return _targetFramework; }
            set
            {
                _targetFramework = value;
                FrameworkName = NuGetFramework.Parse(_targetFramework);
            }
        }

        [NotMapped]
        public NuGetFramework FrameworkName { get; private set; }

        public int Key { get; set; }

        public bool Equals(PackageFramework framework)
        {
            return framework != null && FrameworkName == framework.FrameworkName;
        }

        public override bool Equals(object obj)
        {
            var other = obj as PackageFramework;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            return FrameworkName.GetHashCode();
        }
    }
}