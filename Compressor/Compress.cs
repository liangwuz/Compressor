using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Compressor
{
    public class Compress
    {
        public readonly struct CompressResult
        {
            public readonly int OriginSize;
            public readonly int CompressedSize;
            private readonly double CompressedRatio;

            public CompressResult(int originSzie, int compressedSize)
            {
                OriginSize = originSzie;
                CompressedSize = compressedSize;
                CompressedRatio = (double)CompressedSize / OriginSize;
            }

            public override string ToString()
            {
                return "Origin file size: " + OriginSize.ToString("N0")
                    + "B    Compressed file size: " + CompressedSize.ToString("N0")
                    + "B    Compression: " + ((double)OriginSize / CompressedSize).ToString("#.##") + " Times";
            }
        }

        // compress the jpeg image at the specified path to the output file path
        public static CompressResult CompressImage(String inFile, String outFile)
        {
            var bitmap = new Bitmap(inFile);

            byte[] data = JPEG.Compress(bitmap);

            var image = new ImageData(bitmap.Height, bitmap.Width, data);
            WriteCompressImage(outFile, image);

            return new CompressResult(image.Height * image.Width * 3, data.Length);
        }

        // decompress the compressed file back to image
        public static Bitmap DecompressImage(String inFile)
        {
            // read in compressed file
            ImageData file = ReadCompressImage(inFile);

            return JPEG.Decompress(file.Height, file.Width, file.Data);
        }

        //public static CompressResult CompressIPframes(string iFreamFile, string pFrameFile, string outFile)
        //{
        //    var i = new Bitmap(iFreamFile);
        //    var p = new Bitmap(pFrameFile);

        //    var mpeg = new MPEG(i.Height, i.Width);
        //    byte[][] ipframes = new byte[2][];
        //    ipframes[0] = mpeg.Compress(i, MPEG.FrameType.I);
        //    ipframes[1] = mpeg.Compress(p, MPEG.FrameType.P);

        //    // store to outFile
        //    WriteCompressIPframes(outFile, new ImagesData(i.Height, i.Width, 1, ipframes));

        //    return new CompressResult(i.Height * i.Width * 6, ipframes[0].Length + ipframes[1].Length);
        //}

        //public static Bitmap[] DecompressIPframes(string inFile)
        //{
        //    ImagesData data = ReadCompressIPframes(inFile);

        //    var mpeg = new MPEG(data.Height, data.Width);

        //    var bms = new Bitmap[2];

        //    bms[0] = mpeg.Decompress(data.EncodedFrames[0], MPEG.FrameType.I);
        //    bms[1] = mpeg.Decompress(data.EncodedFrames[1], MPEG.FrameType.P);

        //    return bms;
        //}

        public static CompressResult CompressIPFrames(String[] framePaths, int iFrameGaps, String outPath)
        {
            if (framePaths == null || framePaths.Length < 1)
            {
                throw new Exception("must at least have 1 frame");
            }

            int frameCount = framePaths.Length;

            Bitmap[] frames = new Bitmap[frameCount];

            for (int i = 0; i < frameCount; ++i)
            {
                frames[i] = new Bitmap(framePaths[i]);
            }

            byte[][] encodedFrames = new byte[frameCount][];
            int height = frames[0].Height, width = frames[0].Width;
            var mpeg = new MPEG(height, width);

            int compressSize = 0;

            for (int i = 0; i < frameCount; ++i)
            {
                if (i % iFrameGaps == 0)
                {
                    encodedFrames[i] = mpeg.Compress(frames[i], MPEG.FrameType.I);
                }
                else
                {
                    encodedFrames[i] = mpeg.Compress(frames[i], MPEG.FrameType.P);
                }
                compressSize += encodedFrames[i].Length;
            }

            WriteCompressIPframes(outPath, new ImagesData(height, width, iFrameGaps, encodedFrames));

            return new CompressResult(frames[0].Height * frames[0].Width * 3 * frames.Length, compressSize);
        }

        public static Bitmap[] DecompressIPFrames(string inFile)
        {
            ImagesData data = ReadCompressIPframes(inFile);

            int numOfFrames = data.EncodedFrames.Length;
            var bms = new Bitmap[numOfFrames];

            var mpeg = new MPEG(data.Height, data.Width);
            
            for (int i = 0; i < numOfFrames; ++i)
            {
                if (i % data.IFrameGap == 0)
                {
                    bms[i] = mpeg.Decompress(data.EncodedFrames[i], MPEG.FrameType.I);
                }
                else
                {
                    bms[i] = mpeg.Decompress(data.EncodedFrames[i], MPEG.FrameType.P);
                }
            }

            return bms;
        }



        #region File IO

        private struct ImageData
        {
            public int Height;
            public int Width;
            public byte[] Data;

            public ImageData(int height, int width, byte[] data)
            {
                Height = height;
                Width = width;
                Data = data;
            }
        };

        private static ImageData ReadCompressImage(String inFile)
        {
            var fs = new FileStream(inFile, FileMode.Open, FileAccess.Read);
            var reader = new BinaryReader(fs);

            ImageData img;

            img.Height = reader.ReadInt32();
            img.Width = reader.ReadInt32();
            int numOfByte = reader.ReadInt32();
            img.Data = reader.ReadBytes(numOfByte);

            reader.Close();
            fs.Close();

            return img;
        }


        // write data to a file format : height(int) + width(int) + data length + data(bytes)
        private static void WriteCompressImage(String outFile, ImageData img)
        {
            FileStream fs = new FileStream(outFile, FileMode.Create);
            BinaryWriter writer = new BinaryWriter(fs);

            writer.Write(img.Height);
            writer.Write(img.Width);
            writer.Write(img.Data.Length);
            writer.Write(img.Data);

            writer.Close();
            fs.Close();
        }


        private struct ImagesData
        {
            public int Height;
            public int Width;
            public int IFrameGap;
            public byte[][] EncodedFrames;

            public ImagesData(int height, int width, int iFrameGap, byte[][] encodedFrames)
            {
                Height = height;
                Width = width;
                IFrameGap = iFrameGap;
                EncodedFrames = encodedFrames;
            }
        };

        private static void WriteCompressIPframes(String outFile, ImagesData imgs)
        {
            FileStream fs = new FileStream(outFile, FileMode.Create);
            BinaryWriter writer = new BinaryWriter(fs);

            writer.Write(imgs.Height);
            writer.Write(imgs.Width);
            writer.Write(imgs.IFrameGap);
            writer.Write(imgs.EncodedFrames.Length);

            for (int i = 0; i < imgs.EncodedFrames.Length; ++i)
            {
                writer.Write(imgs.EncodedFrames[i].Length);
                writer.Write(imgs.EncodedFrames[i]);
            }


            writer.Close();
            fs.Close();
        }

        private static ImagesData ReadCompressIPframes(String inFile)
        {
            var fs = new FileStream(inFile, FileMode.Open, FileAccess.Read);
            var reader = new BinaryReader(fs);

            ImagesData imgs;

            imgs.Height = reader.ReadInt32();
            imgs.Width = reader.ReadInt32();
            imgs.IFrameGap = reader.ReadInt32();

            int numOfFrames = reader.ReadInt32();
            imgs.EncodedFrames = new byte[numOfFrames][];

            int numOfByte = 0;
            for (int i = 0; i < numOfFrames; ++i)
            {
                numOfByte = reader.ReadInt32();
                imgs.EncodedFrames[i] = reader.ReadBytes(numOfByte);
            }

            reader.Close();
            fs.Close();

            return imgs;
        }

        #endregion

    }
}
