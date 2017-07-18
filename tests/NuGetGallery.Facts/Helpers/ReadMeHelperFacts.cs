using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using Xunit;
using NuGetGallery.Helpers;
using NuGetGallery.RequestModels;
using System.Web;

namespace NuGetGallery.Helpers
{
    public class ReadMeHelperFacts
    {
        [Fact]
        public void HasReadMeNullFormDataFact()
        {
            Assert.False(ReadMeHelper.HasReadMe(null));

            ReadMeRequest readMeRequest = new ReadMeRequest
            {
                ReadMeType = ""
            };
            Assert.False(ReadMeHelper.HasReadMe(readMeRequest));
        }

        [Fact]
        public void HasReadMeUrlFact()
        {
            ReadMeRequest readMeRequest = new ReadMeRequest
            {
                ReadMeType = "Url",
                ReadMeUrl = ""
            };
            Assert.False(ReadMeHelper.HasReadMe(readMeRequest));
            readMeRequest.ReadMeUrl = "helloworld";
            Assert.False(ReadMeHelper.HasReadMe(readMeRequest));
            readMeRequest.ReadMeUrl = "http://www.github.com";
            Assert.True(ReadMeHelper.HasReadMe(readMeRequest));
            readMeRequest.ReadMeUrl = "www.github.com";
            Assert.True(ReadMeHelper.HasReadMe(readMeRequest));
            readMeRequest.ReadMeUrl = "github.com";
            Assert.True(ReadMeHelper.HasReadMe(readMeRequest));
        }

        [Fact]
        public void HasReadMeFilePathFact()
        {
            ReadMeRequest readMeRequest = new ReadMeRequest
            {
                ReadMeType = "File",
                ReadMeFile = null
            };
            Assert.False(ReadMeHelper.HasReadMe(readMeRequest));
        }

        [Fact]
        public void HasReadMeWritten()
        {
            ReadMeRequest readMeRequest = new ReadMeRequest
            {
                ReadMeType = "Written",
                ReadMeWritten = null
            };
            Assert.False(ReadMeHelper.HasReadMe(readMeRequest));
            readMeRequest.ReadMeWritten = "";
            Assert.False(ReadMeHelper.HasReadMe(readMeRequest));
            Mock<ReadMeRequest> mockRequest = new Mock<ReadMeRequest>();
            mockRequest.Setup(x => x.ReadMeWritten).Returns("foo");
            readMeRequest.ReadMeWritten = mockRequest.Object.ReadMeWritten;
            Assert.True(ReadMeHelper.HasReadMe(readMeRequest));
        }


    }
}
