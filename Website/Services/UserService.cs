using System;
using System.Data.Entity;
using System.Linq;

namespace NuGetGallery
{
    public class UserService : IUserService
    {
        private readonly ICryptographyService _cryptoSvc;
        private readonly GallerySetting _settings;
        private readonly IEntityRepository<User> _userRepo;

        public UserService(
            GallerySetting settings,
            ICryptographyService cryptoSvc,
            IEntityRepository<User> userRepo)
        {
            _settings = settings;
            _cryptoSvc = cryptoSvc;
            _userRepo = userRepo;
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
            {
                throw new EntityException(Strings.UsernameNotAvailable, username);
            }

            existingUser = FindByEmailAddress(emailAddress);
            if (existingUser != null)
            {
                throw new EntityException(Strings.EmailAddressBeingUsed, emailAddress);
            }

            var hashedPassword = _cryptoSvc.GenerateSaltedHash(password, Constants.PBKDF2HashAlgorithmId);

            var newUser = new User(
                username,
                hashedPassword)
                {
                    ApiKey = Guid.NewGuid(),
                    EmailAllowed = true,
                    UnconfirmedEmailAddress = emailAddress,
                    EmailConfirmationToken = _cryptoSvc.GenerateToken(),
                    PasswordHashAlgorithm = Constants.PBKDF2HashAlgorithmId,
                };

            if (!_settings.ConfirmEmailAddresses)
            {
                newUser.ConfirmEmailAddress();
            }

            _userRepo.InsertOnCommit(newUser);
            _userRepo.CommitChanges();

            return newUser;
        }

        public void UpdateProfile(User user, string emailAddress, bool emailAllowed)
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
                user.EmailConfirmationToken = _cryptoSvc.GenerateToken();
            }

            user.EmailAllowed = emailAllowed;
            _userRepo.CommitChanges();
        }

        public User FindByApiKey(Guid apiKey)
        {
            return _userRepo.GetAll().SingleOrDefault(u => u.ApiKey == apiKey);
        }

        public virtual User FindByEmailAddress(string emailAddress)
        {
            // TODO: validate input

            return _userRepo.GetAll().SingleOrDefault(u => u.EmailAddress == emailAddress);
        }

        public virtual User FindByUnconfimedEmailAddress(string unconfirmedEmailAddress)
        {
            // TODO: validate input

            return _userRepo.GetAll().SingleOrDefault(u => u.UnconfirmedEmailAddress == unconfirmedEmailAddress);
        }

        public virtual User FindByUsername(string username)
        {
            // TODO: validate input

            return _userRepo.GetAll()
                .Include(u => u.Roles)
                .SingleOrDefault(u => u.Username == username);
        }

        public virtual User FindByUsernameAndPassword(string username, string password)
        {
            // TODO: validate input

            var user = FindByUsername(username);

            if (user == null)
            {
                return null;
            }

            if (!_cryptoSvc.ValidateSaltedHash(user.HashedPassword, password, user.PasswordHashAlgorithm))
            {
                return null;
            }

            return user;
        }

        public virtual User FindByUsernameOrEmailAddressAndPassword(string usernameOrEmail, string password)
        {
            // TODO: validate input

            var user = FindByUsername(usernameOrEmail)
                       ?? FindByEmailAddress(usernameOrEmail);

            if (user == null)
            {
                return null;
            }

            if (!_cryptoSvc.ValidateSaltedHash(user.HashedPassword, password, user.PasswordHashAlgorithm))
            {
                return null;
            }
            else if (!user.PasswordHashAlgorithm.Equals(Constants.PBKDF2HashAlgorithmId, StringComparison.OrdinalIgnoreCase))
            {
                // If the user can be authenticated and they are using an older password algorithm, migrate them to the current one.
                ChangePasswordInternal(user, password);
                _userRepo.CommitChanges();
            }

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
            _userRepo.CommitChanges();
            return newApiKey.ToString();
        }

        public bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            // Review: If the old password is hashed using something other than PBKDF2, we end up making an extra db call that changes the old hash password.
            // This operation is rare enough that I'm not inclined to change it.
            var user = FindByUsernameAndPassword(username, oldPassword);
            if (user == null)
            {
                return false;
            }

            ChangePasswordInternal(user, newPassword);
            _userRepo.CommitChanges();
            return true;
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

            _userRepo.CommitChanges();
            return true;
        }

        public User GeneratePasswordResetToken(string usernameOrEmail, int tokenExpirationMinutes)
        {
            if (String.IsNullOrEmpty(usernameOrEmail))
            {
                throw new ArgumentNullException("usernameOrEmail");
            }
            if (tokenExpirationMinutes < 1)
            {
                throw new ArgumentException(
                    "Token expiration should give the user at least a minute to change their password", "tokenExpirationMinutes");
            }

            var user = FindByEmailAddress(usernameOrEmail);
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

            user.PasswordResetToken = _cryptoSvc.GenerateToken();
            user.PasswordResetTokenExpirationDate = DateTime.UtcNow.AddMinutes(tokenExpirationMinutes);

            _userRepo.CommitChanges();
            return user;
        }

        public bool ResetPasswordWithToken(string username, string token, string newPassword)
        {
            if (String.IsNullOrEmpty(newPassword))
            {
                throw new ArgumentNullException("newPassword");
            }

            var user = (from u in _userRepo.GetAll()
                        where u.Username == username
                        select u).FirstOrDefault();

            if (user != null && user.PasswordResetToken == token && !user.PasswordResetTokenExpirationDate.IsInThePast())
            {
                if (!user.Confirmed)
                {
                    throw new InvalidOperationException(Strings.UserIsNotYetConfirmed);
                }

                ChangePasswordInternal(user, newPassword);
                user.PasswordResetToken = null;
                user.PasswordResetTokenExpirationDate = null;
                _userRepo.CommitChanges();
                return true;
            }

            return false;
        }

        private void ChangePasswordInternal(User user, string newPassword)
        {
            var hashedPassword = _cryptoSvc.GenerateSaltedHash(newPassword, Constants.PBKDF2HashAlgorithmId);
            user.PasswordHashAlgorithm = Constants.PBKDF2HashAlgorithmId;
            user.HashedPassword = hashedPassword;
        }
    }
}