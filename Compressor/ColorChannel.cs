using System;
using System.Drawing;

namespace Compressor
{
    public struct YCbCr
    {
        public double Y, Cb, Cr;
    }

    class ColorChannel
    {
        public static YCbCr RgbToYcbcr(Color rgb)
        {
            YCbCr ycbcr;
            double R = rgb.R, G = rgb.G, B = rgb.B;

            ycbcr.Y = 0.299 * R + 0.587 * G + 0.114 * B;
            ycbcr.Cb = 128 - 0.168736 * R - 0.331264 * G + 0.5 * B;
            ycbcr.Cr = 128 + 0.5 * R - 0.418688 * G - 0.081312 * B;

            return ycbcr;
        }

        public static Color YcbcrToRgb(YCbCr ycbcr)
        {
            double Y = ycbcr.Y, Cb = ycbcr.Cb, Cr = ycbcr.Cr;

            byte r = (byte)Math.Max(0, Math.Min(255, (Y + 1.402 * (Cr - 128))));
            byte g = (byte)Math.Max(0, Math.Min(255, (Y - 0.344136 * (Cb - 128) - 0.714136 * (Cr - 128))));
            byte b = (byte)Math.Max(0, Math.Min(255, (Y + 1.772 * (Cb - 128))));

            return Color.FromArgb(r, g, b);
        }

        public static YCbCr[,] ReadYCbCrs(Bitmap image)
        {
            int w = image.Width, h = image.Height;

            // convert rgb array to ycbcr
            var ycbcrs = new YCbCr[h, w];

            for (int row = 0; row < h; ++row)
            {
                for (int col = 0; col < w; ++col)
                {
                    ycbcrs[row, col] = ColorChannel.RgbToYcbcr(image.GetPixel(col, row));
                }
            }
            return ycbcrs;
        }

        public static Color[,] ReadRGBs(Bitmap image)
        {
            int w = image.Width, h = image.Height;

            Color[,] rgbs = new Color[h, w];

            for (int row = 0; row < h; ++row)
            {
                for (int col = 0; col < w; ++col)
                {
                    rgbs[row, col] = image.GetPixel(col, row);
                }
            }
            return rgbs;
        }
    }

}
