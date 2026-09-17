using System;
using System.IO;
using System.Threading;

namespace VncLib.Console.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            VncConnection vnc = new VncConnection();
            vnc.UpdateInterval = 500;
            vnc.AutoUpdate = true;
            vnc.Connect("10.40.1.126", 5901, "Initial1");
            vnc.UpdateScreen();
            for (int i = 0; ; ++i)
            {
                Thread.Sleep(3000);
                var fb = vnc.GetFramebuffer();
                if (fb != null)
                {
                    SaveBmp(fb, vnc.FramebufferWidth, vnc.FramebufferHeight, $"image{i}.bmp");
                }
            }
        }

        private static void SaveBmp(byte[] bgr32, int width, int height, string path)
        {
            int stride = width * 4;
            int imageSize = stride * height;
            int fileSize = 54 + imageSize;

            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                // BITMAPFILEHEADER (14 bytes)
                bw.Write((byte)'B');
                bw.Write((byte)'M');
                bw.Write(fileSize);
                bw.Write(0); // reserved
                bw.Write(54); // data offset

                // BITMAPINFOHEADER (40 bytes)
                bw.Write(40); // header size
                bw.Write(width);
                bw.Write(height); // positive = bottom-up
                bw.Write((ushort)1); // planes
                bw.Write((ushort)32); // bit count
                bw.Write(0); // compression (BI_RGB)
                bw.Write(imageSize);
                bw.Write(0); // X pixels per meter
                bw.Write(0); // Y pixels per meter
                bw.Write(0); // colors used
                bw.Write(0); // colors important

                // Pixel data (bottom-up: write row height-1 first, row 0 last)
                for (int y = height - 1; y >= 0; y--)
                {
                    bw.Write(bgr32, y * stride, stride);
                }
            }
        }
    }
}
