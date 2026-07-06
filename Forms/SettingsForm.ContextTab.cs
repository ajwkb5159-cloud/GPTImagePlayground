namespace ImageGenerator.Forms;

internal partial class SettingsForm
{
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
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Size = new Size(42, 28),
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
            "ConversationStoreDirHelp",
            1);
        _contextTable.Controls.Add(_conversationBrowseBtn, 2, 0);
        AddContextRow(_contextTable, "活跃消息数", _maxActiveMessagesNumeric, 1,
            "ActiveMessagesHelp");
        AddContextRow(_contextTable, "压缩触发阈值", _compressionTriggerNumeric, 2,
            "CompressionTriggerHelp");
        AddContextRow(_contextTable, "保留最近消息", _keepRecentNumeric, 3,
            "KeepRecentHelp");
        AddContextRow(_contextTable, "最近提示词数", _maxContextPromptsNumeric, 4,
            "RecentPromptsHelp");
        AddContextRow(_contextTable, "最多历史参考图", _maxContextImagesNumeric, 5,
            "MaxHistoryImagesHelp");
        AddContextRow(_contextTable, "自动附图阈值", _contextAutoAttachThresholdNumeric, 6,
            "AutoAttachThresholdHelp");
        AddContextRow(_contextTable, "智能引用历史图", _referenceDetectionCheck, 7,
            "SmartReferenceImagesHelp");
        AddContextRow(_contextTable, "提示词上下文", _promptEnhancementCheck, 8,
            "PromptContextHelp");
        AddContextRow(_contextTable, "显示决策提示", _showContextDecisionHintCheck, 9,
            "ShowDecisionHintHelp");
        AddContextRow(_contextTable, "附件叠加历史图", _allowHistoryImagesWithManualAttachmentsCheck, 10,
            "MergeAttachmentsHistoryHelp");

        var helpLabel = new Label
        {
            Text = "智能上下文会根据本轮提示词、最近提示词、历史生成图和手动附件自动判断是否需要补充上下文。",
            Tag = "ContextHelp",
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
            Tag = row switch
            {
                0 => "ConversationStoreDir",
                1 => "ActiveMessages",
                2 => "CompressionTrigger",
                3 => "KeepRecent",
                4 => "RecentPrompts",
                5 => "MaxHistoryImages",
                6 => "AutoAttachThreshold",
                7 => "SmartReferenceImages",
                8 => "PromptContext",
                9 => "ShowDecisionHint",
                10 => "MergeAttachmentsHistory",
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
}
