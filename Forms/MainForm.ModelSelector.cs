using ImageGenerator.Models;
using ImageGenerator.Services;

namespace ImageGenerator.Forms;

/// <summary>
/// Image-model picker shown in the input action bar, between the upload and generate buttons.
/// Switching the model updates the persisted config and the token budget of the active conversation.
/// </summary>
internal partial class MainForm
{
    private const int ModelSelectorMinWidth = 96;

    private Panel _modelBar = null!;
    private Panel _modelSelectorHost = null!;
    private Panel _monitorStrip = null!;
    private ComboBox _modelSelector = null!;
    private CheckBox _reuseImageCheck = null!;
    private Label _contextUsageLabel = null!;
    private ToolTip _modelSelectorToolTip = null!;
    private Color _monitorBorderColor = Color.FromArgb(226, 232, 240);
    private int _monitorMaxLines = 3;
    private bool _isUpdatingModelSelector;
    private bool _isUpdatingReuseToggle;

    private void BuildModelSelector()
    {
        _modelSelectorToolTip = new ToolTip(components)
        {
            AutoPopDelay = 12000,
            InitialDelay = 350,
            ReshowDelay = 120,
            ShowAlways = true,
        };

        _modelSelector = new ComboBox
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            IntegralHeight = false,
            DisplayMember = nameof(ModelContextProfile.Model),
        };
        _modelSelector.SelectedIndexChanged += ModelSelector_SelectedIndexChanged;

        _modelSelectorHost = new Panel
        {
            Dock = DockStyle.Left,
            Width = 180,
        };
        _modelSelectorHost.Controls.Add(_modelSelector);

        // The reuse switch is the only thing that decides whether the previous image travels with
        // the request; there is no prompt-keyword guesswork behind it.
        _reuseImageCheck = new CheckBox
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Checked = _config.ReuseLastImage,
            Padding = new Padding(0, 0, ScaleValue(10), 0),
        };
        _reuseImageCheck.CheckedChanged += ReuseImageCheck_CheckedChanged;

        _modelBar = new Panel { Dock = DockStyle.Fill };
        _modelBar.Controls.Add(_reuseImageCheck);
        _modelBar.Controls.Add(_modelSelectorHost);

        // Index 0 makes the fill panel dock last, so it lands exactly in the gap between the
        // left-docked upload button and the right-docked generate button.
        actionBar.Controls.Add(_modelBar);
        actionBar.Controls.SetChildIndex(_modelBar, 0);

        // The token monitor gets its own full-width row below the action bar: there it can wrap
        // instead of being squeezed between the model picker and the generate button.
        _contextUsageLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0),
        };

        _monitorStrip = new Panel
        {
            Dock = DockStyle.Bottom,
            Padding = new Padding(0, ScaleValue(2), 0, ScaleValue(2)),
        };
        _monitorStrip.Paint += (_, e) =>
        {
            using var pen = new Pen(_monitorBorderColor);
            e.Graphics.DrawLine(pen, 0, 0, _monitorStrip.Width, 0);
        };
        _monitorStrip.Controls.Add(_contextUsageLabel);
        inputCard.Controls.Add(_monitorStrip);

        RefreshModelSelectorItems();
    }

    /// <summary>Rebuilds the picker from the persisted per-model profiles.</summary>
    private void RefreshModelSelectorItems()
    {
        ModelProfileStore.Ensure(_config, _config.Model);
        var profiles = ModelProfileStore.Ordered(_config.ModelProfiles).ToList();

        _isUpdatingModelSelector = true;
        try
        {
            _modelSelector.BeginUpdate();
            _modelSelector.Items.Clear();
            _modelSelector.Items.AddRange(profiles.ToArray());
            var index = profiles.FindIndex(profile => string.Equals(
                profile.Model,
                _config.Model,
                StringComparison.OrdinalIgnoreCase));
            _modelSelector.SelectedIndex = index >= 0
                ? index
                : profiles.Count > 0 ? 0 : -1;
            _modelSelector.EndUpdate();
        }
        finally
        {
            _isUpdatingModelSelector = false;
        }

        _isUpdatingReuseToggle = true;
        try
        {
            _reuseImageCheck.Checked = _config.ReuseLastImage;
        }
        finally
        {
            _isUpdatingReuseToggle = false;
        }

        RefreshContextUsageLabel();
    }

    private void ReuseImageCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingReuseToggle)
            return;

        _config.ReuseLastImage = _reuseImageCheck.Checked;
        _configManager.Save(_config);
        RefreshContextUsageLabel();
    }

    private void ModelSelector_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingModelSelector
            || _modelSelector.SelectedItem is not ModelContextProfile profile)
        {
            return;
        }

        if (!string.Equals(profile.Model, _config.Model, StringComparison.OrdinalIgnoreCase))
        {
            _config.Model = profile.Model;
            ModelProfileStore.Ensure(_config, _config.Model);
            _configManager.Save(_config);
            _apiService = new ImageApiService(_config);
            _conversationManager?.UpdateConfig(_config);
        }

        RefreshContextUsageLabel();
        if (!IsDisposed)
            _promptBox.Focus();
    }

    /// <summary>
    /// Token monitor below the composer: line 1 is the context window of the selected model plus
    /// the share the last request used, line 2 is the session totals with the cache breakdown.
    /// </summary>
    private void RefreshContextUsageLabel()
    {
        var profile = ModelProfileStore.Resolve(_config, _config.Model);
        var messages = _conversationManager?.ActiveConversation?.Messages;
        var latestUsage = FindLatestUsage(messages);
        var totals = UsageSummary.Sum(messages);

        var sharePercent = latestUsage != null && profile.MaxContextTokens > 0
            ? Math.Min(999, (int)Math.Round(latestUsage.TotalTokens * 100.0 / profile.MaxContextTokens))
            : 0;

        var text = string.Format(
            T("ContextUsage"),
            profile.Model.Length == 0 ? "-" : profile.Model,
            UsageSummary.FormatCount(profile.MaxContextTokens),
            sharePercent);

        text += Environment.NewLine;

        if (totals.Requests > 0)
        {
            text += string.Format(
                T("SessionTokens"),
                totals.Requests,
                UsageSummary.FormatCount(totals.InputTokens),
                UsageSummary.FormatOptional(totals.CachedTokens),
                UsageSummary.FormatCount(
                    Math.Max(0, totals.InputTokens - (totals.CachedTokens ?? 0))),
                UsageSummary.FormatOptional(totals.CacheReadTokens),
                UsageSummary.FormatOptional(totals.CacheWriteTokens),
                UsageSummary.FormatCount(totals.OutputTokens),
                UsageSummary.FormatCount(totals.TotalTokens));
        }
        else
        {
            text += T("SessionTokensEmpty");
        }

        _contextUsageLabel.Text = text;

        _modelSelectorToolTip?.SetToolTip(
            _contextUsageLabel,
            latestUsage == null
                ? T("UsageTooltipEmpty")
                : string.Format(
                    T("UsageTooltip"),
                    UsageSummary.FormatCount(latestUsage.InputTokens),
                    UsageSummary.FormatOptional(latestUsage.ReportedCachedTokens),
                    UsageSummary.FormatCount(latestUsage.UncachedInputTokens),
                    UsageSummary.FormatOptional(latestUsage.CacheReadInputTokens),
                    UsageSummary.FormatOptional(latestUsage.CacheCreationInputTokens),
                    UsageSummary.FormatCount(latestUsage.OutputTokens)));

        // The monitor text changes with every request, so the row height follows it right away.
        UpdateMonitorStripHeight(_monitorMaxLines);
        UpdateInputPanelHeight();
    }

    /// <summary>
    /// Grows the monitor row to the height its wrapped text needs, so the whole line stays visible
    /// instead of being cut off with an ellipsis.
    /// </summary>
    private void UpdateMonitorStripHeight(int maxLines)
    {
        var availableWidth = Math.Max(
            ScaleValue(160),
            inputCard.ClientSize.Width - inputCard.Padding.Horizontal - _monitorStrip.Padding.Horizontal);
        var measured = TextRenderer.MeasureText(
            _contextUsageLabel.Text,
            _contextUsageLabel.Font,
            new Size(availableWidth, 0),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

        var oneLineHeight = _contextUsageLabel.Font.Height + _monitorStrip.Padding.Vertical;
        _monitorStrip.Height = Math.Clamp(
            measured.Height + _monitorStrip.Padding.Vertical + ScaleValue(2),
            oneLineHeight,
            oneLineHeight * Math.Max(1, maxLines));
    }

    private static UsageInfo? FindLatestUsage(IReadOnlyList<ChatMessage>? messages)
    {
        if (messages == null)
            return null;

        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Usage != null)
                return messages[i].Usage;
        }

        return null;
    }

    private void ApplyModelSelectorTheme(ThemePalette palette)
    {
        _modelSelector.BackColor = palette.InputBack;
        _modelSelector.ForeColor = palette.Text;
        _contextUsageLabel.ForeColor = palette.MutedText;
        _reuseImageCheck.ForeColor = palette.Text;
        _monitorBorderColor = palette.Border;
        _monitorStrip.Invalidate();
    }

    private void ApplyModelSelectorLayout(bool compact, bool tight)
    {
        var comboWidth = tight ? ModelSelectorMinWidth : compact ? 130 : 180;
        _modelSelectorHost.Width = Math.Max(ScaleValue(ModelSelectorMinWidth), ScaleValue(comboWidth));
        _modelSelectorHost.Padding = new Padding(0, ScaleValue(4), ScaleValue(6), 0);
        _modelSelector.DropDownHeight = ScaleValue(tight ? 160 : 240);
        _modelSelector.Font = UiFont(compact ? 9F : 9.5F);
        _reuseImageCheck.Text = T("ReuseLastImage");
        _reuseImageCheck.Font = UiFont(compact ? 8.5F : 9F);
        _reuseImageCheck.Padding = new Padding(0, 0, ScaleValue(tight ? 4 : 10), 0);
        _reuseImageCheck.Visible = !tight;
        _monitorMaxLines = tight ? 2 : 3;
        _modelSelectorToolTip?.SetToolTip(_modelSelector, T("SwitchModel"));
        _modelSelectorToolTip?.SetToolTip(_reuseImageCheck, T("ReuseLastImageHelp"));
    }

    private void DisposeModelSelector()
    {
        _modelSelectorToolTip?.Dispose();
    }
}
