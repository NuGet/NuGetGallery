﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Versioning;
using NuGet;

namespace NuGetGallery.Data.Model
{
    public class PackageFramework : IEntity, IEquatable<PackageFramework>
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
                FrameworkName = VersionUtility.ParseFrameworkName(_targetFramework);
            }
        }

        [NotMapped]
        public FrameworkName FrameworkName { get; private set; }

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