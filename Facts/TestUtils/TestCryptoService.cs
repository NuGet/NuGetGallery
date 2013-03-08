using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class TestCryptoService : ICryptographyService
    {
        public string GenerateHash(byte[] input, string hashAlgorithmId = Constants.Sha512HashAlgorithmId)
        {
            throw new NotImplementedException();
        }

        public string GenerateSaltedHash(string input, string hashAlgorithmId)
        {
            throw new NotImplementedException();
        }

        public bool ValidateHash(string hash, byte[] input, string hashAlgorithmId = Constants.Sha512HashAlgorithmId)
        {
            throw new NotImplementedException();
        }

        public bool ValidateSaltedHash(string hash, string input, string hashAlgorithmId)
        {
            throw new NotImplementedException();
        }

        public string GenerateToken()
        {
            throw new NotImplementedException();
        }

        public string EncryptString(string clearText, string purpose)
        {
            return clearText + "," + purpose;
        }

        public string DecryptString(string cipherText, string purpose)
        {
            if (!cipherText.EndsWith("," + purpose))
            {
                throw new CryptographicException("The purpose is incorrect");
            }
            return cipherText.Substring(0, cipherText.Length - purpose.Length - 1);
        }
    }
}
