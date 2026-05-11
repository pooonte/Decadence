using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Numerics;

namespace Decadence.Experimental
{
    public static class PatternGenerator
    {
        private static Random _random = new Random();
        private static int _cachedWidth = 0;
        private static int _cachedHeight = 0;
        private static Vector2[] _cachedPoints;
        private static int _cachedCols = 0;
        private static int _cachedRows = 0;

        // Добавь новый параметр для анимации
        public static async Task<WriteableBitmap> GeneratePatternAsync(int width, int height, double animationTime = 0)
        {
            var wb = new WriteableBitmap(width, height);

            using (var stream = wb.PixelBuffer.AsStream())
            {
                // 🔹 ИСПРАВЛЕНИЕ: фиксируем тип узора
                int patternType = _random.Next(1, 5); // ← 5 = анимированные круги

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color color = GetPatternColor(x, y, width, height, patternType, animationTime);
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

        // 🔹 LowPoly с кэшированием сетки (адаптивный)
        private static Color GenerateLowPolyPixels(int px, int py, int width, int height, Color[] colors)
        {
            // Адаптивный размер ячейки
            float cellSize = Math.Max(50f, Math.Min(width, height) / 10f);
            float jitter = cellSize * 0.6f;
            float margin = cellSize * 1.5f;

            int cols = (int)((width + margin * 2) / cellSize) + 2;
            int rows = (int)((height + margin * 2) / cellSize) + 2;

            // Кэшируем сетку
            if (_cachedWidth != width || _cachedHeight != height || _cachedPoints == null)
            {
                var rnd = new Random(42);
                _cachedPoints = new Vector2[rows * cols];
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        float jx = (float)(rnd.NextDouble() * jitter * 2 - jitter);
                        float jy = (float)(rnd.NextDouble() * jitter * 2 - jitter);
                        _cachedPoints[y * cols + x] = new Vector2(
                            (x * cellSize) - margin + jx,
                            (y * cellSize) - margin + jy
                        );
                    }
                }
                _cachedWidth = width;
                _cachedHeight = height;
                _cachedCols = cols;
                _cachedRows = rows;
            }
            else
            {
                cols = _cachedCols;
                rows = _cachedRows;
            }

            // Ищем треугольник (с проверкой границ)
            for (int y = 0; y < rows - 1; y++)
            {
                for (int x = 0; x < cols - 1; x++)
                {
                    int p1 = y * cols + x;
                    int p2 = p1 + 1;
                    int p3 = (y + 1) * cols + x;
                    int p4 = p3 + 1;

                    // Проверка границ массива
                    if (p1 >= 0 && p1 < _cachedPoints.Length &&
                        p2 >= 0 && p2 < _cachedPoints.Length &&
                        p3 >= 0 && p3 < _cachedPoints.Length &&
                        p4 >= 0 && p4 < _cachedPoints.Length)
                    {
                        if (IsPointInTriangle(px, py, _cachedPoints[p1], _cachedPoints[p2], _cachedPoints[p3]))
                        {
                            int colorHash = (p1 + p2 + p3) % colors.Length;
                            return colors[colorHash];
                        }

                        if (IsPointInTriangle(px, py, _cachedPoints[p2], _cachedPoints[p4], _cachedPoints[p3]))
                        {
                            int colorHash = (p2 + p4 + p3) % colors.Length;
                            return colors[colorHash];
                        }
                    }
                }
            }

            return colors[0];
        }
        // 🔹 Проверка: точка внутри треугольника (барицентрический метод)
        private static bool IsPointInTriangle(int px, int py, Vector2 a, Vector2 b, Vector2 c)
        {
            float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
            {
                return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
            }

            var p = new Vector2(px, py);
            bool b1 = Sign(p, a, b) < 0.0f;
            bool b2 = Sign(p, b, c) < 0.0f;
            bool b3 = Sign(p, c, a) < 0.0f;

            return (b1 == b2) && (b2 == b3);
        }
        // 🔹 Анимация: круги расширяются из центра
        private static Color GetAnimatedExpandingCircles(int px, int py, int width, int height, Color[] colors, double time)
        {
            int centerX = width / 2;
            int centerY = height / 2;

            // Расстояние от пикселя до центра
            double distance = Math.Sqrt((px - centerX) * (px - centerX) + (py - centerY) * (py - centerY));

            // Скорость расширения (пикселей в секунду)
            float speed = 150f;

            // Текущий радиус волны (зависит от времени)
            double currentRadius = (time * speed) % (Math.Max(width, height) * 1.5f);

            // Толщина волны
            float waveThickness = 40f;

            // Проверяем, попадает ли пиксель в текущую волну
            double distFromWave = Math.Abs(distance - currentRadius);

            if (distFromWave < waveThickness)
            {
                // Пиксель внутри волны — яркий цвет
                float intensity = 1.0f - (float)(distFromWave / waveThickness);
                return Color.FromArgb(
                    255,
                    (byte)(colors[3].R * intensity + colors[0].R * (1 - intensity)),
                    (byte)(colors[3].G * intensity + colors[0].G * (1 - intensity)),
                    (byte)(colors[3].B * intensity + colors[0].B * (1 - intensity))
                );
            }
            else if (distFromWave < waveThickness * 2)
            {
                // След от волны — затухающий цвет
                return colors[1];
            }
            else
            {
                // Фон
                return colors[0];
            }
        }
        private static Color GetPatternColor(int x, int y, int width, int height, int patternType, double animationTime = 0)
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
                case 1: // 🔥 HEAVY METAL GLITCH/STROBE
                    int t = (int)(animationTime * 14.0f);
                    int beat = t & 3;
                    int shift = t * 23;

                    int wx = x + ((y ^ beat) & 7) - 4;
                    int wy = y + ((x ^ (beat + 1)) & 7) - 4;

                    int p = (wx ^ wy ^ shift) & 0xFF;
                    p = (int)((p * 0x9E3779B9u) >> 24); // Фикс: u-суффикс + явное приведение

                    Color bg = colors[p % colors.Length]; // Фикс: переименовал base -> bg

                    byte intensity = ((t >> 2) & 1) == 0 ? (byte)255 : (byte)50;

                    int rp = ((wx + 4) ^ wy ^ shift) & 0xFF;
                    int gp = (wx ^ (wy + 3) ^ shift) & 0xFF;
                    int bp = (wx ^ wy ^ (shift + 7)) & 0xFF;

                    byte r = (byte)((bg.R * intensity) / 255 + (rp > 128 ? 60 : 0));
                    byte g = (byte)((bg.G * intensity) / 255 + (gp > 128 ? 60 : 0));
                    byte b = (byte)((bg.B * intensity) / 255 + (bp > 128 ? 60 : 0));

                    if ((y & 1) == 0) { r >>= 1; g >>= 1; b >>= 1; }

                    return Color.FromArgb(255, r, g, b);

                default: // Случайный шум
                    return colors[_random.Next(colors.Length)];
            }
        }
    }
}