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
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Windows.Media.Ocr.Cli
{
    /// <summary>
    /// 剪贴板读写。剪贴板属于 OLE，必须在 STA 线程访问，且可能被其他程序短暂占用，需要重试。
    /// </summary>
    internal static class ClipboardHelper
    {
        const int RetryCount = 5;
        const int RetryDelayMs = 100;

        /// <summary>
        /// 把剪贴板里的图片存成临时 PNG 并返回路径（调用方负责删除）。
        /// 支持：位图、浏览器写入的 PNG 流、复制的图片文件、指向图片的路径文本。
        /// </summary>
        public static string SaveImageToTempFile()
        {
            try
            {
                return RunSta(SaveCore);
            }
            catch (ExternalException ex)
            {
                throw new Exception("读取剪贴板图片失败，请确认剪贴板未被其他程序占用。", ex);
            }
        }

        /// <summary>
        /// 把识别结果写回剪贴板。
        /// </summary>
        public static void SetText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            try
            {
                RunSta(() =>
                {
                    Clipboard.SetText(text);
                    return true;
                });
            }
            catch (ExternalException ex)
            {
                throw new Exception("写入剪贴板文本失败，请确认剪贴板未被其他程序占用。", ex);
            }
        }

        static string SaveCore()
        {
            var data = Clipboard.GetDataObject();
            if (data == null)
            {
                throw new Exception("剪贴板中没有图片。");
            }

            // 1) 常规位图（截图工具、画图、Office 等）
            if (data.GetDataPresent(DataFormats.Bitmap))
            {
                using (var image = Clipboard.GetImage())
                {
                    if (image != null)
                    {
                        return SavePng(image);
                    }
                }
            }

            // 2) 浏览器复制的 PNG 流（带透明通道，GetImage 通常取不到）
            if (data.GetDataPresent("PNG"))
            {
                var stream = data.GetData("PNG") as Stream;
                if (stream != null)
                {
                    using (stream)
                    using (var image = Image.FromStream(stream))
                    {
                        return SavePng(image);
                    }
                }
            }

            // 3) 在资源管理器里复制的图片文件
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = Clipboard.GetFileDropList();
                foreach (var file in files)
                {
                    if (!string.IsNullOrEmpty(file) && File.Exists(file) && IsImageFile(file))
                    {
                        return Path.GetFullPath(file);
                    }
                }
            }

            // 4) 复制的文本恰好是图片路径
            if (data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Text))
            {
                var text = (Clipboard.GetText() ?? string.Empty).Trim().Trim('"');
                if (text.Length > 0 && File.Exists(text) && IsImageFile(text))
                {
                    return Path.GetFullPath(text);
                }
            }

            throw new Exception("剪贴板中没有图片（支持：位图、PNG、图片文件、图片路径文本）。");
        }

        static string SavePng(Image image)
        {
            var path = Path.Combine(Path.GetTempPath(), "Windows.Media.Ocr.Cli-" + Guid.NewGuid().ToString("N") + ".png");
            // 透明背景铺白，否则 OCR 几乎识别不出来
            using (var canvas = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb))
            using (var graphics = global::System.Drawing.Graphics.FromImage(canvas))
            {
                graphics.Clear(Color.White);
                graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));
                canvas.Save(path, ImageFormat.Png);
            }
            return path;
        }

        static bool IsImageFile(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                case ".gif":
                case ".tif":
                case ".tiff":
                case ".ico":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 在独立 STA 线程上执行剪贴板操作，遇 ExternalException（剪贴板被占用）重试。
        /// </summary>
        static T RunSta<T>(Func<T> action)
        {
            T result = default(T);
            Exception error = null;

            var thread = new Thread(() =>
            {
                ExternalException lastError = null;
                for (var i = 0; i < RetryCount; i++)
                {
                    try
                    {
                        result = action();
                        return;
                    }
                    catch (ExternalException ex)
                    {
                        lastError = ex;
                        Thread.Sleep(RetryDelayMs);
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                        return;
                    }
                }
                error = lastError;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join();

            if (error != null)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }
            return result;
        }
    }
}
