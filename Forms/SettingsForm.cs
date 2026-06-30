using ImageGenerator.Models;

namespace ImageGenerator.Forms;

internal partial class SettingsForm : Form
{
    private const int ReferenceWidth = 644;
    private const int ReferenceHeight = 432;
    private const float MinUiScale = 0.78F;
    private const float MaxUiScale = 1.05F;
    private const float DesignDpi = 96F;

    private float _uiScale = 1F;
    private bool _isApplyingResponsiveLayout;
    private bool _wasMinimized;
    private bool _restoreLayoutQueued;

    public AppConfig Result { get; private set; }

    public SettingsForm(AppConfig currentConfig)
    {
        InitializeComponent();

        Result = currentConfig;

        _baseUrlBox.Text = currentConfig.BaseUrl;
        _apiKeyBox.Text = currentConfig.ApiKey;
        _modelBox.Text = currentConfig.Model;
        _outputDirBox.Text = currentConfig.OutputDir;
        _timeoutNumeric.Value = Clamp(currentConfig.TimeoutMinutes, _timeoutNumeric.Minimum, _timeoutNumeric.Maximum);

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

        showKeyBtn.Click += ShowKeyBtn_Click;
        browseBtn.Click += BrowseBtn_Click;
        saveBtn.Click += SaveBtn_Click;
        cancelBtn.Click += CancelBtn_Click;
        _sizeAutoRadio.CheckedChanged += (_, _) => UpdateSizeControlStates();
        _sizePresetRadio.CheckedChanged += (_, _) => UpdateSizeControlStates();
        _sizeCustomRadio.CheckedChanged += (_, _) => UpdateSizeControlStates();
        _transparentBackgroundCheck.CheckedChanged += (_, _) => UpdateTransparentLabel();
        Resize += (_, _) => HandleResponsiveResize();

        ConfigureRoundedButtons();
        Load += (_, _) =>
        {
            FitInitialWindowToScreen();
            ApplyResponsiveLayout();
        };
        UpdateSizeControlStates();
    }

    private void FitInitialWindowToScreen()
    {
        var workArea = Screen.FromControl(this).WorkingArea;
        var maxWidth = Math.Max(MinimumSize.Width, (int)(workArea.Width * 0.9F));
        var maxHeight = Math.Max(MinimumSize.Height, (int)(workArea.Height * 0.9F));
        var targetSize = new Size(Math.Min(Width, maxWidth), Math.Min(Height, maxHeight));

        if (targetSize != Size)
            Size = targetSize;
    }

    private void HandleResponsiveResize()
    {
        if (WindowState == FormWindowState.Minimized)
        {
            _wasMinimized = true;
            return;
        }

        ApplyResponsiveLayout();

        if (_wasMinimized)
        {
            _wasMinimized = false;
            QueueRestoreLayoutRefresh();
        }
    }

    private void QueueRestoreLayoutRefresh()
    {
        if (_restoreLayoutQueued || !IsHandleCreated || IsDisposed)
            return;

        _restoreLayoutQueued = true;
        BeginInvoke(() =>
        {
            _restoreLayoutQueued = false;
            if (IsDisposed || WindowState == FormWindowState.Minimized)
                return;

            ApplyResponsiveLayout();
            ForceTextLayoutRefresh(this);
            Invalidate(true);
        });
    }

    private void ApplyResponsiveLayout()
    {
        if (_isApplyingResponsiveLayout
            || WindowState == FormWindowState.Minimized
            || ClientSize.Width <= 0
            || ClientSize.Height <= 0)
            return;

        _isApplyingResponsiveLayout = true;
        try
        {
            _uiScale = CalculateUiScale();
            var logicalClientSize = GetLogicalClientSize();
            var compact = logicalClientSize.Width < 560 || logicalClientSize.Height < 400;
            var padding = ScaleValue(compact ? 12 : 20);
            var tabPadding = ScaleValue(compact ? 8 : 12);
            var rowHeight = ScaleValue(compact ? 38 : 44);

            SuspendLayout();
            tabs.SuspendLayout();
            basicTable.SuspendLayout();
            sizeTable.SuspendLayout();
            formatTable.SuspendLayout();
            buttonPanel.SuspendLayout();

            Padding = new Padding(padding);
            basicTab.Padding = new Padding(tabPadding);
            sizeTab.Padding = new Padding(tabPadding);
            formatTab.Padding = new Padding(tabPadding);

            SetColumnWidth(basicTable, 0, compact ? 92 : 110);
            SetColumnWidth(basicTable, 2, compact ? 68 : 86);
            SetColumnWidth(sizeTable, 0, compact ? 92 : 110);
            SetColumnWidth(sizeTable, 2, compact ? 88 : 110);
            SetColumnWidth(formatTable, 0, compact ? 98 : 120);

            SetAbsoluteRows(basicTable, 5, rowHeight);
            SetAbsoluteRows(sizeTable, 6, ScaleValue(compact ? 36 : 40));
            SetAbsoluteRows(formatTable, 4, rowHeight);

            ApplyTableControlSpacing(basicTable, rowHeight, compact);
            ApplyTableControlSpacing(sizeTable, ScaleValue(compact ? 36 : 40), compact);
            ApplyTableControlSpacing(formatTable, rowHeight, compact);

            _sizeHelpLabel.MaximumSize = new Size(Math.Max(ScaleValue(240), sizeTable.ClientSize.Width - ScaleValue(12)), 0);

            buttonPanel.Height = ScaleValue(compact ? 40 : 46);
            saveBtn.Size = new Size(ScaleValue(compact ? 82 : 96), ScaleValue(compact ? 30 : 34));
            cancelBtn.Size = saveBtn.Size;
            cancelBtn.Margin = new Padding(0, 0, ScaleValue(compact ? 8 : 12), 0);
            saveBtn.Font = UiFont(compact ? 9F : 10F);
            cancelBtn.Font = UiFont(compact ? 9F : 10F);
            browseBtn.Font = UiFont(compact ? 9F : 10F);
            showKeyBtn.Size = new Size(showKeyBtn.Width, ScaleValue(compact ? 28 : 30));
            UpdateShowKeyIcon();

            var buttonRadius = ScaleValue(8);
            UpdateButtonRegion(showKeyBtn, buttonRadius);
            UpdateButtonRegion(browseBtn, buttonRadius);
            UpdateButtonRegion(saveBtn, buttonRadius);
            UpdateButtonRegion(cancelBtn, buttonRadius);
        }
        finally
        {
            buttonPanel.ResumeLayout(true);
            formatTable.ResumeLayout(true);
            sizeTable.ResumeLayout(true);
            basicTable.ResumeLayout(true);
            tabs.ResumeLayout(true);
            ResumeLayout(true);
            _isApplyingResponsiveLayout = false;
        }
    }

    private float CalculateUiScale()
    {
        var logicalClientSize = GetLogicalClientSize();
        var widthScale = logicalClientSize.Width / ReferenceWidth;
        var heightScale = logicalClientSize.Height / ReferenceHeight;
        return Clamp(Math.Min(widthScale, heightScale), MinUiScale, MaxUiScale);
    }

    private float DpiScale => Math.Max(DesignDpi, DeviceDpi) / DesignDpi;

    private SizeF GetLogicalClientSize() =>
        new(ClientSize.Width / DpiScale, ClientSize.Height / DpiScale);

    private int ScaleValue(int value) =>
        Math.Max(1, (int)Math.Round(value * _uiScale * DpiScale));

    private Font UiFont(float size, FontStyle style = FontStyle.Regular) =>
        new("Microsoft YaHei UI", Math.Max(7.5F, size * _uiScale), style);

    private void SetColumnWidth(TableLayoutPanel table, int columnIndex, int width)
    {
        table.ColumnStyles[columnIndex].SizeType = SizeType.Absolute;
        table.ColumnStyles[columnIndex].Width = ScaleValue(width);
    }

    private void SetAbsoluteRows(TableLayoutPanel table, int fixedRowCount, int height)
    {
        for (var i = 0; i < fixedRowCount && i < table.RowStyles.Count; i++)
        {
            table.RowStyles[i].SizeType = SizeType.Absolute;
            table.RowStyles[i].Height = height;
        }
    }

    private void ApplyTableControlSpacing(TableLayoutPanel table, int rowHeight, bool compact)
    {
        var verticalMargin = ScaleValue(compact ? 5 : 7);
        var rightMargin = ScaleValue(compact ? 6 : 8);

        foreach (Control control in table.Controls)
        {
            if (control is Label label)
            {
                label.Height = rowHeight;
                label.Font = UiFont(compact ? 9F : 10F);
                continue;
            }

            control.Font = UiFont(compact ? 9F : 10F);
            control.Margin = new Padding(0, verticalMargin, rightMargin, verticalMargin);
        }
    }

    private static decimal Clamp(int value, decimal min, decimal max) =>
        Math.Min(max, Math.Max(min, value));

    private static int Clamp(int value, int min, int max) =>
        Math.Min(max, Math.Max(min, value));

    private static float Clamp(float value, float min, float max) =>
        Math.Min(max, Math.Max(min, value));

    private void UpdateShowKeyIcon()
    {
        SetScaledButtonImage(
            showKeyBtn,
            _apiKeyBox.UseSystemPasswordChar ? "visibility-24.png" : "visibility-off-24.png",
            20);
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
            SizeMode = GetSelectedSizeMode(),
            SizeTier = _sizeTierBox.SelectedItem?.ToString() ?? "1K",
            AspectRatio = _aspectRatioBox.SelectedItem?.ToString() ?? "1:1",
            CustomWidth = (int)_customWidthNumeric.Value,
            CustomHeight = (int)_customHeightNumeric.Value,
            OutputFormat = _outputFormatBox.SelectedItem?.ToString() ?? "png",
            TransparentBackground = _transparentBackgroundCheck.Checked,
            Moderation = _moderationBox.SelectedItem?.ToString() ?? "auto",
            ImageCount = (int)_imageCountNumeric.Value,
        };

        if (string.IsNullOrWhiteSpace(Result.BaseUrl))
        {
            MessageBox.Show(this, "API 地址不能为空。", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(Result.ApiKey))
        {
            MessageBox.Show(this, "API Key 不能为空。", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(Result.Model))
        {
            MessageBox.Show(this, "模型名称不能为空。", "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private string GetSelectedSizeMode()
    {
        if (_sizePresetRadio.Checked) return "preset";
        if (_sizeCustomRadio.Checked) return "custom";
        return "auto";
    }
}
