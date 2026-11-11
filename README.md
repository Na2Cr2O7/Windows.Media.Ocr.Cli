# Windows.Media.Ocr.Cli
Ocr 命令行工具， **本地调用** UWP API Windows.Media.Ocr 支持多种语言识别。

## 使用


### 命令行执行
```powershell
PS C:\Tools>Windows.Media.Ocr.Cli.exe .\x.png
9·哪位科学家发现了电磁感应现象？
PS C:\Tools>Windows.Media.Ocr.Cli.exe -o .\res.txt .\x.png
-> 将内容输出到res.txt中
PS C:\Tools>Windows.Media.Ocr.Cli.exe -c
-> 从剪贴板读取图片进行识别，并将识别结果写回剪贴板
PS C:\Tools>Windows.Media.Ocr.Cli.exe -c -n
-> 从剪贴板读取图片进行识别，并在完成后显示右下角提示
PS C:\Tools>Windows.Media.Ocr.Cli.exe -b .\x.png
-> 识别并在 .\x-out.png 上画出文字区域方块
PS C:\Tools>Windows.Media.Ocr.Cli.exe -b -bo .\marked.png .\x.png
-> 方块图输出到指定路径
PS C:\Tools>Windows.Media.Ocr.Cli.exe -e 1.5 .\x.png
-> 识别前先把对比度放大 1.5 倍（图片太淡识别不出来时用）
PS C:\Tools>Windows.Media.Ocr.Cli.exe -e auto .\x.png
-> 自动色阶：按直方图把 1%~99% 的灰度拉伸到全范围，不用试参数
```

### 查看帮助
```powershell
PS C:\Tools>Windows.Media.Ocr.Cli.exe -h
Usage: Windows.Media.Ocr.Cli.exe [options...] <image file path>
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
```

### 运行依赖
1. 需要 Windows 10 /11 系统，其他平台没有测试，理论上可以支持所有支持 UWP 的设备，包括 Xbox 甚至安装 Windows 10 IoT 的树莓派，不太了解 Xamarin 能不能引入 UWP 的 runtime，如果能引入 UWP 就可以用在 iOS 和 Android 平台。

## 功能
- [x] 文字识别
- [x] 输出到文件
- [x] 输出图片显示文字区域方块
- [x] 提供提高对比参数（有时后明明图片很清晰，但是不调高对比度什么文字都没识别）
- [x] 从剪贴板读取图片识别，结果写回剪贴板
- [x] 识别完成后显示 Windows 通知

## 参考
- Get Started With Optical Character Recognition with the OCR Library for Windows Runtime <div align="center">
  <a href="https://www.youtube.com/watch?v=9TXl0sUHEMg"><img src="https://img.youtube.com/vi/9TXl0sUHEMg/0.jpg" alt="Get Started With Optical Character Recognition with the OCR Library for Windows Runtime"></a>
  </div>

- 微软官方文档 https://docs.microsoft.com/en-us/uwp/api/Windows.Media.Ocr
- 官方示例 APP https://github.com/Microsoft/Windows-universal-samples/tree/master/Samples/OCR


## 声明
仅供学习参考，请勿用于服务器端生产环境。

请遵守微软 Windows Runtime 使用协议 [MICROSOFT OCR LIBRARY FOR WINDOWS RUNTIME](https://www.microsoft.com/web/webpi/eula/windows_runtime_ocr_library_terms_of_use.htm)
> distribute Distributable Code to run on a platform other than the Windows Store or Windows Phone;
