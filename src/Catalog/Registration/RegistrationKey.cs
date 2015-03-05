
namespace NuGet.Services.Metadata.Catalog.Registration
{
    public class RegistrationKey
    {
        public string Id { get; set; }
        public string Version { get; set; }
            
        public override string ToString()
        {
            return Id + "/" + Version;
        }

        public override int GetHashCode()
        {
            int hashCode = ToString().GetHashCode();
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            RegistrationKey rhs = obj as RegistrationKey;

            if (rhs == null)
            {
                return false;
            }

            return (Id == rhs.Id) && (Version == rhs.Version); 
        }
    }
}
