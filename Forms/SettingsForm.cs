using ImageGenerator.Models;
using ImageGenerator.Services;

namespace ImageGenerator.Forms;

internal partial class SettingsForm : Form
{
    private const int ReferenceWidth = 644;
    private const int ReferenceHeight = 432;
    private const float MinUiScale = 0.78F;
    private const float MaxUiScale = 1.05F;
    private const float DesignDpi = 96F;
    private const int ResizeDebounceMs = 50;

    private float _uiScale = 1F;
    private bool _isApplyingResponsiveLayout;
    private bool _wasMinimized;
    private bool _restoreLayoutQueued;
    private readonly Dictionary<(int SizeHundredths, FontStyle Style), Font> _fontCache = [];
    private readonly System.Windows.Forms.Timer _resizeDebounceTimer = new() { Interval = ResizeDebounceMs };
    private int _savedImageCount;
    private ToolTip _contextToolTip = null!;
    private TabPage _contextTab = null!;
    private Panel _contextScrollPanel = null!;
    private TableLayoutPanel _contextTable = null!;
    private ComboBox _contextModelCombo = null!;
    private Button _fetchModelsBtn = null!;
    private NumericUpDown _maxContextTokensNumeric = null!;
    private NumericUpDown _maxOutputTokensNumeric = null!;
    private Label _contextStatusLabel = null!;
    private TextBox _conversationDirBox = null!;
    private Button _conversationBrowseBtn = null!;
    private ToolTip _buttonToolTip = null!;

    private readonly ModelDiscoveryService _modelDiscovery = new();
    private List<ModelContextProfile> _modelProfiles = [];
    private CancellationTokenSource? _fetchModelsCts;
    private bool _isFetchingModels;
    private bool _isLoadingModelProfile;
    private bool _contextStatusIsError;
    private bool _suppressModelFilter;
    private bool _isSyncingModelFields;
    private string _contextEditorModel = "";

    public AppConfig Result { get; private set; }

    public SettingsForm(AppConfig currentConfig)
    {
        InitializeComponent();
        BuildContextTab();
        BuildAppearanceTab();
        BuildAboutTab();

        Result = currentConfig;
        SetAppearanceSelections(currentConfig.Theme, currentConfig.Language);

        _baseUrlBox.Text = currentConfig.BaseUrl;
        _apiKeyBox.Text = currentConfig.ApiKey;
        _modelBox.Text = currentConfig.Model;
        _outputDirBox.Text = currentConfig.OutputDir;
        _timeoutNumeric.Value = Clamp(currentConfig.TimeoutMinutes, _timeoutNumeric.Minimum, _timeoutNumeric.Maximum);
        _verifySslCertificateCheck.Checked = currentConfig.VerifySslCertificate;

        SelectComboValue(_sizeTierBox, currentConfig.SizeTier, "1K");
        SelectComboValue(_aspectRatioBox, currentConfig.AspectRatio, "1:1");
        _customWidthNumeric.Value = Clamp(currentConfig.CustomWidth, _customWidthNumeric.Minimum, _customWidthNumeric.Maximum);
        _customHeightNumeric.Value = Clamp(currentConfig.CustomHeight, _customHeightNumeric.Minimum, _customHeightNumeric.Maximum);
        _sizeAutoRadio.Checked = string.Equals(currentConfig.SizeMode, "auto", StringComparison.OrdinalIgnoreCase);
        _sizePresetRadio.Checked = string.Equals(currentConfig.SizeMode, "preset", StringComparison.OrdinalIgnoreCase);
        _sizeCustomRadio.Checked = !_sizeAutoRadio.Checked && !_sizePresetRadio.Checked;

        SelectComboValue(_outputFormatBox, currentConfig.OutputFormat, "png");
        _transparentBackgroundCheck.Checked = currentConfig.TransparentBackground;
        UpdateTransparentLabel();
        SelectComboValue(_moderationBox, currentConfig.Moderation, "auto");
        _imageCountNumeric.Value = Clamp(currentConfig.ImageCount, _imageCountNumeric.Minimum, _imageCountNumeric.Maximum);
        _concurrentCheck.Checked = currentConfig.UseConcurrentStrategy;
        _concurrencyNumeric.Value = Clamp(currentConfig.MaxConcurrency, _concurrencyNumeric.Minimum, _concurrencyNumeric.Maximum);
        _conversationDirBox.Text = currentConfig.ConversationStoreDir;
        LoadModelProfiles(currentConfig);

        showKeyBtn.Click += ShowKeyBtn_Click;
        browseBtn.Click += BrowseBtn_Click;
        _conversationBrowseBtn.Click += ConversationBrowseBtn_Click;
        _modelBox.TextChanged += (_, _) => SyncContextTabWithModelField();
        saveBtn.Click += SaveBtn_Click;
        cancelBtn.Click += CancelBtn_Click;
        _sizeAutoRadio.CheckedChanged += (_, _) => UpdateSizeControlStates();
        _sizePresetRadio.CheckedChanged += (_, _) => UpdateSizeControlStates();
        _sizeCustomRadio.CheckedChanged += (_, _) => UpdateSizeControlStates();
        _transparentBackgroundCheck.CheckedChanged += (_, _) => UpdateTransparentLabel();
        _concurrentCheck.CheckedChanged += (_, _) => UpdateConcurrencyControlStates();
        _resizeDebounceTimer.Tick += (_, _) => FlushResponsiveResize();
        Resize += (_, _) => HandleResponsiveResize();
        tabs.SelectedIndexChanged += (_, _) => ApplyResponsiveLayout();

        ConfigureRoundedButtons();
        _buttonToolTip = new ToolTip(components);
        ApplyButtonIcons();
        ApplyLocalization();
        ApplyTheme();
        Load += (_, _) =>
        {
            FitInitialWindowToScreen();
            ApplyResponsiveLayout();
        };
        UpdateSizeControlStates();
        UpdateConcurrencyControlStates();
    }

    private void UpdateShowKeyIcon()
    {
        SetScaledButtonImage(
            showKeyBtn,
            _apiKeyBox.UseSystemPasswordChar ? "visibility-24.png" : "visibility-off-24.png",
            20);
        showKeyBtn.Text = "";
        showKeyBtn.ImageAlign = ContentAlignment.MiddleCenter;
        showKeyBtn.TextImageRelation = TextImageRelation.Overlay;
        showKeyBtn.Padding = Padding.Empty;
        _buttonToolTip?.SetToolTip(showKeyBtn, _apiKeyBox.UseSystemPasswordChar ? T("ShowApiKey") : T("HideApiKey"));
    }

    private void SetScaledButtonImage(Button button, string fileName, int logicalSize)
    {
        var pixelSize = ScaleValue(logicalSize);
        var imageKey = $"{fileName}:{pixelSize}";
        if (button.Tag as string == imageKey)
            return;

        var oldImage = button.Image;
        button.Image = LoadIconImage(fileName, pixelSize);
        button.Tag = imageKey;
        oldImage?.Dispose();
    }

    private static Image LoadIconImage(string fileName, int pixelSize)
    {
        using var source = LoadIconImage(fileName);
        var bitmap = new Bitmap(pixelSize, pixelSize);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(0, 0, pixelSize, pixelSize));
        return bitmap;
    }

    private static void SelectComboValue(ComboBox comboBox, string? value, string fallback)
    {
        var selected = string.IsNullOrWhiteSpace(value) ? fallback : value;
        var index = comboBox.Items.IndexOf(selected);
        comboBox.SelectedIndex = index >= 0 ? index : comboBox.Items.IndexOf(fallback);
        if (comboBox.SelectedIndex < 0 && comboBox.Items.Count > 0)
            comboBox.SelectedIndex = 0;
    }

    private void ConfigureRoundedButtons()
    {
        ApplyRoundedButtonStyle(showKeyBtn, 8);
        ApplyRoundedButtonStyle(browseBtn, 8);
        ApplyRoundedButtonStyle(_conversationBrowseBtn, 8);
        ApplyRoundedButtonStyle(_fetchModelsBtn, 8);
        ApplyRoundedButtonStyle(saveBtn, 8);
        ApplyRoundedButtonStyle(cancelBtn, 8);
    }

    private static void ApplyRoundedButtonStyle(Button button, int radius)
    {
        button.Resize += (_, _) => UpdateButtonRegion(button, radius);
        button.HandleCreated += (_, _) => UpdateButtonRegion(button, radius);
        UpdateButtonRegion(button, radius);
    }

    private static void UpdateButtonRegion(Button button, int radius)
    {
        if (button.Width <= 0 || button.Height <= 0)
            return;

        var diameter = Math.Min(radius * 2, Math.Min(button.Width, button.Height));
        var bounds = new Rectangle(0, 0, button.Width, button.Height);

        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        var oldRegion = button.Region;
        button.Region = new Region(path);
        oldRegion?.Dispose();
    }

    private void UpdateSizeControlStates()
    {
        var presetEnabled = _sizePresetRadio.Checked;
        var customEnabled = _sizeCustomRadio.Checked;
        _sizeTierBox.Enabled = presetEnabled;
        _aspectRatioBox.Enabled = presetEnabled;
        _customWidthNumeric.Enabled = customEnabled;
        _customHeightNumeric.Enabled = customEnabled;
    }

    private void UpdateTransparentLabel()
    {
        _transparentBackgroundCheck.Text = _transparentBackgroundCheck.Checked ? "true" : "false";
    }

    private void UpdateConcurrencyControlStates()
    {
        _concurrentCheck.Text = _concurrentCheck.Checked ? "true" : "false";
        _concurrencyNumeric.Enabled = _concurrentCheck.Checked;
        if (_concurrentCheck.Checked)
        {
            _imageCountNumeric.Enabled = true;
            if (_savedImageCount > 0)
            {
                _imageCountNumeric.Value = Clamp(
                    _savedImageCount,
                    _imageCountNumeric.Minimum,
                    _imageCountNumeric.Maximum);
            }

            return;
        }

        if (_imageCountNumeric.Enabled || _savedImageCount <= 0)
            _savedImageCount = (int)_imageCountNumeric.Value;

        _imageCountNumeric.Value = 1;
        _imageCountNumeric.Enabled = false;
    }

    private void ShowKeyBtn_Click(object? sender, EventArgs e)
    {
        _apiKeyBox.UseSystemPasswordChar = !_apiKeyBox.UseSystemPasswordChar;
        UpdateShowKeyIcon();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyResponsiveLayout();
        QueueRestoreLayoutRefresh();
    }

    private static void ForceTextLayoutRefresh(Control root)
    {
        foreach (Control child in root.Controls)
            ForceTextLayoutRefresh(child);

        root.PerformLayout();
        root.Invalidate();
    }

    private void BrowseBtn_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog();
        if (!string.IsNullOrWhiteSpace(_outputDirBox.Text) && Directory.Exists(_outputDirBox.Text))
            dlg.SelectedPath = _outputDirBox.Text;
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _outputDirBox.Text = dlg.SelectedPath;
    }

    private void ConversationBrowseBtn_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog();
        if (!string.IsNullOrWhiteSpace(_conversationDirBox.Text) && Directory.Exists(_conversationDirBox.Text))
            dlg.SelectedPath = _conversationDirBox.Text;
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _conversationDirBox.Text = dlg.SelectedPath;
    }

    private void CancelBtn_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void SaveBtn_Click(object? sender, EventArgs e)
    {
        Result = new AppConfig
        {
            BaseUrl = _baseUrlBox.Text.Trim(),
            ApiKey = _apiKeyBox.Text.Trim(),
            Model = _modelBox.Text.Trim(),
            OutputDir = _outputDirBox.Text.Trim(),
            TimeoutMinutes = (int)_timeoutNumeric.Value,
            VerifySslCertificate = _verifySslCertificateCheck.Checked,
            SizeMode = GetSelectedSizeMode(),
            SizeTier = _sizeTierBox.SelectedItem?.ToString() ?? "1K",
            AspectRatio = _aspectRatioBox.SelectedItem?.ToString() ?? "1:1",
            CustomWidth = (int)_customWidthNumeric.Value,
            CustomHeight = (int)_customHeightNumeric.Value,
            OutputFormat = _outputFormatBox.SelectedItem?.ToString() ?? "png",
            TransparentBackground = _transparentBackgroundCheck.Checked,
            Moderation = _moderationBox.SelectedItem?.ToString() ?? "auto",
            ImageCount = (int)_imageCountNumeric.Value,
            UseConcurrentStrategy = _concurrentCheck.Checked,
            MaxConcurrency = (int)_concurrencyNumeric.Value,
            ConversationStoreDir = string.IsNullOrWhiteSpace(_conversationDirBox.Text)
                ? "conversations"
                : _conversationDirBox.Text.Trim(),
            LastConversationId = Result.LastConversationId,
            ModelProfiles = BuildModelProfilesForSave(_modelBox.Text.Trim()),
            Theme = SelectedTheme,
            Language = SelectedLanguage,
        };

        if (string.IsNullOrWhiteSpace(Result.BaseUrl))
        {
            MessageBox.Show(this, T("ApiUrlRequired"), T("ValidationFailed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(Result.ApiKey))
        {
            MessageBox.Show(this, T("ApiKeyRequired"), T("ValidationFailed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(Result.Model))
        {
            MessageBox.Show(this, T("ModelRequired"), T("ValidationFailed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_maxOutputTokensNumeric.Value >= _maxContextTokensNumeric.Value)
        {
            MessageBox.Show(this, T("MaxOutputTokensInvalid"), T("ValidationFailed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!TryValidateWritableDirectory(Result.OutputDir, Path.GetTempPath(), out var outputDirError))
        {
            MessageBox.Show(this, string.Format(T("OutputDirInvalid"), outputDirError), T("ValidationFailed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!TryValidateWritableDirectory(Result.ConversationStoreDir, "conversations", out var conversationDirError))
        {
            MessageBox.Show(this, string.Format(T("ConversationDirInvalid"), conversationDirError), T("ValidationFailed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static bool TryValidateWritableDirectory(string? configuredPath, string fallbackPath, out string error)
    {
        error = "";
        try
        {
            var effectivePath = string.IsNullOrWhiteSpace(configuredPath)
                ? fallbackPath
                : configuredPath.Trim();
            var fullPath = Path.IsPathRooted(effectivePath)
                ? effectivePath
                : Path.Combine(AppContext.BaseDirectory, effectivePath);

            Directory.CreateDirectory(fullPath);
            var probePath = Path.Combine(fullPath, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "");
            File.Delete(probePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            error = ex.Message;
            return false;
        }
    }

    private string GetSelectedSizeMode()
    {
        if (_sizePresetRadio.Checked) return "preset";
        if (_sizeCustomRadio.Checked) return "custom";
        return "auto";
    }
}
