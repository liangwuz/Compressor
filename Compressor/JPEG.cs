using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace Compressor
{
    public class JPEG
    {
        // test data for dct and idct
        static readonly byte[,] TestData =
        {
            { 62 , 55 , 55 , 54 , 49 , 48 , 47 , 55  },
            { 62 , 57 , 54 , 52 , 48 , 47 , 48 , 53  },
            { 61 , 60 , 52 , 49 , 48 , 47 , 49 , 54  },
            { 63 , 61 , 60 , 60 , 63 , 65 , 68 , 65  },
            { 67 , 67 , 70 , 74 , 79 , 85 , 91 , 92  },
            { 82 , 95 , 101, 106, 114, 115, 112, 117 },
            { 96 , 111, 115, 119, 128, 128, 130, 127 },
            { 109, 121, 127, 133, 139, 141, 140, 133 }
        };

        public static readonly byte[,] LuminanceQuantizationTable =
        {
            { 16, 11, 10, 16, 24,  40,  51,  61 },
            { 14, 13, 16, 24, 40,  57,  69,  56 },
            { 14, 17, 22, 29, 51,  87,  80,  62 },
            { 18, 22, 37, 56, 68,  109, 103, 77 },
            { 12, 12, 14, 19, 26,  58,  60,  55 },
            { 24, 35, 55, 64, 81,  104, 113, 92 },
            { 49, 64, 78, 87, 103, 121, 120, 101},
            { 72, 92, 95, 98, 112, 100, 103, 99 }
        };

        public static readonly byte[,] ChrominanceQuantizationTable =
        {
            { 17, 18, 24, 47, 99, 99, 99, 99 },
            { 18, 21, 26, 66, 99, 99, 99, 99 },
            { 24, 26, 56, 99, 99, 99, 99, 99 },
            { 47, 66, 99, 99, 99, 99, 99, 99 },
            { 99, 99, 99, 99, 99, 99, 99, 99 },
            { 99, 99, 99, 99, 99, 99, 99, 99 },
            { 99, 99, 99, 99, 99, 99, 99, 99 },
            { 99, 99, 99, 99, 99, 99, 99, 99 }
        };

        private static double a(int z)
        {
            return (z == 0) ? 0.70710678118654752440084436210485 : 1;
        }


        // y, cb, cr channels. each channel is a two dimension array
        public struct YCbCrSubSamples
        {
            public double[,] Y, Cb, Cr;
        }

        // sub sampling Cb and Cr channel
        public static YCbCrSubSamples SubSample(YCbCr[,] yCbCrs)
        {
            YCbCrSubSamples chans;
            int h = yCbCrs.GetLength(0), w = yCbCrs.GetLength(1);

            chans.Y = new double[h, w];
            // cb cr channels subsample to 1/4
            int subH = (h >> 1) + (h & 1), subW = (w >> 1) + (w & 1);
            chans.Cb = new double[subH, subW];
            chans.Cr = new double[subH, subW];

            for (int row = 0; row < h; ++row)
            {
                for (int col = 0; col < w; ++col)
                {
                    chans.Y[row, col] = yCbCrs[row, col].Y;
                    // sample the top left corner number of a quare
                    if (((row | col) & 1) == 0)
                    {
                        int subR = row >> 1, subC = col >> 1;
                        chans.Cb[subR, subC] = yCbCrs[row, col].Cb;
                        chans.Cr[subR, subC] = yCbCrs[row, col].Cr;
                    }
                }
            }
            return chans;
        }

        // duplicate cb cr channels to reproduce ycbcr pixels
        public static YCbCr[,] UpSample(YCbCrSubSamples chans)
        {
            int h = chans.Y.GetLength(0), w = chans.Y.GetLength(1);
            YCbCr[,] yCbCrs = new YCbCr[h, w];

            for (int row = 0; row < h; ++row)
            {
                for (int col = 0; col < w; ++col)
                {
                    yCbCrs[row, col].Y = chans.Y[row, col];
                    // fill up the square with top left number
                    int subR = row >> 1, subC = col >> 1;
                    yCbCrs[row, col].Cb = chans.Cb[subR, subC];
                    yCbCrs[row, col].Cr = chans.Cr[subR, subC];
                }
            }
            return yCbCrs;
        }

        // dct dimension
        const byte N = 8;

        // YCbCr DCT
        public static double[,] DCT8(double[,] f)
        {
            double[,] F = new double[N,N];

            for (int u = 0; u < N; ++u)
            {
                for (int v = 0; v < N; ++v)
                {
                    for (int i = 0; i < N; ++i)
                    {
                        for (int j = 0; j < N; ++j)
                        {
                            F[u,v] += Math.Cos(Math.PI * u * (i + 0.5) / N) * Math.Cos(Math.PI * v * (j + 0.5) / N) * f[i,j];
                        }
                    }
                    F[u,v] *= a(u) * a(v) * 0.25;
                }
            }
            return F;
        }

        // IDCT to YCbCr
        public static double[,] IDCT8(double[,] F)
        {
            var f = new double[N, N];

            for (int i = 0; i < N; ++i)
            {
                for (int j = 0; j < N; ++j)
                {
                    for (int u = 0; u < N; ++u)
                        for (int v = 0; v < N; ++v)
                            f[i, j] += a(u) * a(v) * 0.25 * F[u, v]
                                * Math.Cos(Math.PI * u * (i + 0.5) / N) * Math.Cos(Math.PI * v * (j + 0.5) / N);
                }
            }
            return f;
        }

        //8*8, after runing dct, divide the number by the quantized table
        public static sbyte[,] DCTSubQuantized8(double[,] f, byte[,] table)
        {
            sbyte[,] F = new sbyte[N, N];

            for (int u = 0; u < N; ++u)
            {
                for (int v = 0; v < N; ++v)
                {
                    double sum = 0;

                    for (int i = 0; i < N; ++i)
                    {
                        for (int j = 0; j < N; ++j)
                        {
                            sum += Math.Cos(Math.PI * u * (i + 0.5) / N) * Math.Cos(Math.PI * v * (j + 0.5) / N) * f[i, j];
                        }
                    }
                    F[u, v] = (sbyte)Math.Min(127, Math.Max(-128, Math.Round(sum * a(u) * a(v) * 0.25 / table[u, v])));
                }
            }
            return F;
        }

        // return 8*8 byte block times quantized table
        private static int[,] UpQuantized(sbyte[,] f, byte[,] table)
        {

            int[,] F = new int[N, N];

            for (int i = 0; i < N; ++i)
            {
                for (int j = 0; j < N; ++j)
                {
                    F[i, j] = table[i, j] * f[i, j];
                }
            }
            return F;
        }

        // F should be 8*8 block, after runing idct, multiple the number by the quantized table
        public static double[,] UpQuantizedIDCT8(sbyte[,] F, byte[,] table)
        {
            var f = new double[N, N];
            int[,] quantized = UpQuantized(F, table);

            for (int i = 0; i < N; ++i)
            {
                for (int j = 0; j < N; ++j)
                {
                    for (int u = 0; u < N; ++u)
                        for (int v = 0; v < N; ++v)
                            f[i, j] += a(u) * a(v) * 0.25 * quantized[u, v]
                                * Math.Cos(Math.PI * u * (i + 0.5) / N) * Math.Cos(Math.PI * v * (j + 0.5) / N);
                }
            }
            return f;
        }

        public static sbyte[,] DCTSubQuantized(double[,] yCbCrChannel, byte[,] quantizedTable)
        {
            int originHeight = yCbCrChannel.GetLength(0), originWidth = yCbCrChannel.GetLength(1);
            // dimension with paddings if origin image is not multiples of 8
            int h = (int)Math.Ceiling((double)originHeight / N) << 3;
            int w = (int)Math.Ceiling((double)originWidth / N) << 3;
            var result = new sbyte[h, w];

            // dec to prevent -1 calculation inside the loop
            --originHeight; --originWidth;

            // divide into 8*8, if the dimension is not divisible by 8, stuff the rest
            for (int row = 0; row <= originHeight; row += N)
            {
                for (int col = 0; col <= originWidth; col += N)
                {
                    var block8 = new double[N, N];
                    // 8*8 block filling
                    for (int _y = 0, Y = row; _y < N; ++_y)
                    {
                        for (int _x = 0, X = col; _x < N; ++_x)
                        {
                            block8[_y, _x] = yCbCrChannel[Y, X];
                            if (X < originWidth) // padding number use the boundary number
                                ++X;
                        }
                        if (Y < originHeight)
                            ++Y;
                    }
                    // DCT on the block and put zigzag transform
                    sbyte[,] quantizedBlock8 = DCTSubQuantized8(block8, quantizedTable);

                    // copy to the result array
                    for (int _y = 0, Y = row; _y < N; ++_y, ++Y)
                    {
                        for (int _x = 0, X = col; _x < N; ++_x, ++X)
                        {
                            result[Y, X] = quantizedBlock8[_y, _x];
                        }
                    }
                }
            }
            return result;
        }

        // sub sampled channel origin height and width
        public static double[,] UpQuantizedIDCT(int height, int width, sbyte[,] subQuantizedData, byte[,] quantizedTable)
        {
            var channel = new double[height, width];

            for (int row = 0; row < height; row += N)
            {
                for (int col = 0; col < width; col += N)
                {
                    var block8 = new sbyte[N, N];
                    // 8*8 block filling
                    for (int _y = 0, Y = row; _y < N; ++_y, ++Y)
                    {
                        for (int _x = 0, X = col; _x < N; ++_x, ++X)
                        {
                            block8[_y, _x] = subQuantizedData[Y, X];
                        }
                    }

                    // quantized IDCT
                    double[,] channelBlock8 = UpQuantizedIDCT8(block8, quantizedTable);

                    // copy value
                    for (int _y = 0, Y = row; _y < N && Y < height; ++_y, ++Y)
                    {
                        for (int _x = 0, X = col; _x < N && X < width; ++_x, ++X)
                        {
                            channel[Y, X] = channelBlock8[_y, _x];
                        }
                    }
                }
            }
            return channel;
        }

        private static byte[] ZigZagRunLengthEncode(sbyte[,] data)
        {
            return RunLengthEncode(ZigZagTransform(data));
        }

        //// height and width of the padded array
        //public static sbyte[,] RunLengthDecodeZigZag(int height, int width, byte[] data)
        //{
        //    return ZigZagTransform(height, width, RunLengthDecode(data));
        //}

        // the dimension of data is multiple of 8
        public static sbyte[] ZigZagTransform(sbyte[,] data)
        {
            int h = data.GetLength(0), w = data.GetLength(1);
            var stream = new sbyte[h * w];
            int streamIndex = 0;

            for (int row = 0; row < h; row += N)
            {
                for (int col = 0; col < w; col += N)
                {
                    var block8 = new sbyte[N, N];
                    // 8*8 block filling
                    for (int _y = 0, Y = row; _y < N; ++_y, ++Y)
                    {
                        for (int _x = 0, X = col; _x < N; ++_x, ++X)
                        {
                            block8[_y, _x] = data[Y, X];
                        }
                    }

                    sbyte[] zigzagStream8 = ZigZagTransform8(block8);

                    // append all return blocks sequentially
                    for (int i = 0; i < 64; ++i)
                    {
                        stream[streamIndex++] = zigzagStream8[i];
                    }
                }
            }
            return stream;
        }

        private static sbyte[,] ZigZagTransform(int height, int width, sbyte[] stream)
        {
            var result = new sbyte[height, width];
            int streamIndex = 0;

            for (int row = 0; row < height; row += N)
            {
                for (int col = 0; col < width; col += N)
                {
                    // get 64 bytes from the stream, run zigzag transform
                    var zigzagStream = new sbyte[64];
                    for (int i = 0; i < 64; ++i)
                    {
                        zigzagStream[i] = stream[streamIndex++];
                    }
                    sbyte[,] block8 = ZigZagTransform8(zigzagStream);

                    for (int _y = 0, Y = row; _y < N; ++_y, ++Y)
                    {
                        for (int _x = 0, X = col; _x < N; ++_x, ++X)
                        {
                            result[Y, X] = block8[_y, _x];
                        }
                    }
                }
            }
            return result;
        }

        // 8*8 block 
        public static sbyte[] ZigZagTransform8(sbyte[,] block)
        {
            sbyte[] stream = new sbyte[64];
            int i = 0, x = 0, y = 0;
            bool isDown = true;

            stream[i++] = block[y, x++]; // take (0,0) move to (0,1)

            // top left corner from (0,0) to (7,0)
            while (y < 7)
            {
                if (isDown)
                {
                    stream[i++] = block[y++, x--];
                    if (x == 0) // hit the left, move downward
                    {
                        stream[i++] = block[y++, x];
                        isDown = false;
                    }
                }
                else
                {
                    stream[i++] = block[y--, x++];
                    if (y == 0) // hit the top, move right
                    {
                        stream[i++] = block[y, x++];
                        isDown = true;
                    }
                }
            }
            // after terminate, y = 8, x = 0;
            y = 7; x = 1;

            // bottom right
            while (x < N)
            {
                if (isDown) 
                {
                    stream[i++] = block[y++, x--];
                    if (y == 7) // move right and upward
                    {
                        stream[i++] = block[y, x++];
                        isDown = false;
                    }
                }
                else
                {
                    stream[i++] = block[y--, x++];
                    if (x == 7) // move down and downward
                    {
                        stream[i++] = block[y++, x];
                        isDown = true;
                    }
                }
            }
            return stream;
        }

        private static sbyte[,] ZigZagTransform8(sbyte[] stream)
        {
            var block = new sbyte[N, N];
            int i = 0, x = 0, y = 0;
            bool isDown = true;

            block[y, x++] = stream[i++]; // take (0,0) move to (0,1)

            // top left corner from (0,0) to (7,0)
            while (y < 7)
            {
                if (isDown)
                {
                    block[y++, x--] = stream[i++];
                    if (x == 0) // hit the left, move downward
                    {
                        block[y++, x] = stream[i++];
                        isDown = false;
                    }
                }
                else
                {
                    block[y--, x++] = stream[i++];
                    if (y == 0) // hit the top, move right
                    {
                        block[y, x++] = stream[i++];
                        isDown = true;
                    }
                }
            }
            // after terminate, y = 8, x = 0;
            y = 7; x = 1;

            // bottom right
            while (x < N)
            {
                if (isDown)
                {
                    block[y++, x--] = stream[i++];
                    if (y == 7) // move right and upward
                    {
                        block[y, x++] = stream[i++];
                        isDown = false;
                    }
                }
                else
                {
                    block[y--, x++] = stream[i++];
                    if (x == 7) // move down and downward
                    {
                        block[y++, x] = stream[i++];
                        isDown = true;
                    }
                }
            }
            return block;
        }




        // sbyte to byte for file writing
        public static byte[] RunLengthEncode(sbyte[] sbytes)
        {
            var bytes = new List<byte>(sbytes.Length >> 2);

            for (int i = 0; i < sbytes.Length;)
            {
                if (sbytes[i] != 0)
                {
                    bytes.Add((byte)sbytes[i++]);
                }
                else
                {
                    byte count = 0;
                    while (i < sbytes.Length && count < 255 && sbytes[i] == 0)
                    {
                        ++count;
                        i++;
                    }
                    bytes.Add(0);
                    bytes.Add(count);
                }
            }
            return bytes.ToArray();
        }

        public static sbyte[] RunLengthDecode(byte[] bytes)
        {
            var sbytes = new List<sbyte>(bytes.Length << 3);

            for (int i = 0; i < bytes.Length;)
            {
                if (bytes[i] != 0)
                {
                    sbytes.Add((sbyte)bytes[i++]);
                }
                else
                {
                    byte numOfZeros = bytes[++i]; // number after 0 is the run.
                    ++i; // increase to next number
                    while (numOfZeros-- > 0)
                        sbytes.Add(0);
                }
            }
            return sbytes.ToArray();
        }

        public static byte[] Compress(Bitmap image)
        {
            // 1. get YCbCr channels of the image
            YCbCr[,] yCbCrs = ColorChannel.ReadYCbCrs(image);

            // 2. sub sample
            YCbCrSubSamples yCbCrSubSamples = SubSample(yCbCrs);

            // for each channel, run a sperate thread for DCT, quantized, zigzag sampling, and RLE
            var tasks = new Task[3];
            sbyte[] yStream = null, cbStream = null, crStream = null;

            tasks[0] = Task.Factory.StartNew(() =>
            {
                //3,4. DCT sub quantized
                sbyte[,] s = DCTSubQuantized(yCbCrSubSamples.Y, LuminanceQuantizationTable);
                yStream = ZigZagTransform(s);
            });
            tasks[1] = Task.Factory.StartNew(() =>
            {
                sbyte[,] s = DCTSubQuantized(yCbCrSubSamples.Cb, ChrominanceQuantizationTable);
                cbStream = ZigZagTransform(s);
            });
            tasks[2] = Task.Factory.StartNew(() =>
            {
                sbyte[,] s = DCTSubQuantized(yCbCrSubSamples.Cr, ChrominanceQuantizationTable);
                crStream = ZigZagTransform(s);
            });
            Task.WaitAll(tasks);

            // concatennate byte stream
            var data = new sbyte[yStream.Length + cbStream.Length + crStream.Length];
            yStream.CopyTo(data, 0);
            cbStream.CopyTo(data, yStream.Length);
            crStream.CopyTo(data, yStream.Length + cbStream.Length);

            return RunLengthEncode(data);
        }

        public static Bitmap Decompress(int height, int width, byte[] compressedData)
        {
            YCbCrSubSamples yCbCrSubSamples = DecodeImgData(height, width, compressedData);

            // up sample
            YCbCr[,] yCbCrs = UpSample(yCbCrSubSamples);

            // change to RGB and write to bitmap
            int h = yCbCrs.GetLength(0), w = yCbCrs.GetLength(1);
            Bitmap img = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            for (int row = 0; row < h; ++row)
            {
                for (int col = 0; col < w; ++col)
                {
                    img.SetPixel(col, row, ColorChannel.YcbcrToRgb(yCbCrs[row, col]));
                }
            }
            return img;
        }

        public static YCbCrSubSamples DecodeImgData(int height, int width, byte[] data)
        {
            sbyte[] decoded = RunLengthDecode(data); // data including paddings

            return DecodeImgData(height, width, decoded);
        }

        public static YCbCrSubSamples DecodeImgData(int height, int width, sbyte[] decoded)
        {

            // y channel dimension after 8*8 bloks padding
            int yPaddedH = (int)Math.Ceiling((double)height / 8) << 3;
            int yPaddedW = (int)Math.Ceiling((double)width / 8) << 3;

            // origin cb, cr chnnel dimension
            int ch = (height >> 1) + (height & 1), cw = (width >> 1) + (width & 1);
            // cb cr after padding
            int cPaddedH = (int)Math.Ceiling((double)ch / 8) << 3;
            int cPaddedW = (int)Math.Ceiling((double)cw / 8) << 3;

            // based on image height and width to get the portion of each channel in decoded image.data
            sbyte[] yStream = new sbyte[yPaddedH * yPaddedW];
            Array.Copy(decoded, 0, yStream, 0, yStream.Length);

            sbyte[] cbStream = new sbyte[cPaddedH * cPaddedW];
            Array.Copy(decoded, yStream.Length, cbStream, 0, cbStream.Length);

            sbyte[] crStream = new sbyte[cPaddedH * cPaddedW];
            Array.Copy(decoded, yStream.Length + cbStream.Length, crStream, 0, crStream.Length);

            YCbCrSubSamples channels;
            channels.Y = null; channels.Cb = null; channels.Cr = null;

            // for each sbyte array channel do: zigzag, quantized, IDCT to get two dimension array
            var tasks = new Task[3];

            tasks[0] = Task.Factory.StartNew(() =>
            {
                sbyte[,] quantizedData = ZigZagTransform(yPaddedH, yPaddedW, yStream);
                channels.Y = UpQuantizedIDCT(height, width, quantizedData, LuminanceQuantizationTable);
            });
            tasks[1] = Task.Factory.StartNew(() =>
            {
                sbyte[,] quantizedData = ZigZagTransform(cPaddedH, cPaddedW, cbStream);
                channels.Cb = UpQuantizedIDCT(ch, cw, quantizedData, ChrominanceQuantizationTable);
            });
            tasks[2] = Task.Factory.StartNew(() =>
            {
                sbyte[,] quantizedData = ZigZagTransform(cPaddedH, cPaddedW, crStream);
                channels.Cr = UpQuantizedIDCT(ch, cw, quantizedData, ChrominanceQuantizationTable);
            });
            Task.WaitAll(tasks);

            return channels;
        }
    }
}
