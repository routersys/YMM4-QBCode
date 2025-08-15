using System;
using System.IO;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.WIC;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.Windows.Compatibility;

namespace YMM4QRBarcodePlugin
{
    internal class QRBarcodeSource : IShapeSource
    {
        private readonly IGraphicsDevicesAndContext devices;
        private readonly QRBarcodeParameter parameter;
        private readonly DisposeCollector disposer = new();
        private readonly IWICImagingFactory wicFactory;

        private string? lastText, lastFont;
        private QRBarcodeType lastCodeType;
        private QRErrorCorrectionLevel lastErrorLevel;
        private double lastWidth, lastHeight, lastMargin, lastFontSize;
        private bool lastShowText;

        private ID2D1CommandList? commandList;
        private ID2D1Bitmap? bitmap;

        public ID2D1Image Output => commandList ?? throw new InvalidOperationException("出力にアクセスする前にUpdateを呼び出す必要があります");

        public QRBarcodeSource(IGraphicsDevicesAndContext devices, QRBarcodeParameter parameter)
        {
            this.devices = devices;
            this.parameter = parameter;
            wicFactory = new IWICImagingFactory();
            disposer.Collect(wicFactory);
        }

        public void Update(TimelineItemSourceDescription desc)
        {
            try
            {
                var frame = desc.ItemPosition.Frame;
                var length = desc.ItemDuration.Frame;
                var fps = desc.FPS;

                var codeType = parameter.CodeType;
                var text = parameter.Text;
                var errorLevel = parameter.ErrorCorrectionLevel;
                var width = Math.Max(10, parameter.Width.GetValue(frame, length, fps));
                var height = Math.Max(10, parameter.Height.GetValue(frame, length, fps));
                var margin = Math.Max(0, parameter.Margin.GetValue(frame, length, fps));
                var fontSize = Math.Max(1, parameter.FontSize.GetValue(frame, length, fps));
                var font = parameter.Font;
                var showText = parameter.ShowText;

                var hasChanged = lastText != text ||
                                lastCodeType != codeType ||
                                lastErrorLevel != errorLevel ||
                                Math.Abs(lastWidth - width) > 1.0 ||
                                Math.Abs(lastHeight - height) > 1.0 ||
                                Math.Abs(lastMargin - margin) > 1.0 ||
                                Math.Abs(lastFontSize - fontSize) > 1.0 ||
                                lastFont != font ||
                                lastShowText != showText;

                if (!hasChanged && commandList != null)
                    return;

                UpdateGraphics(text, codeType, errorLevel, (int)width, (int)height, (int)margin, (float)fontSize, font, showText);

                lastText = text;
                lastCodeType = codeType;
                lastErrorLevel = errorLevel;
                lastWidth = width;
                lastHeight = height;
                lastMargin = margin;
                lastFontSize = fontSize;
                lastFont = font;
                lastShowText = showText;
            }
            catch
            {
                CreateErrorGraphics("エラー");
            }
        }

        private void UpdateGraphics(string text, QRBarcodeType codeType, QRErrorCorrectionLevel errorLevel,
                                    int width, int height, int margin, float fontSize, string fontName,
                                    bool showText)
        {
            disposer.RemoveAndDispose(ref bitmap);
            System.Drawing.Bitmap? generatedBitmap = null;
            try
            {
                generatedBitmap = GenerateCodeBitmap(text, codeType, errorLevel, width, height,
                                                     margin, fontSize, fontName, showText);
                if (generatedBitmap != null)
                {
                    bitmap = ConvertBitmapToD2DBitmap(generatedBitmap);
                    if (bitmap != null)
                        disposer.Collect(bitmap);
                }
            }
            catch
            {
                using var errorBitmap = CreateErrorBitmap(width, height, "生成エラー");
                bitmap = ConvertBitmapToD2DBitmap(errorBitmap);
                if (bitmap != null)
                    disposer.Collect(bitmap);
            }
            finally
            {
                generatedBitmap?.Dispose();
                CreateCommandListWithBitmap();
            }
        }

        private System.Drawing.Bitmap? GenerateCodeBitmap(string text, QRBarcodeType codeType, QRErrorCorrectionLevel errorLevel,
                                                  int width, int height, int margin, float fontSize, string fontName, bool showText)
        {
            if (string.IsNullOrWhiteSpace(text))
                return CreateErrorBitmap(width, height, "テキストを入力してください");

            var (isValid, errorMessage) = BarcodeHelper.Validate(codeType, text);
            if (!isValid)
            {
                return CreateErrorBitmap(width, height, errorMessage);
            }

            try
            {
                var innerWidth = Math.Max(1, width - margin * 2);
                var innerHeight = Math.Max(1, height - margin * 2);

                var writer = new BarcodeWriter
                {
                    Format = BarcodeHelper.GetBarcodeFormat(codeType),
                    Options = new EncodingOptions
                    {
                        Height = innerHeight,
                        Width = innerWidth,
                        Margin = 0,
                    },
                    Renderer = new ZXing.Windows.Compatibility.BitmapRenderer
                    {
                        Foreground = System.Drawing.Color.Black,
                        Background = System.Drawing.Color.Transparent,
                        TextFont = new System.Drawing.Font(fontName, fontSize)
                    }
                };

                if (!showText)
                {
                    writer.Options.PureBarcode = true;
                }

                if (codeType == QRBarcodeType.QRCode)
                {
                    var eccLevel = errorLevel switch
                    {
                        QRErrorCorrectionLevel.Low => ZXing.QrCode.Internal.ErrorCorrectionLevel.L,
                        QRErrorCorrectionLevel.Medium => ZXing.QrCode.Internal.ErrorCorrectionLevel.M,
                        QRErrorCorrectionLevel.Quartile => ZXing.QrCode.Internal.ErrorCorrectionLevel.Q,
                        _ => ZXing.QrCode.Internal.ErrorCorrectionLevel.H
                    };
                    writer.Options = new QrCodeEncodingOptions
                    {
                        Height = innerHeight,
                        Width = innerWidth,
                        Margin = 0,
                        ErrorCorrection = eccLevel,
                        PureBarcode = !showText
                    };
                }

                using var barcodeBitmap = writer.Write(text);

                var outputBitmap = new System.Drawing.Bitmap(width, height);
                using (var g = System.Drawing.Graphics.FromImage(outputBitmap))
                {
                    g.Clear(System.Drawing.Color.White);

                    var x = margin;
                    var y = margin;
                    g.DrawImage(barcodeBitmap, x, y);
                }

                return outputBitmap;
            }
            catch
            {
                return CreateErrorBitmap(width, height, "生成エラー");
            }
        }

        private void CreateCommandListWithBitmap()
        {
            disposer.RemoveAndDispose(ref commandList);
            commandList = devices.DeviceContext.CreateCommandList();
            disposer.Collect(commandList);

            var dc = devices.DeviceContext;
            dc.Target = commandList;
            dc.BeginDraw();
            dc.Clear(null);

            if (bitmap != null)
            {
                var bitmapSize = bitmap.Size;
                var offset = new Vector2(-bitmapSize.Width / 2f, -bitmapSize.Height / 2f);
                dc.DrawImage(bitmap, offset);
            }

            dc.EndDraw();
            dc.Target = null;
            commandList.Close();
        }

        private void CreateErrorGraphics(string message)
        {
            disposer.RemoveAndDispose(ref bitmap);
            using var errorBitmap = CreateErrorBitmap(200, 200, message);
            if (errorBitmap != null)
            {
                bitmap = ConvertBitmapToD2DBitmap(errorBitmap);
                if (bitmap != null) disposer.Collect(bitmap);
            }
            CreateCommandListWithBitmap();
        }

        private System.Drawing.Bitmap CreateErrorBitmap(int width, int height, string message)
        {
            var bmp = new System.Drawing.Bitmap(Math.Max(width, 100), Math.Max(height, 50));
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.White);
                using var font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Regular);
                using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Red);
                var textSize = g.MeasureString(message, font);
                var x = Math.Max(0, (bmp.Width - textSize.Width) / 2);
                var y = Math.Max(0, (bmp.Height - textSize.Height) / 2);
                g.DrawString(message, font, brush, x, y);
            }
            return bmp;
        }

        private ID2D1Bitmap? ConvertBitmapToD2DBitmap(System.Drawing.Bitmap? bmp)
        {
            if (bmp == null) return null;
            try
            {
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;

                using var decoder = wicFactory.CreateDecoderFromStream(ms, DecodeOptions.CacheOnDemand);
                using var frame = decoder.GetFrame(0);
                using var converter = wicFactory.CreateFormatConverter();
                converter.Initialize(frame, Vortice.WIC.PixelFormat.Format32bppPBGRA, BitmapDitherType.None, null, 0, BitmapPaletteType.MedianCut);
                return devices.DeviceContext.CreateBitmapFromWicBitmap(converter);
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            disposer.DisposeAndClear();
        }
    }
}