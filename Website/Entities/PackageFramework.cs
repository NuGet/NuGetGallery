using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Versioning;
using NuGet;

namespace NuGetGallery
{
    public class PackageFramework : IEntity, IEquatable<PackageFramework>
    {
        private string targetFramework;

        public int Key { get; set; }

        public Package Package { get; set; }
        public string TargetFramework
        {
            get
            {
                return targetFramework;
            }
            set
            {
                targetFramework = value;
                FrameworkName = VersionUtility.ParseFrameworkName(targetFramework);
            }
        }

        [NotMapped]
        public FrameworkName FrameworkName
        {
            get;
            private set;
        }

        public override bool Equals(object obj)
        {
            PackageFramework other = obj as PackageFramework;
            return Equals(other);
        }

        public bool Equals(PackageFramework framework)
        {
            return framework != null && FrameworkName == framework.FrameworkName;
        }

        public override int GetHashCode()
        {
            return FrameworkName.GetHashCode();
        }
    }
}