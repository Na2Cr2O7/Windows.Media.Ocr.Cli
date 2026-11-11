//*********************************************************
//
// Copyright (c) zh-h. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Windows.Media.Ocr.Cli
{
    /// <summary>
    /// 图片预处理与结果标注：对比度增强、把识别出的文字区域画成方块。
    /// </summary>
    internal static class ImageProcessor
    {
        /// <summary>
        /// 读取图片为 SoftwareBitmap。autoLevels 或 contrast != 1 时先用 System.Drawing 处理像素，
        /// 再编码成 PNG 交给 WinRT 解码，避免手工操作 SoftwareBitmap 的像素缓冲。
        /// </summary>
        public static async Task<SoftwareBitmap> LoadAsync(string path, double contrast, bool autoLevels)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Image not found: " + fullPath, fullPath);
            }

            if (!autoLevels && Math.Abs(contrast - 1.0) < 0.0001)
            {
                var file = await StorageFile.GetFileFromPathAsync(fullPath);
                using (var stream = await file.OpenReadAsync())
                {
                    return await DecodeAsync(stream);
                }
            }

            var png = RenderEnhancedPng(fullPath, contrast, autoLevels);
            var memory = new InMemoryRandomAccessStream();
            try
            {
                // 用 DataWriter 写入；AsStreamForWrite 的包装流在 Dispose 时会连底层流一起关掉，
                // 导致后续解码报 "The object has been closed"
                var dataWriter = new DataWriter(memory.GetOutputStreamAt(0));
                dataWriter.WriteBytes(png);
                await dataWriter.StoreAsync();
                await dataWriter.FlushAsync();
                dataWriter.DetachStream();

                memory.Seek(0);
                return await DecodeAsync(memory);
            }
            finally
            {
                memory.Dispose();
            }
        }

        /// <summary>
        /// 在图片上标出识别到的文字区域（行用红框、词用蓝框），返回输出文件路径。
        /// destPath 为空时输出到原图同目录的 &lt;name&gt;-out.png。
        /// </summary>
        public static string DrawBoxes(OCRJson result, string sourcePath, string destPath)
        {
            sourcePath = Path.GetFullPath(sourcePath);
            if (string.IsNullOrEmpty(destPath))
            {
                destPath = Path.Combine(
                    Path.GetDirectoryName(sourcePath) ?? ".",
                    Path.GetFileNameWithoutExtension(sourcePath) + "-out.png");
            }

            using (var canvas = new Bitmap(sourcePath))
            using (var graphics = global::System.Drawing.Graphics.FromImage(canvas))
            using (var linePen = new Pen(Color.FromArgb(235, 230, 40, 40), Math.Max(1.5f, canvas.Width / 900f)))
            using (var wordPen = new Pen(Color.FromArgb(170, 40, 120, 230), Math.Max(1f, canvas.Width / 1800f)))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                foreach (var line in result.Lines)
                {
                    var lineRect = ToRectangle(line.X, line.Y, line.Width, line.Height);
                    if (lineRect.Width > 0 && lineRect.Height > 0)
                    {
                        graphics.DrawRectangle(linePen, lineRect);
                    }

                    foreach (var word in line.Words)
                    {
                        var wordRect = ToRectangle(word.X, word.Y, word.Width, word.Height);
                        if (wordRect.Width > 0 && wordRect.Height > 0)
                        {
                            graphics.DrawRectangle(wordPen, wordRect);
                        }
                    }
                }

                canvas.Save(destPath, ImageFormat.Png);
            }

            return destPath;
        }

        static async Task<SoftwareBitmap> DecodeAsync(IRandomAccessStream stream)
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        /// <summary>
        /// 生成增强后的 PNG：autoLevels 走直方图自动色阶，否则按 contrast 做线性对比度拉伸。
        /// </summary>
        static byte[] RenderEnhancedPng(string path, double contrast, bool autoLevels)
        {
            using (var source = new Bitmap(path))
            using (var target = autoLevels ? AutoLevels(source) : ApplyContrast(source, contrast))
            using (var buffer = new MemoryStream())
            {
                target.Save(buffer, ImageFormat.Png);
                return buffer.ToArray();
            }
        }

        /// <summary>
        /// ColorMatrix 对比度公式：out = (in - 0.5) * contrast + 0.5，contrast=1 时不变。
        /// 透明区域先铺白底，OCR 对纯透明背景识别很差。
        /// </summary>
        static Bitmap ApplyContrast(Bitmap source, double contrast)
        {
            var target = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var graphics = global::System.Drawing.Graphics.FromImage(target))
            {
                var c = (float)contrast;
                var offset = (1f - c) / 2f;
                var matrix = new ColorMatrix(new[]
                {
                    new[] { c, 0f, 0f, 0f, 0f },
                    new[] { 0f, c, 0f, 0f, 0f },
                    new[] { 0f, 0f, c, 0f, 0f },
                    new[] { 0f, 0f, 0f, 1f, 0f },
                    new[] { offset, offset, offset, 0f, 1f },
                });

                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(matrix);
                    graphics.Clear(Color.White);
                    graphics.DrawImage(
                        source,
                        new Rectangle(0, 0, source.Width, source.Height),
                        0, 0, source.Width, source.Height,
                        GraphicsUnit.Pixel,
                        attributes);
                }
            }
            return target;
        }

        /// <summary>
        /// 自动色阶：把灰度直方图 1%~99% 的两端拉伸到 0~255。
        /// 比猜一个固定倍数稳，适合“看着很清楚但 OCR 识别不出来”的低对比截图。
        /// </summary>
        static Bitmap AutoLevels(Bitmap source)
        {
            var target = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var graphics = global::System.Drawing.Graphics.FromImage(target))
            {
                graphics.Clear(Color.White);   // 透明区域铺白
                graphics.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
            }

            var bounds = new Rectangle(0, 0, target.Width, target.Height);
            var data = target.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var length = Math.Abs(data.Stride) * target.Height;
                var buffer = new byte[length];
                Marshal.Copy(data.Scan0, buffer, 0, length);

                var histogram = new int[256];
                for (var i = 0; i + 3 < length; i += 4)
                {
                    // 32bppArgb 在内存里是 BGRA，按 BT.601 系数算亮度
                    var gray = (buffer[i] * 29 + buffer[i + 1] * 150 + buffer[i + 2] * 77) >> 8;
                    histogram[gray]++;
                }

                var total = target.Width * target.Height;
                var low = Percentile(histogram, total / 100);            // 1% 分位当黑点
                var high = Percentile(histogram, total - total / 100);   // 99% 分位当白点

                if (high - low >= 8)   // 纯色图拉伸只会放大噪点
                {
                    var lut = new byte[256];
                    for (var v = 0; v < 256; v++)
                    {
                        lut[v] = (byte)Math.Max(0, Math.Min(255, (v - low) * 255 / (high - low)));
                    }

                    for (var i = 0; i + 3 < length; i += 4)
                    {
                        buffer[i] = lut[buffer[i]];
                        buffer[i + 1] = lut[buffer[i + 1]];
                        buffer[i + 2] = lut[buffer[i + 2]];
                    }

                    Marshal.Copy(buffer, 0, data.Scan0, length);
                }
            }
            finally
            {
                target.UnlockBits(data);
            }

            return target;
        }

        static int Percentile(int[] histogram, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            var accumulated = 0;
            for (var v = 0; v < histogram.Length; v++)
            {
                accumulated += histogram[v];
                if (accumulated >= count)
                {
                    return v;
                }
            }
            return histogram.Length - 1;
        }

        static Rectangle ToRectangle(double x, double y, double width, double height)
        {
            // 边框外扩 1px，避免线条压住字形影响观感
            return new Rectangle(
                (int)Math.Floor(x) - 1,
                (int)Math.Floor(y) - 1,
                (int)Math.Ceiling(width) + 2,
                (int)Math.Ceiling(height) + 2);
        }
    }
}
