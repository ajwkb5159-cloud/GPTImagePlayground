using System.Runtime.InteropServices;
using ImageGenerator.Models;
using ImageGenerator.Services;

namespace ImageGenerator.Forms;

internal partial class MainForm : Form
{
    // P/Invoke for setting edit-control margins (fixes placeholder text indentation)
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private const int EM_SETMARGINS = 0xD3;
    private const int EC_LEFTMARGIN = 0x0001;
    private const int ReferenceWidth = 960;
    private const int ReferenceHeight = 680;
    private const float MinUiScale = 0.72F;
    private const float MaxUiScale = 1.08F;
    private const float DesignDpi = 96F;

    private readonly ConfigManager _configManager;
    private AppConfig _config;
    private ImageApiService? _apiService;

    // State
    private readonly List<string> _attachedImages = [];
    private bool _isGenerating;
    private Panel? _pendingResponseRow;
    private Panel? _pendingResponseBubble;
    private Label? _pendingResponseLabel;
    private float _uiScale = 1F;
    private bool _isApplyingResponsiveLayout;
    private bool _wasMinimized;
    private bool _restoreLayoutQueued;

    public MainForm()
    {
        InitializeComponent();

        _configManager = new ConfigManager();
        _config = _configManager.Load();
        _apiService = new ImageApiService(_config);

        _chatPanel.AutoSize = false;
        _chatPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // ── Event wiring ──
        settingsBtn.Click += SettingsBtn_Click;
        _attachBtn.Click += AttachBtn_Click;
        _sendBtn.Click += SendBtn_Click;
        _promptBox.KeyDown += PromptBox_KeyDown;

        Resize += (_, _) => HandleResponsiveResize();

        chatContainer.SizeChanged += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
                return;

            UpdateChatPanelBounds();
            ReflowChatRows();
        };

        _loadingOverlay.SizeChanged += (_, _) => CenterLoadingLabel();

        // ── Global key bindings ──
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                TriggerSend();
            }
        };

        // ── Post-handle initialization ──
        Load += (_, _) =>
        {
            FitInitialWindowToScreen();
            ApplyResponsiveLayout();

            // Fix placeholder text positioning — set 12px left margin on the edit control
            if (_promptBox.IsHandleCreated)
                SendMessage(_promptBox.Handle, EM_SETMARGINS, (IntPtr)EC_LEFTMARGIN, (IntPtr)ScaleValue(12));

            UpdateChatPanelBounds();

            AddWelcomeMessage();
        };
    }

    // ═══════════════════════════════════════════════════
    //  Event Handlers
    // ═══════════════════════════════════════════════════

    private void FitInitialWindowToScreen()
    {
        var workArea = Screen.FromControl(this).WorkingArea;
        var maxWidth = Math.Max(MinimumSize.Width, (int)(workArea.Width * 0.88F));
        var maxHeight = Math.Max(MinimumSize.Height, (int)(workArea.Height * 0.88F));
        var targetSize = new Size(Math.Min(Width, maxWidth), Math.Min(Height, maxHeight));

        if (targetSize != Size)
            Size = targetSize;

        Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
    }

    private void HandleResponsiveResize()
    {
        if (WindowState == FormWindowState.Minimized)
        {
            _wasMinimized = true;
            return;
        }

        ApplyResponsiveLayout();

        if (_wasMinimized)
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
            var compact = logicalClientSize.Width < 560 || logicalClientSize.Height < 520;
            var tight = logicalClientSize.Width < 470;

            SuspendLayout();
            topBar.SuspendLayout();
            inputPanel.SuspendLayout();
            inputCard.SuspendLayout();
            actionBar.SuspendLayout();

            topBar.Height = ScaleValue(compact ? 42 : 48);
            topBar.Padding = new Padding(ScaleValue(compact ? 10 : 16), 0, ScaleValue(8), 0);

            settingsBtn.Text = compact ? "" : " " + "\u8bbe\u7f6e";
            settingsBtn.Font = UiFont(compact ? 9F : 10F, FontStyle.Bold);
            settingsBtn.Size = new Size(ScaleValue(compact ? 38 : 94), ScaleValue(compact ? 30 : 34));
            settingsBtn.Padding = new Padding(ScaleValue(compact ? 4 : 6), 0, ScaleValue(compact ? 4 : 8), 0);
            SetScaledButtonImage(settingsBtn, "settings-24.png", compact ? 18 : 20);

            titlePanel.Width = Math.Max(
                ScaleValue(tight ? 150 : 180),
                Math.Min(ScaleValue(300), ClientSize.Width - settingsBtn.Width - ScaleValue(28)));

            titleIcon.Size = new Size(ScaleValue(compact ? 20 : 24), ScaleValue(compact ? 20 : 24));
            titleIcon.Location = new Point(0, Math.Max(0, (topBar.Height - titleIcon.Height) / 2));

            titleLabel.Font = UiFont(compact ? 10.5F : 12F, FontStyle.Bold);
            titleLabel.Location = new Point(ScaleValue(compact ? 28 : 32), 0);
            titleLabel.Size = new Size(Math.Max(0, titlePanel.Width - titleLabel.Left), topBar.Height);

            chatContainer.Padding = new Padding(ScaleValue(compact ? 8 : 12));
            _loadingLabel.Font = UiFont(compact ? 12F : 14F, FontStyle.Bold);

            var inputHeight = (int)Math.Round(ClientSize.Height * (compact ? 0.28F : 0.26F));
            inputPanel.Height = Clamp(inputHeight, ScaleValue(compact ? 124 : 142), ScaleValue(176));
            inputPanel.Padding = compact
                ? new Padding(ScaleValue(10), ScaleValue(6), ScaleValue(10), ScaleValue(10))
                : new Padding(ScaleValue(18), ScaleValue(8), ScaleValue(18), ScaleValue(16));

            inputCard.Padding = new Padding(ScaleValue(compact ? 9 : 12));
            _thumbnailStrip.Height = ScaleValue(compact ? 42 : 50);
            promptHost.Padding = compact
                ? new Padding(ScaleValue(9), ScaleValue(6), ScaleValue(9), ScaleValue(6))
                : new Padding(ScaleValue(12), ScaleValue(8), ScaleValue(12), ScaleValue(8));
            actionBar.Height = ScaleValue(compact ? 38 : 44);
            actionBar.Padding = new Padding(0, ScaleValue(compact ? 6 : 8), 0, 0);

            _attachBtn.Text = tight ? "" : compact ? " " + "\u4e0a\u4f20" : " " + "\u4e0a\u4f20\u56fe\u7247";
            _attachBtn.Font = UiFont(compact ? 9F : 10F, FontStyle.Bold);
            _attachBtn.Size = new Size(ScaleValue(tight ? 38 : compact ? 92 : 124), ScaleValue(compact ? 30 : 34));
            _attachBtn.Padding = new Padding(ScaleValue(compact ? 5 : 8), 0, ScaleValue(compact ? 6 : 10), 0);
            SetScaledButtonImage(_attachBtn, "upload-image-24.png", compact ? 18 : 20);

            _sendBtn.Text = compact ? "\u751f\u6210" : "\u25b6 \u751f\u6210\u56fe\u50cf";
            _sendBtn.Font = UiFont(compact ? 8.5F : 9F, FontStyle.Bold);
            _sendBtn.Size = new Size(ScaleValue(tight ? 74 : compact ? 92 : 116), ScaleValue(compact ? 30 : 34));

            _promptBox.Font = UiFont(compact ? 9F : 10F);
            if (_promptBox.IsHandleCreated)
                SendMessage(_promptBox.Handle, EM_SETMARGINS, (IntPtr)EC_LEFTMARGIN, (IntPtr)ScaleValue(compact ? 8 : 12));

            UpdateChatPanelBounds();
            ReflowChatRows();
            CenterLoadingLabel();
        }
        finally
        {
            actionBar.ResumeLayout(true);
            inputCard.ResumeLayout(true);
            inputPanel.ResumeLayout(true);
            topBar.ResumeLayout(true);
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

    private Font UiFont(float size, FontStyle style = FontStyle.Regular) =>
        new("Microsoft YaHei UI", Math.Max(7.5F, size * _uiScale), style);

    private static int Clamp(int value, int min, int max) =>
        Math.Min(max, Math.Max(min, value));

    private static float Clamp(float value, float min, float max) =>
        Math.Min(max, Math.Max(min, value));

    private void SetScaledButtonImage(Button button, string fileName, int logicalSize)
    {
        var pixelSize = ScaleValue(logicalSize);
        var imageKey = $"{fileName}:{pixelSize}";
        if (button.Tag as string == imageKey)
            return;

        var oldImage = button.Image;
        button.Image = LoadIconImage(fileName, pixelSize);
        button.Tag = imageKey;
        oldImage?.Dispose();
    }

    private static Image LoadIconImage(string fileName, int pixelSize)
    {
        using var source = LoadIconImage(fileName);
        var bitmap = new Bitmap(pixelSize, pixelSize);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(0, 0, pixelSize, pixelSize));
        return bitmap;
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyResponsiveLayout();
        QueueRestoreLayoutRefresh();
    }

    private static void ForceTextLayoutRefresh(Control root)
    {
        foreach (Control child in root.Controls)
            ForceTextLayoutRefresh(child);

        root.PerformLayout();
        root.Invalidate();
    }

    private void SettingsBtn_Click(object? sender, EventArgs e)
    {
        using var dlg = new SettingsForm(_config);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _config = dlg.Result;
            _apiService = new ImageApiService(_config);
            _configManager.Save(_config);
            AddSystemMessage("✅ 设置已保存。");
        }
    }

    private void PromptBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            TriggerSend();
        }
    }

    private void AttachBtn_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "选择参考图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.bmp|所有文件|*.*",
            Multiselect = true,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            foreach (var path in dlg.FileNames)
            {
                if (!_attachedImages.Contains(path))
                {
                    _attachedImages.Add(path);
                    AddThumbnail(path);
                }
            }
        }
    }

    private async void SendBtn_Click(object? sender, EventArgs e)
    {
        TriggerSend();
    }

    // ═══════════════════════════════════════════════════
    //  Core Logic
    // ═══════════════════════════════════════════════════

    private async void TriggerSend()
    {
        if (_isGenerating) return;

        var prompt = _promptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt) && _attachedImages.Count == 0) return;

        _isGenerating = true;

        var attachedCopy = new List<string>(_attachedImages);
        _promptBox.Clear();
        _attachedImages.Clear();
        _thumbnailStrip.Controls.Clear();
        _thumbnailStrip.Visible = false;

        try
        {
            // Show the user's turn immediately, then keep a live assistant placeholder.
            var userMsg = ChatMessage.UserMessage(prompt, [.. attachedCopy]);
            AddChatBubble(userMsg);
            AddPendingResponseBubble("正在请求 API...");
            ShowLoading(true, "正在请求 API...");

            if (_apiService == null)
                _apiService = new ImageApiService(_config);

            var progress = new Progress<string>(msg =>
            {
                if (!IsDisposed)
                    BeginInvoke(() =>
                    {
                        ShowLoading(true, msg);
                        UpdatePendingResponseBubble(msg);
                    });
            });

            using var cts = new CancellationTokenSource(
                TimeSpan.FromMinutes(_config.TimeoutMinutes + 1));

            var result = await Task.Run(() =>
                _apiService.GenerateAsync(prompt, attachedCopy, progress, cts.Token));

            // Create assistant message for each generated image
            for (int i = 0; i < result.SavedPaths.Count; i++)
            {
                var imgPath = result.SavedPaths[i];
                var dataUrl = i < result.DataUrls.Count ? result.DataUrls[i] : null;
                var usage = i == 0 ? result.Usage : null;
                var assistantMsg = ChatMessage.AssistantMessage(prompt, imgPath, dataUrl, usage);
                if (i == 0)
                    CompletePendingResponseWithMessage(assistantMsg);
                else
                    AddChatBubble(assistantMsg);
            }

            AddSystemMessage(
                $"✅ 完成！生成 {result.SavedPaths.Count} 张图片，" +
                $"服务器耗时 {result.ServerTimeSeconds:F0} 秒，" +
                $"总耗时 {result.TotalTimeSeconds:F0} 秒。");
        }
        catch (Exception ex)
        {
            var errorMsg = ex switch
            {
                TaskCanceledException => "请求超时，请检查超时设置或降低图片尺寸后重试。",
                HttpRequestException httpEx => $"API 请求失败: {httpEx.Message}",
                InvalidOperationException opEx => opEx.Message,
                _ => $"未知错误: {ex.Message}",
            };
            CompletePendingResponseWithError(errorMsg);
        }
        finally
        {
            ShowLoading(false);
            _isGenerating = false;
            _promptBox.Focus();
        }
    }

    // ═══════════════════════════════════════════════════
    //  UI Builders
    // ═══════════════════════════════════════════════════

    private void ShowLoading(bool show, string? text = null)
    {
        if (show)
        {
            _loadingLabel.Text = text ?? "🔄 正在生成图片...";
            CenterLoadingLabel();
            _loadingOverlay.Visible = false;
            _sendBtn.Enabled = false;
            _attachBtn.Enabled = false;
        }
        else
        {
            _loadingOverlay.Visible = false;
            _sendBtn.Enabled = true;
            _attachBtn.Enabled = true;
        }
    }

    private void CenterLoadingLabel()
    {
        _loadingLabel.Left = Math.Max(0, (_loadingOverlay.Width - _loadingLabel.PreferredWidth) / 2);
        _loadingLabel.Top = Math.Max(0, (_loadingOverlay.Height - _loadingLabel.PreferredHeight) / 2);
    }

    private void AddChatBubble(ChatMessage msg)
    {
        var bubble = CreateBubble(msg);
        _chatPanel.Controls.Add(bubble);
        ResizeChatPanelHeight();

        ScrollChatToBottom();
    }

    private void ScrollChatToBottom()
    {
        if (IsHandleCreated)
        {
            BeginInvoke(() =>
            {
                if (_chatPanel.Parent is ScrollableControl parent)
                {
                    ResizeChatPanelHeight();
                    var viewportHeight = Math.Max(0, parent.ClientSize.Height - parent.Padding.Vertical);
                    if (GetChatContentHeight() <= viewportHeight)
                    {
                        parent.AutoScrollPosition = Point.Empty;
                        _chatPanel.Location = new Point(parent.Padding.Left, parent.Padding.Top);
                        return;
                    }

                    var scrollY = Math.Max(0, parent.Padding.Top + _chatPanel.Height + parent.Padding.Bottom - parent.ClientSize.Height);
                    parent.AutoScrollPosition = new Point(0, scrollY);
                }
            });
        }
    }

    private void AddSystemMessage(string text)
    {
        AddChatBubble(ChatMessage.SystemMessage(text));
    }

    private void AddWelcomeMessage()
    {
        var rowWidth = GetChatRowWidth();
        var bubble = CreateWelcomeBubble(GetSystemBubbleMaxWidth(rowWidth));
        _chatPanel.Controls.Add(CreateBubbleRow(bubble, ChatRole.System, rowWidth));
        ResizeChatPanelHeight();
        ScrollChatToBottom();
    }

    private Panel CreateWelcomeBubble(int maxWidth)
    {
        var bodyFont = UiFont(9F);
        var actionFont = UiFont(9F, FontStyle.Bold);
        var padding = new Padding(ScaleValue(10), ScaleValue(8), ScaleValue(10), ScaleValue(8));
        var contentMaxWidth = Math.Max(ScaleValue(180), maxWidth - padding.Horizontal);
        var panel = new Panel
        {
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = padding,
            Margin = new Padding(0),
        };

        var title = new Label
        {
            Text = "欢迎使用 GPT Image Playground！输入提示词开始生成图片。",
            AutoSize = true,
            MaximumSize = new Size(contentMaxWidth, 0),
            ForeColor = Color.FromArgb(71, 85, 105),
            BackColor = panel.BackColor,
            Font = bodyFont,
            Location = new Point(padding.Left, padding.Top),
        };
        panel.Controls.Add(title);

        var uploadRow = CreateIconTextRow("上传图片", "可上传参考图", "upload-image-24.png", bodyFont, actionFont);
        uploadRow.Location = new Point(padding.Left, title.Bottom + 4);
        panel.Controls.Add(uploadRow);

        var settingsRow = CreateIconTextRow("设置", "配置 API 参数。", "settings-24.png", bodyFont, actionFont);
        settingsRow.Location = new Point(padding.Left, uploadRow.Bottom + 2);
        panel.Controls.Add(settingsRow);

        var contentWidth = Math.Max(title.Width, Math.Max(uploadRow.Width, settingsRow.Width));
        panel.Size = new Size(contentWidth + padding.Horizontal, settingsRow.Bottom + padding.Bottom);
        return panel;
    }

    private Panel CreateIconTextRow(string actionText, string description, string iconFileName, Font bodyFont, Font actionFont)
    {
        var row = new Panel
        {
            BackColor = Color.FromArgb(241, 245, 249),
            Margin = new Padding(0),
        };

        var prefix = new Label
        {
            Text = "点击",
            AutoSize = true,
            ForeColor = Color.FromArgb(71, 85, 105),
            BackColor = row.BackColor,
            Font = bodyFont,
        };
        row.Controls.Add(prefix);

        var icon = new PictureBox
        {
            Image = LoadIconImage(iconFileName),
            Size = new Size(ScaleValue(18), ScaleValue(18)),
            SizeMode = PictureBoxSizeMode.Zoom,
        };
        row.Controls.Add(icon);

        var action = new Label
        {
            Text = actionText,
            AutoSize = true,
            ForeColor = Color.FromArgb(51, 65, 85),
            BackColor = row.BackColor,
            Font = actionFont,
        };
        row.Controls.Add(action);

        var suffix = new Label
        {
            Text = description,
            AutoSize = true,
            ForeColor = Color.FromArgb(71, 85, 105),
            BackColor = row.BackColor,
            Font = bodyFont,
        };
        row.Controls.Add(suffix);

        var gap = ScaleValue(4);
        var x = 0;
        var rowHeight = Math.Max(ScaleValue(18), new[] { prefix.Height, icon.Height, action.Height, suffix.Height }.Max());
        foreach (Control control in row.Controls)
        {
            control.Location = new Point(x, Math.Max(0, (rowHeight - control.Height) / 2));
            x = control.Right + gap;
        }

        row.Size = new Size(Math.Max(0, x - gap), rowHeight);
        return row;
    }

    private Panel CreateBubble(ChatMessage msg)
    {
        var rowWidth = GetChatRowWidth();
        var bubbleWidth = GetBubbleMaxWidth(rowWidth);

        var bubble = msg.Role switch
        {
            ChatRole.User => CreateUserBubble(msg, bubbleWidth),
            ChatRole.Assistant => CreateAssistantBubble(msg, bubbleWidth),
            ChatRole.System => CreateSystemBubble(msg, GetSystemBubbleMaxWidth(rowWidth)),
            _ => new Panel(),
        };

        return CreateBubbleRow(bubble, msg.Role, rowWidth);
    }

    private Panel CreateBubbleRow(Panel bubble, ChatRole role, int rowWidth)
    {
        var row = new Panel
        {
            Width = rowWidth,
            Height = Math.Max(ScaleValue(36), bubble.GetPreferredSize(new Size(bubble.MaximumSize.Width, 0)).Height + ScaleValue(8)),
            Margin = new Padding(0, 4, 0, 4),
            BackColor = Color.Transparent,
            Tag = "chat-row",
        };

        bubble.Margin = new Padding(0);
        row.Controls.Add(bubble);

        row.Layout += (_, _) => LayoutBubbleRow(row, role);
        row.Resize += (_, _) => LayoutBubbleRow(row, role);
        LayoutBubbleRow(row, bubble, role);

        return row;
    }

    private void LayoutBubbleRow(Panel row, ChatRole role)
    {
        if (row.Controls.Count == 0) return;
        LayoutBubbleRow(row, row.Controls[0], role);
    }

    private void LayoutBubbleRow(Panel row, Control bubble, ChatRole role)
    {
        var availableWidth = Math.Max(ScaleValue(180), row.ClientSize.Width);
        if (bubble.AutoSize)
        {
            var preferred = bubble.GetPreferredSize(new Size(availableWidth, 0));
            if (preferred.Width > 0 && preferred.Height > 0)
                bubble.Size = new Size(Math.Min(preferred.Width, availableWidth), preferred.Height);
        }
        else if (bubble.Width > availableWidth)
        {
            bubble.Width = availableWidth;
        }

        row.Height = Math.Max(ScaleValue(36), bubble.Height + ScaleValue(8));
        bubble.Top = 0;
        bubble.Left = role switch
        {
            ChatRole.User => Math.Max(0, row.ClientSize.Width - bubble.Width),
            ChatRole.System => Math.Max(0, (row.ClientSize.Width - bubble.Width) / 2),
            _ => 0,
        };
    }

    private void ReflowChatRows()
    {
        var rowWidth = GetChatRowWidth();
        foreach (Control control in _chatPanel.Controls)
        {
            if (control.Tag as string == "chat-row")
            {
                control.Width = rowWidth;
                control.PerformLayout();
            }
        }
        ResizeChatPanelHeight();
    }

    private int GetChatRowWidth()
    {
        var width = _chatPanel.Width > 0
            ? _chatPanel.Width
            : chatContainer.ClientSize.Width - chatContainer.Padding.Horizontal;
        return Math.Max(width, ScaleValue(220));
    }

    private void UpdateChatPanelBounds()
    {
        var width = Math.Max(ScaleValue(220), chatContainer.ClientSize.Width - chatContainer.Padding.Horizontal);
        _chatPanel.Location = new Point(chatContainer.Padding.Left, chatContainer.Padding.Top);
        _chatPanel.Width = width;
        _chatPanel.MaximumSize = new Size(width, 0);
        ResizeChatPanelHeight();
    }

    private int GetBubbleMaxWidth(int rowWidth)
    {
        var ratio = rowWidth < ScaleValue(560) ? 0.92F : 0.72F;
        var minWidth = Math.Min(rowWidth, ScaleValue(180));
        return Clamp((int)Math.Round(rowWidth * ratio), minWidth, rowWidth);
    }

    private int GetSystemBubbleMaxWidth(int rowWidth)
    {
        var horizontalInset = ScaleValue(rowWidth < ScaleValue(560) ? 16 : 40);
        return Clamp(rowWidth - horizontalInset, Math.Min(rowWidth, ScaleValue(180)), Math.Min(rowWidth, ScaleValue(520)));
    }

    private void ResizeChatPanelHeight()
    {
        if (_chatPanel.IsDisposed) return;

        _chatPanel.PerformLayout();
        var contentHeight = GetChatContentHeight();
        var viewportHeight = Math.Max(0, chatContainer.ClientSize.Height - chatContainer.Padding.Vertical);
        _chatPanel.Height = contentHeight <= viewportHeight
            ? contentHeight
            : contentHeight + ScaleValue(8);
    }

    private int GetChatContentHeight()
    {
        var contentHeight = 0;
        foreach (Control control in _chatPanel.Controls)
        {
            contentHeight = Math.Max(contentHeight, control.Bottom + control.Margin.Bottom);
        }

        return contentHeight;
    }

    private Panel CreateUserBubble(ChatMessage msg, int maxWidth)
    {
        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MaximumSize = new Size(maxWidth, 0),
            BackColor = Color.FromArgb(219, 234, 254),
            Padding = new Padding(ScaleValue(12)),
            Margin = new Padding(0),
        };
        panel.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(panel.BackColor);
            e.Graphics.FillRectangle(brush, panel.ClientRectangle);
            ControlPaint.DrawBorder(e.Graphics, panel.ClientRectangle,
                panel.BackColor, 1, ButtonBorderStyle.Solid,
                panel.BackColor, 1, ButtonBorderStyle.Solid,
                panel.BackColor, 1, ButtonBorderStyle.Solid,
                Color.FromArgb(147, 197, 253), 4, ButtonBorderStyle.Solid);
        };

        var label = new Label
        {
            Text = string.IsNullOrWhiteSpace(msg.Prompt) ? "(图片)" : msg.Prompt,
            AutoSize = true,
            MaximumSize = new Size(maxWidth - ScaleValue(30), 0),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = UiFont(10F),
        };
        label.ContextMenuStrip = CreatePromptContextMenu(msg.Prompt);
        panel.Controls.Add(label);

        // Attached image thumbnails
        if (msg.AttachedImagePaths.Count > 0)
        {
            var y = label.Bottom + 8;
            foreach (var path in msg.AttachedImagePaths)
            {
                try
                {
                    var thumb = CreateThumbnail(path, ScaleValue(100));
                    thumb.Location = new Point(0, y);
                    thumb.Enabled = false;
                    panel.Controls.Add(thumb);
                    y += thumb.Height + ScaleValue(4);
                }
                catch { /* skip broken images */ }
            }
            panel.Height = y + ScaleValue(12);
        }

        return panel;
    }

    private ContextMenuStrip CreatePromptContextMenu(string prompt)
    {
        var menu = new ContextMenuStrip();
        var hasPrompt = !string.IsNullOrWhiteSpace(prompt);
        var copyItem = menu.Items.Add("复制提示词");
        copyItem.Enabled = hasPrompt;
        copyItem.Click += (_, _) =>
        {
            if (hasPrompt)
                Clipboard.SetText(prompt);
        };

        var refillItem = menu.Items.Add("填回输入框");
        refillItem.Enabled = hasPrompt;
        refillItem.Click += (_, _) => FillPromptFromMessage(prompt);
        return menu;
    }

    private void FillPromptFromMessage(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return;

        _promptBox.Text = prompt;
        _promptBox.SelectionStart = _promptBox.TextLength;
        _promptBox.Focus();
    }

    private Panel CreateAssistantBubble(ChatMessage msg, int maxWidth)
    {
        var padding = ScaleValue(12);
        var contentWidth = Math.Max(ScaleValue(160), maxWidth - (padding * 2));
        var panel = new Panel
        {
            AutoSize = false,
            MaximumSize = new Size(maxWidth, 0),
            BackColor = Color.White,
            Padding = new Padding(padding),
            Margin = new Padding(0),
        };
        panel.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(panel.BackColor);
            e.Graphics.FillRectangle(brush, panel.ClientRectangle);
            ControlPaint.DrawBorder(e.Graphics, panel.ClientRectangle,
                Color.FromArgb(34, 197, 94), 4, ButtonBorderStyle.Solid,
                Color.FromArgb(226, 232, 240), 1, ButtonBorderStyle.Solid,
                Color.FromArgb(226, 232, 240), 1, ButtonBorderStyle.Solid,
                Color.FromArgb(226, 232, 240), 1, ButtonBorderStyle.Solid);
        };

        int y = padding;
        var panelWidth = Math.Max(ScaleValue(180), Math.Min(maxWidth, contentWidth + (padding * 2)));

        // Image preview
        if (!string.IsNullOrWhiteSpace(msg.GeneratedImageDataUrl)
            || (!string.IsNullOrWhiteSpace(msg.GeneratedImagePath) && File.Exists(msg.GeneratedImagePath)))
        {
            try
            {
                var previewImage = LoadGeneratedPreviewImage(msg);
                var previewWidth = Math.Min(ScaleValue(520), contentWidth);
                var previewHeight = Clamp((int)Math.Round(previewWidth * 0.58F), ScaleValue(150), ScaleValue(300));
                var pictureBox = new PictureBox
                {
                    Image = previewImage,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(previewWidth, previewHeight),
                    Location = new Point(padding, y),
                };
                pictureBox.Click += (_, _) =>
                {
                    // Open in default viewer on double click
                    try { System.Diagnostics.Process.Start("explorer.exe", msg.GeneratedImagePath!); }
                    catch { }
                };
                pictureBox.Cursor = Cursors.Hand;
                panel.Controls.Add(pictureBox);
                panelWidth = Math.Max(panelWidth, pictureBox.Right + padding);
                y += pictureBox.Height + ScaleValue(8);
            }
            catch { /* skip if image can't load */ }
        }

        // Saved path
        if (!string.IsNullOrWhiteSpace(msg.GeneratedImagePath))
        {
            var pathLabel = new Label
            {
                Text = $"💾 已保存至: {msg.GeneratedImagePath}",
                AutoSize = true,
                MaximumSize = new Size(contentWidth, 0),
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = UiFont(8F),
                Location = new Point(padding, y),
            };
            panel.Controls.Add(pathLabel);
            panelWidth = Math.Max(panelWidth, Math.Min(maxWidth, pathLabel.Right + padding));
            y += pathLabel.Height + ScaleValue(4);
        }

        // Usage info
        if (msg.Usage != null)
        {
            var usageLabel = new Label
            {
                Text = $"📊 Tokens — 总计: {msg.Usage.TotalTokens}, 输入: {msg.Usage.InputTokens}, 输出: {msg.Usage.OutputTokens}",
                AutoSize = true,
                MaximumSize = new Size(contentWidth, 0),
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = UiFont(8F),
                Location = new Point(padding, y),
            };
            panel.Controls.Add(usageLabel);
            panelWidth = Math.Max(panelWidth, Math.Min(maxWidth, usageLabel.Right + padding));
            y += usageLabel.Height + ScaleValue(4);
        }

        panel.Size = new Size(Math.Min(maxWidth, panelWidth), y + padding);
        return panel;
    }

    private static Image LoadGeneratedPreviewImage(ChatMessage msg)
    {
        if (!string.IsNullOrWhiteSpace(msg.GeneratedImageDataUrl))
        {
            var commaIndex = msg.GeneratedImageDataUrl.IndexOf(',');
            var base64 = commaIndex >= 0
                ? msg.GeneratedImageDataUrl[(commaIndex + 1)..]
                : msg.GeneratedImageDataUrl;
            return LoadImageFromBytes(Convert.FromBase64String(base64));
        }

        if (string.IsNullOrWhiteSpace(msg.GeneratedImagePath))
            throw new FileNotFoundException("No generated image path was provided.");

        return LoadImageFromBytes(File.ReadAllBytes(msg.GeneratedImagePath));
    }

    private static Image LoadImageFromBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var image = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: true);
        return new Bitmap(image);
    }

    private Panel CreateSystemBubble(ChatMessage msg, int maxWidth)
    {
        var isError = msg.ErrorText?.Contains("失败") == true
            || msg.ErrorText?.Contains("错误") == true
            || msg.ErrorText?.Contains("超时") == true
            || msg.ErrorText?.Contains("API") == true;

        var label = new Label
        {
            Text = msg.ErrorText ?? "",
            AutoSize = true,
            MaximumSize = new Size(maxWidth - ScaleValue(30), 0),
            ForeColor = isError ? Color.FromArgb(185, 28, 28) : Color.FromArgb(71, 85, 105),
            BackColor = isError ? Color.FromArgb(254, 242, 242) : Color.FromArgb(241, 245, 249),
            Font = UiFont(9F),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(ScaleValue(8), ScaleValue(6), ScaleValue(8), ScaleValue(6)),
        };

        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MaximumSize = new Size(maxWidth, 0),
        };
        panel.Controls.Add(label);

        return panel;
    }

    private void AddPendingResponseBubble(string text)
    {
        RemovePendingResponseBubble();

        var rowWidth = GetChatRowWidth();
        var bubbleWidth = GetBubbleMaxWidth(rowWidth);
        _pendingResponseBubble = CreatePendingBubble(text, bubbleWidth, false);
        _pendingResponseLabel = _pendingResponseBubble.Controls.OfType<Label>().FirstOrDefault();
        _pendingResponseRow = CreateBubbleRow(_pendingResponseBubble, ChatRole.Assistant, rowWidth);
        _chatPanel.Controls.Add(_pendingResponseRow);
        ResizeChatPanelHeight();
        ScrollChatToBottom();
    }

    private Panel CreatePendingBubble(string text, int maxWidth, bool isError)
    {
        var padding = new Padding(ScaleValue(14), ScaleValue(8), ScaleValue(12), ScaleValue(8));
        var contentMaxWidth = Math.Max(ScaleValue(120), maxWidth - padding.Horizontal);
        var label = new Label
        {
            Text = isError ? text : $"⏳ {text}",
            AutoSize = true,
            MaximumSize = new Size(contentMaxWidth, 0),
            ForeColor = isError ? Color.FromArgb(185, 28, 28) : Color.FromArgb(71, 85, 105),
            Font = UiFont(9F),
            Location = new Point(padding.Left, padding.Top),
            Padding = new Padding(0),
        };

        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MaximumSize = new Size(maxWidth, 0),
            BackColor = isError ? Color.FromArgb(254, 242, 242) : Color.White,
            Padding = padding,
            Margin = new Padding(0),
        };

        panel.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(panel.BackColor);
            e.Graphics.FillRectangle(brush, panel.ClientRectangle);
            var accent = isError ? Color.FromArgb(239, 68, 68) : Color.FromArgb(59, 130, 246);
            ControlPaint.DrawBorder(e.Graphics, panel.ClientRectangle,
                accent, 4, ButtonBorderStyle.Solid,
                Color.FromArgb(226, 232, 240), 1, ButtonBorderStyle.Solid,
                Color.FromArgb(226, 232, 240), 1, ButtonBorderStyle.Solid,
                Color.FromArgb(226, 232, 240), 1, ButtonBorderStyle.Solid);
        };

        panel.Controls.Add(label);
        panel.MinimumSize = new Size(0, label.Bottom + padding.Bottom);
        return panel;
    }

    private void UpdatePendingResponseBubble(string text)
    {
        if (_pendingResponseLabel == null || _pendingResponseRow == null) return;

        _pendingResponseLabel.Text = $"⏳ {text}";
        _pendingResponseRow.PerformLayout();
        ResizeChatPanelHeight();
        ScrollChatToBottom();
    }

    private void CompletePendingResponseWithError(string text)
    {
        if (_pendingResponseRow == null)
        {
            AddChatBubble(ChatMessage.SystemMessage(text));
            return;
        }

        var rowWidth = GetChatRowWidth();
        var bubbleWidth = GetBubbleMaxWidth(rowWidth);
        _pendingResponseRow.Controls.Clear();
        _pendingResponseBubble = CreatePendingBubble(text, bubbleWidth, true);
        _pendingResponseLabel = _pendingResponseBubble.Controls.OfType<Label>().FirstOrDefault();
        _pendingResponseRow.Controls.Add(_pendingResponseBubble);
        _pendingResponseRow.PerformLayout();
        ResizeChatPanelHeight();
        ScrollChatToBottom();
    }

    private void CompletePendingResponseWithMessage(ChatMessage msg)
    {
        if (_pendingResponseRow == null)
        {
            AddChatBubble(msg);
            return;
        }

        var rowWidth = GetChatRowWidth();
        var bubbleWidth = GetBubbleMaxWidth(rowWidth);
        var bubble = CreateAssistantBubble(msg, bubbleWidth);
        _pendingResponseRow.Controls.Clear();
        _pendingResponseRow.Controls.Add(bubble);
        _pendingResponseBubble = null;
        _pendingResponseLabel = null;
        LayoutBubbleRow(_pendingResponseRow, bubble, ChatRole.Assistant);
        ResizeChatPanelHeight();
        _pendingResponseRow = null;
        ScrollChatToBottom();
    }

    private void RemovePendingResponseBubble()
    {
        if (_pendingResponseRow != null)
        {
            _chatPanel.Controls.Remove(_pendingResponseRow);
            _pendingResponseRow.Dispose();
            ResizeChatPanelHeight();
        }

        _pendingResponseRow = null;
        _pendingResponseBubble = null;
        _pendingResponseLabel = null;
    }

    // ═══════════════════════════════════════════════════
    //  Thumbnail Helpers
    // ═══════════════════════════════════════════════════

    private void AddThumbnail(string imagePath)
    {
        var thumb = CreateThumbnail(imagePath, ScaleValue(40));
        thumb.Cursor = Cursors.Hand;
        thumb.Click += (_, _) =>
        {
            _attachedImages.Remove(imagePath);
            _thumbnailStrip.Controls.Remove(thumb);
            thumb.Dispose();
            _thumbnailStrip.Visible = _thumbnailStrip.Controls.Count > 0;
        };
        _thumbnailStrip.Visible = true;
        _thumbnailStrip.Controls.Add(thumb);
    }

    private PictureBox CreateThumbnail(string imagePath, int size)
    {
        var pb = new PictureBox
        {
            Size = new Size(size, size),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
        };

        try
        {
            pb.Image = LoadImageFromBytes(File.ReadAllBytes(imagePath));
        }
        catch
        {
            pb.BackColor = Color.LightGray;
        }

        return pb;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _configManager.Save(_config);
        base.OnFormClosing(e);
    }
}
