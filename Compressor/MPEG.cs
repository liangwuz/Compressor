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

        private int height;
        private int width;

        public MPEG(int height, int width)
        {
            this.height = height;
            this.width = width;
        }

        private YCbCrSubSamples frameMemory;

        private readonly int N = 8;

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
            YCbCrSubSamples yCbCrSubSamples = JPEG.SubSample(yCbCrs);

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
            YCbCrSubSamples yCbCrSubSamples = JPEG.SubSample(yCbCrs);

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

            // upquantized IDCT, add to frame memory
            //int yHeight = yCbCrSubSamples.Y.GetLength(0), yWidth = yCbCrSubSamples.Y.GetLength(1), cHeight = yCbCrSubSamples.Cb.GetLength(0), cWidth = yCbCrSubSamples.Cb.GetLength(1);
            //tasks[0] = Task.Factory.StartNew(() =>
            //{
            //    frameMemory.Y = UpQuantizedIDCT(yHeight, yWidth, yQuantized, LuminanceQuantizationTable);
            //});
            //tasks[1] = Task.Factory.StartNew(() =>
            //{
            //    frameMemory.Cb = UpQuantizedIDCT(cHeight, cWidth, cbQuantized, ChrominanceQuantizationTable);
            //});
            //tasks[2] = Task.Factory.StartNew(() =>
            //{
            //    frameMemory.Cr = UpQuantizedIDCT(cHeight, cWidth, crQuantized, ChrominanceQuantizationTable);
            //});

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

            //Task.WaitAll(tasks);

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
            frameMemory = DecodeImgData(height, width, bs);

            // up sample
            YCbCr[,] yCbCrs = UpSample(frameMemory);

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

        private Bitmap DecompressPFrame(byte[] bs)
        {
            int decodeIndex = 0;
            sbyte[] decoded = RunLengthDecode(bs); // data including paddings

            int height = frameMemory.Y.GetLength(0);
            int width = frameMemory.Y.GetLength(1);

            int h = (int)Math.Ceiling((double)height / N);
            int w = (int)Math.Ceiling((double)width / N);

            // read in vectors
            var vectors = new Vector[h*w];

            for (int i = 0; i < vectors.Length; ++i)
            {
                vectors[i].x = decoded[decodeIndex++];
                vectors[i].y = decoded[decodeIndex++];
            }

            var data = new sbyte[decoded.Length - decodeIndex];
            Array.Copy(decoded, decodeIndex, data, 0, data.Length);

            YCbCrSubSamples diffs = DecodeImgData(height, width, data);

            int vectorIndex = 0;
            --width; --height;

            for (int row = 0; row <= height; row += N)
            {
                for (int col = 0; col <= width; col += N)
                {
                    Vector v = vectors[vectorIndex++];

                    for (int Y = row, YB = row + N; Y < YB && Y <= height; ++Y)
                    {
                        for (int X = col, XB = col + N; X < XB && X <= width; ++X)
                        {
                            int ry = Math.Max(0, Math.Min(Y - v.y, height));
                            int rx = Math.Max(0, Math.Min(X - v.x, width));
                            diffs.Y[Y, X] += frameMemory.Y[ry, rx];
                        }
                    }

                    int ch = height >> 1, cw = width >> 1;
                    for (int Y = (row >> 1), YB = Y + (N >> 1); Y < YB && Y <= ch; ++Y)
                    {
                        for (int X = (col >> 1), XB = X + (N >> 1); X < XB && X <= cw; ++X)
                        {
                            int ry = Math.Max(0, Math.Min(Y - v.y, ch));
                            int rx = Math.Max(0, Math.Min(X - v.x, cw));
                            diffs.Cb[Y, X] += frameMemory.Cb[ry, rx];
                            diffs.Cr[Y, X] += frameMemory.Cr[ry, rx];
                        }
                    }

                }
            }

            frameMemory = diffs;

            YCbCr[,] yCbCrs = UpSample(diffs);

            // change to RGB and write to bitmap
            Bitmap img = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            for (int row = 0; row < height; ++row)
            {
                for (int col = 0; col < width; ++col)
                {
                    img.SetPixel(col, row, ColorChannel.YcbcrToRgb(yCbCrs[row, col]));
                }
            }
            return img;

        }
    }
}
