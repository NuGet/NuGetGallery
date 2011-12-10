using System;

namespace NuGetGallery
{
    public interface ICryptographyService
    {
        // TODO: combine these into one Generate and Validate method that detects the salt based on the number of bytes

        string GenerateHash(
            byte[] input,
            string hashAlgorithmId = Constants.Sha512HashAlgorithmId);

        string GenerateSaltedHash(
            string input,
            string hashAlgorithmId = Constants.Sha1HashAlgorithmId);

        bool ValidateHash(
            string hash,
            byte[] input,
            string hashAlgorithmId = Constants.Sha512HashAlgorithmId);

        bool ValidateSaltedHash(
            string hash,
            string input,
            string hashAlgorithmId = Constants.Sha1HashAlgorithmId);

        string GenerateToken();
    }
}