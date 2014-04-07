
namespace Resolver.Metadata
{
    public class SemanticVersion
    {
        public static SemanticVersion Min { get; private set; }

        static SemanticVersion()
        {
            SemanticVersion.Min = new SemanticVersion(0); 
        }

        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }
        public string Prerelease { get; set; }
        public string BuildMetadata { get; set; }

        public SemanticVersion(int major, int minor = 0, int patch = 0, string prerelease = null, string buildMetadata = null)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Prerelease = prerelease;
            BuildMetadata = buildMetadata;
        }

        public override string ToString()
        {
            return string.Format("{0}.{1}.{2}{3}{4}{5}{6}", 
                Major, Minor, Patch, 
                Prerelease == null ? string.Empty : "-",
                Prerelease,
                BuildMetadata == null ? string.Empty : "+",
                BuildMetadata);
        }

        public static SemanticVersion Parse(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return null;
            }

            //TODO: this is very basic parsing!!!

            int index = s.IndexOf('-');

            if (index > 0)
            {
                s = s.Substring(0, index);
            }

            string[] fields = s.Split('.');
            int major = int.Parse(fields[0]);
            int minor = (fields.Length > 1) ? int.Parse(fields[1]) : 0;
            int patch = (fields.Length > 2) ? int.Parse(fields[2]) : 0;
            return new SemanticVersion(major, minor, patch);
        }

        public SemanticVersion Add(SemanticVersionSpan span)
        {
            return new SemanticVersion(Major + span.Major, Minor + span.Minor, Patch + span.Patch);
        }
    }
}
