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
using System.Windows;

namespace Windows.Media.Ocr.Cli
{
    internal class OCRJson
    {
        public double? TextAngle { get; set; } //获取图像中心周围识别的文本的顺时针旋转（以度为单位）。
        public string Text { get; set; } = String.Empty; //获取已识别单词的文本。
        public string PrettyText { get; set; } = String.Empty; //获取适合本国语言分词习惯的文本.
        public Line[] Lines { get; set; } = Array.Empty<Line>();
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

        public Line(string text, Rect rect, Word[] words)
            : this(text, rect)
        {
            Words = words ?? Array.Empty<Word>();
        }

        public string Text { get; set; } //获取适合本国语言分词习惯的文本.

        // 位置
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        // 每个词的位置，用于在图片上画更细的方块
        public Word[] Words { get; set; } = Array.Empty<Word>();
    }

    internal class Word
    {
        public Word() { }

        // 参数用 double，避免 Windows.Foundation.Rect 与 System.Windows.Rect 混用
        public Word(string text, double x, double y, double width, double height)
        {
            Text = text;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public string Text { get; set; } = String.Empty;

        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
