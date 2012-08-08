using System;
using System.Configuration;
using System.DirectoryServices;
using System.Linq;

namespace NuGetGallery
{
    public static class LdapService
    {
        public static bool Enabled
        {
            get { return !string.IsNullOrWhiteSpace(Uri); }
        }

        public static string Domain
        {
            get { return ConfigurationManager.AppSettings.Get("Ldap:Domain"); }
        }

        private static string Uri
        {
            get { return ConfigurationManager.AppSettings.Get("Ldap:Uri"); }
        }

        private static string UserBase
        {
            get { return ConfigurationManager.AppSettings.Get("Ldap:UserBase"); }
        }

        public static class Properties
        {
            public static string Username
            {
                get { return ConfigurationManager.AppSettings.Get("Ldap:Prop:Username") ?? "samaccountname"; }
            }

            public static string DisplayName
            {
                get { return ConfigurationManager.AppSettings.Get("Ldap:Prop:DisplayName") ?? "displayname"; }
            }

            public static string Email
            {
                get { return ConfigurationManager.AppSettings.Get("Ldap:Prop:Email") ?? "mail"; }
            }
        }

        private static DirectoryEntry GetDirectoryEntry(string username, string password)
        {
            return new DirectoryEntry(string.Concat(Uri, '/', UserBase), string.Concat(Domain, '\\', username), password);
        }

        public static SearchResult GetUserSearchResult(string username, string password)
        {
            return new DirectorySearcher(GetDirectoryEntry(username, password), string.Format("({0}={1})", Properties.Username, username), new[] { Properties.Username, Properties.DisplayName, Properties.Email }).FindOne();
        }

        public static bool ValidateUser(string username, string password)
        {
            return ValidateUser(GetDirectoryEntry(username, password));
        }

        private static bool ValidateUser(DirectoryEntry directoryEntry)
        {
            try
            {
                var nativeObject = directoryEntry.NativeObject;
                return nativeObject != null;
            }
            catch
            {
                return false;
            }
        }



        public static User AutoEnroll(string username, string password, ICryptographyService cryptoSvc, IEntityRepository<User> userRepo)
        {
            if (!Enabled)
                return null;

            if (username.ToLowerInvariant().StartsWith(string.Concat(Domain, '\\')))
                username = username.Replace(string.Concat(Domain, '\\'), string.Empty);

            if (ValidateUser(username, password))
            {
                var searchResult = GetUserSearchResult(username, password);
                var user = new User(GetStringProperty(searchResult, Properties.Username).ToLowerInvariant(), cryptoSvc.GenerateSaltedHash(password, Constants.PBKDF2HashAlgorithmId))
                               {
                                   ApiKey = Guid.NewGuid(),
                                   DisplayName = GetStringProperty(searchResult, Properties.DisplayName),
                                   PasswordHashAlgorithm = Constants.PBKDF2HashAlgorithmId,
                               };
                var email = GetStringProperty(searchResult, Properties.Email);
                if (!string.IsNullOrEmpty(email))
                {
                    user.EmailAllowed = true;
                    user.UnconfirmedEmailAddress = email.ToLowerInvariant();
                    user.EmailConfirmationToken = cryptoSvc.GenerateToken();
                    user.ConfirmEmailAddress();
                }
                userRepo.InsertOnCommit(user);
                userRepo.CommitChanges();
                return user;
            }
            return null;
        }

        private static string GetStringProperty(SearchResult searchResult, string propertyName)
        {
            return searchResult.Properties[propertyName].Cast<string>().First();
        }
    }
}