using System.Security.Cryptography;

namespace backend.common
{
    public class StringCipher
    {
        public static string DecryptData(string encryptedData, string hexSaltKey)
        {
            byte[] saltKey = ConvertHexStringToByteArray(hexSaltKey);
            byte[] encryptedBytes = Convert.FromBase64String(encryptedData);

            using (Aes aes = Aes.Create())
            {
                aes.Key = saltKey;

                // Extract IV from the encrypted bytes
                byte[] iv = new byte[aes.BlockSize / 8];
                Array.Copy(encryptedBytes, 0, iv, 0, iv.Length);

                // Extract ciphertext
                byte[] ciphertext = new byte[encryptedBytes.Length - iv.Length];
                Array.Copy(encryptedBytes, iv.Length, ciphertext, 0, ciphertext.Length);

                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (MemoryStream msDecrypt = new MemoryStream(ciphertext))
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }

        private static byte[] ConvertHexStringToByteArray(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
    }
}
