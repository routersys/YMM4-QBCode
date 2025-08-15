using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using YukkuriMovieMaker.Commons;

namespace YMM4QRBarcodePlugin
{
    public partial class ValidationPanel : UserControl, IPropertyEditorControl
    {
        public static readonly DependencyProperty ParameterSetProperty =
            DependencyProperty.Register(nameof(ParameterSet), typeof(QRBarcodeParameter), typeof(ValidationPanel),
                new PropertyMetadata(null, OnParameterSetChanged));

        public QRBarcodeParameter? ParameterSet
        {
            get => (QRBarcodeParameter?)GetValue(ParameterSetProperty);
            set => SetValue(ParameterSetProperty, value);
        }

        private readonly DispatcherTimer _validationTimer;
        private static string? _updateMessage;
        private static bool _updateCheckCompleted = false;
        private static readonly HttpClient _httpClient = new();


        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ValidationPanel()
        {
            InitializeComponent();
            _validationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100), IsEnabled = false };
            _validationTimer.Tick += (s, e) =>
            {
                _validationTimer.Stop();
                Validate();
            };
            if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("YMM4-QBCode", GetCurrentVersion()));
            }
            Loaded += async (s, e) => await CheckForUpdatesAsync();
        }

        private static void OnParameterSetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ValidationPanel panel) return;

            if (e.OldValue is QRBarcodeParameter oldParam)
            {
                oldParam.PropertyChanged -= panel.Parameter_PropertyChanged;
            }
            if (e.NewValue is QRBarcodeParameter newParam)
            {
                newParam.PropertyChanged += panel.Parameter_PropertyChanged;
                panel.Validate();
            }
        }

        private void Parameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _validationTimer.Stop();
            _validationTimer.Start();
        }

        private void Validate()
        {
            if (!string.IsNullOrEmpty(_updateMessage))
            {
                ShowValidation(_updateMessage, "Update");
                return;
            }

            if (ParameterSet is null)
            {
                HideValidation();
                return;
            }

            var (isValid, message) = BarcodeHelper.Validate(ParameterSet.CodeType, ParameterSet.Text);

            if (isValid)
            {
                ShowValidation("問題はありません", "Info");
            }
            else
            {
                ShowValidation(message, "Error");
            }
        }

        private void ShowValidation(string message, string level)
        {
            MainValidationPanel.Tag = level;
            MessageText.Text = message;
            MainValidationPanel.Visibility = Visibility.Visible;
        }

        private void HideValidation()
        {
            MainValidationPanel.Tag = null;
            MainValidationPanel.Visibility = Visibility.Collapsed;
        }

        private static string GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.1";
        }

        private async Task CheckForUpdatesAsync()
        {
            if (_updateCheckCompleted) return;

            try
            {
                var response = await _httpClient.GetAsync("https://api.github.com/repos/routersys/YMM4-QBCode/releases/latest");
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(jsonString);
                var root = jsonDoc.RootElement;
                if (root.TryGetProperty("tag_name", out var tagNameElement))
                {
                    string latestVersionTag = tagNameElement.GetString() ?? "";
                    string latestVersionStr = latestVersionTag.StartsWith("v") ? latestVersionTag.Substring(1) : latestVersionTag;

                    if (root.TryGetProperty("body", out var bodyElement))
                    {
                        string body = bodyElement.GetString() ?? "";
                        body = Regex.Replace(body, @"[#*]", "").Trim();
                        _updateMessage = $"新しいバージョン v{latestVersionStr} が利用可能です\n{body}";
                    }

                    if (Version.TryParse(latestVersionStr, out var latestVersion) &&
                        Version.TryParse(GetCurrentVersion(), out var currentVersion) &&
                        latestVersion > currentVersion)
                    {
                        Validate();
                    }
                }
            }
            catch
            {
            }
            finally
            {
                _updateCheckCompleted = true;
            }
        }
    }
}