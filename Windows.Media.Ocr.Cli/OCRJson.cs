using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows;

namespace Windows.Media.Ocr.Cli
{
    internal class OCRJson
    {

        public double? TextAngle { get; set; } //获取图像中心周围识别的文本的顺时针旋转（以度为单位）。
        public string Text { get; set; } = String.Empty; //获取已识别单词的文本。
        public string PrettyText { get; set; } = String.Empty; //获取适合本国语言分词习惯的文本.
        public Line[] Lines { get; set; } =
        Array.Empty<Line>();
    }
    internal class Line
    {
        public Line() { }  
        public Line(string text, double x, double y, double width, double height)
        {
            Text = text;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
        public Line(string text, Rect rect)
        {
            Text = text;
            X = rect.X;
            Y = rect.Y;
            Width = rect.Width;
            Height = rect.Height;
        }
        public string Text { get; set; } //获取适合本国语言分词习惯的文本.

        // 位置
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

    }
}
