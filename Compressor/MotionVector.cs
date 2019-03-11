using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Compressor.JPEG;

namespace Compressor
{
    class MotionVector
    {
        public struct Vector
        {
            public int x;
            public int y;
        }

        public struct EstimateResult {
            public Vector[] Vectors;
            public double[,] YDiffs;
            public double[,] CbDiffs;
            public double[,] CrDiffs;
        }

        private readonly int N = 8;
        private int P;

        // microblock size N and serach window 2p+1
        public MotionVector(int _P)
        {
            P = _P;
        }

        public EstimateResult Estimate(YCbCrSubSamples reference, YCbCrSubSamples target)
        {
            int height = reference.Y.GetLength(0);
            int width = reference.Y.GetLength(1);

            // dimension of 8*8 blocks to fill the origin channel
            int h = (int)Math.Ceiling((double)height / N);
            int w = (int)Math.Ceiling((double)width / N);

            int resIndex = 0;
            var result = new EstimateResult { };
            result.Vectors = new Vector[h*w];

            // dimension with paddings if origin image is not multiples of 8
            h  <<= 3;
            w <<= 3;
            result.YDiffs = new double[h, w];

            // cb  cr
            h = (int)Math.Ceiling((double)reference.Cb.GetLength(0) / N) << 3;
            w = (int)Math.Ceiling((double)reference.Cb.GetLength(1) / N) << 3;
            result.CbDiffs = new double[h, w];
            result.CrDiffs = new double[h, w];

            // dec to prevent -1 calculation inside the loop
            --width; --height;

            for (int row = 0; row <= height; row += N)
            {
                for (int col = 0; col <= width; col += N)
                {
                    // calculate the difference at the same pos first
                    var r8 = new double[N, N];
                    var t8 = new double[N, N];
                    // 8*8 block filling
                    for (int i = 0, Y = row; i < N; ++i)
                    {
                        for (int j = 0, X = col; j < N; ++j)
                        {
                            // use y channel to get vector
                            r8[i, j] = reference.Y[Y, X];
                            t8[i, j] = target.Y[Y, X];
                            if (X < width) // padding number use the boundary number
                                ++X;
                        }
                        if (Y < height)
                            ++Y;
                    }

                    Differences min = CalDifferences(r8, t8);
                    Vector mv = new Vector { x = 0, y = 0 };

                    // 2d logorithmic search for new reference block (p92)
                    int centerY = row, centerX = col;
                    int offset =(int) Math.Ceiling(P / 2.0);
                    bool last = false;
                    
                    while (!last)
                    {
                        // find one of the nine specified macroblocks that yields minimum MAD, then update centerX, centerY, min Difference. vecotr.
                        for (int offY = centerY - offset; offY <= centerY + offset; offY += offset)
                        {
                            for (int offX = centerX - offset; offX <= centerX + offset; offX += offset)
                            {
                                if ((offX == centerX && offY == centerY) || offX < 0 || offY < 0 || offX > width || offY > height) continue;

                                // 8*8 block filling references block
                                for (int i = 0, Y = offY; i < N; ++i)
                                {
                                    for (int j = 0, X = offX; j < N; ++j)
                                    {
                                        r8[i, j] = reference.Y[Y, X];
                                        if (X < width) // padding number use the boundary number
                                            ++X;
                                    }
                                    if (Y < height)
                                        ++Y;
                                }
                                Differences d = CalDifferences(r8, t8);

                                // compare with min
                                if (d.AbsoluteDifference < min.AbsoluteDifference)
                                {
                                    min = d;
                                    mv.x = col - offX;
                                    mv.y = row - offY;
                                    centerX = offX;
                                    centerY = offY;
                                }
                            }
                        }


                        if (offset == 1) last = true;
                        offset = (int)Math.Ceiling(offset / 2.0);
                    }

                    // cb cr
                    int ch = height >> 1, cw = width >> 1;
                    for (int Y = row >> 1, YB = Y + (N >> 1); Y < YB && Y < ch; ++Y)
                    {
                        for (int X = col >> 1, XB = X + (N >> 1); X < XB && X < cw; ++X)
                        {
                            // boundary check needed
                            int ry = Math.Max(0, Math.Min(Y - mv.y, ch));
                            int rx = Math.Max(0, Math.Min(X - mv.x, cw));
                            result.CbDiffs[Y, X] = target.Cb[Y, X] - reference.Cb[ry, rx];
                            result.CrDiffs[Y, X] = target.Cr[Y, X] - reference.Cr[ry, rx];
                        }
                    }

                    // y
                    for (int _y = 0, Y = row; _y < N; ++_y, ++Y)
                    {
                        for (int _x = 0, X = col; _x < N; ++_x, ++X)
                        {
                            result.YDiffs[Y, X] = min.DifferenceBlock[_y, _x];
                        }
                    }
                    result.Vectors[resIndex++] = mv;
                }
            }

            return result;
        }


        private struct Differences
        {
            public double AbsoluteDifference;
            public double[,] DifferenceBlock;
        }

        // return difference 8*8 block (taret - reference) and absolute diffrence
        private Differences CalDifferences(double[,] reference, double[,] target)
        {
            var diffs = new Differences
            {
                AbsoluteDifference = 0,
                DifferenceBlock = new double[N, N]
            };
            
            for (int row = 0; row < N; ++row)
            {
                for (int col = 0; col < N; ++col)
                {
                    diffs.DifferenceBlock[row, col] = target[row, col] - reference[row, col];
                    diffs.AbsoluteDifference += Math.Abs(diffs.DifferenceBlock[row, col]);
                }
            }
            return diffs;
        }


    }
}

