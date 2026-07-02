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
    private readonly Dictionary<(int SizeHundredths, FontStyle Style), Font> _fontCache = [];
    private int _savedImageCount;
    private ToolTip _contextToolTip = null!;
    private TabPage _contextTab = null!;
    private Panel _contextScrollPanel = null!;
    private TableLayoutPanel _contextTable = null!;
    private TextBox _conversationDirBox = null!;
    private Button _conversationBrowseBtn = null!;
    private NumericUpDown _maxActiveMessagesNumeric = null!;
    private NumericUpDown _compressionTriggerNumeric = null!;
    private NumericUpDown _keepRecentNumeric = null!;
    private NumericUpDown _maxContextPromptsNumeric = null!;
    private NumericUpDown _maxContextImagesNumeric = null!;
    private NumericUpDown _contextAutoAttachThresholdNumeric = null!;
    private CheckBox _referenceDetectionCheck = null!;
    private CheckBox _promptEnhancementCheck = null!;
    private CheckBox _showContextDecisionHintCheck = null!;
    private CheckBox _allowHistoryImagesWithManualAttachmentsCheck = null!;

    public AppConfig Result { get; private set; }

    public SettingsForm(AppConfig currentConfig)
    {
        InitializeComponent();
        BuildContextTab();

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
        _concurrentCheck.Checked = currentConfig.UseConcurrentStrategy;
        _concurrencyNumeric.Value = Clamp(currentConfig.MaxConcurrency, _concurrencyNumeric.Minimum, _concurrencyNumeric.Maximum);
        _conversationDirBox.Text = currentConfig.ConversationStoreDir;
        _maxActiveMessagesNumeric.Value = Clamp(currentConfig.MaxActiveMessages, _maxActiveMessagesNumeric.Minimum, _maxActiveMessagesNumeric.Maximum);
        _compressionTriggerNumeric.Value = Clamp(currentConfig.CompressionTriggerCount, _compressionTriggerNumeric.Minimum, _compressionTriggerNumeric.Maximum);
        _keepRecentNumeric.Value = Clamp(currentConfig.KeepRecentCount, _keepRecentNumeric.Minimum, _keepRecentNumeric.Maximum);
        _maxContextPromptsNumeric.Value = Clamp(currentConfig.MaxContextPrompts, _maxContextPromptsNumeric.Minimum, _maxContextPromptsNumeric.Maximum);
        _maxContextImagesNumeric.Value = Clamp(currentConfig.MaxContextImages, _maxContextImagesNumeric.Minimum, _maxContextImagesNumeric.Maximum);
        _contextAutoAttachThresholdNumeric.Value = Clamp(currentConfig.ContextAutoAttachThreshold, _contextAutoAttachThresholdNumeric.Minimum, _contextAutoAttachThresholdNumeric.Maximum);
        _referenceDetectionCheck.Checked = currentConfig.EnableReferenceDetection;
        _promptEnhancementCheck.Checked = currentConfig.EnablePromptEnhancement;
        _showContextDecisionHintCheck.Checked = currentConfig.ShowContextDecisionHint;
        _allowHistoryImagesWithManualAttachmentsCheck.Checked = currentConfig.AllowHistoryImagesWithManualAttachments;

        showKeyBtn.Click += ShowKeyBtn_Click;
        browseBtn.Click += BrowseBtn_Click;
        _conversationBrowseBtn.Click += ConversationBrowseBtn_Click;
        saveBtn.Click += SaveBtn_Click;
        cancelBtn.Click += CancelBtn_Click;
        _sizeAutoRadio.CheckedChanged += (_, _) => UpdateSizeControlStates();
        _sizePresetRadio.CheckedChanged += (_, _) => UpdateSizeControlStates();
        _sizeCustomRadio.CheckedChanged += (_, _) => UpdateSizeControlStates();
        _transparentBackgroundCheck.CheckedChanged += (_, _) => UpdateTransparentLabel();
        _concurrentCheck.CheckedChanged += (_, _) => UpdateConcurrencyControlStates();
        Resize += (_, _) => HandleResponsiveResize();

        ConfigureRoundedButtons();
        Load += (_, _) =>
        {
            FitInitialWindowToScreen();
            ApplyResponsiveLayout();
        };
        UpdateSizeControlStates();
        UpdateConcurrencyControlStates();
    }

    private void BuildContextTab()
    {
        _contextToolTip = new ToolTip(components)
        {
            AutoPopDelay = 12000,
            InitialDelay = 350,
            ReshowDelay = 120,
            ShowAlways = true,
        };

        _contextTab = new TabPage
        {
            Text = "上下文",
            Padding = new Padding(12),
            UseVisualStyleBackColor = true,
        };

        _contextTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 12,
        };
        _contextTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        _contextTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _contextTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
        _contextTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32F));
        for (var i = 0; i < 11; i++)
            _contextTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        _contextTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));

        _contextScrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            AutoScrollMargin = new Size(0, 8),
            Padding = new Padding(0, 0, 0, 8),
        };

        _conversationDirBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
        _conversationBrowseBtn = new Button
        {
            Text = "浏览...",
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Size = new Size(86, 28),
        };
        ApplyBrowseButtonStyle(_conversationBrowseBtn);
        _maxActiveMessagesNumeric = CreateContextNumeric(5, 100, 20);
        _compressionTriggerNumeric = CreateContextNumeric(10, 200, 30);
        _keepRecentNumeric = CreateContextNumeric(3, 50, 10);
        _maxContextPromptsNumeric = CreateContextNumeric(0, 20, 5);
        _maxContextImagesNumeric = CreateContextNumeric(1, 5, 1);
        _contextAutoAttachThresholdNumeric = new NumericUpDown
        {
            Minimum = 0.10M,
            Maximum = 0.95M,
            Increment = 0.05M,
            DecimalPlaces = 2,
            Value = 0.55M,
            Size = new Size(96, 24),
            Anchor = AnchorStyles.Left,
        };
        _referenceDetectionCheck = CreateContextCheckBox();
        _promptEnhancementCheck = CreateContextCheckBox();
        _showContextDecisionHintCheck = CreateContextCheckBox();
        _allowHistoryImagesWithManualAttachmentsCheck = CreateContextCheckBox();

        AddContextRow(_contextTable, "会话存储目录", _conversationDirBox, 0,
            "保存本地会话、历史提示词和生成记录的位置。切换目录后，新旧会话不会自动合并。",
            1);
        _contextTable.Controls.Add(_conversationBrowseBtn, 2, 0);
        AddContextRow(_contextTable, "活跃消息数", _maxActiveMessagesNumeric, 1,
            "当前会话中直接参与上下文分析的最近消息数量。数值越大，连续性越强，但上下文也更容易变杂。");
        AddContextRow(_contextTable, "压缩触发阈值", _compressionTriggerNumeric, 2,
            "当消息数量超过该值时，较早历史会被压缩成摘要，避免会话无限变长。");
        AddContextRow(_contextTable, "保留最近消息", _keepRecentNumeric, 3,
            "压缩历史时始终保留的最近消息数量。建议小于压缩触发阈值。");
        AddContextRow(_contextTable, "最近提示词数", _maxContextPromptsNumeric, 4,
            "允许注入到上下文中的最近用户提示词数量。设为 0 时不注入最近提示词。");
        AddContextRow(_contextTable, "最多历史参考图", _maxContextImagesNumeric, 5,
            "自动附加历史生成图的上限。gpt-image-2 会处理图像输入，数量越多成本和耗时越高。");
        AddContextRow(_contextTable, "自动附图阈值", _contextAutoAttachThresholdNumeric, 6,
            "上下文决策置信度达到该值才自动附加历史图。越高越保守，越低越积极。");
        AddContextRow(_contextTable, "智能引用历史图", _referenceDetectionCheck, 7,
            "开启后，系统会判断本轮请求是否依赖最近生成图，并在需要时自动附加历史图作为参考图。");
        AddContextRow(_contextTable, "提示词上下文", _promptEnhancementCheck, 8,
            "开启后，系统会把会话摘要和最近提示词整理进请求，帮助模型理解连续创作意图。");
        AddContextRow(_contextTable, "显示决策提示", _showContextDecisionHintCheck, 9,
            "生成前显示本次是否使用了历史图或文本上下文，便于理解自动行为。");
        AddContextRow(_contextTable, "附件叠加历史图", _allowHistoryImagesWithManualAttachmentsCheck, 10,
            "开启后，即使用户手动上传了图片，系统也可能额外附加相关历史图。默认关闭，避免混入不需要的参考图。");

        var helpLabel = new Label
        {
            Text = "智能上下文会根据本轮提示词、最近提示词、历史生成图和手动附件自动判断是否需要补充上下文。",
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _contextTable.SetColumnSpan(helpLabel, 4);
        _contextTable.Controls.Add(helpLabel, 0, 11);

        _contextScrollPanel.Controls.Add(_contextTable);
        _contextTab.Controls.Add(_contextScrollPanel);
        tabs.Controls.Add(_contextTab);
    }

    private static NumericUpDown CreateContextNumeric(int min, int max, int value) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = value,
        Size = new Size(96, 24),
        Anchor = AnchorStyles.Left,
    };

    private static CheckBox CreateContextCheckBox() => new()
    {
        Text = "启用",
        Anchor = AnchorStyles.Left,
        AutoSize = true,
    };

    private void AddContextRow(
        TableLayoutPanel table,
        string labelText,
        Control control,
        int row,
        string helpText,
        int controlColumnSpan = 2)
    {
        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var help = new Label
        {
            Text = "?",
            Tag = "context-help",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Help,
            ForeColor = Color.FromArgb(37, 99, 235),
        };
        _contextToolTip.SetToolTip(label, helpText);
        _contextToolTip.SetToolTip(control, helpText);
        _contextToolTip.SetToolTip(help, helpText);

        table.Controls.Add(label, 0, row);
        table.Controls.Add(control, 1, row);
        if (controlColumnSpan > 1)
            table.SetColumnSpan(control, controlColumnSpan);
        table.Controls.Add(help, 3, row);
    }

    private void ApplyBrowseButtonStyle(Button button)
    {
        button.FlatStyle = browseBtn.FlatStyle;
        button.BackColor = browseBtn.BackColor;
        button.ForeColor = browseBtn.ForeColor;
        button.Font = browseBtn.Font;
        button.TextAlign = browseBtn.TextAlign;
        button.UseVisualStyleBackColor = browseBtn.UseVisualStyleBackColor;
        button.FlatAppearance.BorderSize = browseBtn.FlatAppearance.BorderSize;
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
            _contextScrollPanel.SuspendLayout();
            _contextTable.SuspendLayout();
            buttonPanel.SuspendLayout();

            Padding = new Padding(padding);
            basicTab.Padding = new Padding(tabPadding);
            sizeTab.Padding = new Padding(tabPadding);
            formatTab.Padding = new Padding(tabPadding);
            _contextTab.Padding = new Padding(tabPadding);

            SetColumnWidth(basicTable, 0, compact ? 92 : 110);
            SetColumnWidth(basicTable, 2, compact ? 68 : 86);
            SetColumnWidth(_contextTable, 0, compact ? 112 : 150);
            SetColumnWidth(_contextTable, 2, compact ? 68 : 86);
            SetColumnWidth(_contextTable, 3, compact ? 28 : 32);
            SetColumnWidth(sizeTable, 0, compact ? 92 : 110);
            SetColumnWidth(sizeTable, 2, compact ? 88 : 110);
            SetColumnWidth(formatTable, 0, compact ? 98 : 120);

            SetAbsoluteRows(basicTable, 5, rowHeight);
            SetAbsoluteRows(_contextTable, 11, compact ? ScaleValue(38) : rowHeight);
            _contextTable.RowStyles[11].SizeType = SizeType.Absolute;
            _contextTable.RowStyles[11].Height = ScaleValue(compact ? 48 : 54);
            SetAbsoluteRows(sizeTable, 6, ScaleValue(compact ? 36 : 40));
            SetAbsoluteRows(formatTable, 6, rowHeight);
            formatTable.RowStyles[6].SizeType = SizeType.Absolute;
            formatTable.RowStyles[6].Height = ScaleValue(compact ? 76 : 72);
            var contextBottomGap = ScaleValue(compact ? 10 : 12);
            _contextScrollPanel.AutoScrollMargin = new Size(0, contextBottomGap);
            _contextScrollPanel.Padding = new Padding(0, 0, 0, contextBottomGap);

            ApplyTableControlSpacing(basicTable, rowHeight, compact);
            ApplyTableControlSpacing(_contextTable, compact ? ScaleValue(38) : rowHeight, compact);
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
            _conversationBrowseBtn.Font = UiFont(compact ? 9F : 10F);
            showKeyBtn.Size = new Size(showKeyBtn.Width, ScaleValue(compact ? 28 : 30));
            UpdateShowKeyIcon();

            var buttonRadius = ScaleValue(8);
            UpdateButtonRegion(showKeyBtn, buttonRadius);
            UpdateButtonRegion(browseBtn, buttonRadius);
            UpdateButtonRegion(_conversationBrowseBtn, buttonRadius);
            UpdateButtonRegion(saveBtn, buttonRadius);
            UpdateButtonRegion(cancelBtn, buttonRadius);
        }
        finally
        {
            buttonPanel.ResumeLayout(true);
            _contextTable.ResumeLayout(true);
            _contextScrollPanel.ResumeLayout(true);
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

    private Font UiFont(float size, FontStyle style = FontStyle.Regular)
    {
        var fontSize = Math.Max(7.5F, size * _uiScale);
        var sizeKey = (int)Math.Round(fontSize * 100F);
        var key = (sizeKey, style);
        if (_fontCache.TryGetValue(key, out var font))
            return font;

        font = new Font("Microsoft YaHei UI", sizeKey / 100F, style);
        _fontCache[key] = font;
        return font;
    }

    private void DisposeCachedResources()
    {
        foreach (var font in _fontCache.Values)
            font.Dispose();

        _fontCache.Clear();
    }

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
                label.Font = Equals(label.Tag, "context-help")
                    ? UiFont(compact ? 8.5F : 9F, FontStyle.Bold)
                    : label == lblConcurrencyHint
                    ? UiFont(8F)
                    : UiFont(compact ? 9F : 10F);
                continue;
            }

            control.Font = UiFont(compact ? 9F : 10F);
            if (control is Button button)
                button.TextAlign = ContentAlignment.MiddleCenter;

            var column = table.GetColumn(control);
            var trailingMargin = column == table.ColumnCount - 1 ? 0 : rightMargin;
            control.Margin = new Padding(0, verticalMargin, trailingMargin, verticalMargin);
        }
    }

    private static decimal Clamp(int value, decimal min, decimal max) =>
        Math.Min(max, Math.Max(min, value));

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
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
        ApplyRoundedButtonStyle(_conversationBrowseBtn, 8);
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
            MaxActiveMessages = (int)_maxActiveMessagesNumeric.Value,
            CompressionTriggerCount = (int)_compressionTriggerNumeric.Value,
            KeepRecentCount = (int)_keepRecentNumeric.Value,
            MaxContextPrompts = (int)_maxContextPromptsNumeric.Value,
            MaxContextImages = (int)_maxContextImagesNumeric.Value,
            ContextAutoAttachThreshold = _contextAutoAttachThresholdNumeric.Value,
            ShowContextDecisionHint = _showContextDecisionHintCheck.Checked,
            AllowHistoryImagesWithManualAttachments = _allowHistoryImagesWithManualAttachmentsCheck.Checked,
            EnableReferenceDetection = _referenceDetectionCheck.Checked,
            EnablePromptEnhancement = _promptEnhancementCheck.Checked,
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
