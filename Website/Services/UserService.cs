using System;
using System.Data.Entity;
using System.Linq;

namespace NuGetGallery {
    public class UserService : IUserService {
        readonly IConfiguration configuration;
        readonly ICryptographyService cryptoSvc;
        readonly IEntityRepository<User> userRepo;

        public UserService(
            IConfiguration configuration,
            ICryptographyService cryptoSvc,
            IEntityRepository<User> userRepo) {
            this.configuration = configuration;
            this.cryptoSvc = cryptoSvc;
            this.userRepo = userRepo;
        }

        public virtual User Create(
            string username,
            string password,
            string emailAddress) {
            // TODO: validate input
            // TODO: consider encrypting email address with a public key, and having the background process that send messages have the private key to decrypt

            var existingUser = FindByUsername(username);
            if (existingUser != null)
                throw new EntityException(Strings.UsernameNotAvailable, username);

            existingUser = FindByEmailAddress(emailAddress);
            if (existingUser != null)
                throw new EntityException(Strings.EmailAddressBeingUsed, emailAddress);

            var hashedPassword = cryptoSvc.GenerateSaltedHash(password);

            var newUser = new User(
                username,
                hashedPassword,
                emailAddress) {
                    ApiKey = Guid.NewGuid(),
                    EmailAllowed = true,
                    ConfirmationToken = cryptoSvc.GenerateToken()
                };

            if (!configuration.ConfirmEmailAddresses)
                newUser.Confirmed = true;

            userRepo.InsertOnCommit(newUser);
            userRepo.CommitChanges();

            return newUser;
        }

        public User FindByApiKey(Guid apiKey) {
            return userRepo.GetAll()
                .Where(u => u.ApiKey == apiKey)
                .SingleOrDefault();
        }

        public virtual User FindByEmailAddress(string emailAddress) {
            // TODO: validate input

            return userRepo.GetAll()
                .Where(u => u.EmailAddress == emailAddress)
                .SingleOrDefault();
        }

        public virtual User FindByUsername(string username) {
            // TODO: validate input

            return userRepo.GetAll()
                .Include(u => u.Roles)
                .Where(u => u.Username == username)
                .SingleOrDefault();
        }

        public virtual User FindByUsernameAndPassword(
            string username,
            string password) {
            // TODO: validate input

            var user = FindByUsername(username);

            if (user == null)
                return null;

            if (!cryptoSvc.ValidateSaltedHash(user.HashedPassword, password))
                return null;

            return user;
        }

        public string GenerateApiKey(string username) {
            var user = FindByUsername(username);
            if (user == null) {
                return null;
            }

            var newApiKey = Guid.NewGuid();
            user.ApiKey = newApiKey;
            userRepo.CommitChanges();
            return newApiKey.ToString();
        }

        public bool ChangePassword(string username, string oldPassword, string newPassword) {
            var user = FindByUsernameAndPassword(username, oldPassword);
            if (user == null) {
                return false;
            }

            var hashedPassword = cryptoSvc.GenerateSaltedHash(newPassword);
            user.HashedPassword = hashedPassword;
            userRepo.CommitChanges();
            return true;
        }

        public bool ConfirmAccount(string token) {
            if (String.IsNullOrEmpty(token)) {
                throw new ArgumentNullException("token");
            }
            var user = (from u in userRepo.GetAll()
                        where u.ConfirmationToken == token
                        select u).FirstOrDefault();
            if (user == null) {
                return false;
            }

            if (user.ConfirmationToken != token) {
                return false;
            }
            user.Confirmed = true;
            userRepo.CommitChanges();
            return true;
        }
    }
}