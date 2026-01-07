// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace NuGetGallery.FunctionalTests.Security
{
	/// <summary>
	/// Verify that an expected series of security headers is returned as part of the response.
	/// </summary>
	public class SecurityHeaderTest
	{
		[Fact]
		[Priority(2)]
		[Category("P2Tests")]
		public async Task HomePageReturnsExpectedSecurityHeaders()
		{
            // Arrange & Act
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(UrlHelper.BaseUrl);

            // Assert
            response.EnsureSuccessStatusCode();

            AssertSecurityHeaders(response);
        }

		[Fact]
		[Priority(2)]
		[Category("P2Tests")]
		public async Task PackagesPageReturnsExpectedSecurityHeaders()
		{
            // Arrange & Act
            using var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(UrlHelper.PackagesPageUrl);

            // Assert
            response.EnsureSuccessStatusCode();

            AssertSecurityHeaders(response);
        }

		private static void AssertSecurityHeaders(HttpResponseMessage response)
		{
			Assert.True(response.Headers.TryGetValues("X-Frame-Options", out var xFrameOptions));
			Assert.Contains("DENY", string.Join(", ", xFrameOptions), System.StringComparison.OrdinalIgnoreCase);

			Assert.True(response.Headers.TryGetValues("X-XSS-Protection", out var xssProtection));
			Assert.Contains("1; mode=block", string.Join(", ", xssProtection));

			Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var contentTypeOptions));
			Assert.Contains("nosniff", string.Join(", ", contentTypeOptions));

			Assert.True(response.Headers.TryGetValues("Strict-Transport-Security", out var strictTransportSecurity));
			Assert.Contains("max-age=31536000", string.Join(", ", strictTransportSecurity));
		}
	}
}
