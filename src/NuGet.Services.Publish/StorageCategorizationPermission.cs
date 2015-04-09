
namespace NuGet.Services.Publish
{
    public class StorageCategorizationPermission : ICategorizationPermission
    {
        public bool IsAllowedToSpecifyCategory(string id)
        {
            return true;
        }
    }
}