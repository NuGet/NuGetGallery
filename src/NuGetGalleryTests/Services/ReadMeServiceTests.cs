using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGetGallery.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Services.Tests
{
    [TestClass()]
    public class ReadMeServiceTests
    {
        [TestMethod()]
        public void GetReadMeUrlFromRepositoryUrlTestPass()
        {
            Assert.AreNotEqual(ReadMeService.GetReadMeUrlFromRepositoryUrl("http://github.com/fay/scrabble"), "");
            Assert.AreNotEqual(ReadMeService.GetReadMeUrlFromRepositoryUrl("http://www.github.com/fay/scrabble"), "");
            Assert.AreNotEqual(ReadMeService.GetReadMeUrlFromRepositoryUrl("www.github.com/fay/scrabble"), "");
            Assert.AreNotEqual(ReadMeService.GetReadMeUrlFromRepositoryUrl("github.com/fay/scrabble"), "");
            Assert.AreNotEqual(ReadMeService.GetReadMeUrlFromRepositoryUrl("http://github.com/fay/scrabble/"), "");
            Assert.AreNotEqual(ReadMeService.GetReadMeUrlFromRepositoryUrl("https://github.com/fay/scrabble/"), "");
            Assert.AreNotEqual(ReadMeService.GetReadMeUrlFromRepositoryUrl("http://github.com/fay456/scrabble5345/"), "");
            Assert.AreNotEqual(ReadMeService.GetReadMeUrlFromRepositoryUrl("http://github.com/234fay/234scrabble/"), "");
        }
        [TestMethod()]
        public void GetReadMeUrlFromRepositoryUrlTestFail()
        {
            Assert.AreEqual(ReadMeService.GetReadMeUrlFromRepositoryUrl("github.com/"), "");
            Assert.AreEqual(ReadMeService.GetReadMeUrlFromRepositoryUrl("github.com/r//"), "");
            Assert.AreEqual(ReadMeService.GetReadMeUrlFromRepositoryUrl("http://github.com///"), "");
            Assert.AreEqual(ReadMeService.GetReadMeUrlFromRepositoryUrl("http://github.com/face/book/skdlfj"), "");
            Assert.AreEqual(ReadMeService.GetReadMeUrlFromRepositoryUrl("http://www.bitbucket.org/sdf/sdf/"), "");
        }
    }
}