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

    private readonly ConfigManager _configManager;
    private AppConfig _config;
    private ImageApiService? _apiService;

    // State
    private readonly List<string> _attachedImages = [];
    private bool _isGenerating;
    private Panel? _pendingResponseRow;
    private Panel? _pendingResponseBubble;
    private Label? _pendingResponseLabel;

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

        chatContainer.SizeChanged += (_, _) =>
        {
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
            // Fix placeholder text positioning — set 12px left margin on the edit control
            if (_promptBox.IsHandleCreated)
                SendMessage(_promptBox.Handle, EM_SETMARGINS, (IntPtr)EC_LEFTMARGIN, (IntPtr)12);

            UpdateChatPanelBounds();

            AddWelcomeMessage();
        };
    }

    // ═══════════════════════════════════════════════════
    //  Event Handlers
    // ═══════════════════════════════════════════════════

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

        // Clear input (before async work, so the user can type again immediately)
        _promptBox.Text = "";
        var attachedCopy = new List<string>(_attachedImages);
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
                    parent.AutoScrollPosition = new Point(0, parent.DisplayRectangle.Height);
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
        var bubble = CreateWelcomeBubble(Math.Min(520, rowWidth - 40));
        _chatPanel.Controls.Add(CreateBubbleRow(bubble, ChatRole.System, rowWidth));
        ResizeChatPanelHeight();
        ScrollChatToBottom();
    }

    private Panel CreateWelcomeBubble(int maxWidth)
    {
        var bodyFont = new Font("Microsoft YaHei UI", 9F);
        var actionFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        var padding = new Padding(10, 8, 10, 8);
        var contentMaxWidth = Math.Max(260, maxWidth - padding.Horizontal);
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
            Size = new Size(18, 18),
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

        const int gap = 4;
        var x = 0;
        var rowHeight = Math.Max(18, new[] { prefix.Height, icon.Height, action.Height, suffix.Height }.Max());
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
        var bubbleWidth = Math.Max((int)(rowWidth * 0.72), 220);

        var bubble = msg.Role switch
        {
            ChatRole.User => CreateUserBubble(msg, bubbleWidth),
            ChatRole.Assistant => CreateAssistantBubble(msg, bubbleWidth),
            ChatRole.System => CreateSystemBubble(msg, Math.Min(520, rowWidth - 40)),
            _ => new Panel(),
        };

        return CreateBubbleRow(bubble, msg.Role, rowWidth);
    }

    private Panel CreateBubbleRow(Panel bubble, ChatRole role, int rowWidth)
    {
        var row = new Panel
        {
            Width = rowWidth,
            Height = Math.Max(36, bubble.GetPreferredSize(new Size(bubble.MaximumSize.Width, 0)).Height + 8),
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
        var availableWidth = Math.Max(200, row.ClientSize.Width);
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

        row.Height = Math.Max(36, bubble.Height + 8);
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
        return Math.Max(width, 320);
    }

    private void UpdateChatPanelBounds()
    {
        var width = Math.Max(320, chatContainer.ClientSize.Width - chatContainer.Padding.Horizontal);
        _chatPanel.Location = new Point(chatContainer.Padding.Left, chatContainer.Padding.Top);
        _chatPanel.Width = width;
        _chatPanel.MaximumSize = new Size(width, 0);
        ResizeChatPanelHeight();
    }

    private void ResizeChatPanelHeight()
    {
        if (_chatPanel.IsDisposed) return;

        _chatPanel.PerformLayout();
        var contentHeight = 0;
        foreach (Control control in _chatPanel.Controls)
        {
            contentHeight = Math.Max(contentHeight, control.Bottom + control.Margin.Bottom);
        }

        var minHeight = Math.Max(0, chatContainer.ClientSize.Height - chatContainer.Padding.Vertical);
        _chatPanel.Height = Math.Max(minHeight, contentHeight + 8);
    }

    private Panel CreateUserBubble(ChatMessage msg, int maxWidth)
    {
        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MaximumSize = new Size(maxWidth, 0),
            BackColor = Color.FromArgb(219, 234, 254),
            Padding = new Padding(12),
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
            MaximumSize = new Size(maxWidth - 30, 0),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Microsoft YaHei UI", 10F),
        };
        panel.Controls.Add(label);

        // Attached image thumbnails
        if (msg.AttachedImagePaths.Count > 0)
        {
            var y = label.Bottom + 8;
            foreach (var path in msg.AttachedImagePaths)
            {
                try
                {
                    var thumb = CreateThumbnail(path, 100);
                    thumb.Location = new Point(0, y);
                    thumb.Enabled = false;
                    panel.Controls.Add(thumb);
                    y += thumb.Height + 4;
                }
                catch { /* skip broken images */ }
            }
            panel.Height = y + 12;
        }

        return panel;
    }

    private Panel CreateAssistantBubble(ChatMessage msg, int maxWidth)
    {
        const int padding = 12;
        var contentWidth = Math.Max(180, maxWidth - (padding * 2));
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
        var panelWidth = Math.Max(220, Math.Min(maxWidth, contentWidth + (padding * 2)));

        // Image preview
        if (!string.IsNullOrWhiteSpace(msg.GeneratedImageDataUrl)
            || (!string.IsNullOrWhiteSpace(msg.GeneratedImagePath) && File.Exists(msg.GeneratedImagePath)))
        {
            try
            {
                var previewImage = LoadGeneratedPreviewImage(msg);
                var previewWidth = Math.Min(520, contentWidth);
                var pictureBox = new PictureBox
                {
                    Image = previewImage,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(previewWidth, 300),
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
                y += pictureBox.Height + 8;
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
                Font = new Font("Microsoft YaHei UI", 8F),
                Location = new Point(padding, y),
            };
            panel.Controls.Add(pathLabel);
            panelWidth = Math.Max(panelWidth, Math.Min(maxWidth, pathLabel.Right + padding));
            y += pathLabel.Height + 4;
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
                Font = new Font("Microsoft YaHei UI", 8F),
                Location = new Point(padding, y),
            };
            panel.Controls.Add(usageLabel);
            panelWidth = Math.Max(panelWidth, Math.Min(maxWidth, usageLabel.Right + padding));
            y += usageLabel.Height + 4;
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
            MaximumSize = new Size(maxWidth - 30, 0),
            ForeColor = isError ? Color.FromArgb(185, 28, 28) : Color.FromArgb(71, 85, 105),
            BackColor = isError ? Color.FromArgb(254, 242, 242) : Color.FromArgb(241, 245, 249),
            Font = new Font("Microsoft YaHei UI", 9F),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(8, 6, 8, 6),
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
        var bubbleWidth = Math.Max((int)(rowWidth * 0.72), 220);
        _pendingResponseBubble = CreatePendingBubble(text, bubbleWidth, false);
        _pendingResponseLabel = _pendingResponseBubble.Controls.OfType<Label>().FirstOrDefault();
        _pendingResponseRow = CreateBubbleRow(_pendingResponseBubble, ChatRole.Assistant, rowWidth);
        _chatPanel.Controls.Add(_pendingResponseRow);
        ResizeChatPanelHeight();
        ScrollChatToBottom();
    }

    private Panel CreatePendingBubble(string text, int maxWidth, bool isError)
    {
        var label = new Label
        {
            Text = isError ? text : $"⏳ {text}",
            AutoSize = true,
            MaximumSize = new Size(maxWidth - 30, 0),
            ForeColor = isError ? Color.FromArgb(185, 28, 28) : Color.FromArgb(71, 85, 105),
            Font = new Font("Microsoft YaHei UI", 9F),
            Padding = new Padding(0),
        };

        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MaximumSize = new Size(maxWidth, 0),
            BackColor = isError ? Color.FromArgb(254, 242, 242) : Color.White,
            Padding = new Padding(12),
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
        var bubbleWidth = Math.Max((int)(rowWidth * 0.72), 220);
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
        var bubbleWidth = Math.Max((int)(rowWidth * 0.72), 220);
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
        var thumb = CreateThumbnail(imagePath, 40);
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
