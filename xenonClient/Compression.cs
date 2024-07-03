using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace xenonClient
{
    class Compression
    {
        const ushort COMPRESSION_FORMAT_LZNT1 = 2;
        const ushort COMPRESSION_ENGINE_MAXIMUM = 0x100;

        [DllImport("ntdll.dll")]
        private static extern uint RtlGetCompressionWorkSpaceSize(ushort CompressionFormat, out uint pNeededBufferSize, out uint Unknown);

        [DllImport("ntdll.dll")]
        private static extern uint RtlDecompressBuffer(ushort CompressionFormat, byte[] UncompressedBuffer, int UncompressedBufferSize, byte[] CompressedBuffer,
            int CompressedBufferSize, out int FinalUncompressedSize);

        [DllImport("ntdll.dll")]
        private static extern uint RtlCompressBuffer(ushort CompressionFormat, byte[] SourceBuffer, int SourceBufferLength, byte[] DestinationBuffer,
            int DestinationBufferLength, uint Unknown, out int pDestinationSize, IntPtr WorkspaceBuffer);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LocalAlloc(int uFlags, IntPtr sizetdwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr hMem);

        public static byte[] Compress(byte[] buffer)
        {
            // 定义输出缓冲区，假设压缩后的大小不会超过原始大小的6倍
            var outBuf = new byte[buffer.Length * 6];
            uint dwSize = 0, dwRet = 0;

            // 获取压缩所需的工作空间大小
            uint ret = RtlGetCompressionWorkSpaceSize(COMPRESSION_FORMAT_LZNT1 | COMPRESSION_ENGINE_MAXIMUM, out dwSize, out dwRet);
            if (ret != 0)
            {
                // 如果获取工作空间大小失败，返回null
                return null;
            }

            // 初始化压缩后的数据大小为0
            int dstSize = 0;
            // 分配工作空间
            IntPtr hWork = LocalAlloc(0, new IntPtr(dwSize));

            // 执行压缩操作
            ret = RtlCompressBuffer(COMPRESSION_FORMAT_LZNT1 | COMPRESSION_ENGINE_MAXIMUM, buffer, buffer.Length, outBuf, outBuf.Length, 0, out dstSize, hWork);
            if (ret != 0)
            {
                // 如果压缩失败，释放工作空间并返回null
                LocalFree(hWork);
                return null;
            }

            // 压缩成功，释放工作空间
            LocalFree(hWork);
            // 调整输出缓冲区的大小以匹配压缩后的数据大小
            Array.Resize(ref outBuf, dstSize);

            // 返回压缩后的数据
            return outBuf;
        }
        public static byte[] Decompress(byte[] buffer, int original_size)
        {
            int dwRet = 0;
            byte[] a = new byte[original_size];
            RtlDecompressBuffer(COMPRESSION_FORMAT_LZNT1, a, original_size, buffer, buffer.Length, out dwRet);
            return a;
        }
    }
}
