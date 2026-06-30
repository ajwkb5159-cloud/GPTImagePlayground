#nullable disable

namespace ImageGenerator.Forms;

partial class SettingsForm
{
    private System.ComponentModel.IContainer components;

    private TabControl tabs;
    private TabPage basicTab;
    private TabPage sizeTab;
    private TabPage formatTab;

    private TableLayoutPanel basicTable;
    private TextBox _baseUrlBox;
    private TextBox _apiKeyBox;
    private Button showKeyBtn;
    private TextBox _modelBox;
    private TextBox _outputDirBox;
    private Button browseBtn;
    private NumericUpDown _timeoutNumeric;

    private TableLayoutPanel sizeTable;
    private RadioButton _sizeAutoRadio;
    private RadioButton _sizePresetRadio;
    private ComboBox _sizeTierBox;
    private ComboBox _aspectRatioBox;
    private RadioButton _sizeCustomRadio;
    private NumericUpDown _customWidthNumeric;
    private NumericUpDown _customHeightNumeric;
    private Label _sizeHelpLabel;

    private TableLayoutPanel formatTable;
    private ComboBox _outputFormatBox;
    private CheckBox _transparentBackgroundCheck;
    private ComboBox _moderationBox;
    private NumericUpDown _imageCountNumeric;

    private FlowLayoutPanel buttonPanel;
    private Button saveBtn;
    private Button cancelBtn;

    private static Image LoadIconImage(string fileName)
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", fileName);
        if (!File.Exists(iconPath))
            return new Bitmap(24, 24);

        using var stream = File.OpenRead(iconPath);
        using var image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
        return new Bitmap(image);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        tabs = new TabControl();
        basicTab = new TabPage();
        sizeTab = new TabPage();
        formatTab = new TabPage();
        basicTable = new TableLayoutPanel();
        lblApiUrl = new Label();
        _baseUrlBox = new TextBox();
        lblApiKey = new Label();
        _apiKeyBox = new TextBox();
        showKeyBtn = new Button();
        lblModel = new Label();
        _modelBox = new TextBox();
        lblOutputDir = new Label();
        _outputDirBox = new TextBox();
        browseBtn = new Button();
        lblTimeout = new Label();
        _timeoutNumeric = new NumericUpDown();
        sizeTable = new TableLayoutPanel();
        _sizeAutoRadio = new RadioButton();
        _sizePresetRadio = new RadioButton();
        lblSizeTier = new Label();
        _sizeTierBox = new ComboBox();
        lblAspectRatio = new Label();
        _aspectRatioBox = new ComboBox();
        _sizeCustomRadio = new RadioButton();
        lblCustomWidth = new Label();
        _customWidthNumeric = new NumericUpDown();
        lblCustomHeight = new Label();
        _customHeightNumeric = new NumericUpDown();
        _sizeHelpLabel = new Label();
        formatTable = new TableLayoutPanel();
        lblOutputFormat = new Label();
        _outputFormatBox = new ComboBox();
        lblTransparent = new Label();
        _transparentBackgroundCheck = new CheckBox();
        lblModeration = new Label();
        _moderationBox = new ComboBox();
        lblImageCount = new Label();
        _imageCountNumeric = new NumericUpDown();
        buttonPanel = new FlowLayoutPanel();
        saveBtn = new Button();
        cancelBtn = new Button();
        tabs.SuspendLayout();
        basicTab.SuspendLayout();
        sizeTab.SuspendLayout();
        formatTab.SuspendLayout();
        basicTable.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_timeoutNumeric).BeginInit();
        sizeTable.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_customWidthNumeric).BeginInit();
        ((System.ComponentModel.ISupportInitialize)_customHeightNumeric).BeginInit();
        formatTable.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_imageCountNumeric).BeginInit();
        buttonPanel.SuspendLayout();
        SuspendLayout();

        tabs.Controls.Add(basicTab);
        tabs.Controls.Add(sizeTab);
        tabs.Controls.Add(formatTab);
        tabs.Dock = DockStyle.Fill;
        tabs.Location = new Point(20, 20);
        tabs.Name = "tabs";
        tabs.SelectedIndex = 0;
        tabs.Size = new Size(604, 346);
        tabs.TabIndex = 0;

        basicTab.Controls.Add(basicTable);
        basicTab.Location = new Point(4, 26);
        basicTab.Name = "basicTab";
        basicTab.Padding = new Padding(12);
        basicTab.Size = new Size(596, 316);
        basicTab.TabIndex = 0;
        basicTab.Text = "基础";
        basicTab.UseVisualStyleBackColor = true;

        basicTable.ColumnCount = 3;
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
        basicTable.Controls.Add(lblApiUrl, 0, 0);
        basicTable.Controls.Add(_baseUrlBox, 1, 0);
        basicTable.Controls.Add(lblApiKey, 0, 1);
        basicTable.Controls.Add(_apiKeyBox, 1, 1);
        basicTable.Controls.Add(showKeyBtn, 2, 1);
        basicTable.Controls.Add(lblModel, 0, 2);
        basicTable.Controls.Add(_modelBox, 1, 2);
        basicTable.Controls.Add(lblOutputDir, 0, 3);
        basicTable.Controls.Add(_outputDirBox, 1, 3);
        basicTable.Controls.Add(browseBtn, 2, 3);
        basicTable.Controls.Add(lblTimeout, 0, 4);
        basicTable.Controls.Add(_timeoutNumeric, 1, 4);
        basicTable.Dock = DockStyle.Fill;
        basicTable.Location = new Point(12, 12);
        basicTable.Name = "basicTable";
        basicTable.RowCount = 6;
        for (var i = 0; i < 5; i++)
            basicTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        basicTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        basicTable.Size = new Size(572, 292);
        basicTable.TabIndex = 0;

        lblApiUrl.Location = new Point(3, 0);
        lblApiUrl.Name = "lblApiUrl";
        lblApiUrl.Size = new Size(104, 44);
        lblApiUrl.Text = "API 地址";
        lblApiUrl.TextAlign = ContentAlignment.MiddleLeft;

        _baseUrlBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _baseUrlBox.Location = new Point(110, 10);
        _baseUrlBox.Margin = new Padding(0, 7, 8, 7);
        _baseUrlBox.Name = "_baseUrlBox";
        _baseUrlBox.Size = new Size(368, 24);
        _baseUrlBox.TabIndex = 1;

        lblApiKey.Location = new Point(3, 44);
        lblApiKey.Name = "lblApiKey";
        lblApiKey.Size = new Size(104, 44);
        lblApiKey.Text = "API Key";
        lblApiKey.TextAlign = ContentAlignment.MiddleLeft;

        _apiKeyBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _apiKeyBox.Location = new Point(110, 54);
        _apiKeyBox.Margin = new Padding(0, 7, 8, 7);
        _apiKeyBox.Name = "_apiKeyBox";
        _apiKeyBox.Size = new Size(368, 24);
        _apiKeyBox.TabIndex = 2;
        _apiKeyBox.UseSystemPasswordChar = true;

        showKeyBtn.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        showKeyBtn.BackColor = Color.FromArgb(241, 245, 249);
        showKeyBtn.FlatAppearance.BorderSize = 0;
        showKeyBtn.FlatStyle = FlatStyle.Flat;
        showKeyBtn.Image = LoadIconImage("visibility-24.png");
        showKeyBtn.ImageAlign = ContentAlignment.MiddleCenter;
        showKeyBtn.Location = new Point(486, 52);
        showKeyBtn.Margin = new Padding(0, 7, 0, 7);
        showKeyBtn.Name = "showKeyBtn";
        showKeyBtn.Size = new Size(86, 28);
        showKeyBtn.TabIndex = 3;
        showKeyBtn.UseVisualStyleBackColor = false;

        lblModel.Location = new Point(3, 88);
        lblModel.Name = "lblModel";
        lblModel.Size = new Size(104, 44);
        lblModel.Text = "模型";
        lblModel.TextAlign = ContentAlignment.MiddleLeft;

        _modelBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _modelBox.Location = new Point(110, 98);
        _modelBox.Margin = new Padding(0, 7, 8, 7);
        _modelBox.Name = "_modelBox";
        _modelBox.Size = new Size(368, 24);
        _modelBox.TabIndex = 4;

        lblOutputDir.Location = new Point(3, 132);
        lblOutputDir.Name = "lblOutputDir";
        lblOutputDir.Size = new Size(104, 44);
        lblOutputDir.Text = "输出目录";
        lblOutputDir.TextAlign = ContentAlignment.MiddleLeft;

        _outputDirBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _outputDirBox.Location = new Point(110, 142);
        _outputDirBox.Margin = new Padding(0, 7, 8, 7);
        _outputDirBox.Name = "_outputDirBox";
        _outputDirBox.Size = new Size(368, 24);
        _outputDirBox.TabIndex = 5;

        browseBtn.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        browseBtn.Location = new Point(486, 140);
        browseBtn.Margin = new Padding(0, 7, 0, 7);
        browseBtn.Name = "browseBtn";
        browseBtn.Size = new Size(86, 28);
        browseBtn.TabIndex = 6;
        browseBtn.Text = "浏览...";

        lblTimeout.Location = new Point(3, 176);
        lblTimeout.Name = "lblTimeout";
        lblTimeout.Size = new Size(104, 44);
        lblTimeout.Text = "超时(分钟)";
        lblTimeout.TextAlign = ContentAlignment.MiddleLeft;

        _timeoutNumeric.Anchor = AnchorStyles.Left;
        _timeoutNumeric.Location = new Point(110, 186);
        _timeoutNumeric.Margin = new Padding(0, 7, 8, 7);
        _timeoutNumeric.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
        _timeoutNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _timeoutNumeric.Name = "_timeoutNumeric";
        _timeoutNumeric.Size = new Size(96, 24);
        _timeoutNumeric.TabIndex = 7;
        _timeoutNumeric.Value = new decimal(new int[] { 1, 0, 0, 0 });

        sizeTab.Controls.Add(sizeTable);
        sizeTab.Location = new Point(4, 26);
        sizeTab.Name = "sizeTab";
        sizeTab.Padding = new Padding(12);
        sizeTab.Size = new Size(596, 316);
        sizeTab.TabIndex = 1;
        sizeTab.Text = "尺寸";
        sizeTab.UseVisualStyleBackColor = true;

        sizeTable.ColumnCount = 4;
        sizeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
        sizeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        sizeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
        sizeTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        sizeTable.Controls.Add(_sizeAutoRadio, 0, 0);
        sizeTable.Controls.Add(_sizePresetRadio, 0, 1);
        sizeTable.Controls.Add(lblSizeTier, 1, 1);
        sizeTable.Controls.Add(_sizeTierBox, 2, 1);
        sizeTable.Controls.Add(lblAspectRatio, 1, 2);
        sizeTable.Controls.Add(_aspectRatioBox, 2, 2);
        sizeTable.Controls.Add(_sizeCustomRadio, 0, 3);
        sizeTable.Controls.Add(lblCustomWidth, 1, 3);
        sizeTable.Controls.Add(_customWidthNumeric, 2, 3);
        sizeTable.Controls.Add(lblCustomHeight, 1, 4);
        sizeTable.Controls.Add(_customHeightNumeric, 2, 4);
        sizeTable.Controls.Add(_sizeHelpLabel, 0, 6);
        sizeTable.Dock = DockStyle.Fill;
        sizeTable.Location = new Point(12, 12);
        sizeTable.Name = "sizeTable";
        sizeTable.RowCount = 7;
        for (var i = 0; i < 6; i++)
            sizeTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        sizeTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        sizeTable.Size = new Size(572, 292);
        sizeTable.TabIndex = 0;

        _sizeAutoRadio.AutoSize = true;
        sizeTable.SetColumnSpan(_sizeAutoRadio, 4);
        _sizeAutoRadio.Location = new Point(3, 9);
        _sizeAutoRadio.Margin = new Padding(3, 9, 3, 3);
        _sizeAutoRadio.Name = "_sizeAutoRadio";
        _sizeAutoRadio.Size = new Size(86, 21);
        _sizeAutoRadio.TabIndex = 0;
        _sizeAutoRadio.Text = "自动 auto";
        _sizeAutoRadio.UseVisualStyleBackColor = true;

        _sizePresetRadio.AutoSize = true;
        _sizePresetRadio.Location = new Point(3, 49);
        _sizePresetRadio.Margin = new Padding(3, 9, 3, 3);
        _sizePresetRadio.Name = "_sizePresetRadio";
        _sizePresetRadio.Size = new Size(83, 21);
        _sizePresetRadio.TabIndex = 1;
        _sizePresetRadio.Text = "比例预设";
        _sizePresetRadio.UseVisualStyleBackColor = true;

        lblSizeTier.Anchor = AnchorStyles.Left;
        lblSizeTier.AutoSize = true;
        lblSizeTier.Name = "lblSizeTier";
        lblSizeTier.Text = "分辨率";
        lblSizeTier.TextAlign = ContentAlignment.MiddleLeft;

        _sizeTierBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _sizeTierBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _sizeTierBox.Items.AddRange(new object[] { "1K", "2K", "4K" });
        _sizeTierBox.Location = new Point(344, 48);
        _sizeTierBox.Margin = new Padding(0, 7, 8, 7);
        _sizeTierBox.Name = "_sizeTierBox";
        _sizeTierBox.Size = new Size(106, 25);
        _sizeTierBox.TabIndex = 2;

        lblAspectRatio.Anchor = AnchorStyles.Left;
        lblAspectRatio.AutoSize = true;
        lblAspectRatio.Name = "lblAspectRatio";
        lblAspectRatio.Text = "图像比例";
        lblAspectRatio.TextAlign = ContentAlignment.MiddleLeft;

        _aspectRatioBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _aspectRatioBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _aspectRatioBox.Items.AddRange(new object[] { "1:1", "3:2", "2:3", "16:9", "9:16", "4:3", "3:4", "21:9" });
        _aspectRatioBox.Location = new Point(344, 88);
        _aspectRatioBox.Margin = new Padding(0, 7, 8, 7);
        _aspectRatioBox.Name = "_aspectRatioBox";
        _aspectRatioBox.Size = new Size(106, 25);
        _aspectRatioBox.TabIndex = 3;

        _sizeCustomRadio.AutoSize = true;
        _sizeCustomRadio.Location = new Point(3, 129);
        _sizeCustomRadio.Margin = new Padding(3, 9, 3, 3);
        _sizeCustomRadio.Name = "_sizeCustomRadio";
        _sizeCustomRadio.Size = new Size(83, 21);
        _sizeCustomRadio.TabIndex = 4;
        _sizeCustomRadio.Text = "自定义";
        _sizeCustomRadio.UseVisualStyleBackColor = true;

        lblCustomWidth.Anchor = AnchorStyles.Left;
        lblCustomWidth.AutoSize = true;
        lblCustomWidth.Name = "lblCustomWidth";
        lblCustomWidth.Text = "宽度";

        _customWidthNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _customWidthNumeric.Increment = new decimal(new int[] { 16, 0, 0, 0 });
        _customWidthNumeric.Location = new Point(344, 128);
        _customWidthNumeric.Margin = new Padding(0, 7, 8, 7);
        _customWidthNumeric.Maximum = new decimal(new int[] { 20000, 0, 0, 0 });
        _customWidthNumeric.Minimum = new decimal(new int[] { 16, 0, 0, 0 });
        _customWidthNumeric.Name = "_customWidthNumeric";
        _customWidthNumeric.Size = new Size(106, 24);
        _customWidthNumeric.TabIndex = 5;
        _customWidthNumeric.Value = new decimal(new int[] { 1024, 0, 0, 0 });

        lblCustomHeight.Anchor = AnchorStyles.Left;
        lblCustomHeight.AutoSize = true;
        lblCustomHeight.Name = "lblCustomHeight";
        lblCustomHeight.Text = "高度";

        _customHeightNumeric.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _customHeightNumeric.Increment = new decimal(new int[] { 16, 0, 0, 0 });
        _customHeightNumeric.Location = new Point(344, 168);
        _customHeightNumeric.Margin = new Padding(0, 7, 8, 7);
        _customHeightNumeric.Maximum = new decimal(new int[] { 20000, 0, 0, 0 });
        _customHeightNumeric.Minimum = new decimal(new int[] { 16, 0, 0, 0 });
        _customHeightNumeric.Name = "_customHeightNumeric";
        _customHeightNumeric.Size = new Size(106, 24);
        _customHeightNumeric.TabIndex = 6;
        _customHeightNumeric.Value = new decimal(new int[] { 1024, 0, 0, 0 });

        _sizeHelpLabel.AutoSize = true;
        sizeTable.SetColumnSpan(_sizeHelpLabel, 4);
        _sizeHelpLabel.ForeColor = Color.FromArgb(71, 85, 105);
        _sizeHelpLabel.Location = new Point(3, 246);
        _sizeHelpLabel.Margin = new Padding(3, 6, 3, 0);
        _sizeHelpLabel.MaximumSize = new Size(540, 0);
        _sizeHelpLabel.Name = "_sizeHelpLabel";
        _sizeHelpLabel.Size = new Size(532, 34);
        _sizeHelpLabel.Text = "由于模型限制，最终输出会自动规整到合法尺寸：宽高均为16倍数，最大边长3840px，宽高比不超过3:1，总像素限制为655360-8294400。";

        formatTab.Controls.Add(formatTable);
        formatTab.Location = new Point(4, 26);
        formatTab.Name = "formatTab";
        formatTab.Padding = new Padding(12);
        formatTab.Size = new Size(596, 316);
        formatTab.TabIndex = 2;
        formatTab.Text = "格式";
        formatTab.UseVisualStyleBackColor = true;

        formatTable.ColumnCount = 2;
        formatTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        formatTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        formatTable.Controls.Add(lblOutputFormat, 0, 0);
        formatTable.Controls.Add(_outputFormatBox, 1, 0);
        formatTable.Controls.Add(lblTransparent, 0, 1);
        formatTable.Controls.Add(_transparentBackgroundCheck, 1, 1);
        formatTable.Controls.Add(lblModeration, 0, 2);
        formatTable.Controls.Add(_moderationBox, 1, 2);
        formatTable.Controls.Add(lblImageCount, 0, 3);
        formatTable.Controls.Add(_imageCountNumeric, 1, 3);
        formatTable.Dock = DockStyle.Fill;
        formatTable.Location = new Point(12, 12);
        formatTable.Name = "formatTable";
        formatTable.RowCount = 5;
        for (var i = 0; i < 4; i++)
            formatTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        formatTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        formatTable.Size = new Size(572, 292);
        formatTable.TabIndex = 0;

        lblOutputFormat.Location = new Point(3, 0);
        lblOutputFormat.Name = "lblOutputFormat";
        lblOutputFormat.Size = new Size(114, 44);
        lblOutputFormat.Text = "格式";
        lblOutputFormat.TextAlign = ContentAlignment.MiddleLeft;

        _outputFormatBox.Anchor = AnchorStyles.Left;
        _outputFormatBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _outputFormatBox.Items.AddRange(new object[] { "png", "jpeg", "webp" });
        _outputFormatBox.Location = new Point(120, 9);
        _outputFormatBox.Margin = new Padding(0, 7, 8, 7);
        _outputFormatBox.Name = "_outputFormatBox";
        _outputFormatBox.Size = new Size(160, 25);
        _outputFormatBox.TabIndex = 0;

        lblTransparent.Location = new Point(3, 44);
        lblTransparent.Name = "lblTransparent";
        lblTransparent.Size = new Size(114, 44);
        lblTransparent.Text = "透明背景";
        lblTransparent.TextAlign = ContentAlignment.MiddleLeft;

        _transparentBackgroundCheck.Anchor = AnchorStyles.Left;
        _transparentBackgroundCheck.AutoSize = true;
        _transparentBackgroundCheck.Location = new Point(120, 55);
        _transparentBackgroundCheck.Margin = new Padding(0, 7, 8, 7);
        _transparentBackgroundCheck.Name = "_transparentBackgroundCheck";
        _transparentBackgroundCheck.Size = new Size(53, 21);
        _transparentBackgroundCheck.TabIndex = 1;
        _transparentBackgroundCheck.Text = "true";
        _transparentBackgroundCheck.UseVisualStyleBackColor = true;

        lblModeration.Location = new Point(3, 88);
        lblModeration.Name = "lblModeration";
        lblModeration.Size = new Size(114, 44);
        lblModeration.Text = "审核";
        lblModeration.TextAlign = ContentAlignment.MiddleLeft;

        _moderationBox.Anchor = AnchorStyles.Left;
        _moderationBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _moderationBox.Items.AddRange(new object[] { "auto", "low" });
        _moderationBox.Location = new Point(120, 97);
        _moderationBox.Margin = new Padding(0, 7, 8, 7);
        _moderationBox.Name = "_moderationBox";
        _moderationBox.Size = new Size(160, 25);
        _moderationBox.TabIndex = 2;

        lblImageCount.Location = new Point(3, 132);
        lblImageCount.Name = "lblImageCount";
        lblImageCount.Size = new Size(114, 44);
        lblImageCount.Text = "数量";
        lblImageCount.TextAlign = ContentAlignment.MiddleLeft;

        _imageCountNumeric.Anchor = AnchorStyles.Left;
        _imageCountNumeric.Location = new Point(120, 142);
        _imageCountNumeric.Margin = new Padding(0, 7, 8, 7);
        _imageCountNumeric.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
        _imageCountNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _imageCountNumeric.Name = "_imageCountNumeric";
        _imageCountNumeric.Size = new Size(96, 24);
        _imageCountNumeric.TabIndex = 3;
        _imageCountNumeric.Value = new decimal(new int[] { 1, 0, 0, 0 });

        buttonPanel.Controls.Add(saveBtn);
        buttonPanel.Controls.Add(cancelBtn);
        buttonPanel.Dock = DockStyle.Bottom;
        buttonPanel.FlowDirection = FlowDirection.RightToLeft;
        buttonPanel.Location = new Point(20, 366);
        buttonPanel.Name = "buttonPanel";
        buttonPanel.Size = new Size(604, 46);
        buttonPanel.TabIndex = 1;
        buttonPanel.WrapContents = false;

        saveBtn.BackColor = Color.FromArgb(59, 130, 246);
        saveBtn.FlatAppearance.BorderSize = 0;
        saveBtn.FlatStyle = FlatStyle.Flat;
        saveBtn.ForeColor = Color.White;
        saveBtn.Location = new Point(508, 0);
        saveBtn.Margin = new Padding(0);
        saveBtn.Name = "saveBtn";
        saveBtn.Size = new Size(96, 34);
        saveBtn.TabIndex = 0;
        saveBtn.Text = "保存";
        saveBtn.UseVisualStyleBackColor = false;

        cancelBtn.BackColor = Color.FromArgb(241, 245, 249);
        cancelBtn.FlatAppearance.BorderSize = 0;
        cancelBtn.FlatStyle = FlatStyle.Flat;
        cancelBtn.ForeColor = Color.FromArgb(71, 85, 105);
        cancelBtn.Location = new Point(400, 0);
        cancelBtn.Margin = new Padding(0, 0, 12, 0);
        cancelBtn.Name = "cancelBtn";
        cancelBtn.Size = new Size(96, 34);
        cancelBtn.TabIndex = 1;
        cancelBtn.Text = "取消";
        cancelBtn.UseVisualStyleBackColor = false;

        AcceptButton = saveBtn;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        CancelButton = cancelBtn;
        ClientSize = new Size(644, 432);
        Controls.Add(tabs);
        Controls.Add(buttonPanel);
        Font = new Font("Microsoft YaHei UI", 10F);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimumSize = new Size(460, 360);
        MinimizeBox = false;
        Name = "SettingsForm";
        Padding = new Padding(20);
        StartPosition = FormStartPosition.CenterParent;
        Text = "设置";
        tabs.ResumeLayout(false);
        basicTab.ResumeLayout(false);
        sizeTab.ResumeLayout(false);
        formatTab.ResumeLayout(false);
        basicTable.ResumeLayout(false);
        basicTable.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_timeoutNumeric).EndInit();
        sizeTable.ResumeLayout(false);
        sizeTable.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_customWidthNumeric).EndInit();
        ((System.ComponentModel.ISupportInitialize)_customHeightNumeric).EndInit();
        formatTable.ResumeLayout(false);
        formatTable.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_imageCountNumeric).EndInit();
        buttonPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    private Label lblApiUrl;
    private Label lblApiKey;
    private Label lblModel;
    private Label lblOutputDir;
    private Label lblTimeout;
    private Label lblSizeTier;
    private Label lblAspectRatio;
    private Label lblCustomWidth;
    private Label lblCustomHeight;
    private Label lblOutputFormat;
    private Label lblTransparent;
    private Label lblModeration;
    private Label lblImageCount;
}
