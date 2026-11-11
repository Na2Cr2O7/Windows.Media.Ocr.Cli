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
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Windows.Media.Ocr.Cli
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            string imagePath = null;
            string language = "zh-Hans-CN";
            string outputPath = "";
            bool enableJsonOutput = false;
            bool outputToClipboard = false;
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "-s")
                {
                    Console.WriteLine("Supported languages");
                    foreach (var lang in OcrEngine.AvailableRecognizerLanguages)
                    {
                        Console.WriteLine(lang.LanguageTag);
                    }
                    break;
                }
                if (arg == "-oc" || arg == "--output-to-clipboard")
                {
                    outputToClipboard = true;
                    continue;
                }

                if (arg == "-h" || arg == "--help")
                {
                    PrintHelp();
                    break;
                }
                if (arg == "-l" && i < args.Length - 1)
                {
                    language = args[i + 1];
                    i++;
                    continue;
                }
                if (arg == "-o" || arg == "--output")
                {
                    outputPath = args[i + 1];
                    i++;
                    continue;
                }
                if (arg == "-j" || arg == "--json")
                {
                    enableJsonOutput = true;

                    continue;
                }


                if (arg == "-c" || arg == "--clipboard")
                {
                    imagePath = "!Clipboard!";
                }
                if (imagePath == null)
                {
                    imagePath = arg;
                }


            }

            try
            {
                var result = RecognizeAsync(imagePath, language).GetAwaiter().GetResult();
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,                                        // 美化输出
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,       // 中文不转 \uXXXX
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };
                string json = JsonSerializer.Serialize(result, options);

                var text = enableJsonOutput ? json : result.PrettyText;
                if (outputPath != "")
                {
                    WriteTextToFile(text, outputPath);
                }
                if (outputToClipboard)
                {
                    WriteTextToClipboard(text);
                }
                Console.WriteLine(text);
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e.Message);
            }
#if DEBUG
            Console.WriteLine("Press Any Key to Continue...");
            Console.ReadLine();
#endif
        }

        static void PrintHelp()
        {
            Console.WriteLine(@"Usage: Windows.Media.Ocr.Cli.exe [options...] <image file path>
Example: Windows.Media.Ocr.Cli.exe -o c:\res.txt x.png
-l      <language>              Default:zh-Hans-CN   Specify language to reconize
-o      --output                <output_path> Output the resalut to a file, It should end with a file name, such as res.txt.
-s                              Show all supported languages
-h      --help                  Show help like this
-oc     --output-to-clipboard   Save output to clipboard
-j      --json                  Output as json
-c      --clipboard             Input from clipboard (unusable)
");
        }

        static async Task<OCRJson> RecognizeAsync(string imagePath, string language)
        {
            SoftwareBitmap softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Rgba16, 1, 1);
            if (imagePath == "!Clipboard!")
            {
                var data = Clipboard.GetDataObject() ?? throw new Exception("Clipboard has no data.");
                if (data.GetDataPresent(DataFormats.MetafilePict))
                {
                    var img = Clipboard.GetImage();
                    img.Save("temp.jpg");
                    imagePath = "temp.jpg";
                }
                else if (data.GetDataPresent(DataFormats.Text))
                {
                    imagePath = Clipboard.GetText();
                }
                else if (data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = Clipboard.GetFileDropList();
                    if (files.Count == 0)
                    {
                        throw new Exception("No Files Found");
                    }
                    imagePath = files[0];
                }
            }
            StorageFile storageFile;
            var path = Path.GetFullPath(imagePath); // x.png
            var extName = Path.GetExtension(path); // .png
                                                   //var outPath = path.Replace(extName, "") + "-out" + extName;  // x-out.png
            storageFile = await StorageFile.GetFileFromPathAsync(path);
            IRandomAccessStream randomAccessStream = await storageFile.OpenReadAsync();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);



            Globalization.Language lang = new Globalization.Language(language);
            string space = language.Contains("zh") ? "" : " ";
            OCRJson result = new OCRJson();
            List<Line> lines = new List<Line>();
            if (OcrEngine.IsLanguageSupported(lang))
            {
                OcrEngine engine = OcrEngine.TryCreateFromLanguage(lang);
                if (engine != null)
                {
                    OcrResult ocrResult = await engine.RecognizeAsync(softwareBitmap);
                    result.TextAngle = ocrResult.TextAngle;
                    result.Text = ocrResult.Text;

                    foreach (var tempLine in ocrResult.Lines)
                    {
                        string line = "";
                        double left = double.MaxValue;
                        double top = double.MaxValue;
                        double right = double.MinValue;
                        double bottom = double.MinValue;

                        foreach (OcrWord word in tempLine.Words)
                        {
                            line += word.Text + space;

                            var r = word.BoundingRect;
                            left = Math.Min(left, r.X);
                            top = Math.Min(top, r.Y);
                            right = Math.Max(right, r.X + r.Width);
                            bottom = Math.Max(bottom, r.Y + r.Height);
                        }
                        Rect rect = new Rect(0, 0, 0, 0);
                        if (tempLine.Words.Count > 0)   // 防御：空行时 left/top 还是 MaxValue，不能直接用
                        {
                            rect = new Rect(left, top, right - left, bottom - top);

                        }

                        lines.Add(new Line(line, rect));
                        result.PrettyText += line + Environment.NewLine;
                    }
                }
            }
            else
            {
                throw new Exception(string.Format("Language {0} is not supported", language));
            }
            ;
            randomAccessStream.Dispose();
            softwareBitmap.Dispose();
            result.Lines = lines.ToArray();
            return result;
            //return await Task<string>.Run(() =>
            //{
            //    return result;
            //});

        }
        static void WriteTextToFile(string text, string filePath)
        {
            // 创建一个输出流
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                // 将字符串转换成UTF-8编码并写入文件
                using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                {
                    writer.Write(text);
                }
            }
        }
        static void WriteTextToClipboard(string text)
        {
            //System.Threading.ThreadStateException:“Current thread must be set to single thread apartment (STA) mode before OLE calls can be made. Ensure that your Main function has STAThreadAttribute marked on it.”
            //Clipboard.SetText(text);
            Thread staThread = new Thread(() =>
            {
                Clipboard.SetText(text);
            });
            staThread.SetApartmentState(ApartmentState.STA); // 关键：设置为 STA
            staThread.IsBackground = true; // 设为后台线程，避免阻止进程退出
            staThread.Start();
            staThread.Join(); // 等待操作完成...
        }
    }
}
