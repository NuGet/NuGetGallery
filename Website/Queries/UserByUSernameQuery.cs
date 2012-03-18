using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
{
    public interface IUserByUsernameQuery
    {
        User Execute(
            string username,
            bool includeRoles = true);
    }

    public class UserByUsernameQuery : IUserByUsernameQuery
    {
        private readonly EntitiesContext _dbContext;

        public UserByUsernameQuery(EntitiesContext dbContext)
        {
            _dbContext = dbContext;
        }

        public User Execute(
            string username, 
            bool includeRoles = true)
        {
            var users = _dbContext.Users;
            
            if (includeRoles)
                users.Include(u => u.Roles);
            
            return users.SingleOrDefault(u => u.Username == username);
        }
    }
}