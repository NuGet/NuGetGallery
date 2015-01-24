using System.Threading.Tasks;

namespace NuGet.Services.Publish
{
    public interface IRegistrationOwnership
    {
        bool IsAuthorized { get; }
        Task<bool> RegistrationExists(string id);
        Task<bool> IsAuthorizedToRegistration(string id);
        Task CreateRegistration(string id);
        Task DeleteRegistration(string id);
        Task AddRegistrationOwner(string id);
        Task<string> GetUserName();
    }
}