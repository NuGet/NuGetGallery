using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace NuGetGallery {
    public class CryptographyService : ICryptographyService {
        readonly HashAlgorithm hashAlgorithm = new SHA512Managed();
        readonly int saltLengthInBytes = 8;

        public string HashAlgorithmId { get { return Const.Sha512HashAlgorithmId; } }

        public string GenerateHash(
            byte[] input,
            string hashAlgorithmId = Const.Sha512HashAlgorithmId) {
            ValidateHashAlgorithmId(hashAlgorithmId);

            var hashBytes = hashAlgorithm.ComputeHash(input);

            var hash = Convert.ToBase64String(hashBytes);

            return hash;
        }

        public string GenerateSaltedHash(
            string input,
            string hashAlgorithmId = Const.Sha512HashAlgorithmId) {
            ValidateHashAlgorithmId(hashAlgorithmId);

            var saltBytes = new byte[saltLengthInBytes];

            using (var cryptoProvider = new RNGCryptoServiceProvider())
                cryptoProvider.GetNonZeroBytes(saltBytes);

            var textBytes = Encoding.UTF8.GetBytes(input);

            var saltedTextBytes = new byte[saltBytes.Length + textBytes.Length];
            Array.Copy(saltBytes, saltedTextBytes, saltBytes.Length);
            Array.Copy(textBytes, 0, saltedTextBytes, saltBytes.Length, textBytes.Length);

            var hashBytes = hashAlgorithm.ComputeHash(saltedTextBytes);

            var saltPlusHashBytes = new byte[saltBytes.Length + hashBytes.Length];
            Array.Copy(saltBytes, saltPlusHashBytes, saltBytes.Length);
            Array.Copy(hashBytes, 0, saltPlusHashBytes, saltBytes.Length, hashBytes.Length);

            var saltedHash = Convert.ToBase64String(saltPlusHashBytes);
            return saltedHash;
        }

        public bool ValidateHash(
            string hash,
            byte[] input,
            string hashAlgorithmId = Const.Sha512HashAlgorithmId) {
            ValidateHashAlgorithmId(hashAlgorithmId);

            return hash.Equals(GenerateHash(input));
        }

        public bool ValidateSaltedHash(
            string hash,
            string input,
            string hashAlgorithmId = Const.Sha512HashAlgorithmId) {
            ValidateHashAlgorithmId(hashAlgorithmId);

            var saltPlusHashBytes = Convert.FromBase64String(hash);

            var saltBytes = saltPlusHashBytes.Take(saltLengthInBytes).ToArray();
            var hashToValidateBytes = saltPlusHashBytes.Skip(saltLengthInBytes).ToArray();

            var textBytes = Encoding.UTF8.GetBytes(input);

            var saltedTextBytes = new byte[saltBytes.Length + textBytes.Length];
            Array.Copy(saltBytes, saltedTextBytes, saltBytes.Length);
            Array.Copy(textBytes, 0, saltedTextBytes, saltBytes.Length, textBytes.Length);

            var hashBytes = hashAlgorithm.ComputeHash(saltedTextBytes);

            for (int i = 0; i < hashBytes.Length; i++)
                if (!hashBytes[i].Equals(hashToValidateBytes[i]))
                    return false;

            return true;
        }

        public void Dispose() {
            hashAlgorithm.Dispose();
        }

        void ValidateHashAlgorithmId(string id) {
            if (!id.Equals(Const.Sha512HashAlgorithmId, StringComparison.InvariantCultureIgnoreCase))
                throw new Exception(string.Format("Unexpected hash algorithm identifier '{0}'", id));
        }

        public string GenerateToken() {
            byte[] data = new byte[0x10];
            using (var crypto = new RNGCryptoServiceProvider()) {
                crypto.GetBytes(data);

                return HttpServerUtility.UrlTokenEncode(data);
            }
        }

        public string ConvertToBase64UrlString(byte[] data) {
            // It's possible for the token to have a slash in it which is bad for routing. :(
            // URL encoding is not enough as ASP.NET or IIS still translates it as a slash 
            // to avoid path canonicalization hacks.

            // Using a modified Base64 for URL encoding instead: http://en.wikipedia.org/wiki/Base64#RFC_4648.        
            return Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').Replace("=", String.Empty);
        }
    }
}