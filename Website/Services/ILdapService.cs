using System.DirectoryServices;

namespace NuGetGallery
{
    public interface ILdapService
    {
        bool Enabled { get; }
        string Domain { get; }
        SearchResult GetUserSearchResult(string username, string password);
        bool ValidateUser(string username, string password);
        User AutoEnroll(string username, string password);
    }
}