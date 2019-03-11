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

            //image.Save(outFile, System.Drawing.Imaging.ImageFormat.Png);
        }

        public static CompressResult CompressIPframes(string iFreamFile, string pFrameFile, string outFile)
        {
            var i = new Bitmap(iFreamFile);
            var p = new Bitmap(pFrameFile);

            var mpeg = new MPEG(i.Height, i.Width);
            byte[] istream = mpeg.Compress(i, MPEG.FrameType.I);
            byte[] pstream = mpeg.Compress(p, MPEG.FrameType.P);

            // store to outFile
            WriteCompressIPframes(outFile, new ImagesData(i.Height, i.Width, istream, pstream));

            return new CompressResult(i.Height * i.Width * 6, istream.Length + pstream.Length);
        }

        public static Bitmap[] DecompressIPframes(string inFile)
        {
            ImagesData data = ReadCompressIPframes(inFile);

            var mpeg = new MPEG(data.Height, data.Width);

            var bms = new Bitmap[2];

            bms[0] = mpeg.Decompress(data.Iframe, MPEG.FrameType.I);
            bms[1] = mpeg.Decompress(data.Pframe, MPEG.FrameType.P);

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
            public byte[] Iframe;
            public byte[] Pframe;

            public ImagesData(int height, int width, byte[] iframe, byte[] pframe)
            {
                Height = height;
                Width = width;
                Iframe= iframe;
                Pframe = pframe;
            }
        };

        private static void WriteCompressIPframes(String outFile, ImagesData imgs)
        {
            FileStream fs = new FileStream(outFile, FileMode.Create);
            BinaryWriter writer = new BinaryWriter(fs);

            writer.Write(imgs.Height);
            writer.Write(imgs.Width);

            writer.Write(imgs.Iframe.Length);
            writer.Write(imgs.Iframe);

            writer.Write(imgs.Pframe.Length);
            writer.Write(imgs.Pframe);

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

            int numOfByte = reader.ReadInt32();
            imgs.Iframe = reader.ReadBytes(numOfByte);

            numOfByte = reader.ReadInt32();
            imgs.Pframe = reader.ReadBytes(numOfByte);

            return imgs;
        }

        #endregion

    }
}
