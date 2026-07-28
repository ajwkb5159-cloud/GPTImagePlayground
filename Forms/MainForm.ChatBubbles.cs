using ImageGenerator.Models;

namespace ImageGenerator.Forms;

internal partial class MainForm
{
    private void AddWelcomeMessage()
    {
        var rowWidth = GetChatRowWidth();
        var bubble = CreateWelcomeBubble(GetSystemBubbleMaxWidth(rowWidth));
        _chatPanel.Controls.Add(CreateBubbleRow(bubble, ChatRole.System, rowWidth));
        StackChatRows();
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
        var rowWidth = Math.Max(ScaleValue(180), row.ClientSize.Width);
        var isLeftSystemBubble = role == ChatRole.System && bubble is Panel { Tag: "system-bubble" };
        var availableWidth = role switch
        {
            ChatRole.User or ChatRole.Assistant => GetBubbleMaxWidth(rowWidth),
            ChatRole.System => isLeftSystemBubble ? GetBubbleMaxWidth(rowWidth) : GetSystemBubbleMaxWidth(rowWidth),
            _ => rowWidth,
        };
        bubble.MaximumSize = new Size(availableWidth, 0);

        if (bubble is Panel { Tag: "system-bubble" } systemBubble)
            LayoutSystemBubble(systemBubble, availableWidth);
        else if (bubble is Panel { Tag: "system-center" } centeredSystemBubble)
            LayoutCenteredSystemBubble(centeredSystemBubble, availableWidth);
        else if (bubble is Panel { Tag: "assistant-bubble" } assistantBubble)
            RelayoutAssistantBubble(assistantBubble, availableWidth);

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
            ChatRole.System when !isLeftSystemBubble => Math.Max(0, (row.ClientSize.Width - bubble.Width) / 2),
            _ => 0,
        };
    }

    private void ReflowChatRows()
    {
        var scrollY = GetChatScrollY();
        var wasAtBottom = IsChatScrolledToBottom();

        StackChatRows();
        ResizeChatPanelHeight();
        RestoreChatScroll(wasAtBottom ? GetChatMaxScrollY() : scrollY);
        RememberChatScrollState();
    }

    private void StackChatRows()
    {
        if (_chatPanel.IsDisposed)
            return;

        var y = 0;
        var rowWidth = GetChatRowWidth();
        foreach (Control control in _chatPanel.Controls)
        {
            if (control.Tag as string != "chat-row")
                continue;

            y += control.Margin.Top;
            control.Location = new Point(control.Margin.Left, y);
            control.Width = Math.Max(0, rowWidth - control.Margin.Horizontal);
            control.PerformLayout();
            y = control.Bottom + control.Margin.Bottom;
        }
    }

    private int GetChatScrollY() =>
        Math.Max(0, chatContainer.Padding.Top - _chatPanel.Top);

    private int GetChatMaxScrollY() =>
        Math.Max(0, GetChatContentHeight() + chatContainer.Padding.Vertical - chatContainer.ClientSize.Height);

    private bool IsChatScrolledToBottom()
    {
        var maxScrollY = GetChatMaxScrollY();
        return maxScrollY == 0 || GetChatScrollY() >= maxScrollY - ScaleValue(4);
    }

    private void RestoreChatScroll(int scrollY)
    {
        if (chatContainer.IsDisposed)
            return;

        var maxScrollY = GetChatMaxScrollY();
        if (maxScrollY <= 0)
        {
            _chatPanel.Location = new Point(chatContainer.Padding.Left, chatContainer.Padding.Top);
            _lastChatScrollY = 0;
            _lastChatWasAtBottom = true;
            return;
        }

        var clampedScrollY = Math.Min(scrollY, maxScrollY);
        _chatPanel.Location = new Point(chatContainer.Padding.Left, chatContainer.Padding.Top - clampedScrollY);
        _lastChatScrollY = clampedScrollY;
        _lastChatWasAtBottom = clampedScrollY >= maxScrollY - ScaleValue(4);
    }

    private void RememberChatScrollState()
    {
        if (chatContainer.IsDisposed || _chatPanel.IsDisposed)
            return;

        _lastChatScrollY = GetChatScrollY();
        _lastChatWasAtBottom = IsChatScrolledToBottom();
    }

    private void FinalizeRestoreChatScroll(bool restoreToBottom, int restoreScrollY)
    {
        UpdateChatPanelBounds();
        StackChatRows();
        ResizeChatPanelHeight();
        RestoreChatScroll(restoreToBottom ? GetChatMaxScrollY() : restoreScrollY);
        RememberChatScrollState();
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
        _chatPanel.Left = chatContainer.Padding.Left;
        _chatPanel.Width = width;
        _chatPanel.MaximumSize = new Size(width, 0);
        StackChatRows();
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

        _chatPanel.Height = GetChatContentHeight();
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
            Tag = "assistant-bubble",
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
                pictureBox.Disposed += DisposePictureBoxImage;
                pictureBox.DoubleClick += (_, _) =>
                {
                    OpenGeneratedImage(msg.GeneratedImagePath);
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

    private static void DisposePictureBoxImage(object? sender, EventArgs e)
    {
        if (sender is not PictureBox pictureBox)
            return;

        var image = pictureBox.Image;
        pictureBox.Image = null;
        image?.Dispose();
    }

    private static void OpenGeneratedImage(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(imagePath)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // The saved path remains visible if the OS cannot launch an image viewer.
        }
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

        if (!isError)
            return CreateCenteredSystemBubble(msg, maxWidth);

        var padding = new Padding(ScaleValue(12), ScaleValue(8), ScaleValue(12), ScaleValue(8));
        var contentMaxWidth = Math.Max(ScaleValue(120), maxWidth - padding.Horizontal);
        var panelBackColor = isError ? Color.FromArgb(254, 242, 242) : Color.White;

        var label = new Label
        {
            Text = msg.ErrorText ?? "",
            AutoSize = true,
            MaximumSize = new Size(contentMaxWidth, 0),
            ForeColor = isError ? Color.FromArgb(185, 28, 28) : Color.FromArgb(71, 85, 105),
            BackColor = panelBackColor,
            Font = UiFont(9F),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0),
            Location = new Point(padding.Left, padding.Top),
        };

        var panel = new Panel
        {
            AutoSize = false,
            MaximumSize = new Size(maxWidth, 0),
            BackColor = panelBackColor,
            Padding = padding,
            Margin = new Padding(0),
            Tag = "system-bubble",
        };
        panel.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(panel.BackColor);
            e.Graphics.FillRectangle(brush, panel.ClientRectangle);
            var accent = isError ? Color.FromArgb(239, 68, 68) : Color.FromArgb(148, 163, 184);
            ControlPaint.DrawBorder(e.Graphics, panel.ClientRectangle,
                accent, 4, ButtonBorderStyle.Solid,
                Color.FromArgb(226, 232, 240), 1, ButtonBorderStyle.Solid,
                Color.FromArgb(226, 232, 240), 1, ButtonBorderStyle.Solid,
                Color.FromArgb(226, 232, 240), 1, ButtonBorderStyle.Solid);
        };
        panel.Controls.Add(label);
        LayoutSystemBubble(panel, maxWidth);

        return panel;
    }

    private Panel CreateCenteredSystemBubble(ChatMessage msg, int maxWidth)
    {
        var label = new Label
        {
            Text = msg.ErrorText ?? "",
            AutoSize = true,
            MaximumSize = new Size(maxWidth - ScaleValue(30), 0),
            ForeColor = Color.FromArgb(71, 85, 105),
            BackColor = Color.FromArgb(241, 245, 249),
            Font = UiFont(9F),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(ScaleValue(8), ScaleValue(6), ScaleValue(8), ScaleValue(6)),
        };

        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MaximumSize = new Size(maxWidth, 0),
            Tag = "system-center",
        };
        panel.Controls.Add(label);

        return panel;
    }

    private void LayoutCenteredSystemBubble(Panel panel, int maxWidth)
    {
        var label = panel.Controls.OfType<Label>().FirstOrDefault();
        if (label == null)
            return;

        label.MaximumSize = new Size(Math.Max(ScaleValue(120), maxWidth - ScaleValue(30)), 0);
    }

    private void LayoutSystemBubble(Panel panel, int maxWidth)
    {
        var label = panel.Controls.OfType<Label>().FirstOrDefault();
        if (label == null)
            return;

        var contentMaxWidth = Math.Max(ScaleValue(120), maxWidth - panel.Padding.Horizontal);
        label.MaximumSize = new Size(contentMaxWidth, 0);
        label.Location = new Point(panel.Padding.Left, panel.Padding.Top);

        var preferred = label.GetPreferredSize(new Size(contentMaxWidth, 0));
        label.Size = preferred;
        panel.Size = new Size(
            Math.Min(maxWidth, preferred.Width + panel.Padding.Horizontal),
            preferred.Height + panel.Padding.Vertical);
    }

    private void RelayoutAssistantBubble(Panel bubble, int availableWidth)
    {
        var padding = bubble.Padding;
        var contentWidth = Math.Max(ScaleValue(160), availableWidth - padding.Horizontal);

        int y = padding.Top;
        foreach (Control child in bubble.Controls)
        {
            if (child is PictureBox pb)
            {
                var targetWidth = Math.Min(pb.Width, contentWidth);
                if (targetWidth != pb.Width)
                {
                    var ratio = (float)targetWidth / Math.Max(1, pb.Width);
                    pb.Height = Math.Max(ScaleValue(48), (int)Math.Round(pb.Height * ratio));
                    pb.Width = targetWidth;
                }
                pb.Location = new Point(padding.Left, y);
                y = pb.Bottom + ScaleValue(8);
            }
            else if (child is Label lbl)
            {
                lbl.MaximumSize = new Size(contentWidth, 0);
                lbl.Location = new Point(lbl.Left, y);
                y = lbl.Bottom + ScaleValue(4);
            }
        }

        bubble.Width = Math.Min(availableWidth,
            Math.Max(padding.Horizontal + contentWidth,
                bubble.Controls.Count > 0
                    ? bubble.Controls.Cast<Control>().Max(c => c.Right) + padding.Right
                    : padding.Horizontal));
        bubble.Height = y + padding.Bottom;
    }
}
