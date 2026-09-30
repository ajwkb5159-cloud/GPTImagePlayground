using ImageGenerator.Services;

namespace ImageGenerator.Forms;

internal partial class MainForm
{
    private const int IconTextGap = 5;
    private const float MinButtonContentScale = 0.82F;

    private string T(string key) => AppAppearance.Text(_config.Language, key);

    private void ApplyLocalization()
    {
        Text = "GPT Image Playground";
        titleLabel.Text = "GPT Image Playground";
        var logicalClientSize = ClientSize.Width > 0 && ClientSize.Height > 0
            ? GetLogicalClientSize()
            : new SizeF(ReferenceWidth, ReferenceHeight);
        var compact = logicalClientSize.Width < 560 || logicalClientSize.Height < 520;
        var tight = logicalClientSize.Width < 470;

        newConversationBtn.Text = "";
        settingsBtn.Text = compact ? "" : T("Settings");
        _attachBtn.Text = tight ? "" : compact ? T("Upload") : T("UploadImage");
        _sendBtn.Text = compact ? T("Generate") : T("GenerateImage");
        _promptBox.PlaceholderText = T("PromptPlaceholder");
        _modelSelector.AccessibleName = T("SwitchModel");
        _modelSelectorToolTip?.SetToolTip(_modelSelector, T("SwitchModel"));
        RefreshContextUsageLabel();
    }

    private void ApplyTheme()
    {
        var palette = AppAppearance.Palette(_config.Theme);

        BackColor = palette.WindowBack;
        topBar.BackColor = palette.Surface;
        titlePanel.BackColor = palette.Surface;
        conversationBar.BackColor = palette.Surface;
        separator.BackColor = palette.Border;
        chatContainer.BackColor = palette.WindowBack;
        _chatPanel.BackColor = palette.WindowBack;
        inputPanel.BackColor = palette.WindowBack;
        inputCard.BackColor = palette.Surface;
        promptHost.BackColor = palette.InputBack;
        _promptBox.BackColor = palette.InputBack;
        _promptBox.ForeColor = palette.Text;
        titleLabel.ForeColor = palette.Text;

        ApplyButtonTheme(settingsBtn, palette, primary: false);
        ApplyButtonTheme(newConversationBtn, palette, primary: true);
        ApplyButtonTheme(_attachBtn, palette, primary: false);
        ApplyButtonTheme(_sendBtn, palette, primary: true);
        ApplyModelSelectorTheme(palette);

        RebuildConversationTabs();
    }

    private static void ApplyButtonTheme(Button button, ThemePalette palette, bool primary)
    {
        button.BackColor = primary ? palette.Primary : palette.SecondaryButton;
        button.ForeColor = primary ? palette.PrimaryText : palette.SecondaryButtonText;
        button.FlatAppearance.BorderSize = 0;
        button.UseVisualStyleBackColor = false;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.ImageAlign = ContentAlignment.MiddleCenter;
        button.TextAlign = ContentAlignment.MiddleCenter;
    }

    private void ApplyIconOnlyButtonLayout(Button button)
    {
        RemoveAdaptiveIconTextButtonLayout(button);
        button.Text = "";
        button.Padding = Padding.Empty;
        button.ImageAlign = ContentAlignment.MiddleCenter;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.TextImageRelation = TextImageRelation.Overlay;
    }

    private void ApplyIconTextButtonLayout(Button button, int horizontalPadding)
    {
        button.Padding = new Padding(ScaleValue(horizontalPadding), 0, ScaleValue(horizontalPadding), 0);
        button.ImageAlign = ContentAlignment.MiddleCenter;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
    }

    private void ApplyAdaptiveIconTextButton(
        Button button,
        string text,
        string iconFileName,
        int minWidth,
        int maxWidth,
        int height,
        float fontSize,
        FontStyle fontStyle,
        int iconSize,
        int horizontalPadding)
    {
        if (string.IsNullOrEmpty(text))
        {
            button.Size = new Size(ScaleValue(minWidth), ScaleValue(height));
            button.Font = UiFont(fontSize, fontStyle);
            ApplyIconOnlyButtonLayout(button);
            SetScaledButtonImage(button, iconFileName, iconSize);
            return;
        }

        var layoutText = " " + text.Trim();
        var baseFont = UiFont(fontSize, fontStyle);
        var minPixelWidth = ScaleValue(minWidth);
        var maxPixelWidth = ScaleValue(maxWidth);
        var desiredWidth = MeasureIconTextButtonWidth(layoutText, baseFont, iconSize, horizontalPadding);
        var width = Clamp(desiredWidth, minPixelWidth, maxPixelWidth);
        var contentScale = CalculateButtonContentScale(
            layoutText,
            width,
            fontSize,
            fontStyle,
            iconSize,
            horizontalPadding);

        var scaledPadding = Math.Max(4, (int)Math.Round(horizontalPadding * contentScale));
        var scaledIconSize = Math.Max(14, (int)Math.Round(iconSize * contentScale));

        button.Font = UiFont(fontSize * contentScale, fontStyle);
        button.Size = new Size(width, ScaleValue(height));
        ApplyIconTextButtonLayout(button, scaledPadding);
        ApplyAdaptiveIconTextButtonState(button, text.Trim(), iconFileName, scaledIconSize);
    }

    private float CalculateButtonContentScale(
        string text,
        int targetWidth,
        float fontSize,
        FontStyle fontStyle,
        int iconSize,
        int horizontalPadding)
    {
        var scale = 1F;
        for (var i = 0; i < 8; i++)
        {
            var measuredWidth = MeasureIconTextButtonWidth(
                text,
                UiFont(fontSize * scale, fontStyle),
                Math.Max(14, (int)Math.Round(iconSize * scale)),
                Math.Max(4, (int)Math.Round(horizontalPadding * scale)));

            if (measuredWidth <= targetWidth || scale <= MinButtonContentScale)
                break;

            scale = Math.Max(MinButtonContentScale, scale * targetWidth / measuredWidth);
        }

        return scale;
    }

    private int MeasureIconTextButtonWidth(string text, Font font, int iconSize, int horizontalPadding)
    {
        var textWidth = TextRenderer.MeasureText(
            text,
            font,
            Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;

        return textWidth + ScaleValue(iconSize + IconTextGap + horizontalPadding * 2);
    }

    private void ApplyAdaptiveIconTextButtonState(Button button, string text, string iconFileName, int logicalIconSize)
    {
        var oldButtonImage = button.Image;
        button.Text = "";
        button.Image = null;
        button.Tag = null;
        oldButtonImage?.Dispose();

        var state = GetIconTextButtonState(button);
        var pixelSize = ScaleValue(logicalIconSize);
        var imageKey = $"{iconFileName}:{pixelSize}";
        if (state.ImageKey != imageKey)
        {
            state.Image?.Dispose();
            state.Image = LoadIconImage(iconFileName, pixelSize);
            state.ImageKey = imageKey;
        }

        state.Text = text;
        state.Gap = ScaleValue(IconTextGap);
        button.AccessibleName = text;
        button.Invalidate();
    }

    private IconTextButtonState GetIconTextButtonState(Button button)
    {
        if (_iconTextButtonStates.TryGetValue(button, out var state))
            return state;

        state = new IconTextButtonState();
        _iconTextButtonStates[button] = state;
        button.Paint += DrawAdaptiveIconTextButton;
        button.Disposed += AdaptiveIconTextButton_Disposed;
        return state;
    }

    private void RemoveAdaptiveIconTextButtonLayout(Button button)
    {
        if (!_iconTextButtonStates.Remove(button, out var state))
            return;

        button.Paint -= DrawAdaptiveIconTextButton;
        button.Disposed -= AdaptiveIconTextButton_Disposed;
        state.Image?.Dispose();
    }

    private void AdaptiveIconTextButton_Disposed(object? sender, EventArgs e)
    {
        if (sender is Button button)
            RemoveAdaptiveIconTextButtonLayout(button);
    }

    private void DrawAdaptiveIconTextButton(object? sender, PaintEventArgs e)
    {
        if (sender is not Button button
            || !_iconTextButtonStates.TryGetValue(button, out var state)
            || state.Image == null)
        {
            return;
        }

        var textSize = TextRenderer.MeasureText(
            state.Text,
            button.Font,
            Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        var contentWidth = state.Image.Width + state.Gap + textSize.Width;
        var left = Math.Max(button.Padding.Left, (button.ClientSize.Width - contentWidth) / 2);
        var imageTop = Math.Max(0, (button.ClientSize.Height - state.Image.Height) / 2);
        var textLeft = left + state.Image.Width + state.Gap;
        var textBounds = new Rectangle(
            textLeft,
            0,
            Math.Max(0, button.ClientSize.Width - textLeft - button.Padding.Right),
            button.ClientSize.Height);
        var textColor = button.Enabled ? button.ForeColor : SystemColors.GrayText;

        e.Graphics.DrawImage(state.Image, new Rectangle(left, imageTop, state.Image.Width, state.Image.Height));
        TextRenderer.DrawText(
            e.Graphics,
            state.Text,
            button.Font,
            textBounds,
            textColor,
            TextFormatFlags.SingleLine
                | TextFormatFlags.VerticalCenter
                | TextFormatFlags.Left
                | TextFormatFlags.EndEllipsis
                | TextFormatFlags.NoPadding);
    }

    private string FormatDefaultConversationTitle(int displayNumber) =>
        string.Format(T("ConversationDefaultTitle"), displayNumber);
}
