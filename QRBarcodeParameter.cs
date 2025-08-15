using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;
using ZXing;

namespace YMM4QRBarcodePlugin
{
    public enum QRBarcodeType
    {
        [Description("QRコード")] QRCode = 0,
        [Description("Code128")] Code128 = 1,
        [Description("Code39")] Code39 = 2,
        [Description("EAN13")] EAN13 = 3,
        [Description("EAN8")] EAN8 = 4,
        [Description("UPC-A")] UPCA = 5,
        [Description("UPC-E")] UPCE = 6,
        [Description("ITF")] ITF = 7,
        [Description("Codabar")] Codabar = 8,
        [Description("PDF417")] PDF417 = 9,
        [Description("DataMatrix")] DataMatrix = 10
    }

    public enum QRErrorCorrectionLevel
    {
        [Description("低（7%）")] Low = 0,
        [Description("中（15%）")] Medium = 1,
        [Description("標準（25%）")] Quartile = 2,
        [Description("高（30%）")] High = 3
    }

    public static class BarcodeHelper
    {
        public static BarcodeFormat GetBarcodeFormat(QRBarcodeType codeType) => codeType switch
        {
            QRBarcodeType.Code128 => BarcodeFormat.CODE_128,
            QRBarcodeType.Code39 => BarcodeFormat.CODE_39,
            QRBarcodeType.EAN13 => BarcodeFormat.EAN_13,
            QRBarcodeType.EAN8 => BarcodeFormat.EAN_8,
            QRBarcodeType.UPCA => BarcodeFormat.UPC_A,
            QRBarcodeType.UPCE => BarcodeFormat.UPC_E,
            QRBarcodeType.ITF => BarcodeFormat.ITF,
            QRBarcodeType.Codabar => BarcodeFormat.CODABAR,
            QRBarcodeType.PDF417 => BarcodeFormat.PDF_417,
            QRBarcodeType.DataMatrix => BarcodeFormat.DATA_MATRIX,
            _ => BarcodeFormat.QR_CODE
        };

        public static bool IsSquareFormat(QRBarcodeType codeType)
        {
            return codeType == QRBarcodeType.QRCode ||
                   codeType == QRBarcodeType.DataMatrix;
        }

        public static (bool, string) Validate(QRBarcodeType type, string text)
        {
            if (string.IsNullOrEmpty(text))
                return (false, "テキストが空です");

            switch (type)
            {
                case QRBarcodeType.EAN13:
                    if (!text.All(char.IsDigit)) return (false, "EAN-13は数字のみ使用できます。");
                    if (text.Length != 12 && text.Length != 13) return (false, "EAN-13は12桁または13桁(チェックデジット込)である必要があります");
                    break;
                case QRBarcodeType.EAN8:
                    if (!text.All(char.IsDigit)) return (false, "EAN-8は数字のみ使用できます。");
                    if (text.Length != 7 && text.Length != 8) return (false, "EAN-8は7桁または8桁(チェックデジット込)である必要があります");
                    break;
                case QRBarcodeType.UPCA:
                    if (!text.All(char.IsDigit)) return (false, "UPC-Aは数字のみ使用できます。");
                    if (text.Length != 11 && text.Length != 12) return (false, "UPC-Aは11桁または12桁(チェックデジット込)である必要があります");
                    break;
                case QRBarcodeType.UPCE:
                    if (!text.All(char.IsDigit)) return (false, "UPC-Eは数字のみ使用できます。");
                    if (text.Length != 7 && text.Length != 8) return (false, "UPC-Eは7桁または8桁(チェックデジット込)である必要があります");
                    break;
                case QRBarcodeType.ITF:
                    if (!text.All(char.IsDigit)) return (false, "ITFには数字のみ使用できます");
                    if (text.Length % 2 != 0) return (false, "ITFの桁数は偶数である必要があります");
                    break;
                case QRBarcodeType.Codabar:
                    if (!Regex.IsMatch(text, @"^[A-D][0-9\-\$\:\/\.\+]+[A-D]$", RegexOptions.IgnoreCase)) return (false, "CodabarはA,B,C,Dで始まり、終わる必要があります");
                    break;
                case QRBarcodeType.Code39:
                    if (!Regex.IsMatch(text, @"^[\w\s\-\.\$\/\+\%]+$")) return (false, "Code39で無効な文字が使用されています");
                    break;
            }
            return (true, string.Empty);
        }
    }

    public class QRBarcodeParameter : ShapeParameterBase
    {
        private bool isUpdatingSize = false;

        [Display(GroupName = "基本設定", Name = "", Description = "現在の設定に問題がある場合に警告やエラーを表示します。", Order = 0)]
        [ValidationPanelEditor(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public bool ValidationPlaceholder { get; set; }

        [Display(GroupName = "基本設定", Name = "種類", Description = "生成するコードの種類を選択します。", Order = 1)]
        [EnumComboBox]
        public QRBarcodeType CodeType { get => codeType; set => Set(ref codeType, value); }
        private QRBarcodeType codeType = QRBarcodeType.QRCode;

        [Display(GroupName = "基本設定", Name = "テキスト", Description = "コードに変換する文字列を入力します。", Order = 2)]
        [TextEditor]
        public string Text { get => text; set => Set(ref text, value ?? ""); }
        private string text = "";

        [Display(GroupName = "サイズ", Name = "幅", Description = "コードの幅を指定します。", Order = 3)]
        [AnimationSlider("F0", "px", 10, 2000)]
        public Animation Width { get; } = new Animation(300, 10, 2000);

        [Display(GroupName = "サイズ", Name = "サイズを同期", Description = "オンにすると、幅と高さが同じ値に保たれます。正方形フォーマット（QRコード、DataMatrix）で有効です。", Order = 4)]
        [ToggleSlider]
        public bool IsSizeSynced { get => isSizeSynced; set => Set(ref isSizeSynced, value); }
        private bool isSizeSynced = true;

        [Display(GroupName = "サイズ", Name = "高さ", Description = "コードの高さを指定します。", Order = 5)]
        [AnimationSlider("F0", "px", 10, 2000)]
        public Animation Height { get; } = new Animation(300, 10, 2000);

        [Display(GroupName = "表示設定", Name = "エラー訂正レベル", Description = "QRコードの汚れや破損に対する耐性を設定します。レベルが高いほど多くの情報を訂正できますが、コードが複雑になります。", Order = 6)]
        [EnumComboBox]
        public QRErrorCorrectionLevel ErrorCorrectionLevel { get => errorCorrectionLevel; set => Set(ref errorCorrectionLevel, value); }
        private QRErrorCorrectionLevel errorCorrectionLevel = QRErrorCorrectionLevel.Medium;

        [Display(GroupName = "表示設定", Name = "マージン", Description = "コードの周囲の余白の大きさを指定します。", Order = 7)]
        [AnimationSlider("F0", "px", 0, 50)]
        public Animation Margin { get; } = new Animation(10, 0, 100);

        [Display(GroupName = "テキスト設定", Name = "テキスト表示", Description = "オンにすると、一部のバーコードの下にテキストを表示します。", Order = 10)]
        [ToggleSlider]
        public bool ShowText { get => showText; set => Set(ref showText, value); }
        private bool showText = true;

        [Display(GroupName = "テキスト設定", Name = "テキストサイズ", Description = "バーコードの下に表示されるテキストの大きさを指定します。", Order = 11)]
        [AnimationSlider("F0", "pt", 1, 100)]
        public Animation FontSize { get; } = new(12, 1, 200);

        [Display(GroupName = "テキスト設定", Name = "フォント", Description = "バーコードの下に表示されるテキストのフォントを選択します。", Order = 12)]
        [FontComboBox]
        public string Font { get => font; set => Set(ref font, value); }
        private string font = "Arial";

        public QRBarcodeParameter() : this(null) { }

        public QRBarcodeParameter(SharedDataStore? sharedData) : base(sharedData)
        {
            Width.PropertyChanged += (s, e) => HandleSizeChanged(Width);
            Height.PropertyChanged += (s, e) => HandleSizeChanged(Height);
            PropertyChanged += OnParameterPropertyChanged;
        }

        private void HandleSizeChanged(Animation source)
        {
            if (isUpdatingSize || !IsSizeSynced) return;
            if (!BarcodeHelper.IsSquareFormat(CodeType))
            {
                IsSizeSynced = false;
                return;
            }

            isUpdatingSize = true;
            if (source == Width)
            {
                Height.CopyFrom(Width);
            }
            else
            {
                Width.CopyFrom(Height);
            }
            isUpdatingSize = false;
        }

        private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CodeType))
            {
                Text = "";
                if (!BarcodeHelper.IsSquareFormat(CodeType) && IsSizeSynced)
                {
                    IsSizeSynced = false;
                }
            }

            if (e.PropertyName == nameof(IsSizeSynced) && IsSizeSynced)
            {
                if (!BarcodeHelper.IsSquareFormat(CodeType))
                {
                    IsSizeSynced = false;
                    return;
                }

                if (isUpdatingSize) return;
                isUpdatingSize = true;
                Height.CopyFrom(Width);
                isUpdatingSize = false;
            }
        }

        public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskParameters) => [];
        public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc) => [];
        public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices) => new QRBarcodeSource(devices, this);
        protected override IEnumerable<IAnimatable> GetAnimatables() => [Width, Height, Margin, FontSize];

        protected override void LoadSharedData(SharedDataStore store)
        {
            var data = store.Load<SharedData>();
            data?.CopyTo(this);
        }

        protected override void SaveSharedData(SharedDataStore store) => store.Save(new SharedData(this));

        public class SharedData
        {
            public QRBarcodeType CodeType { get; }
            public string Text { get; }
            public Animation Width { get; } = new(300, 10, 2000);
            public Animation Height { get; } = new(300, 10, 2000);
            public bool IsSizeSynced { get; }
            public QRErrorCorrectionLevel ErrorCorrectionLevel { get; }
            public Animation Margin { get; } = new(10, 0, 100);
            public Animation FontSize { get; } = new(12, 1, 200);
            public string Font { get; }
            public bool ShowText { get; }

            public SharedData(QRBarcodeParameter p)
            {
                CodeType = p.CodeType;
                Text = p.Text;
                Width.CopyFrom(p.Width);
                Height.CopyFrom(p.Height);
                IsSizeSynced = p.IsSizeSynced;
                ErrorCorrectionLevel = p.ErrorCorrectionLevel;
                Margin.CopyFrom(p.Margin);
                FontSize.CopyFrom(p.FontSize);
                Font = p.Font;
                ShowText = p.ShowText;
            }

            public void CopyTo(QRBarcodeParameter p)
            {
                p.CodeType = CodeType;
                p.Text = Text;
                p.Width.CopyFrom(Width);
                p.Height.CopyFrom(Height);
                p.IsSizeSynced = IsSizeSynced;
                p.ErrorCorrectionLevel = ErrorCorrectionLevel;
                p.Margin.CopyFrom(Margin);
                p.FontSize.CopyFrom(FontSize);
                p.Font = Font;
                p.ShowText = ShowText;
            }
        }
    }
}