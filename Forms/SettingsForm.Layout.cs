namespace ImageGenerator.Forms;

internal partial class SettingsForm
{
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
            _resizeDebounceTimer.Stop();
            return;
        }

        _resizeDebounceTimer.Stop();
        _resizeDebounceTimer.Start();
    }

    private void FlushResponsiveResize()
    {
        _resizeDebounceTimer.Stop();
        if (IsDisposed || WindowState == FormWindowState.Minimized)
            return;

        var restoringFromMinimized = _wasMinimized;
        ApplyResponsiveLayout();

        if (restoringFromMinimized)
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
            var english = string.Equals(SelectedLanguage, Services.AppAppearance.English, StringComparison.OrdinalIgnoreCase);
            var padding = ScaleValue(compact ? 12 : 20);
            var tabPadding = ScaleValue(compact ? 8 : 12);
            var rowHeight = ScaleValue(english ? compact ? 48 : 52 : compact ? 38 : 44);

            SuspendLayout();
            tabs.SuspendLayout();
            basicTable.SuspendLayout();
            sizeTable.SuspendLayout();
            formatTable.SuspendLayout();
            _contextScrollPanel.SuspendLayout();
            _contextTable.SuspendLayout();
            _appearanceTable.SuspendLayout();
            buttonPanel.SuspendLayout();

            Padding = new Padding(padding);
            basicTab.Padding = new Padding(tabPadding);
            sizeTab.Padding = new Padding(tabPadding);
            formatTab.Padding = new Padding(tabPadding);
            _contextTab.Padding = new Padding(tabPadding);
            _appearanceTab.Padding = new Padding(tabPadding);

            SetColumnWidth(basicTable, 0, english ? compact ? 132 : 150 : compact ? 92 : 110);
            SetColumnWidth(basicTable, 2, compact ? 42 : 48);
            SetColumnWidth(_contextTable, 0, english ? compact ? 148 : 170 : compact ? 112 : 150);
            SetColumnWidth(_contextTable, 2, compact ? 42 : 48);
            SetColumnWidth(_contextTable, 3, compact ? 28 : 32);
            SetColumnWidth(_appearanceTable, 0, english ? compact ? 118 : 132 : compact ? 98 : 120);
            SetColumnWidth(sizeTable, 0, english ? compact ? 118 : 132 : compact ? 92 : 110);
            SetColumnWidth(sizeTable, 2, english ? compact ? 110 : 128 : compact ? 88 : 110);
            SetColumnWidth(formatTable, 0, english ? compact ? 132 : 150 : compact ? 98 : 120);

            SetAbsoluteRows(basicTable, 5, rowHeight);
            SetAbsoluteRows(_appearanceTable, 2, rowHeight);
            SetAbsoluteRows(_contextTable, 11, compact ? ScaleValue(38) : rowHeight);
            _contextTable.RowStyles[11].SizeType = SizeType.Absolute;
            _contextTable.RowStyles[11].Height = ScaleValue(compact ? 48 : 54);
            SetAbsoluteRows(sizeTable, 6, ScaleValue(english ? compact ? 44 : 48 : compact ? 36 : 40));
            SetAbsoluteRows(formatTable, 6, rowHeight);
            var contextBottomGap = ScaleValue(compact ? 10 : 12);
            _contextScrollPanel.AutoScrollMargin = new Size(0, contextBottomGap);
            _contextScrollPanel.Padding = new Padding(0, 0, 0, contextBottomGap);

            ApplyTableControlSpacing(basicTable, rowHeight, compact);
            ApplyTableControlSpacing(_appearanceTable, rowHeight, compact);
            ApplyTableControlSpacing(_contextTable, compact ? ScaleValue(38) : rowHeight, compact);
            ApplyTableControlSpacing(sizeTable, ScaleValue(compact ? 36 : 40), compact);
            ApplyTableControlSpacing(formatTable, rowHeight, compact);

            var sizeHelpRowHeight = MeasureHelpLabelRowHeight(_sizeHelpLabel, sizeTable, compact ? 34 : 40, compact);
            var formatHelpRowHeight = MeasureHelpLabelRowHeight(lblConcurrencyHint, formatTable, compact ? 34 : 40, compact);
            sizeTable.RowStyles[6].SizeType = SizeType.Absolute;
            sizeTable.RowStyles[6].Height = sizeHelpRowHeight;
            formatTable.RowStyles[6].SizeType = SizeType.Absolute;
            formatTable.RowStyles[6].Height = formatHelpRowHeight;
            AdjustHeightForSelectedTab(
                Math.Max(
                    GetAbsoluteRowsHeight(sizeTable, 7),
                    GetAbsoluteRowsHeight(formatTable, 7)));

            buttonPanel.Height = ScaleValue(compact ? 40 : 46);
            saveBtn.Size = new Size(ScaleValue(compact ? 82 : 96), ScaleValue(compact ? 30 : 34));
            cancelBtn.Size = saveBtn.Size;
            cancelBtn.Margin = new Padding(0, 0, ScaleValue(compact ? 8 : 12), 0);
            saveBtn.Font = UiFont(compact ? 9F : 10F);
            cancelBtn.Font = UiFont(compact ? 9F : 10F);
            browseBtn.Font = UiFont(compact ? 9F : 10F);
            _conversationBrowseBtn.Font = UiFont(compact ? 9F : 10F);
            showKeyBtn.Size = new Size(showKeyBtn.Width, ScaleValue(compact ? 28 : 30));
            browseBtn.Size = new Size(browseBtn.Width, ScaleValue(compact ? 28 : 30));
            _conversationBrowseBtn.Size = new Size(_conversationBrowseBtn.Width, ScaleValue(compact ? 28 : 30));
            ApplyButtonIcons();

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
            _appearanceTable.ResumeLayout(true);
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
        _resizeDebounceTimer.Dispose();

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
                if (label == _sizeHelpLabel || label == lblConcurrencyHint)
                {
                    label.Font = label == lblConcurrencyHint
                        ? UiFont(8F)
                        : UiFont(compact ? 9F : 10F);
                    continue;
                }

                label.Height = rowHeight;
                label.Font = Equals(label.Tag, "context-help")
                    ? UiFont(compact ? 8.5F : 9F, FontStyle.Bold)
                    : label == lblConcurrencyHint
                    ? UiFont(8F)
                    : UiFont(compact ? 9F : 10F);
                continue;
            }

            control.Font = UiFont(compact ? 9F : 10F);
            if (control is Button button && button != showKeyBtn && button != browseBtn && button != _conversationBrowseBtn)
                button.TextAlign = ContentAlignment.MiddleCenter;

            var column = table.GetColumn(control);
            var trailingMargin = column == table.ColumnCount - 1 ? 0 : rightMargin;
            control.Margin = new Padding(0, verticalMargin, trailingMargin, verticalMargin);
        }
    }

    private int MeasureHelpLabelRowHeight(Label label, TableLayoutPanel table, int minLogicalHeight, bool compact)
    {
        var availableWidth = Math.Max(
            ScaleValue(240),
            table.ClientSize.Width - ScaleValue(compact ? 8 : 12));
        label.MaximumSize = new Size(availableWidth, 0);

        var measured = TextRenderer.MeasureText(
            label.Text,
            label.Font,
            new Size(availableWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);

        return Math.Max(ScaleValue(minLogicalHeight), measured.Height + ScaleValue(compact ? 14 : 18));
    }

    private static int GetAbsoluteRowsHeight(TableLayoutPanel table, int rowCount)
    {
        var height = 0;
        for (var i = 0; i < rowCount && i < table.RowStyles.Count; i++)
            height += (int)Math.Ceiling(table.RowStyles[i].Height);

        return height;
    }

    private void AdjustHeightForSelectedTab(int requiredHelpTableHeight)
    {
        var displayRect = tabs.DisplayRectangle;
        if (displayRect.Height <= 0)
            return;

        // All tab pages share the same padding (set in ApplyResponsiveLayout) and same display area,
        // so the table client height derived from DisplayRectangle is always accurate
        // regardless of which tab is currently selected or whether SuspendLayout is active.
        var tabPaddingVertical = basicTab.Padding.Vertical;
        var currentTableHeight = displayRect.Height - tabPaddingVertical;
        if (currentTableHeight <= 0)
            return;

        var nonTableHeight = ClientSize.Height - currentTableHeight;
        var baseClientHeight = Math.Max(
            MinimumSize.Height - (Height - ClientSize.Height),
            ScaleValue(ReferenceHeight));
        var targetClientHeight = tabs.SelectedTab == sizeTab || tabs.SelectedTab == formatTab
            ? Math.Max(baseClientHeight, nonTableHeight + requiredHelpTableHeight)
            : baseClientHeight;
        var workArea = Screen.FromControl(this).WorkingArea;
        var maxClientHeight = Math.Max(baseClientHeight, (int)(workArea.Height * 0.9F) - (Height - ClientSize.Height));
        targetClientHeight = Clamp(targetClientHeight, baseClientHeight, maxClientHeight);

        if (Math.Abs(ClientSize.Height - targetClientHeight) > ScaleValue(2))
            ClientSize = new Size(ClientSize.Width, targetClientHeight);
    }

    private static decimal Clamp(int value, decimal min, decimal max) =>
        Math.Min(max, Math.Max(min, value));

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        Math.Min(max, Math.Max(min, value));

    private static int Clamp(int value, int min, int max) =>
        Math.Min(max, Math.Max(min, value));

    private static float Clamp(float value, float min, float max) =>
        Math.Min(max, Math.Max(min, value));
}
