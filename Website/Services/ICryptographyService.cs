using System;

namespace NuGetGallery
{
    public interface ICryptographyService : IDisposable
    {
        // TODO: combine these into one Generate and Validate method that detects the salt based on the number of bytes
        
        string HashAlgorithmId { get; }
        
        string GenerateHash(
            byte[] input,
            string hashAlgorithmId = Const.Sha512HashAlgorithmId);
        
        string GenerateSaltedHash(
            string input,
            string hashAlgorithmId = Const.Sha512HashAlgorithmId);

        bool ValidateHash(
            string hash,
            byte[] input,
            string hashAlgorithmId = Const.Sha512HashAlgorithmId);
        
        bool ValidateSaltedHash(
            string hash,
            string input,
            string hashAlgorithmId = Const.Sha512HashAlgorithmId);
    }
}