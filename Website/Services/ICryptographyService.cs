﻿namespace NuGetGallery
{
    public interface ICryptographyService
    {
        // TODO: combine these into one Generate and Validate method that detects the salt based on the number of bytes

        string GenerateHash(
            byte[] input,
            string hashAlgorithmId = Constants.Sha512HashAlgorithmId);

        string GenerateSaltedHash(
            string input,
            string hashAlgorithmId);

        bool ValidateHash(
            string hash,
            byte[] input,
            string hashAlgorithmId = Constants.Sha512HashAlgorithmId);

        bool ValidateSaltedHash(
            string hash,
            string input,
            string hashAlgorithmId);

        string GenerateToken();

        string EncryptString(string clearText, string purpose);
        string DecryptString(string cipherText, string purpose);
    }
}