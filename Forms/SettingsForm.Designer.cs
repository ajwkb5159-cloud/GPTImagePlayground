#nullable disable

namespace ImageGenerator.Forms;

partial class SettingsForm
{
    private System.ComponentModel.IContainer components;

    private TableLayoutPanel table;
    private TextBox _baseUrlBox;
    private TextBox _apiKeyBox;
    private Button showKeyBtn;
    private TextBox _modelBox;
    private TextBox _outputDirBox;
    private Button browseBtn;
    private NumericUpDown _timeoutNumeric;
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
        table = new TableLayoutPanel();
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
        buttonPanel = new FlowLayoutPanel();
        saveBtn = new Button();
        cancelBtn = new Button();
        table.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_timeoutNumeric).BeginInit();
        buttonPanel.SuspendLayout();
        SuspendLayout();
        // 
        // table
        // 
        table.ColumnCount = 3;
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84F));
        table.Controls.Add(lblApiUrl, 0, 0);
        table.Controls.Add(_baseUrlBox, 1, 0);
        table.Controls.Add(lblApiKey, 0, 1);
        table.Controls.Add(_apiKeyBox, 1, 1);
        table.Controls.Add(showKeyBtn, 2, 1);
        table.Controls.Add(lblModel, 0, 2);
        table.Controls.Add(_modelBox, 1, 2);
        table.Controls.Add(lblOutputDir, 0, 3);
        table.Controls.Add(_outputDirBox, 1, 3);
        table.Controls.Add(browseBtn, 2, 3);
        table.Controls.Add(lblTimeout, 0, 4);
        table.Controls.Add(_timeoutNumeric, 1, 4);
        table.Controls.Add(buttonPanel, 0, 5);
        table.Dock = DockStyle.Fill;
        table.Location = new Point(20, 20);
        table.Name = "table";
        table.RowCount = 6;
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        table.Size = new Size(524, 280);
        table.TabIndex = 0;
        // 
        // lblApiUrl
        // 
        lblApiUrl.Location = new Point(3, 0);
        lblApiUrl.Name = "lblApiUrl";
        lblApiUrl.Size = new Size(98, 44);
        lblApiUrl.TabIndex = 0;
        lblApiUrl.Text = "API 地址";
        lblApiUrl.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _baseUrlBox
        // 
        _baseUrlBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _baseUrlBox.Location = new Point(104, 10);
        _baseUrlBox.Margin = new Padding(0, 7, 8, 7);
        _baseUrlBox.Name = "_baseUrlBox";
        _baseUrlBox.Size = new Size(328, 24);
        _baseUrlBox.TabIndex = 1;
        // 
        // lblApiKey
        // 
        lblApiKey.Location = new Point(3, 44);
        lblApiKey.Name = "lblApiKey";
        lblApiKey.Size = new Size(98, 44);
        lblApiKey.TabIndex = 3;
        lblApiKey.Text = "API Key";
        lblApiKey.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _apiKeyBox
        // 
        _apiKeyBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _apiKeyBox.Location = new Point(104, 54);
        _apiKeyBox.Margin = new Padding(0, 7, 8, 7);
        _apiKeyBox.Name = "_apiKeyBox";
        _apiKeyBox.Size = new Size(328, 24);
        _apiKeyBox.TabIndex = 4;
        _apiKeyBox.UseSystemPasswordChar = true;
        // 
        // showKeyBtn
        // 
        showKeyBtn.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        showKeyBtn.BackColor = Color.FromArgb(241, 245, 249);
        showKeyBtn.FlatAppearance.BorderSize = 0;
        showKeyBtn.FlatStyle = FlatStyle.Flat;
        showKeyBtn.Location = new Point(440, 52);
        showKeyBtn.Margin = new Padding(0, 7, 0, 7);
        showKeyBtn.Name = "showKeyBtn";
        showKeyBtn.Size = new Size(84, 28);
        showKeyBtn.TabIndex = 5;
        showKeyBtn.Image = LoadIconImage("visibility-24.png");
        showKeyBtn.ImageAlign = ContentAlignment.MiddleCenter;
        showKeyBtn.UseVisualStyleBackColor = false;
        // 
        // lblModel
        // 
        lblModel.Location = new Point(3, 88);
        lblModel.Name = "lblModel";
        lblModel.Size = new Size(98, 44);
        lblModel.TabIndex = 6;
        lblModel.Text = "模型";
        lblModel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _modelBox
        // 
        _modelBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _modelBox.Location = new Point(104, 98);
        _modelBox.Margin = new Padding(0, 7, 8, 7);
        _modelBox.Name = "_modelBox";
        _modelBox.Size = new Size(328, 24);
        _modelBox.TabIndex = 7;
        // 
        // lblOutputDir
        // 
        lblOutputDir.Location = new Point(3, 132);
        lblOutputDir.Name = "lblOutputDir";
        lblOutputDir.Size = new Size(98, 44);
        lblOutputDir.TabIndex = 9;
        lblOutputDir.Text = "输出目录";
        lblOutputDir.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _outputDirBox
        // 
        _outputDirBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _outputDirBox.Location = new Point(104, 142);
        _outputDirBox.Margin = new Padding(0, 7, 8, 7);
        _outputDirBox.Name = "_outputDirBox";
        _outputDirBox.Size = new Size(328, 24);
        _outputDirBox.TabIndex = 10;
        // 
        // browseBtn
        // 
        browseBtn.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        browseBtn.Location = new Point(440, 140);
        browseBtn.Margin = new Padding(0, 7, 0, 7);
        browseBtn.Name = "browseBtn";
        browseBtn.Size = new Size(84, 28);
        browseBtn.TabIndex = 11;
        browseBtn.Text = "浏览...";
        // 
        // lblTimeout
        // 
        lblTimeout.Location = new Point(3, 176);
        lblTimeout.Name = "lblTimeout";
        lblTimeout.Size = new Size(98, 44);
        lblTimeout.TabIndex = 12;
        lblTimeout.Text = "超时(分钟)";
        lblTimeout.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _timeoutNumeric
        // 
        _timeoutNumeric.Anchor = AnchorStyles.Left;
        _timeoutNumeric.Location = new Point(104, 186);
        _timeoutNumeric.Margin = new Padding(0, 7, 8, 7);
        _timeoutNumeric.Maximum = new decimal(new int[] { 60, 0, 0, 0 });
        _timeoutNumeric.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        _timeoutNumeric.Name = "_timeoutNumeric";
        _timeoutNumeric.Size = new Size(96, 24);
        _timeoutNumeric.TabIndex = 13;
        _timeoutNumeric.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // buttonPanel
        // 
        table.SetColumnSpan(buttonPanel, 3);
        buttonPanel.Controls.Add(saveBtn);
        buttonPanel.Controls.Add(cancelBtn);
        buttonPanel.Dock = DockStyle.Fill;
        buttonPanel.FlowDirection = FlowDirection.RightToLeft;
        buttonPanel.Location = new Point(0, 234);
        buttonPanel.Margin = new Padding(0, 14, 0, 0);
        buttonPanel.Name = "buttonPanel";
        buttonPanel.Size = new Size(524, 46);
        buttonPanel.TabIndex = 15;
        buttonPanel.WrapContents = false;
        // 
        // saveBtn
        // 
        saveBtn.BackColor = Color.FromArgb(59, 130, 246);
        saveBtn.FlatAppearance.BorderSize = 0;
        saveBtn.FlatStyle = FlatStyle.Flat;
        saveBtn.ForeColor = Color.White;
        saveBtn.Location = new Point(428, 0);
        saveBtn.Margin = new Padding(0);
        saveBtn.Name = "saveBtn";
        saveBtn.Size = new Size(96, 34);
        saveBtn.TabIndex = 0;
        saveBtn.Text = "保存";
        saveBtn.UseVisualStyleBackColor = false;
        // 
        // cancelBtn
        // 
        cancelBtn.BackColor = Color.FromArgb(241, 245, 249);
        cancelBtn.FlatAppearance.BorderSize = 0;
        cancelBtn.FlatStyle = FlatStyle.Flat;
        cancelBtn.ForeColor = Color.FromArgb(71, 85, 105);
        cancelBtn.Location = new Point(332, 0);
        cancelBtn.Margin = new Padding(0, 0, 12, 0);
        cancelBtn.Name = "cancelBtn";
        cancelBtn.Size = new Size(96, 34);
        cancelBtn.TabIndex = 1;
        cancelBtn.Text = "取消";
        cancelBtn.UseVisualStyleBackColor = false;
        // 
        // SettingsForm
        // 
        AcceptButton = saveBtn;
        BackColor = Color.White;
        CancelButton = cancelBtn;
        ClientSize = new Size(564, 320);
        Controls.Add(table);
        Font = new Font("Microsoft YaHei UI", 10F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SettingsForm";
        Padding = new Padding(20);
        StartPosition = FormStartPosition.CenterParent;
        Text = "设置";
        table.ResumeLayout(false);
        table.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)_timeoutNumeric).EndInit();
        buttonPanel.ResumeLayout(false);
        ResumeLayout(false);
    }

    private Label lblApiUrl;
    private Label lblApiKey;
    private Label lblModel;
    private Label lblOutputDir;
    private Label lblTimeout;
}
