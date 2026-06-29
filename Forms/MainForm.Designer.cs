using ImageGenerator.Models;

#nullable disable

namespace ImageGenerator.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components;

    // ── Top bar ──
    private Panel topBar;
    private Panel titlePanel;
    private PictureBox titleIcon;
    private Label titleLabel;
    private Button settingsBtn;
    private Panel separator;

    // ── Chat area ──
    private Panel chatContainer;
    private FlowLayoutPanel _chatPanel;

    // ── Loading overlay ──
    private Panel _loadingOverlay;
    private Label _loadingLabel;

    // ── Input area ──
    private Panel inputPanel;
    private Panel inputCard;
    private FlowLayoutPanel _thumbnailStrip;
    private Panel promptHost;
    private Panel actionBar;
    private Button _attachBtn;
    private Button _sendBtn;
    private TextBox _promptBox;

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
        this.components = new System.ComponentModel.Container();
        this.topBar = new Panel();
        this.titlePanel = new Panel();
        this.titleIcon = new PictureBox();
        this.titleLabel = new Label();
        this.settingsBtn = new Button();
        this.separator = new Panel();
        this.chatContainer = new Panel();
        this._chatPanel = new FlowLayoutPanel();
        this._loadingOverlay = new Panel();
        this._loadingLabel = new Label();
        this.inputPanel = new Panel();
        this.inputCard = new Panel();
        this._thumbnailStrip = new FlowLayoutPanel();
        this.promptHost = new Panel();
        this.actionBar = new Panel();
        this._attachBtn = new Button();
        this._sendBtn = new Button();
        this._promptBox = new TextBox();

        // ═══════════════════════════════════════════════════
        //  MainForm
        // ═══════════════════════════════════════════════════
        this.SuspendLayout();
        this.topBar.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.titleIcon)).BeginInit();
        this.titlePanel.SuspendLayout();
        this.inputPanel.SuspendLayout();
        this.inputCard.SuspendLayout();
        this.promptHost.SuspendLayout();
        this.actionBar.SuspendLayout();

        this.Text = "GPT Image Playground";
        this.Size = new Size(960, 720);
        this.MinimumSize = new Size(640, 480);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Font = new Font("Microsoft YaHei UI", 10F);
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.KeyPreview = true;

        // ═══════════════════════════════════════════════════
        //  topBar
        // ═══════════════════════════════════════════════════
        this.topBar.Dock = DockStyle.Top;
        this.topBar.Height = 48;
        this.topBar.BackColor = Color.White;
        this.topBar.Padding = new Padding(16, 0, 8, 0);

        //  titlePanel
        this.titlePanel.Dock = DockStyle.Left;
        this.titlePanel.Width = 300;
        this.titlePanel.BackColor = Color.White;

        //  titleIcon
        this.titleIcon.Image = LoadIconImage("app-image-24.png");
        this.titleIcon.Size = new Size(24, 24);
        this.titleIcon.Location = new Point(0, 12);
        this.titleIcon.SizeMode = PictureBoxSizeMode.Zoom;
        this.titleIcon.TabStop = false;

        //  titleLabel
        this.titleLabel.Text = "GPT Image Playground";
        this.titleLabel.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
        this.titleLabel.ForeColor = Color.FromArgb(30, 41, 59);
        this.titleLabel.Location = new Point(32, 0);
        this.titleLabel.Size = new Size(260, 48);
        this.titleLabel.TextAlign = ContentAlignment.MiddleLeft;

        //  settingsBtn
        this.settingsBtn.FlatStyle = FlatStyle.Flat;
        this.settingsBtn.Text = " " + "\u8bbe\u7f6e";
        this.settingsBtn.Image = LoadIconImage("settings-24.png");
        this.settingsBtn.ImageAlign = ContentAlignment.MiddleLeft;
        this.settingsBtn.TextAlign = ContentAlignment.MiddleRight;
        this.settingsBtn.TextImageRelation = TextImageRelation.ImageBeforeText;
        this.settingsBtn.BackColor = Color.Transparent;
        this.settingsBtn.ForeColor = Color.FromArgb(100, 116, 139);
        this.settingsBtn.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        this.settingsBtn.Size = new Size(94, 34);
        this.settingsBtn.Dock = DockStyle.Right;
        this.settingsBtn.Padding = new Padding(6, 0, 8, 0);
        this.settingsBtn.FlatAppearance.BorderSize = 0;

        this.titlePanel.Controls.Add(this.titleLabel);
        this.titlePanel.Controls.Add(this.titleIcon);
        this.topBar.Controls.Add(this.titlePanel);
        this.topBar.Controls.Add(this.settingsBtn);

        // ═══════════════════════════════════════════════════
        //  separator
        // ═══════════════════════════════════════════════════
        this.separator.Dock = DockStyle.Top;
        this.separator.Height = 1;
        this.separator.BackColor = Color.FromArgb(226, 232, 240);

        // ═══════════════════════════════════════════════════
        //  chatContainer
        // ═══════════════════════════════════════════════════
        this.chatContainer.Dock = DockStyle.Fill;
        this.chatContainer.AutoScroll = true;
        this.chatContainer.Padding = new Padding(12);

        //  _chatPanel
        this._chatPanel.FlowDirection = FlowDirection.TopDown;
        this._chatPanel.AutoSize = true;
        this._chatPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this._chatPanel.Width = this.chatContainer.Width - 24;
        this._chatPanel.Padding = new Padding(0);
        this._chatPanel.WrapContents = false;

        this.chatContainer.Controls.Add(this._chatPanel);

        // ═══════════════════════════════════════════════════
        //  _loadingOverlay
        // ═══════════════════════════════════════════════════
        this._loadingOverlay.Visible = false;
        this._loadingOverlay.BackColor = Color.FromArgb(200, 245, 247, 250);
        this._loadingOverlay.Dock = DockStyle.Fill;

        //  _loadingLabel
        this._loadingLabel.Text = "🔄 正在生成图片...";
        this._loadingLabel.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
        this._loadingLabel.ForeColor = Color.FromArgb(59, 130, 246);
        this._loadingLabel.AutoSize = true;
        this._loadingLabel.TextAlign = ContentAlignment.MiddleCenter;

        // ═══════════════════════════════════════════════════
        //  inputPanel
        // ═══════════════════════════════════════════════════
        this.inputPanel.Dock = DockStyle.Bottom;
        this.inputPanel.Height = 176;
        this.inputPanel.BackColor = Color.FromArgb(245, 247, 250);
        this.inputPanel.Padding = new Padding(18, 8, 18, 16);

        //  inputCard
        this.inputCard.Dock = DockStyle.Fill;
        this.inputCard.BackColor = Color.White;
        this.inputCard.Padding = new Padding(12);

        //  _thumbnailStrip
        this._thumbnailStrip.Height = 50;
        this._thumbnailStrip.Dock = DockStyle.Top;
        this._thumbnailStrip.FlowDirection = FlowDirection.LeftToRight;
        this._thumbnailStrip.WrapContents = false;
        this._thumbnailStrip.AutoScroll = true;
        this._thumbnailStrip.Visible = false;
        this._thumbnailStrip.Margin = new Padding(0, 0, 0, 8);

        //  promptHost
        this.promptHost.Dock = DockStyle.Fill;
        this.promptHost.BackColor = Color.FromArgb(248, 250, 252);
        this.promptHost.Padding = new Padding(12, 8, 12, 8);

        //  actionBar
        this.actionBar.Dock = DockStyle.Bottom;
        this.actionBar.Height = 44;
        this.actionBar.Padding = new Padding(0, 8, 0, 0);

        //  _attachBtn
        this._attachBtn.FlatStyle = FlatStyle.Flat;
        this._attachBtn.Text = " " + "\u4e0a\u4f20\u56fe\u7247";
        this._attachBtn.Image = LoadIconImage("upload-image-24.png");
        this._attachBtn.ImageAlign = ContentAlignment.MiddleLeft;
        this._attachBtn.TextAlign = ContentAlignment.MiddleRight;
        this._attachBtn.TextImageRelation = TextImageRelation.ImageBeforeText;
        this._attachBtn.Size = new Size(124, 34);
        this._attachBtn.Dock = DockStyle.Left;
        this._attachBtn.BackColor = Color.FromArgb(241, 245, 249);
        this._attachBtn.ForeColor = Color.FromArgb(71, 85, 105);
        this._attachBtn.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        this._attachBtn.Padding = new Padding(8, 0, 10, 0);
        this._attachBtn.FlatAppearance.BorderSize = 0;

        //  _sendBtn
        this._sendBtn.Text = "▶ 生成图像";
        this._sendBtn.FlatStyle = FlatStyle.Flat;
        this._sendBtn.Size = new Size(116, 34);
        this._sendBtn.Dock = DockStyle.Right;
        this._sendBtn.BackColor = Color.FromArgb(59, 130, 246);
        this._sendBtn.ForeColor = Color.White;
        this._sendBtn.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        this._sendBtn.FlatAppearance.BorderSize = 0;

        //  _promptBox
        this._promptBox.Multiline = true;
        this._promptBox.Dock = DockStyle.Fill;
        this._promptBox.BorderStyle = BorderStyle.None;
        this._promptBox.Font = new Font("Microsoft YaHei UI", 10F);
        this._promptBox.ForeColor = Color.FromArgb(30, 41, 59);
        this._promptBox.PlaceholderText = "输入提示词，描述你想生成的图片...";
        this._promptBox.AcceptsReturn = true;
        this._promptBox.ScrollBars = ScrollBars.Vertical;
        this._promptBox.BackColor = Color.FromArgb(248, 250, 252);
        this._promptBox.Margin = new Padding(0);

        // ── input card layout: thumbnails → prompt → actions ──
        this.promptHost.Controls.Add(this._promptBox);
        this.actionBar.Controls.Add(this._sendBtn);
        this.actionBar.Controls.Add(this._attachBtn);
        this.inputCard.Controls.Add(this.promptHost);
        this.inputCard.Controls.Add(this.actionBar);
        this.inputCard.Controls.Add(this._thumbnailStrip);

        this.inputPanel.Controls.Add(this.inputCard);

        // ═══════════════════════════════════════════════════
        //  Assemble main form (bottom → top docking order)
        // ═══════════════════════════════════════════════════
        this.Controls.Add(this.chatContainer);
        this.Controls.Add(this._loadingOverlay);
        this.Controls.Add(this.separator);
        this.Controls.Add(this.topBar);
        this.Controls.Add(this.inputPanel);

        this.actionBar.ResumeLayout(false);
        this.promptHost.ResumeLayout(false);
        this.promptHost.PerformLayout();
        this.inputCard.ResumeLayout(false);
        this.inputPanel.ResumeLayout(false);
        this.titlePanel.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)(this.titleIcon)).EndInit();
        this.topBar.ResumeLayout(false);
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
