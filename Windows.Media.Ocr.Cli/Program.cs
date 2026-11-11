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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Windows.Media.Ocr;

namespace Windows.Media.Ocr.Cli
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var exitCode = 1;
            try
            {
                exitCode = Run(args);
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e.Message);
            }
#if DEBUG
            if (!Console.IsInputRedirected)
            {
                Console.WriteLine("Press Enter to exit...");
                Console.ReadLine();
            }
#endif
            return exitCode;
        }

        static int Run(string[] args)
        {
            string imagePath = null;
            string language = "zh-Hans-CN";
            string outputPath = "";
            var enableJsonOutput = false;
            var outputToClipboard = false;
            var readClipboard = false;
            var drawBoxes = false;
            var contrast = 1.0;
            var autoLevels = false;
            var boxOutputPath = "";
            var enableNotification = false;
            var notificationDurationMs = 5000;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg == "-s")
                {
                    Console.WriteLine("All of supported language");
                    foreach (var lang in OcrEngine.AvailableRecognizerLanguages)
                    {
                        Console.WriteLine(lang.LanguageTag);
                    }
                    return 0;
                }
                if (arg == "-h" || arg == "--help")
                {
                    PrintHelp();
                    return 0;
                }
                if (arg == "-l" || arg == "--language")
                {
                    language = NextValue(args, ref i, arg, "<language>");
                    continue;
                }
                if (arg == "-o" || arg == "--output")
                {
                    outputPath = NextValue(args, ref i, arg, "<output_path>");
                    continue;
                }
                if (arg == "-j" || arg == "--json")
                {
                    enableJsonOutput = true;
                    continue;
                }
                if (arg == "-oc" || arg == "--output-to-clipboard")
                {
                    outputToClipboard = true;
                    continue;
                }
                if (arg == "-c" || arg == "--clipboard")
                {
                    readClipboard = true;
                    continue;
                }
                if (arg == "-b" || arg == "--boxes")
                {
                    drawBoxes = true;
                    continue;
                }
                if (arg == "-bo" || arg == "--boxes-output")
                {
                    boxOutputPath = NextValue(args, ref i, arg, "<output_path>");
                    drawBoxes = true;
                    continue;
                }
                if (arg == "-e" || arg == "--enhance")
                {
                    var value = NextValue(args, ref i, arg, "<contrast|auto>");
                    if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
                    {
                        autoLevels = true;
                        continue;
                    }
                    contrast = ParseContrast(value);
                    autoLevels = false;
                    continue;
                }
                if (arg == "-n" || arg == "--notify")
                {
                    enableNotification = true;
                    continue;
                }
                if (arg == "-nt" || arg == "--notify-timeout")
                {
                    notificationDurationMs = ParseDuration(NextValue(args, ref i, arg, "<milliseconds>"));
                    continue;
                }
                if (arg.Length > 1 && arg[0] == '-')
                {
                    throw new ArgumentException("unknown option: " + arg + " (use -h for help)");
                }
                if (imagePath == null)
                {
                    imagePath = arg;
                }
            }

            string clipboardTempFile = null;
            try
            {
                if (readClipboard)
                {
                    clipboardTempFile = ClipboardHelper.SaveImageToTempFile();
                    imagePath = clipboardTempFile;
                }

                if (string.IsNullOrEmpty(imagePath))
                {
                    PrintHelp();
                    return 2;
                }

                var result = RecognizeAsync(imagePath, language, contrast, autoLevels).GetAwaiter().GetResult();
                var hasText = !string.IsNullOrWhiteSpace(result.PrettyText);
                var payload = enableJsonOutput ? Serialize(result) : result.PrettyText;

                if (outputPath != "")
                {
                    WriteTextToFile(payload, outputPath);
                }

                if (drawBoxes)
                {
                    var target = boxOutputPath;
                    if (target == "" && readClipboard)
                    {
                        target = DefaultBoxPath();
                    }
                    var boxPath = ImageProcessor.DrawBoxes(result, imagePath, target);
                    Console.WriteLine("Boxes: " + boxPath);
                }

                // -c 的语义：识别结果直接写回剪贴板，省掉手动复制这一步
                if (outputToClipboard || readClipboard)
                {
                    if (hasText)
                    {
                        ClipboardHelper.SetText(payload);
                    }
                }

                Console.WriteLine(hasText ? payload : "未识别到文字。");

                if (enableNotification)
                {
                    ShowNotification(
                        "OCR 完成",
                        BuildNotificationText(result.PrettyText, readClipboard, hasText),
                        ToolTipIcon.Info,
                        notificationDurationMs);
                }

                return hasText ? 0 : 3;
            }
            catch (Exception e)
            {
                if (enableNotification)
                {
                    ShowNotification("OCR 失败", e.Message, ToolTipIcon.Error, notificationDurationMs);
                }
                Console.WriteLine("ERROR: " + e.Message);
                return 1;
            }
            finally
            {
                // SaveImageToTempFile 在剪贴板内容是文件/路径时直接返回原路径，
                // 只有真正位于 %TEMP% 的文件才允许删除，否则会删掉用户的原文件
                if (clipboardTempFile != null && File.Exists(clipboardTempFile)
                    && clipboardTempFile.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(clipboardTempFile);
                    }
                    catch (IOException)
                    {
                        // 临时文件删不掉不影响结果
                    }
                }
            }
        }

        static string NextValue(string[] args, ref int i, string option, string placeholder)
        {
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException("option " + option + " requires a value " + placeholder);
            }
            return args[++i];
        }

        static double ParseContrast(string value)
        {
            var text = (value ?? string.Empty).Trim();
            var isPercent = text.EndsWith("%", StringComparison.Ordinal);
            if (isPercent)
            {
                text = text.Substring(0, text.Length - 1).Trim();
            }

            double parsed;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) || parsed <= 0)
            {
                throw new ArgumentException("-e/--enhance requires a number (1.5), a percentage (150%) or auto");
            }

            if (isPercent)
            {
                parsed /= 100.0;
            }

            if (parsed < 0.1 || parsed > 10)
            {
                throw new ArgumentException("-e/--enhance must be between 0.1 and 10 (10% to 1000%)");
            }

            return parsed;
        }

        static int ParseDuration(string value)
        {
            int duration;
            if (!int.TryParse(value, out duration) || duration <= 0)
            {
                throw new ArgumentException("-nt/--notify-timeout requires a positive milliseconds value");
            }
            return duration;
        }

        static void PrintHelp()
        {
            Console.WriteLine(@"Usage: Windows.Media.Ocr.Cli.exe [options...] <image file path>
Example: Windows.Media.Ocr.Cli.exe -o c:\res.txt x.png
Example: Windows.Media.Ocr.Cli.exe -c -n
-l      --language            <language>  Default:zh-Hans-CN   Specify language to reconizing
-o      --output              <output_path> Output the resalut to a file, It should end with a file name, such as res.txt.
-c      --clipboard           Recognize image from clipboard and write result text back to clipboard
-b      --boxes               Save the recognized text boxes as <image>-out.png
-bo     --boxes-output        <output_path> Save the boxes image to the given path (implies -b)
-e      --enhance             <contrast|auto>  Boost contrast before OCR: 1.5, 150% or auto (Default:1, no change)
-j      --json                Output as json
-oc     --output-to-clipboard Save output to clipboard
-n      --notify              Show a Windows notification after OCR completes
-nt     --notify-timeout      <milliseconds> Notification duration in milliseconds. Default:5000
-s                            Show all supported languages
-h      --help                Show help like this
");
        }

        static async Task<OCRJson> RecognizeAsync(string imagePath, string language, double contrast, bool autoLevels)
        {
            var lang = new Globalization.Language(language);
            if (!OcrEngine.IsLanguageSupported(lang))
            {
                throw new Exception(string.Format("Language {0} is not supported", language));
            }

            var engine = OcrEngine.TryCreateFromLanguage(lang);
            if (engine == null)
            {
                throw new Exception(string.Format("Failed to create OCR engine for language {0}", language));
            }

            var space = language.Contains("zh") ? "" : " ";
            var result = new OCRJson();
            var lines = new List<Line>();

            using (var softwareBitmap = await ImageProcessor.LoadAsync(imagePath, contrast, autoLevels))
            {
                OcrResult ocrResult = await engine.RecognizeAsync(softwareBitmap);

                result.TextAngle = ocrResult.TextAngle;
                result.Text = ocrResult.Text;

                foreach (var tempLine in ocrResult.Lines)
                {
                    var builder = new StringBuilder();
                    var words = new List<Word>();
                    var left = double.MaxValue;
                    var top = double.MaxValue;
                    var right = double.MinValue;
                    var bottom = double.MinValue;

                    foreach (OcrWord word in tempLine.Words)
                    {
                        builder.Append(word.Text).Append(space);

                        var r = word.BoundingRect;
                        words.Add(new Word(word.Text, r.X, r.Y, r.Width, r.Height));

                        left = Math.Min(left, r.X);
                        top = Math.Min(top, r.Y);
                        right = Math.Max(right, r.X + r.Width);
                        bottom = Math.Max(bottom, r.Y + r.Height);
                    }

                    // 空行时 left/top 还是 MaxValue，不能直接用
                    var rect = words.Count > 0
                        ? new Rect(left, top, right - left, bottom - top)
                        : new Rect(0, 0, 0, 0);

                    var line = new Line(builder.ToString(), rect, words.ToArray());
                    lines.Add(line);
                    result.PrettyText += line.Text + Environment.NewLine;
                }
            }

            result.Lines = lines.ToArray();
            return result;
        }

        static string Serialize(OCRJson result)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,                                  // 美化输出
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,  // 中文不转 \uXXXX
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            return JsonSerializer.Serialize(result, options);
        }

        static string DefaultBoxPath()
        {
            return Path.Combine(Environment.CurrentDirectory, "clipboard-out.png");
        }

        static void WriteTextToFile(string text, string filePath)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                // 将字符串转换成UTF-8编码并写入文件
                using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                {
                    writer.Write(text);
                }
            }
        }

        static string BuildNotificationText(string result, bool useClipboard, bool hasRecognizedText)
        {
            if (!hasRecognizedText)
            {
                return useClipboard ? "识别完成，但没有识别到文字，未更新剪贴板。" : "识别完成，但没有识别到文字。";
            }

            var preview = (result ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (preview.Length > 40)
            {
                preview = preview.Substring(0, 40) + "...";
            }

            if (preview == "")
            {
                return useClipboard ? "识别完成，结果已写回剪贴板。" : "识别完成。";
            }

            var prefix = useClipboard ? "识别完成，结果已写回剪贴板。" : "识别完成。";
            return prefix + "\n" + preview;
        }

        static void ShowNotification(string title, string text, ToolTipIcon icon, int durationMs)
        {
            var titleBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(title ?? string.Empty));
            var textBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text ?? string.Empty));
            var systemIconName = icon == ToolTipIcon.Error ? "Error" : icon == ToolTipIcon.Warning ? "Warning" : "Information";
            var script = string.Format(
@"Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$title = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{0}'))
$text = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{1}'))
$notifyIcon = New-Object System.Windows.Forms.NotifyIcon
$notifyIcon.Icon = [System.Drawing.SystemIcons]::{2}
$notifyIcon.Visible = $true
$notifyIcon.BalloonTipIcon = [System.Windows.Forms.ToolTipIcon]::{3}
$notifyIcon.BalloonTipTitle = $title
$notifyIcon.BalloonTipText = $text
$notifyIcon.ShowBalloonTip({4})
Start-Sleep -Milliseconds {4}
$notifyIcon.Dispose()
",
                titleBase64,
                textBase64,
                systemIconName,
                icon,
                durationMs);
            var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -STA -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand " + encodedScript,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(startInfo);
        }
    }
}
