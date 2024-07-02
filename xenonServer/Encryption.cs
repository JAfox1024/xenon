using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace xenonServer
{
    class Encryption
    {
        /// <summary>
        /// 使用AES加密算法加密数据。
        /// </summary>
        /// <param name="data">要加密的数据。</param>
        /// <param name="key">加密密钥。</param>
        /// <returns>加密后的数据。</returns>
        public static byte[] Encrypt(byte[] data, byte[] key)
        {
            // 初始化向量为16字节的零数组，对于每次加密应该是唯一的，但这里为了简化示例使用了固定值。
            byte[] iv = new byte[16];

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.IV = iv;

                // 使用aesAlg的Key和IV创建加密器对象
                using (ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV))
                using (MemoryStream msEncrypt = new MemoryStream())
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    // 写入加密后的数据到内存流
                    csEncrypt.Write(data, 0, data.Length);
                    csEncrypt.FlushFinalBlock();

                    // 返回加密后的数据
                    return msEncrypt.ToArray();
                }
            }
        }

        /// <summary>
        /// 使用AES解密算法解密数据。
        /// </summary>
        /// <param name="data">要解密的数据。</param>
        /// <param name="key">解密密钥。</param>
        /// <returns>解密后的数据。</returns>
        public static byte[] Decrypt(byte[] data, byte[] key)
        {
            // 初始化向量为16字节的零数组，对于每次解密应该是唯一的，但这里为了简化示例使用了固定值。
            byte[] iv = new byte[16];

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.IV = iv;

                // 使用aesAlg的Key和IV创建解密器对象
                using (ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV))
                using (MemoryStream msDecrypt = new MemoryStream())
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Write))
                {
                    // 写入解密后的数据到内存流
                    csDecrypt.Write(data, 0, data.Length);
                    csDecrypt.FlushFinalBlock();

                    // 返回解密后的数据
                    return msDecrypt.ToArray();
                }
            }
        }
    }
}
