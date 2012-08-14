using System;
using Moq;
using Xunit;

namespace NuGetGallery
{
    public class LdapServiceFacts
    {
        public class TheAutoEnrollMethod
        {
            //[Fact]
            //public void WillSaveNewUser()
            //{
            //    var cryptoSvc = new Mock<ICryptographyService>();
            //    cryptoSvc
            //        .Setup(x => x.GenerateSaltedHash(It.IsAny<string>(), It.IsAny<string>()))
            //        .Returns("theHashedPassword");
            //    var userRepo = new Mock<IEntityRepository<User>>();
            //    var ldapSvc = CreateLdapService(
            //        cryptoSvc: cryptoSvc,
            //        userRepo: userRepo);

            //    var user = ldapSvc.AutoEnroll(
            //        "theUsername",
            //        "thePassword");

            //    userRepo.Verify(x => x.InsertOnCommit(It.Is<User>(u =>
            //        u.Username == "theUsername" &&
            //        u.HashedPassword == "theHashedPassword" &&
            //        u.UnconfirmedEmailAddress == "theEmailAddress")));
            //    userRepo.Verify(x => x.CommitChanges());
            //}

            //[Fact]
            //public void SetsAnApiKey()
            //{
            //    var userRepo = new Mock<IEntityRepository<User>>();
            //    var ldapSvc = CreateLdapService(
            //        userRepo: userRepo);

            //    var user = ldapSvc.AutoEnroll(
            //        "theUsername",
            //        "thePassword");

            //    Assert.NotEqual(Guid.Empty, user.ApiKey);
            //}

            //[Fact]
            //public void SetsTheEmailToConfirmed()
            //{
            //    var crypto = new Mock<ICryptographyService>();
            //    crypto.Setup(c => c.GenerateToken()).Returns("secret!");
            //    var ldapSvc = CreateLdapService(cryptoSvc: crypto);

            //    var user = ldapSvc.AutoEnroll(
            //        "theUsername",
            //        "thePassword");

            //    Assert.Equal(true, user.Confirmed);
            //}
        }

        static LdapService CreateLdapService(
            Mock<ICryptographyService> cryptoSvc = null,
            Mock<IEntityRepository<User>> userRepo = null)
        {
            cryptoSvc = cryptoSvc ?? new Mock<ICryptographyService>();
            userRepo = userRepo ?? new Mock<IEntityRepository<User>>();

            var ldapSvc = new Mock<LdapService>(
                cryptoSvc.Object,
                userRepo.Object
                ) { CallBase = true };

            return ldapSvc.Object;
        }
    }
}