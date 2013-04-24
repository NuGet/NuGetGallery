using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Internal.Web.Utils;

namespace NuGetGallery
{
    /// <summary>
    /// Represents a normalized, spec-compatible (with NuGet extensions), Semantic Version as defined at http://semver.org
    /// </summary>
    public struct SemVer : IEquatable<SemVer>, IComparable<SemVer>
    {
        public static readonly SemVer Zero = new SemVer();

        // Cache for the hash code, since this type is immutable
        private int? _hashCode;

        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Patch { get; private set; }
        public int Revision { get; private set; }
        public string Tag { get; private set; }

        public SemVer(int major, int minor) : this(major, minor, 0, 0, null) { }
        public SemVer(int major, int minor, int patch) : this(major, minor, patch, 0, null) { }
        public SemVer(int major, int minor, int patch, int revision) : this(major, minor, patch, revision, null) { }
        public SemVer(int major, int minor, string tag) : this(major, minor, 0, 0, tag) { }
        public SemVer(int major, int minor, int patch, string tag) : this(major, minor, patch, 0, tag) { }
        public SemVer(int major, int minor, int patch, int revision, string tag)
            : this()
        {
            if (major < 0)
            {
                throw new ArgumentOutOfRangeException(
                    "major",
                    major,
                    String.Format(Strings.ParameterMustBeNonNegative, "major"));
            }

            if (minor < 0)
            {
                throw new ArgumentOutOfRangeException(
                    "minor",
                    minor,
                    String.Format(Strings.ParameterMustBeNonNegative, "minor"));
            }

            if (patch < 0)
            {
                throw new ArgumentOutOfRangeException(
                    "patch",
                    patch,
                    String.Format(Strings.ParameterMustBeNonNegative, "patch"));
            }

            if (revision < 0)
            {
                throw new ArgumentOutOfRangeException(
                    "revision",
                    revision,
                    String.Format(Strings.ParameterMustBeNonNegative, "revision"));
            }

            Major = major;
            Minor = minor;
            Patch = patch;
            Revision = revision;

            // In order to ensure all SemVer values are unique and different, empty strings are NOT allowed for tags. They are coalesced to null
            // Without this, a SemVer with an empty tag and one with a null tag could end up as unique objects.
            Tag = String.IsNullOrEmpty(tag) ? null : tag;
        }

        public override bool Equals(object obj)
        {
            // Can't use "as" with structs :(
            if (obj is SemVer)
            {
                return Equals((SemVer)obj);
            }
            return false;
        }

        public override int GetHashCode()
        {
            if (!_hashCode.HasValue)
            {
                return (_hashCode = HashCodeCombiner
                    .Start()
                    .Add(Major)
                    .Add(Minor)
                    .Add(Patch)
                    .Add(Revision)
                    .Add(Tag)
                    .CombinedHash).Value;
            }
            return _hashCode.Value;
        }

        /// <summary>
        /// Gets the unique, storable, string representation of a Semantic Version.
        /// </summary>
        /// <remarks>
        /// This value is guaranteed to be identical for all SemVer instances with the same
        /// <see cref="Major"/>, <see cref="Minor"/>, <see cref="Patch"/>, <see cref="Revision"/>, and <see cref="Tag"/> values.
        /// </remarks>
        /// <returns>A string representation of a Semantic Version</returns>
        public override string ToString()
        {
            return String.Format(
                "{0}.{1}.{2}.{3}{4}",
                Major,
                Minor,
                Patch,
                Revision,
                String.IsNullOrEmpty(Tag) ? String.Empty : ("-" + Tag));
        }

        /// <summary>
        /// Gets a displayable string for the Semantic Version
        /// </summary>
        public string ToDisplayString()
        {
            return String.Format(
                "{0}.{1}.{2}{3}{4}",
                Major,
                Minor,
                Patch,
                Revision == 0 ?
                    String.Empty :
                    ("." + Revision.ToString()),
                String.IsNullOrEmpty(Tag) ?
                    String.Empty :
                    ("-" + Tag));
        }


        public static readonly Regex SemanticVersionFormatRegex = new Regex(@"^(?<major>\d+)\.(?<minor>\d+)(\.(?<patch>\d+)(\.(?<revision>\d+))?)?(-(?<tag>[A-Za-z0-9-\.]+))?$", RegexOptions.ExplicitCapture);
        public static SemVer Parse(string input)
        {
            SemVer result;
            if (!TryParse(input, out result))
            {
                throw new FormatException(String.Format(Strings.InvalidSemanticVersion, input));
            }
            return result;
        }

        public static bool TryParse(string input, out SemVer result)
        {
            var match = SemanticVersionFormatRegex.Match(input);
            if (!match.Success)
            {
                result = SemVer.Zero;
                return false;
            }
            int major = Int32.Parse(match.Groups["major"].Value);
            int minor = Int32.Parse(match.Groups["minor"].Value);
            int patch = match.Groups["patch"].Success ? Int32.Parse(match.Groups["patch"].Value) : 0;
            int revision = match.Groups["revision"].Success ? Int32.Parse(match.Groups["revision"].Value) : 0;
            string tag = match.Groups["tag"].Success ? match.Groups["tag"].Value : null;

            result = new SemVer(major, minor, patch, revision, tag);
            return true;
        }

        public int CompareTo(SemVer other)
        {
            // Start by comparing numbers
            int compareResult = Major.CompareTo(other.Major);
            if (compareResult != 0) { return compareResult; }
            compareResult = Minor.CompareTo(other.Minor);
            if (compareResult != 0) { return compareResult; }
            compareResult = Patch.CompareTo(other.Patch);
            if (compareResult != 0) { return compareResult; }
            compareResult = Revision.CompareTo(other.Revision);
            if (compareResult != 0) { return compareResult; }

            // Now compare tags
            if (!String.IsNullOrEmpty(Tag) && String.IsNullOrEmpty(other.Tag))
            {
                // Other is "Later"
                return -1;
            }
            else if (String.IsNullOrEmpty(Tag) && !String.IsNullOrEmpty(other.Tag))
            {
                // Other is "Earlier"
                return 1;
            }
            
            // Both have tags, check their segments
            string[] mySegments = Tag.Split('.');
            string[] otherSegments = other.Tag.Split('.');

            for (int i = 0; i < Math.Min(mySegments.Length, otherSegments.Length); i++)
            {
                // Try to parse my segment as an int
                int myVal;
                bool mineNumeric = Int32.TryParse(mySegments[i], out myVal);
                int otherVal;
                bool otherNumeric = Int32.TryParse(otherSegments[i], out otherVal);
                if (mineNumeric && otherNumeric)
                {
                    // Compare Numerically
                    compareResult = myVal.CompareTo(otherVal);
                    if(compareResult != 0) { return compareResult; }
                }
                else if(mineNumeric) 
                {
                        // "Numeric identifiers always have lower precedence than non-numeric identifiers"
                        // - SemVer #12
                        // Other is "Later"
                        return -1;
                }
                else if (otherNumeric)
                {
                    // Other is "Earlier"
                    return 1;
                }
                else
                {
                    // Neither is numeric, compare lexically.
                    compareResult = mySegments[i].CompareTo(otherSegments[i]);
                    if (compareResult != 0) { return compareResult; }
                }
            }
            
            // Reached the end, if one has a longer tag, it is Later
            if (mySegments.Length < otherSegments.Length)
            {
                // Other is "Later"
                return -1;
            }
            else if (mySegments.Length > otherSegments.Length)
            {
                // Other is "Earlier"
                return 1;
            }
            else
            {
                // They're equal!
                return 0;
            }
        }

        public bool Equals(SemVer other)
        {
            return other.Major == Major &&
                other.Minor == Minor &&
                other.Patch == Patch &&
                other.Revision == Revision &&
                String.Equals(other.Tag, Tag, StringComparison.Ordinal);
        }
    }
}