using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace NuGetGallery
{
    public class CryptographyService : ICryptographyService
    {
        readonly int saltLengthInBytes = 16;

        public string GenerateHash(
            byte[] input,
            string hashAlgorithmId = Const.Sha512HashAlgorithmId)
        {
            byte[] hashBytes;

            using (var hashAlgorithm = HashAlgorithm.Create(hashAlgorithmId))
            {
                hashBytes = hashAlgorithm.ComputeHash(input);
            }
            
            var hash = Convert.ToBase64String(hashBytes);
            return hash;
        }

        public string GenerateSaltedHash(
            string input,
            string hashAlgorithmId = Const.Sha1HashAlgorithmId)
        {
            var saltBytes = new byte[saltLengthInBytes];

            using (var cryptoProvider = new RNGCryptoServiceProvider())
                cryptoProvider.GetNonZeroBytes(saltBytes);

            var textBytes = Encoding.Unicode.GetBytes(input);

            var saltedTextBytes = new byte[saltBytes.Length + textBytes.Length];
            Array.Copy(saltBytes, saltedTextBytes, saltBytes.Length);
            Array.Copy(textBytes, 0, saltedTextBytes, saltBytes.Length, textBytes.Length);

            byte[] hashBytes;
            using (var hashAlgorithm = HashAlgorithm.Create(hashAlgorithmId))
            {
                hashBytes = hashAlgorithm.ComputeHash(saltedTextBytes);
            }

            var saltPlusHashBytes = new byte[saltBytes.Length + hashBytes.Length];
            Array.Copy(saltBytes, saltPlusHashBytes, saltBytes.Length);
            Array.Copy(hashBytes, 0, saltPlusHashBytes, saltBytes.Length, hashBytes.Length);

            var saltedHash = Convert.ToBase64String(saltPlusHashBytes);
            return saltedHash;
        }

        public string GenerateToken()
        {
            byte[] data = new byte[0x10];
            
            using (var crypto = new RNGCryptoServiceProvider())
            {
                crypto.GetBytes(data);
            
                return HttpServerUtility.UrlTokenEncode(data);
            }
        }

        public bool ValidateHash(
            string hash,
            byte[] input,
            string hashAlgorithmId = Const.Sha512HashAlgorithmId)
        {
            return hash.Equals(GenerateHash(input));
        }

        public bool ValidateSaltedHash(
            string hash,
            string input,
            string hashAlgorithmId = Const.Sha1HashAlgorithmId)
        {
            var saltPlusHashBytes = Convert.FromBase64String(hash);

            var saltBytes = saltPlusHashBytes.Take(saltLengthInBytes).ToArray();
            var hashToValidateBytes = saltPlusHashBytes.Skip(saltLengthInBytes).ToArray();

            var textBytes = Encoding.Unicode.GetBytes(input);

            var saltedTextBytes = new byte[saltBytes.Length + textBytes.Length];
            Array.Copy(saltBytes, saltedTextBytes, saltBytes.Length);
            Array.Copy(textBytes, 0, saltedTextBytes, saltBytes.Length, textBytes.Length);

            byte[] hashBytes;
            using (var hashAlgorithm = HashAlgorithm.Create(hashAlgorithmId))
            {
                hashBytes = hashAlgorithm.ComputeHash(saltedTextBytes);
            }

            for (int i = 0; i < hashBytes.Length; i++)
                if (!hashBytes[i].Equals(hashToValidateBytes[i]))
                    return false;

            return true;
        }
    }
}