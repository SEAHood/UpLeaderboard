using System;
using System.Security.Cryptography;
using System.Text;

namespace UpWebServices
{
    internal static class HashHelper
    {
        private const string SecurityKey = "up-secure";

        public static bool Verify(string secureField, string encryptedSecureField)
        {
            var decryptedSecureField = Decrypt(encryptedSecureField);
            return secureField.Equals(decryptedSecureField);
        }

        public static string Encrypt(string stringToEncrypt)
        {
            var toEncryptedArray = Encoding.UTF8.GetBytes(stringToEncrypt);
            var md5CryptoServiceProvider = new MD5CryptoServiceProvider();
            var securityKeyArray = md5CryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(SecurityKey));

            md5CryptoServiceProvider.Clear();

            var tripleDesCryptoServiceProvider = new TripleDESCryptoServiceProvider
            {
                Key = securityKeyArray,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };

            var cryptoTransform = tripleDesCryptoServiceProvider.CreateEncryptor();
            var resultArray = cryptoTransform.TransformFinalBlock(toEncryptedArray, 0, toEncryptedArray.Length);
            tripleDesCryptoServiceProvider.Clear();

            return Convert.ToBase64String(resultArray, 0, resultArray.Length);

        }

        public static string Decrypt(string stringToDecrypt)
        {
            var toEncryptArray = Convert.FromBase64String(stringToDecrypt);
            var md5CryptoServiceProvider = new MD5CryptoServiceProvider();
            var securityKeyArray = md5CryptoServiceProvider.ComputeHash(Encoding.UTF8.GetBytes(SecurityKey));

            md5CryptoServiceProvider.Clear();

            var tripleDesCryptoServiceProvider = new TripleDESCryptoServiceProvider
            {
                Key = securityKeyArray,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };

            var cryptoTransform = tripleDesCryptoServiceProvider.CreateDecryptor();
            var resultArray = cryptoTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            tripleDesCryptoServiceProvider.Clear();

            return Encoding.UTF8.GetString(resultArray);
        }
    }
}
