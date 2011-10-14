using System;
using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
{
    public class UserService : IUserService
    {
        readonly IConfiguration configuration;
        readonly ICryptographyService cryptoSvc;
        readonly IEntityRepository<User> userRepo;

        public UserService(
            IConfiguration configuration,
            ICryptographyService cryptoSvc,
            IEntityRepository<User> userRepo)
        {
            this.configuration = configuration;
            this.cryptoSvc = cryptoSvc;
            this.userRepo = userRepo;
        }

        public virtual User Create(
            string username,
            string password,
            string emailAddress)
        {
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
                hashedPassword)
                {
                    ApiKey = Guid.NewGuid(),
                    EmailAllowed = true,
                    UnconfirmedEmailAddress = emailAddress,
                    EmailConfirmationToken = cryptoSvc.GenerateToken()
                };

            if (!configuration.ConfirmEmailAddresses)
            {
                newUser.ConfirmEmailAddress();
            }

            userRepo.InsertOnCommit(newUser);
            userRepo.CommitChanges();

            return newUser;
        }

        public string UpdateProfile(User user, string emailAddress, bool emailAllowed)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            if (emailAddress != user.EmailAddress)
            {
                var existingUser = FindByEmailAddress(emailAddress);
                if (existingUser != null && existingUser.Key != user.Key)
                {
                    throw new EntityException(Strings.EmailAddressBeingUsed, emailAddress);
                }
                user.UnconfirmedEmailAddress = emailAddress;
                user.EmailConfirmationToken = cryptoSvc.GenerateToken();
            }

            user.EmailAllowed = emailAllowed;
            userRepo.CommitChanges();
            return user.EmailConfirmationToken;
        }

        public User FindByApiKey(Guid apiKey)
        {
            return userRepo.GetAll()
                .Where(u => u.ApiKey == apiKey)
                .SingleOrDefault();
        }

        public virtual User FindByEmailAddress(string emailAddress)
        {
            // TODO: validate input

            return userRepo.GetAll()
                .Where(u => u.EmailAddress == emailAddress)
                .SingleOrDefault();
        }

        public virtual User FindByUsername(string username)
        {
            // TODO: validate input

            return userRepo.GetAll()
                .Include(u => u.Roles)
                .Where(u => u.Username == username)
                .SingleOrDefault();
        }

        public virtual User FindByUsernameAndPassword(
            string username,
            string password)
        {
            // TODO: validate input

            var user = FindByUsername(username);

            if (user == null)
                return null;

            if (!cryptoSvc.ValidateSaltedHash(user.HashedPassword, password))
                return null;

            return user;
        }

        public string GenerateApiKey(string username)
        {
            var user = FindByUsername(username);
            if (user == null)
            {
                return null;
            }

            var newApiKey = Guid.NewGuid();
            user.ApiKey = newApiKey;
            userRepo.CommitChanges();
            return newApiKey.ToString();
        }

        public bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            var user = FindByUsernameAndPassword(username, oldPassword);
            if (user == null)
            {
                return false;
            }

            ChangePassword(user, newPassword);
            userRepo.CommitChanges();
            return true;
        }

        private void ChangePassword(User user, string newPassword)
        {
            var hashedPassword = cryptoSvc.GenerateSaltedHash(newPassword);
            user.HashedPassword = hashedPassword;
        }

        public bool ConfirmEmailAddress(User user, string token)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }
            if (String.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException("token");
            }

            if (user.EmailConfirmationToken != token)
            {
                return false;
            }

            user.ConfirmEmailAddress();

            userRepo.CommitChanges();
            return true;
        }

        public User GeneratePasswordResetToken(string email, int tokenExpirationMinutes)
        {
            if (String.IsNullOrEmpty(email))
            {
                throw new ArgumentNullException("email");
            }
            if (tokenExpirationMinutes < 1)
            {
                throw new ArgumentException("Token expiration should give the user at least a minute to change their password", "tokenExpirationMinutes");
            }

            var user = FindByEmailAddress(email);
            if (user == null)
            {
                return null;
            }

            if (!user.Confirmed)
            {
                throw new InvalidOperationException(Strings.UserIsNotYetConfirmed);
            }

            if (!String.IsNullOrEmpty(user.PasswordResetToken) && !user.PasswordResetTokenExpirationDate.IsInThePast())
            {
                return user;
            }

            user.PasswordResetToken = cryptoSvc.GenerateToken();
            user.PasswordResetTokenExpirationDate = DateTime.UtcNow.AddMinutes(tokenExpirationMinutes);

            userRepo.CommitChanges();
            return user;
        }

        public bool ResetPasswordWithToken(string username, string token, string newPassword)
        {
            if (String.IsNullOrEmpty(newPassword))
            {
                throw new ArgumentNullException("newPassword");
            }

            var user = (from u in userRepo.GetAll()
                        where u.Username == username
                        select u).FirstOrDefault();

            if (user != null && user.PasswordResetToken == token && !user.PasswordResetTokenExpirationDate.IsInThePast())
            {
                if (!user.Confirmed)
                {
                    throw new InvalidOperationException(Strings.UserIsNotYetConfirmed);
                }

                ChangePassword(user, newPassword);
                user.PasswordResetToken = null;
                user.PasswordResetTokenExpirationDate = null;
                userRepo.CommitChanges();
                return true;
            }

            return false;
        }
    }
}