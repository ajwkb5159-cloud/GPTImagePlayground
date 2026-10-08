using ImageGenerator.Models;
using ImageGenerator.Services;

namespace ImageGenerator.Forms;

internal partial class SettingsForm
{
    /// <summary>Item count of the model drop-down when the filter matches little or nothing.</summary>
    private const int ModelDropDownMinItems = 4;

    /// <summary>
    /// Upper bound of the model drop-down. It grows with the number of matches because a ComboBox
    /// drop-down ignores the mouse wheel, so entries below the fold are effectively unreachable.
    /// </summary>
    private const int ModelDropDownMaxItems = 24;

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
            RowCount = 7,
        };
        _contextTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        _contextTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _contextTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
        _contextTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32F));
        for (var i = 0; i < 6; i++)
            _contextTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        // The trailing row carries the help paragraph: it sizes itself to the wrapped text so the
        // scroll panel can reveal all of it instead of cutting the last lines off.
        _contextTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _contextScrollPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            AutoScrollMargin = new Size(0, 8),
            Padding = new Padding(0, 0, 0, 8),
        };

        // ── Model list + per-model token budget ──
        // The model list is a plain drop-down list and the filtering lives in its own text box:
        // an editable ComboBox performs Win32 incremental search on every keystroke, which selects
        // a list entry and overwrites the typed name — that is what made the first typed character
        // disappear and a single Backspace clear the whole field.
        _contextModelCombo = new ComboBox
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            DropDownStyle = ComboBoxStyle.DropDownList,
            IntegralHeight = false,
            MaxDropDownItems = ModelDropDownMinItems,
            Size = new Size(240, 25),
            DisplayMember = nameof(ModelContextProfile.Model),
        };
        _contextModelCombo.SelectedIndexChanged += ContextModelCombo_SelectedIndexChanged;

        _modelSearchBox = new TextBox
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
        };
        _modelSearchBox.TextChanged += (_, _) => FilterModelCombo(_modelSearchBox.Text);

        _fetchModelsBtn = new Button
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Size = new Size(42, 28),
        };
        ApplyBrowseButtonStyle(_fetchModelsBtn);
        _fetchModelsBtn.Click += FetchModelsBtn_Click;

        _maxContextTokensNumeric = CreateContextNumeric(
            ModelProfileStore.MinMaxContextTokens,
            ModelProfileStore.MaxMaxContextTokens,
            ModelProfileStore.DefaultMaxContextTokens);
        _maxContextTokensNumeric.Increment = 1024;
        _maxContextTokensNumeric.ValueChanged += (_, _) => ApplyTokenEditorsToProfile();

        _maxOutputTokensNumeric = CreateContextNumeric(
            ModelProfileStore.MinMaxOutputTokens,
            ModelProfileStore.MaxMaxOutputTokens,
            ModelProfileStore.DefaultMaxOutputTokens);
        _maxOutputTokensNumeric.Increment = 256;
        _maxOutputTokensNumeric.ValueChanged += (_, _) => ApplyTokenEditorsToProfile();

        _contextStatusLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _conversationDirBox = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
        _conversationBrowseBtn = new Button
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Size = new Size(42, 28),
        };
        ApplyBrowseButtonStyle(_conversationBrowseBtn);

        AddContextRow(_contextTable, "模型列表", _contextModelCombo, 0,
            "ModelListHelp",
            1);
        _contextTable.Controls.Add(_fetchModelsBtn, 2, 0);
        AddContextRow(_contextTable, "搜索模型", _modelSearchBox, 1,
            "ModelSearchHelp");
        AddContextRow(_contextTable, "最长上下文 Token", _maxContextTokensNumeric, 2,
            "MaxContextTokensHelp");
        AddContextRow(_contextTable, "最大输出 Token", _maxOutputTokensNumeric, 3,
            "MaxOutputTokensHelp");
        _contextTable.SetColumnSpan(_contextStatusLabel, 4);
        _contextTable.Controls.Add(_contextStatusLabel, 0, 4);
        AddContextRow(_contextTable, "会话存储目录", _conversationDirBox, 5,
            "ConversationStoreDirHelp",
            1);
        _contextTable.Controls.Add(_conversationBrowseBtn, 2, 5);

        _contextHelpLabel = new Label
        {
            Text = "上下文按所选模型的 token 预算真实生效：文本上下文与最近生成图会自动带入本轮请求，超出预算的较早历史会被压缩成摘要。",
            Tag = "ContextHelp",
            Dock = DockStyle.Fill,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _contextTable.SetColumnSpan(_contextHelpLabel, 4);
        _contextTable.Controls.Add(_contextHelpLabel, 0, 6);

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
            Tag = row switch
            {
                0 => "ModelList",
                1 => "ModelSearch",
                2 => "MaxContextTokens",
                3 => "MaxOutputTokens",
                5 => "ConversationStoreDir",
                _ => null,
            },
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var help = new Label
        {
            Text = "?",
            Tag = "context-help",
            AccessibleName = helpText,
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

    // ═══════════════════════════════════════════════════
    //  Model list + token budget
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Copies the persisted profiles so cancelling the dialog cannot modify the live config, then
    /// fills the model picker and the token editors with the copies.
    /// </summary>
    private void LoadModelProfiles(AppConfig config)
    {
        _modelProfiles = [.. config.ModelProfiles.Select(ModelProfileStore.Clone)];
        ModelProfileStore.Ensure(_modelProfiles, config.Model);
        ModelProfileStore.Ordered(_modelProfiles);

        _contextEditorModel = config.Model.Trim();
        _modelSearchBox.Clear();
        FilterModelCombo(_modelSearchBox.Text);
        LoadProfileIntoEditors(FindModelProfile(_contextEditorModel));
    }

    /// <summary>
    /// Rebuilds the model drop-down from the working profile list, using the text of the search box
    /// as a case-insensitive fuzzy filter. The model being edited stays selected while the filter
    /// still contains it.
    /// </summary>
    private void FilterModelCombo(string filter)
    {
        var matches = ModelProfileStore.Ordered(_modelProfiles)
            .Where(profile => MatchesModelFilter(profile.Model, filter))
            .ToArray();

        _suppressModelFilter = true;
        try
        {
            _contextModelCombo.BeginUpdate();
            SyncModelComboItems(matches);
            _contextModelCombo.MaxDropDownItems = Math.Clamp(
                matches.Length,
                ModelDropDownMinItems,
                ModelDropDownMaxItems);
            _contextModelCombo.EndUpdate();

            SelectContextEditorModelInCombo();
        }
        finally
        {
            _suppressModelFilter = false;
        }
    }

    /// <summary>
    /// Points the drop-down at the model the token editors describe; a filtered list that does not
    /// contain it simply leaves the drop-down empty, while the status line keeps naming the model.
    /// </summary>
    private void SelectContextEditorModelInCombo()
    {
        var index = IndexOfModelComboItem(_contextEditorModel);
        if (_contextModelCombo.SelectedIndex != index)
            _contextModelCombo.SelectedIndex = index;
    }

    /// <summary>
    /// Brings the dropdown entries in line with <paramref name="matches"/> without clearing the
    /// collection: entries are removed and re-inserted in place, which keeps the control's own
    /// selection bookkeeping intact and avoids the flicker of a full rebuild on every keystroke.
    /// </summary>
    private void SyncModelComboItems(IReadOnlyList<ModelContextProfile> matches)
    {
        for (var i = _contextModelCombo.Items.Count - 1; i >= 0; i--)
        {
            if (!ContainsModel(matches, _contextModelCombo.Items[i]))
                _contextModelCombo.Items.RemoveAt(i);
        }

        for (var i = 0; i < matches.Count; i++)
        {
            if (i < _contextModelCombo.Items.Count && IsSameModel(_contextModelCombo.Items[i], matches[i]))
                continue;

            var existingIndex = IndexOfModelComboItem(matches[i].Model);
            if (existingIndex >= 0)
                _contextModelCombo.Items.RemoveAt(existingIndex);

            _contextModelCombo.Items.Insert(Math.Min(i, _contextModelCombo.Items.Count), matches[i]);
        }
    }

    /// <summary>Writes the model name into the model field of the basic tab.</summary>
    private void SetModelBoxText(string model)
    {
        if (string.Equals(_modelBox.Text.Trim(), model, StringComparison.OrdinalIgnoreCase))
            return;

        _isSyncingModelFields = true;
        try
        {
            _modelBox.Text = model;
        }
        finally
        {
            _isSyncingModelFields = false;
        }
    }

    /// <summary>
    /// Fuzzy match on the model id: case-insensitive substring matching that ignores the separator
    /// characters of the id and requires every whitespace-separated word of the filter, so
    /// "gemini" lists every Gemini model and "gemini31" also finds "gemini-3.1-pro".
    /// </summary>
    private static bool MatchesModelFilter(string model, string filter)
    {
        var words = filter.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
            return true;

        var normalizedModel = NormalizeModelKey(model);
        foreach (var word in words)
        {
            if (!normalizedModel.Contains(NormalizeModelKey(word), StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static string NormalizeModelKey(string value) =>
        string.Concat(value.Where(character =>
            !char.IsWhiteSpace(character) && character is not ('-' or '_' or '.' or '/' or ':')));

    private static bool IsSameModel(object? item, ModelContextProfile profile) =>
        item is ModelContextProfile existing
        && string.Equals(existing.Model, profile.Model, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsModel(IReadOnlyList<ModelContextProfile> profiles, object? item) =>
        item is ModelContextProfile existing
        && profiles.Any(profile => string.Equals(
            profile.Model,
            existing.Model,
            StringComparison.OrdinalIgnoreCase));

    private int IndexOfModelComboItem(string model)
    {
        for (var i = 0; i < _contextModelCombo.Items.Count; i++)
        {
            if (_contextModelCombo.Items[i] is ModelContextProfile existing
                && string.Equals(existing.Model, model, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void ContextModelCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isLoadingModelProfile
            || _suppressModelFilter
            || _contextModelCombo.SelectedItem is not ModelContextProfile profile)
        {
            return;
        }

        SetContextEditorModel(profile.Model);
    }

    /// <summary>Points the token editors and the basic tab's model field at the given model.</summary>
    private void SetContextEditorModel(string model)
    {
        _contextEditorModel = model;
        SetModelBoxText(model);

        var wasSuppressed = _suppressModelFilter;
        _suppressModelFilter = true;
        try
        {
            SelectContextEditorModelInCombo();
        }
        finally
        {
            _suppressModelFilter = wasSuppressed;
        }

        LoadProfileIntoEditors(FindModelProfile(model));
    }

    private ModelContextProfile? FindModelProfile(string model) =>
        _modelProfiles.FirstOrDefault(profile => string.Equals(
            profile.Model,
            model,
            StringComparison.OrdinalIgnoreCase));

    /// <summary>A null profile means "model without stored tokens": show the defaults.</summary>
    private void LoadProfileIntoEditors(ModelContextProfile? profile)
    {
        if (profile == null)
        {
            LoadTokenEditors(
                ModelProfileStore.DefaultMaxContextTokens,
                ModelProfileStore.DefaultMaxOutputTokens);
            return;
        }

        ModelProfileStore.Normalize(profile);
        _contextEditorModel = profile.Model;
        LoadTokenEditors(profile.MaxContextTokens, profile.MaxOutputTokens);
    }

    private void LoadTokenEditors(int maxContextTokens, int maxOutputTokens)
    {
        _isLoadingModelProfile = true;
        try
        {
            _maxContextTokensNumeric.Value = Clamp(
                maxContextTokens,
                _maxContextTokensNumeric.Minimum,
                _maxContextTokensNumeric.Maximum);
            _maxOutputTokensNumeric.Value = Clamp(
                maxOutputTokens,
                _maxOutputTokensNumeric.Minimum,
                Math.Min(_maxOutputTokensNumeric.Maximum, _maxContextTokensNumeric.Value - 1));
        }
        finally
        {
            _isLoadingModelProfile = false;
        }

        UpdateContextBudgetPreview();
    }

    /// <summary>
    /// Writes the token editors into the profile of the model being edited. Editing tokens for a
    /// name that has no profile yet creates it at this point, so merely typing a name never
    /// pollutes the list.
    /// </summary>
    private void ApplyTokenEditorsToProfile()
    {
        if (_isLoadingModelProfile)
            return;

        var context = (int)_maxContextTokensNumeric.Value;
        var output = (int)_maxOutputTokensNumeric.Value;
        if (output >= context)
        {
            _isLoadingModelProfile = true;
            try
            {
                _maxOutputTokensNumeric.Value = Math.Max(
                    _maxOutputTokensNumeric.Minimum,
                    context - 1);
                output = (int)_maxOutputTokensNumeric.Value;
            }
            finally
            {
                _isLoadingModelProfile = false;
            }
        }

        var model = _contextEditorModel;
        if (model.Length == 0)
        {
            UpdateContextBudgetPreview();
            return;
        }

        var profile = ModelProfileStore.Ensure(_modelProfiles, model);
        profile.Model = model;
        profile.MaxContextTokens = context;
        profile.MaxOutputTokens = output;
        profile.HasManualTokens = true;
        profile.UpdatedAt = DateTime.Now;

        UpdateContextBudgetPreview();
    }

    private void UpdateContextBudgetPreview()
    {
        var model = _contextEditorModel.Length > 0
            ? _contextEditorModel
            : _modelBox.Text.Trim();
        var context = (int)_maxContextTokensNumeric.Value;
        var output = (int)_maxOutputTokensNumeric.Value;
        var text = string.Format(
            T("ModelBudgetPreview"),
            model.Length == 0 ? "-" : model,
            context,
            output,
            Math.Max(0, context - output));

        if (context > ModelProfileStore.OverlargeContextWarningTokens)
            text += T("ContextTokensTooLarge");

        SetContextStatus(text, false);
    }

    private void SetContextStatus(string text, bool isError)
    {
        _contextStatusLabel.Text = text;
        _contextStatusIsError = isError;
        ApplyContextStatusColor();
    }

    private void ApplyContextStatusColor()
    {
        _contextStatusLabel.ForeColor = _contextStatusIsError
            ? Color.FromArgb(220, 38, 38)
            : AppAppearance.Palette(SelectedTheme).MutedText;
    }

    private async void FetchModelsBtn_Click(object? sender, EventArgs e)
    {
        if (_isFetchingModels)
            return;

        var baseUrl = _baseUrlBox.Text.Trim();
        var apiKey = _apiKeyBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            MessageBox.Show(this, T("ApiUrlRequired"), T("ValidationFailed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            MessageBox.Show(this, T("ApiKeyRequired"), T("ValidationFailed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isFetchingModels = true;
        _fetchModelsBtn.Enabled = false;
        SetContextStatus(T("FetchingModels"), false);

        _fetchModelsCts?.Dispose();
        _fetchModelsCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        try
        {
            var models = await _modelDiscovery.GetModelsAsync(
                baseUrl,
                apiKey,
                _verifySslCertificateCheck.Checked,
                (int)_timeoutNumeric.Value,
                _fetchModelsCts.Token);

            foreach (var model in models)
            {
                var profile = ModelProfileStore.Ensure(_modelProfiles, model.Id);
                ModelProfileStore.TryApplyApiHint(
                    profile,
                    model.MaxContextTokens,
                    model.MaxOutputTokens);
            }

            FilterModelCombo(_modelSearchBox.Text);
            LoadProfileIntoEditors(FindModelProfile(_contextEditorModel));
            SetContextStatus(string.Format(T("FetchModelsReady"), models.Count), false);
        }
        catch (OperationCanceledException)
        {
            SetContextStatus(T("FetchModelsCanceled"), true);
        }
        catch (Exception ex)
        {
            SetContextStatus(ex.Message, true);
        }
        finally
        {
            _isFetchingModels = false;
            if (!IsDisposed)
                _fetchModelsBtn.Enabled = true;
        }
    }

    /// <summary>
    /// Keeps both tabs describing the same model when the "model" field on the basic tab changes.
    /// A name that is not in the list yet only updates the budget preview; its profile is created
    /// once tokens are edited or the dialog is saved.
    /// </summary>
    private void SyncContextTabWithModelField()
    {
        if (_isSyncingModelFields || _isLoadingModelProfile)
            return;

        // The search box belongs to the user and is left as it is.
        SetContextEditorModel(_modelBox.Text.Trim());
    }

    /// <summary>Ensures the active model has a profile, then returns the list to persist.</summary>
    private List<ModelContextProfile> BuildModelProfilesForSave(string activeModel)
    {
        var profile = ModelProfileStore.Ensure(_modelProfiles, activeModel);
        profile.Model = activeModel;
        ModelProfileStore.Normalize(profile);

        return [.. ModelProfileStore.Ordered(_modelProfiles).Select(ModelProfileStore.Clone)];
    }
}
