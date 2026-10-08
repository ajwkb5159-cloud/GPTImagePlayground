using ImageGenerator.Services;

namespace ImageGenerator.Forms;

internal partial class SettingsForm
{
    private TabPage _appearanceTab = null!;
    private TableLayoutPanel _appearanceTable = null!;
    private Panel _appearanceScrollPanel = null!;
    private ComboBox _themeBox = null!;
    private ComboBox _languageBox = null!;
    private Label _themeLabel = null!;
    private Label _languageLabel = null!;
    private bool _updatingAppearanceChoices;

    private string SelectedTheme =>
        (_themeBox.SelectedItem as AppearanceOption)?.Value ?? AppAppearance.LightTheme;

    private string SelectedLanguage =>
        (_languageBox.SelectedItem as AppearanceOption)?.Value ?? Result.Language;

    private string T(string key) => AppAppearance.Text(SelectedLanguage, key);

    private void BuildAppearanceTab()
    {
        _appearanceTab = new TabPage
        {
            Padding = new Padding(12),
            UseVisualStyleBackColor = true,
        };

        _appearanceTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        _appearanceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        _appearanceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _appearanceTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        _appearanceTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));

        _appearanceScrollPanel = new Panel
        {
            AutoScroll = true,
            Dock = DockStyle.Fill,
        };

        _themeLabel = CreateAppearanceLabel();
        _languageLabel = CreateAppearanceLabel();
        _themeBox = CreateAppearanceComboBox();
        _languageBox = CreateAppearanceComboBox();

        _appearanceTable.Controls.Add(_themeLabel, 0, 0);
        _appearanceTable.Controls.Add(_themeBox, 1, 0);
        _appearanceTable.Controls.Add(_languageLabel, 0, 1);
        _appearanceTable.Controls.Add(_languageBox, 1, 1);

        _themeBox.SelectedIndexChanged += (_, _) => PreviewAppearanceChanges();
        _languageBox.SelectedIndexChanged += (_, _) => PreviewAppearanceChanges();

        _appearanceScrollPanel.Controls.Add(_appearanceTable);
        _appearanceTab.Controls.Add(_appearanceScrollPanel);
        tabs.Controls.Add(_appearanceTab);
    }

    private static Label CreateAppearanceLabel() => new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static ComboBox CreateAppearanceComboBox() => new()
    {
        Anchor = AnchorStyles.Left,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Size = new Size(180, 25),
    };

    private void SetAppearanceSelections(string? theme, string? language)
    {
        _updatingAppearanceChoices = true;
        try
        {
            var normalizedTheme = AppAppearance.NormalizeTheme(theme);
            var normalizedLanguage = AppAppearance.NormalizeLanguage(language);
            ResetAppearanceOptions(normalizedLanguage, normalizedTheme);
        }
        finally
        {
            _updatingAppearanceChoices = false;
        }
    }

    private void ResetAppearanceOptions(string language, string selectedTheme)
    {
        SetOptions(
            _themeBox,
            selectedTheme,
            new AppearanceOption(AppAppearance.LightTheme, AppAppearance.Text(language, "Light")),
            new AppearanceOption(AppAppearance.DarkTheme, AppAppearance.Text(language, "Dark")));

        SetOptions(
            _languageBox,
            language,
            new AppearanceOption(AppAppearance.SimplifiedChinese, AppAppearance.Text(language, "SimplifiedChinese")),
            new AppearanceOption(AppAppearance.English, AppAppearance.Text(language, "English")),
            new AppearanceOption(AppAppearance.TraditionalChinese, AppAppearance.Text(language, "TraditionalChinese")));
    }

    private static void SetOptions(ComboBox comboBox, string selectedValue, params AppearanceOption[] options)
    {
        comboBox.BeginUpdate();
        comboBox.Items.Clear();
        comboBox.Items.AddRange(options);
        comboBox.SelectedIndex = Math.Max(0, Array.FindIndex(options, option => option.Value == selectedValue));
        comboBox.EndUpdate();
    }

    private void PreviewAppearanceChanges()
    {
        if (_updatingAppearanceChoices)
            return;

        var theme = SelectedTheme;
        var language = AppAppearance.NormalizeLanguage(SelectedLanguage);
        _updatingAppearanceChoices = true;
        try
        {
            ResetAppearanceOptions(language, theme);
        }
        finally
        {
            _updatingAppearanceChoices = false;
        }

        ApplyLocalization();
        ApplyTheme();
        ApplyResponsiveLayout();
    }

    private void ApplyLocalization()
    {
        Text = T("Settings");
        basicTab.Text = T("Basic");
        sizeTab.Text = T("Size");
        formatTab.Text = T("Format");
        _contextTab.Text = T("Context");
        _appearanceTab.Text = T("Appearance");
        _aboutTab.Text = T("About");

        lblApiUrl.Text = T("ApiUrl");
        lblApiKey.Text = T("ApiKey");
        lblModel.Text = T("Model");
        lblOutputDir.Text = T("OutputDir");
        lblTimeout.Text = T("Timeout");
        lblVerifySsl.Text = T("VerifyTls");
        _sizeAutoRadio.Text = T("AutoSize");
        _sizePresetRadio.Text = T("PresetSize");
        lblSizeTier.Text = T("Resolution");
        lblAspectRatio.Text = T("AspectRatio");
        _sizeCustomRadio.Text = T("CustomSize");
        lblCustomWidth.Text = T("Width");
        lblCustomHeight.Text = T("Height");
        lblOutputFormat.Text = T("OutputFormat");
        lblTransparent.Text = T("TransparentBackground");
        lblModeration.Text = T("Moderation");
        lblImageCount.Text = T("ImageCount");
        lblConcurrent.Text = T("ConcurrentMode");
        lblConcurrency.Text = T("MaxConcurrency");
        _sizeHelpLabel.Text = T("SizeHelp");
        lblConcurrencyHint.Text = T("ConcurrencyHint");
        _themeLabel.Text = T("Theme");
        _languageLabel.Text = T("Language");

        _versionCaptionLabel.Text = T("CurrentVersion");
        _updateCaptionLabel.Text = T("UpdateCaption");
        _checkUpdateBtn.Text = " " + T("CheckUpdate");
        _aboutDescriptionLabel.Text = T("AboutDescription");
        browseBtn.Text = "";
        _conversationBrowseBtn.Text = "";
        _buttonToolTip.SetToolTip(browseBtn, T("Browse"));
        _buttonToolTip.SetToolTip(_conversationBrowseBtn, T("Browse"));
        _buttonToolTip.SetToolTip(showKeyBtn, _apiKeyBox.UseSystemPasswordChar ? T("ShowApiKey") : T("HideApiKey"));
        saveBtn.Text = " " + T("Save");
        cancelBtn.Text = " " + T("Cancel");

        ApplyTaggedLocalization(_contextTable);
        ApplyContextHelpLocalization();
        _modelSearchBox.PlaceholderText = T("ModelSearchHint");
        _contextToolTip.SetToolTip(_fetchModelsBtn, T("FetchModels"));
        if (!_isFetchingModels)
            UpdateContextBudgetPreview();
    }

    private void ApplyTaggedLocalization(Control root)
    {
        foreach (Control control in root.Controls)
        {
            if (control.Tag is string key && key != "context-help")
                control.Text = T(key);

            ApplyTaggedLocalization(control);
        }
    }

    private void ApplyContextHelpLocalization()
    {
        foreach (Control control in _contextTable.Controls)
        {
            if (control.AccessibleName is not string helpKey || string.IsNullOrWhiteSpace(helpKey))
                continue;

            var localizedHelp = T(helpKey);
            _contextToolTip.SetToolTip(control, localizedHelp);

            var position = _contextTable.GetPositionFromControl(control);
            foreach (Control peer in _contextTable.Controls)
            {
                if (_contextTable.GetRow(peer) == position.Row)
                    _contextToolTip.SetToolTip(peer, localizedHelp);
            }
        }
    }

    private void ApplyTheme()
    {
        var palette = AppAppearance.Palette(SelectedTheme);

        ApplyThemeToControl(this, palette);
        BackColor = palette.Surface;
        buttonPanel.BackColor = palette.Surface;
        ApplyButtonTheme(showKeyBtn, palette, primary: false);
        ApplyButtonTheme(browseBtn, palette, primary: false);
        ApplyButtonTheme(_conversationBrowseBtn, palette, primary: false);
        ApplyButtonTheme(_fetchModelsBtn, palette, primary: false);
        ApplyButtonTheme(saveBtn, palette, primary: true);
        ApplyButtonTheme(cancelBtn, palette, primary: false);
        ApplyButtonTheme(_checkUpdateBtn, palette, primary: false);
        ApplyIconOnlyButtonLayout(showKeyBtn);
        ApplyIconOnlyButtonLayout(browseBtn);
        ApplyIconOnlyButtonLayout(_conversationBrowseBtn);
        ApplyIconOnlyButtonLayout(_fetchModelsBtn);
        ApplyContextStatusColor();

        _appNameLabel.ForeColor = palette.Text;
        _versionValueLabel.ForeColor = palette.Text;
        _versionCaptionLabel.ForeColor = palette.Text;
        _updateCaptionLabel.ForeColor = palette.Text;
        _aboutDescriptionLabel.ForeColor = palette.MutedText;
        if (!_isCheckingUpdate && _updateStatusLabel.ForeColor != Color.FromArgb(220, 38, 38))
            _updateStatusLabel.ForeColor = palette.MutedText;
    }

    private void ApplyButtonIcons()
    {
        SetScaledButtonImage(browseBtn, "upload-image-24.png", 20);
        SetScaledButtonImage(_conversationBrowseBtn, "upload-image-24.png", 20);
        SetScaledButtonImage(_fetchModelsBtn, "update-24.png", 20);
        SetScaledButtonImage(saveBtn, "save-24.png", 18);
        SetScaledButtonImage(cancelBtn, "close-24.png", 18);
        SetScaledButtonImage(_checkUpdateBtn, "update-24.png", 18);
        UpdateShowKeyIcon();
        ApplyIconOnlyButtonLayout(browseBtn);
        ApplyIconOnlyButtonLayout(_conversationBrowseBtn);
        ApplyIconOnlyButtonLayout(_fetchModelsBtn);
    }

    private static void ApplyIconOnlyButtonLayout(Button button)
    {
        button.Text = "";
        button.ImageAlign = ContentAlignment.MiddleCenter;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.TextImageRelation = TextImageRelation.Overlay;
        button.Padding = Padding.Empty;
    }

    private static void ApplyThemeToControl(Control control, ThemePalette palette)
    {
        switch (control)
        {
            case TabPage tabPage:
                tabPage.UseVisualStyleBackColor = false;
                tabPage.BackColor = palette.Surface;
                tabPage.ForeColor = palette.Text;
                break;
            case TextBox or ComboBox or NumericUpDown:
                control.BackColor = palette.InputBack;
                control.ForeColor = palette.Text;
                break;
            case Button:
                break;
            case CheckBox or RadioButton:
                control.BackColor = palette.Surface;
                control.ForeColor = palette.Text;
                break;
            default:
                control.BackColor = control is Form ? palette.Surface : palette.Surface;
                control.ForeColor = palette.Text;
                break;
        }

        foreach (Control child in control.Controls)
            ApplyThemeToControl(child, palette);
    }

    private static void ApplyButtonTheme(Button button, ThemePalette palette, bool primary)
    {
        button.BackColor = primary ? palette.Primary : palette.SecondaryButton;
        button.ForeColor = primary ? palette.PrimaryText : palette.SecondaryButtonText;
        button.FlatAppearance.BorderSize = 0;
        button.UseVisualStyleBackColor = false;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.TextAlign = ContentAlignment.MiddleCenter;
    }

    private sealed record AppearanceOption(string Value, string Label)
    {
        public override string ToString() => Label;
    }
}
