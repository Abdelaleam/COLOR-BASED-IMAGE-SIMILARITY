using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static ImageSimilarity.ImageOperations;

namespace ImageSimilarity
{
    public struct ChannelStats
    {
        public int[] Hist;
        public int Min;
        public int Max;
        public int Med;
        public double Mean;
        public double StdDev;
    }
    public struct ImageInfo
    {
        public string Path;
        public int Width;
        public int Height;
        public ChannelStats RedStats;
        public ChannelStats GreenStats;
        public ChannelStats BlueStats;
    }

    public struct MatchInfo
    {
        public string MatchedImgPath;
        public double MatchScore;
    }
    public class ImageHistSimilarity
    {
        /// <summary>
        /// Calculate the image stats (Max, Min, Med, Mean, StdDev & Histogram) of each color
        /// </summary>
        /// <param name="imgPath">Image path</param>
        /// <returns>Calculated stats of the given image</returns>
        public static ImageInfo CalculateImageStats(string imgPath)
        {
            RGBPixel[,] imagep = OpenImage(imgPath);
            int length = imagep.GetLength(0);
            int width = imagep.GetLength(1);
            int totalPixels = length * width;
            int rMin = 255, gMin = 255, bMin = 255;
            int rMax = 0, gMax = 0, bMax = 0;
            long sumR = 0, sumG = 0, sumB = 0;
            long sumSqR = 0, sumSqG = 0, sumSqB = 0;
            int[] histR = new int[256];
            int[] histG = new int[256];
            int[] histB = new int[256];
            for (int i = 0; i < length; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    int r = imagep[i, j].red;
                    int g = imagep[i, j].green;
                    int b = imagep[i, j].blue;
                    if (r < rMin) rMin = r;
                    if (r > rMax) rMax = r;
                    if (g < gMin) gMin = g;
                    if (g > gMax) gMax = g;
                    if (b < bMin) bMin = b;
                    if (b > bMax) bMax = b;
                    histR[r]++;
                    histG[g]++;
                    histB[b]++;
                    sumR += r;
                    sumG += g;
                    sumB += b;
                    sumSqR += r * r;
                    sumSqG += g * g;
                    sumSqB += b * b;
                }
            }
            double rMean = sumR / (double)totalPixels;
            double gMean = sumG / (double)totalPixels;
            double bMean = sumB / (double)totalPixels;
            double rVariance = (sumSqR / (double)totalPixels) - (rMean * rMean);
            double gVariance = (sumSqG / (double)totalPixels) - (gMean * gMean);
            double bVariance = (sumSqB / (double)totalPixels) - (bMean * bMean);
            double rStdDev = Math.Sqrt(rVariance);
            double gStdDev = Math.Sqrt(gVariance);
            double bStdDev = Math.Sqrt(bVariance);
            int rMedian = calcMedian(histR, totalPixels);
            int gMedian = calcMedian(histG, totalPixels);
            int bMedian = calcMedian(histB, totalPixels);
            ChannelStats rStats = new ChannelStats
            {
                Hist = histR,
                Min = rMin,
                Max = rMax,
                Med = rMedian,
                Mean = rMean,
                StdDev = rStdDev
            };
            ChannelStats gStats = new ChannelStats
            {
                Hist = histG,
                Min = gMin,
                Max = gMax,
                Med = gMedian,
                Mean = gMean,
                StdDev = gStdDev
            };
            ChannelStats bStats = new ChannelStats
            {
                Hist = histB,
                Min = bMin,
                Max = bMax,
                Med = bMedian,
                Mean = bMean,
                StdDev = bStdDev
            };
            ImageInfo info = new ImageInfo
            {
                Path = imgPath,
                Width = width,
                Height = length,
                RedStats = rStats,
                GreenStats = gStats,
                BlueStats = bStats
            };

            return info;
        }
        private static int calcMedian(int[] hist, int totalPixels)
        {
            int threshold = (totalPixels + 1) / 2;
            int all = 0;
            for (int i = 0; i < 256; i++)
            {
                all += hist[i];
                if (all >= threshold)
                {
                    return i;
                }
            }
            return 255;
        }
        /// <summary>
        /// Load all target images and calculate their stats
        /// </summary>
        /// <param name="targetPaths">Path of each target image</param>
        /// <returns>Calculated stats of each target image</returns>
        public static ImageInfo[] LoadAllImages(string[] targetPaths)
        {
            ImageInfo[] allpi = new ImageInfo[targetPaths.Length];

            Parallel.For(0, targetPaths.Length, i =>
            {
                allpi[i] = CalculateImageStats(targetPaths[i]);
            });
            return allpi;
        }

        /// <summary>
        /// Match the given query image with the given target images and return the TOP matches as specified
        /// </summary>
        /// <param name="queryPath">Path of the query image</param>
        /// <param name="targetImgStats">Calculated stats of each target image</param>
        /// <param name="numOfTopMatches">Desired number of TOP matches to be returned</param>
        /// <returns>Top matches (image path & distance score) </returns>
        public static MatchInfo[] FindTopMatches(string queryPath, ImageInfo[] targetImgStats, int numOfTopMatches)
        {
            ImageInfo qStats = CalculateImageStats(queryPath);
            var matches = targetImgStats.AsParallel().Select(target => new MatchInfo { MatchedImgPath = target.Path, MatchScore = CalcCosDistance(qStats, target) }).ToArray();
            var topMatches = matches.OrderBy(m => m.MatchScore).Take(numOfTopMatches).ToArray();
            return topMatches;
        }
        private static double CalcCosDistance(ImageInfo query, ImageInfo target)
        {
            double redAngle = CalcCosAngle(query.RedStats.Hist, query.Width * query.Height, target.RedStats.Hist, target.Width * target.Height);
            double greenAngle = CalcCosAngle(query.GreenStats.Hist, query.Width * query.Height, target.GreenStats.Hist, target.Width * target.Height);
            double blueAngle = CalcCosAngle(query.BlueStats.Hist, query.Width * query.Height, target.BlueStats.Hist, target.Width * target.Height);
            return (redAngle + greenAngle + blueAngle) / 3.0;
        }
        private static double CalcCosAngle(int[] hist1, int totalPixels1, int[] hist2, int totalPixels2)
        {
            double dot = 0.0;
            double n1 = 0.0;
            double n2 = 0.0;
            for (int i = 0; i < 256; i++)
            {
                double p1 = hist1[i] / (double)totalPixels1;
                double p2 = hist2[i] / (double)totalPixels2;
                dot += p1 * p2;
                n1 += p1 * p1;
                n2 += p2 * p2;
            }
            double cos = 0.0;
            if (n1 > 0 && n2 > 0)
            {
                cos = dot / (Math.Sqrt(n1) * Math.Sqrt(n2));
            }
            cos = Math.Min(1.0, Math.Max(0.0, cos));
            double angleRadians = Math.Acos(cos);
            double angleDegrees = angleRadians * (180.0 / Math.PI);
            return angleDegrees;
        }
    }
}