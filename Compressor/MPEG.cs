using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Compressor.JPEG;
using static Compressor.MotionVector;

namespace Compressor
{
    class MPEG
    {
        public enum FrameType
        {
            I, P
        }

        private readonly int FrameHeight;
        private readonly int FrameWidth;

        public MPEG(int height, int width)
        {
            FrameHeight = height;
            FrameWidth = width;
        }

        private YCbCrSubSample frameMemory;

        const int N = 8;

        public byte[] Compress(Bitmap frame, FrameType type)
        {
            switch (type)
            {
                case FrameType.I:
                    return CompressIframe(frame);
                case FrameType.P:
                    return CompressPframe(frame);
                default:
                    throw new Exception("No implementation");
            }
        }

        private byte[] CompressIframe(Bitmap bitmap)
        {
            YCbCr[,] yCbCrs = ColorChannel.ReadYCbCrs(bitmap);
            YCbCrSubSample yCbCrSubSamples = JPEG.SubSample(yCbCrs);

            //DCT subquantized
            // for each channel, run a sperate thread for DCT, quantized, zigzag sampling, and RLE
            var tasks = new Task[3];
            sbyte[,] yQuantized = null, cbQuantized = null, crQuantized = null;

            tasks[0] = Task.Factory.StartNew(() =>
            {
                yQuantized = DCTSubQuantized(yCbCrSubSamples.Y, LuminanceQuantizationTable);
            });
            tasks[1] = Task.Factory.StartNew(() =>
            {
                cbQuantized = DCTSubQuantized(yCbCrSubSamples.Cb, ChrominanceQuantizationTable);
            });
            tasks[2] = Task.Factory.StartNew(() =>
            {
                crQuantized = DCTSubQuantized(yCbCrSubSamples.Cr, ChrominanceQuantizationTable);
            });
            Task.WaitAll(tasks);

            // upquantized IDCT, store as frame memory
            int yHeight = yCbCrSubSamples.Y.GetLength(0), yWidth = yCbCrSubSamples.Y.GetLength(1), cHeight = yCbCrSubSamples.Cb.GetLength(0), cWidth = yCbCrSubSamples.Cb.GetLength(1);
            tasks[0] = Task.Factory.StartNew(() =>
            {
                frameMemory.Y = UpQuantizedIDCT(yHeight, yWidth, yQuantized, LuminanceQuantizationTable);
            });
            tasks[1] = Task.Factory.StartNew(() =>
            {
                frameMemory.Cb = UpQuantizedIDCT(cHeight, cWidth, cbQuantized, ChrominanceQuantizationTable);
            });
            tasks[2] = Task.Factory.StartNew(() =>
            {
                frameMemory.Cr = UpQuantizedIDCT(cHeight, cWidth, crQuantized, ChrominanceQuantizationTable);
            });

            // zigzag RLE
            sbyte[] yStream = ZigZagTransform(yQuantized);
            sbyte[] cbStream = ZigZagTransform(cbQuantized);
            sbyte[] crStream = ZigZagTransform(crQuantized);

            // concatennate byte stream
            var data = new sbyte[yStream.Length + cbStream.Length + crStream.Length];
            yStream.CopyTo(data, 0);
            cbStream.CopyTo(data, yStream.Length);
            crStream.CopyTo(data, yStream.Length + cbStream.Length);

            Task.WaitAll(tasks);

            return RunLengthEncode(data);
        }

        private byte[] CompressPframe(Bitmap bitmap)
        {
            YCbCr[,] yCbCrs = ColorChannel.ReadYCbCrs(bitmap);
            YCbCrSubSample yCbCrSubSamples = JPEG.SubSample(yCbCrs);

            // motion vecotr estimate
            var mv = new MotionVector(15);
            EstimateResult vecotrDiffs = mv.Estimate(frameMemory, yCbCrSubSamples);

            // dct subquantized differences
            var tasks = new Task[3];
            sbyte[,] yQuantized = null, cbQuantized = null, crQuantized = null;

            tasks[0] = Task.Factory.StartNew(() =>
            {
                yQuantized = DCTSubQuantized(vecotrDiffs.YDiffs, LuminanceQuantizationTable);
            });
            tasks[1] = Task.Factory.StartNew(() =>
            {
                cbQuantized = DCTSubQuantized(vecotrDiffs.CbDiffs, ChrominanceQuantizationTable);
            });
            tasks[2] = Task.Factory.StartNew(() =>
            {
                crQuantized = DCTSubQuantized(vecotrDiffs.CrDiffs, ChrominanceQuantizationTable);
            });
            Task.WaitAll(tasks);

            //upquantized IDCT, add to frame memory
            int yHeight = yCbCrSubSamples.Y.GetLength(0), yWidth = yCbCrSubSamples.Y.GetLength(1), cHeight = yCbCrSubSamples.Cb.GetLength(0), cWidth = yCbCrSubSamples.Cb.GetLength(1);
            tasks[0] = Task.Factory.StartNew(() =>
            {
                yCbCrSubSamples.Y = UpQuantizedIDCT(yHeight, yWidth, yQuantized, LuminanceQuantizationTable);
            });
            tasks[1] = Task.Factory.StartNew(() =>
            {
                yCbCrSubSamples.Cb = UpQuantizedIDCT(cHeight, cWidth, cbQuantized, ChrominanceQuantizationTable);
            });
            tasks[2] = Task.Factory.StartNew(() =>
            {
                yCbCrSubSamples.Cr = UpQuantizedIDCT(cHeight, cWidth, crQuantized, ChrominanceQuantizationTable);
            });

            // zigzag RLE differences
            sbyte[] yStream = ZigZagTransform(yQuantized);
            sbyte[] cbStream = ZigZagTransform(cbQuantized);
            sbyte[] crStream = ZigZagTransform(crQuantized);

            // concatennate byte stream
            var data = new sbyte[vecotrDiffs.Vectors.Length * 2 + yStream.Length + cbStream.Length + crStream.Length];
            for (int i = 0, j = 0; i < vecotrDiffs.Vectors.Length; ++i)
            {
                data[j++] = (sbyte)(vecotrDiffs.Vectors[i].x);
                data[j++] = (sbyte)(vecotrDiffs.Vectors[i].y);
            }

            yStream.CopyTo(data, vecotrDiffs.Vectors.Length * 2);
            cbStream.CopyTo(data, yStream.Length + vecotrDiffs.Vectors.Length * 2);
            crStream.CopyTo(data, yStream.Length + cbStream.Length + vecotrDiffs.Vectors.Length * 2);

            Task.WaitAll(tasks);
            AddDifferencesToMemoryFrame(yCbCrSubSamples, vecotrDiffs.Vectors); // update frame memory
            return RunLengthEncode(data);
        }

        public Bitmap Decompress(byte[] bs, FrameType type)
        {
            switch (type)
            {
                case FrameType.I:
                    return DecompressIFrame(bs);
                case FrameType.P:
                    return DecompressPFrame(bs);
                default:
                    throw new Exception("No implementation");
            }
        }

        private Bitmap DecompressIFrame(byte[] bs)
        {
            frameMemory = DecodeCompressedBytes(FrameHeight, FrameWidth, bs);
            YCbCr[,] yCbCrs = UpSample(frameMemory);
            return ColorChannel.CreateBitmapFromYCbCr(yCbCrs);
        }

        private Bitmap DecompressPFrame(byte[] bs)
        {
            int index = 0; // sbytes index
            sbyte[] decoded = RunLengthDecode(bs); // data including paddings

            // read in vectors
            var vectors = new Vector[(int)Math.Ceiling((double)FrameHeight / N) * (int)Math.Ceiling((double)FrameWidth / N)];
            for (int i = 0; i < vectors.Length; ++i)
            {
                vectors[i].x = decoded[index++];
                vectors[i].y = decoded[index++];
            }

            // ycbcr differences
            var data = new sbyte[decoded.Length - index];
            Array.Copy(decoded, index, data, 0, data.Length);

            YCbCrSubSample diffs = SeparateYCbCrFromSbytes(FrameHeight, FrameWidth, data); // pframe diffs
            AddDifferencesToMemoryFrame(diffs, vectors);

            YCbCr[,] yCbCrs = UpSample(frameMemory);
            return ColorChannel.CreateBitmapFromYCbCr(yCbCrs);
        }

        private void AddDifferencesToMemoryFrame(YCbCrSubSample diffs, Vector[] vectors)
        {
            int vectorIndex = 0;

            int yHeight = FrameHeight - 1;
            int yWidth = FrameWidth - 1;
            int cHeight = yHeight >> 1, cWidth = yWidth >> 1; // cb cr

            for (int row = 0; row <= yHeight; row += N)
            {
                for (int col = 0; col <= yWidth; col += N)
                {
                    Vector v = vectors[vectorIndex++];

                    for (int Y = row, yBound = row + N; Y < yBound && Y <= yHeight; ++Y)
                    {
                        for (int X = col, xBound = col + N; X < xBound && X <= yWidth; ++X)
                        {
                            int ry = Math.Max(0, Math.Min(Y - v.y, yHeight));
                            int rx = Math.Max(0, Math.Min(X - v.x, yWidth));
                            diffs.Y[Y, X] += frameMemory.Y[ry, rx];
                        }
                    }

                    for (int Y = (row >> 1), yBound = Y + (N >> 1); Y < yBound && Y <= cHeight; ++Y)
                    {
                        for (int X = (col >> 1), xBound = X + (N >> 1); X < xBound && X <= cWidth; ++X)
                        {
                            int ry = Math.Max(0, Math.Min(Y - v.y, cHeight));
                            int rx = Math.Max(0, Math.Min(X - v.x, cWidth));
                            diffs.Cb[Y, X] += frameMemory.Cb[ry, rx];
                            diffs.Cr[Y, X] += frameMemory.Cr[ry, rx];
                        }
                    }

                }
            }

            frameMemory = diffs;

        }


    }

}
