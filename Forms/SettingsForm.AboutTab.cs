using ImageGenerator.Services;

namespace ImageGenerator.Forms;

internal partial class SettingsForm
{
    private const string AppDisplayName = "GPT Image Playground";

    private TabPage _aboutTab = null!;
    private TableLayoutPanel _aboutTable = null!;
    private Panel _aboutScrollPanel = null!;
    private Label _appNameLabel = null!;
    private Label _versionCaptionLabel = null!;
    private Label _versionValueLabel = null!;
    private Label _updateCaptionLabel = null!;
    private Button _checkUpdateBtn = null!;
    private Label _updateStatusLabel = null!;
    private Label _aboutDescriptionLabel = null!;

    private CancellationTokenSource? _updateCts;
    private bool _isCheckingUpdate;

    private void BuildAboutTab()
    {
        _aboutTab = new TabPage
        {
            Padding = new Padding(12),
            UseVisualStyleBackColor = true,
        };

        _aboutScrollPanel = new Panel
        {
            AutoScroll = true,
            Dock = DockStyle.Fill,
        };

        _aboutTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 5,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        _aboutTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        _aboutTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _aboutTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        _aboutTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        _aboutTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        _aboutTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        _aboutTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _appNameLabel = new Label
        {
            Text = AppDisplayName,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold),
            AutoEllipsis = true,
        };
        _aboutTable.SetColumnSpan(_appNameLabel, 2);

        _versionCaptionLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _versionValueLabel = new Label
        {
            Text = UpdateService.CurrentVersion,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _updateCaptionLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _checkUpdateBtn = new Button
        {
            Anchor = AnchorStyles.Left,
            Size = new Size(150, 32),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
        };
        _checkUpdateBtn.FlatAppearance.BorderSize = 0;
        ApplyRoundedButtonStyle(_checkUpdateBtn, 8);
        _checkUpdateBtn.Click += CheckUpdateBtn_Click;

        _updateStatusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = SystemColors.GrayText,
            AutoEllipsis = true,
        };
        _aboutTable.SetColumnSpan(_updateStatusLabel, 2);

        _aboutDescriptionLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            MaximumSize = new Size(520, 0),
        };
        _aboutTable.SetColumnSpan(_aboutDescriptionLabel, 2);

        _aboutTable.Controls.Add(_appNameLabel, 0, 0);
        _aboutTable.Controls.Add(_versionCaptionLabel, 0, 1);
        _aboutTable.Controls.Add(_versionValueLabel, 1, 1);
        _aboutTable.Controls.Add(_updateCaptionLabel, 0, 2);
        _aboutTable.Controls.Add(_checkUpdateBtn, 1, 2);
        _aboutTable.Controls.Add(_updateStatusLabel, 0, 3);
        _aboutTable.Controls.Add(_aboutDescriptionLabel, 0, 4);

        _aboutScrollPanel.Controls.Add(_aboutTable);
        _aboutTab.Controls.Add(_aboutScrollPanel);
        tabs.Controls.Add(_aboutTab);
    }

    private async void CheckUpdateBtn_Click(object? sender, EventArgs e)
    {
        if (_isCheckingUpdate)
            return;

        _isCheckingUpdate = true;
        _checkUpdateBtn.Enabled = false;
        _updateStatusLabel.ForeColor = SystemColors.GrayText;
        _updateStatusLabel.Text = T("Checking");

        _updateCts?.Dispose();
        _updateCts = new CancellationTokenSource(TimeSpan.FromMinutes(11));
        var token = _updateCts.Token;

        try
        {
            var service = new UpdateService(_verifySslCertificateCheck.Checked);
            var result = await service.CheckForUpdateAsync(token);

            if (!result.IsUpdateAvailable)
            {
                _updateStatusLabel.Text = string.Format(T("AlreadyLatest"), result.CurrentVersion);
                return;
            }

            if (string.IsNullOrWhiteSpace(result.DownloadUrl))
            {
                _updateStatusLabel.Text = T("NoUpdateAsset");
                return;
            }

            var prompt = string.Format(T("UpdateAvailable"), result.LatestVersion, result.CurrentVersion);
            var confirm = MessageBox.Show(
                this,
                prompt,
                T("UpdateTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (confirm != DialogResult.Yes)
            {
                _updateStatusLabel.Text = "";
                return;
            }

            await DownloadAndApplyUpdateAsync(service, result.DownloadUrl, token);
        }
        catch (OperationCanceledException)
        {
            _updateStatusLabel.ForeColor = Color.FromArgb(220, 38, 38);
            _updateStatusLabel.Text = T("UpdateCheckCanceled");
        }
        catch (Exception ex)
        {
            _updateStatusLabel.ForeColor = Color.FromArgb(220, 38, 38);
            _updateStatusLabel.Text = string.Format(T("UpdateCheckFailed"), ex.Message);
        }
        finally
        {
            _isCheckingUpdate = false;
            if (!IsDisposed)
                _checkUpdateBtn.Enabled = true;
        }
    }

    private async Task DownloadAndApplyUpdateAsync(
        UpdateService service,
        string downloadUrl,
        CancellationToken token)
    {
        var progress = new Progress<int>(percent =>
        {
            if (IsDisposed)
                return;

            _updateStatusLabel.Text = percent < 0
                ? T("Downloading")
                : string.Format(T("DownloadingPercent"), percent);
        });

        _updateStatusLabel.Text = T("Downloading");
        var zipPath = await service.DownloadPackageAsync(downloadUrl, progress, token);

        _updateStatusLabel.Text = T("InstallingUpdate");
        service.LaunchUpdaterAndPrepareExit(zipPath);

        MessageBox.Show(
            this,
            T("RestartToUpdate"),
            T("UpdateTitle"),
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        Application.Exit();
    }
}
