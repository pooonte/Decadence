using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime; // ← добавь это

namespace Decadence.Experimental
{
    public static class PatternGenerator
    {
        private static Random _random = new Random();

        public static async Task<WriteableBitmap> GeneratePatternAsync(int width, int height)
        {
            var wb = new WriteableBitmap(width, height);

            using (var stream = wb.PixelBuffer.AsStream())
            {
                int patternType = _random.Next(1, 6);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color color = GetPatternColor(x, y, width, height, patternType);
                        stream.WriteByte(color.B);
                        stream.WriteByte(color.G);
                        stream.WriteByte(color.R);
                        stream.WriteByte(color.A);
                    }
                }
            }

            wb.Invalidate();
            return wb;
        }

        private static Color GetPatternColor(int x, int y, int width, int height, int patternType)
        {
            Color[] colors = new Color[]
            {
                Color.FromArgb(255, 0x3A, 0x28, 0x20),
                Color.FromArgb(255, 0x4A, 0x38, 0x30),
                Color.FromArgb(255, 0x5A, 0x48, 0x40),
                Color.FromArgb(255, 0x3A, 0x3A, 0x4A),
                Color.FromArgb(255, 0x4A, 0x3A, 0x4A),
                Color.FromArgb(255, 0x2A, 0x3A, 0x3A),
            };

            switch (patternType)
            {
                case 1: // Круги
                    int cx = width / 2, cy = height / 2;
                    double dist = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    return colors[(int)(dist / 30) % colors.Length];

                case 2: // Волны
                    return colors[Math.Abs((int)(Math.Sin(x * 0.05) * 10 + Math.Cos(y * 0.05) * 10)) % colors.Length];

                case 3: // Шахматка
                    return ((x / 20) + (y / 20)) % 2 == 0 ? colors[0] : colors[1];


                default: // Случайный шум
                    return colors[_random.Next(colors.Length)];
            }
        }
    }
}