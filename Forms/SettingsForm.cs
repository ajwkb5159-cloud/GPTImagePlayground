using ImageGenerator.Models;
using ImageGenerator.Services;

namespace ImageGenerator.Forms;

internal partial class SettingsForm : Form
{
    private readonly AppConfig _config;

    public AppConfig Result { get; private set; }

    public SettingsForm(AppConfig currentConfig)
    {
        InitializeComponent();

        _config = currentConfig;
        Result = currentConfig;

        // ── Load current config values ──
        _baseUrlBox.Text = currentConfig.BaseUrl;
        _apiKeyBox.Text = currentConfig.ApiKey;
        _modelBox.Text = currentConfig.Model;
        _outputDirBox.Text = currentConfig.OutputDir;
        _timeoutNumeric.Value = currentConfig.TimeoutMinutes;

        // ── Event wiring ──
        showKeyBtn.Click += ShowKeyBtn_Click;
        browseBtn.Click += BrowseBtn_Click;
        saveBtn.Click += SaveBtn_Click;
        cancelBtn.Click += CancelBtn_Click;
        ConfigureRoundedButtons();
    }

    // ═══════════════════════════════════════════════════
    //  Event Handlers
    // ═══════════════════════════════════════════════════

    private void ConfigureRoundedButtons()
    {
        ApplyRoundedButtonStyle(showKeyBtn, 8);
        ApplyRoundedButtonStyle(browseBtn, 8);
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

    private void ShowKeyBtn_Click(object? sender, EventArgs e)
    {
        _apiKeyBox.UseSystemPasswordChar = !_apiKeyBox.UseSystemPasswordChar;
        var oldImage = showKeyBtn.Image;
        showKeyBtn.Image = LoadIconImage(
            _apiKeyBox.UseSystemPasswordChar ? "visibility-24.png" : "visibility-off-24.png");
        oldImage?.Dispose();
    }

    private void BrowseBtn_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog();
        if (!string.IsNullOrWhiteSpace(_outputDirBox.Text) && Directory.Exists(_outputDirBox.Text))
            dlg.SelectedPath = _outputDirBox.Text;
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _outputDirBox.Text = dlg.SelectedPath;
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
}
