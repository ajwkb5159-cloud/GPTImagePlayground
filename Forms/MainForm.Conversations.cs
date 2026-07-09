using ImageGenerator.Models;
using ImageGenerator.Services;

namespace ImageGenerator.Forms;

internal partial class MainForm
{
    // Serializes user-triggered conversation actions (switch/new/delete/rename/
    // settings-reinit) against each other only. It is deliberately independent of
    // generation state: other conversations may keep generating while one switches.
    private readonly SemaphoreSlim _conversationGate = new(1, 1);

    private async Task RunConversationActionAsync(Func<Task> action, string errorTitle)
    {
        if (!await _conversationGate.WaitAsync(0))
            return;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            HandleUiException(ex, errorTitle);
        }
        finally
        {
            _conversationGate.Release();
        }
    }

    private Task InitializeConversationsAsync() =>
        RunConversationActionAsync(RebuildConversationManagerAsync, "初始化会话失败");

    private async Task RebuildConversationManagerAsync()
    {
        _promptEnhancer = new PromptEnhancer(
            new ContextDecisionService(new TextSimilarityService()));
        _conversationManager = new ConversationManager(
            new ConversationStore(_configManager.ResolveConversationStoreDir(_config)),
            new ContextCache(),
            new ContextCompressor(),
            _config);

        await _conversationManager.InitializeAsync();
        if (IsDisposed || _isClosing)
            return;

        _configManager.Save(_config);
        RebuildConversationTabs();
        LoadActiveConversationMessages();
        UpdateTitleBarText();
    }

    private static string BuildInitialGenerationStatus(ContextDecision? decision)
    {
        if (decision == null
            || (!decision.ShouldInjectTextContext && !decision.ShouldAttachRecentImages))
        {
            return "正在请求 API...";
        }

        return string.IsNullOrWhiteSpace(decision.DecisionReason)
            ? "已整理上下文，正在请求 API..."
            : $"{decision.DecisionReason} 正在请求 API...";
    }

    private string BuildInitialGenerationStatus(ContextDecision? decision, Conversation? conversation)
    {
        if (conversation?.ContextConfig.ShowContextDecisionHint != true)
            return "正在请求 API...";

        return BuildInitialGenerationStatus(decision);
    }

    private void LoadActiveConversationMessages()
    {
        ClearChatPanel();
        var messages = _conversationManager?.ActiveConversation?.Messages;
        if (messages == null || messages.Count == 0)
        {
            if (!TryRestorePendingResponseForActiveConversation())
                AddWelcomeMessage();
            RefreshActiveConversationControls();
            return;
        }

        foreach (var message in messages)
            AddChatBubble(message);

        TryRestorePendingResponseForActiveConversation();
        RefreshActiveConversationControls();
    }

    private void RebuildConversationTabs()
    {
        if (conversationTabs.IsDisposed)
            return;

        conversationTabs.SuspendLayout();
        while (conversationTabs.Controls.Count > 0)
        {
            var control = conversationTabs.Controls[0];
            conversationTabs.Controls.RemoveAt(0);
            control.Dispose();
        }

        if (_conversationManager != null)
        {
            var displayNumbers = BuildConversationDisplayNumbers();
            foreach (var meta in _conversationManager.ConversationList)
                conversationTabs.Controls.Add(CreateConversationTab(meta, displayNumbers.GetValueOrDefault(meta.Id, 1)));
        }

        conversationTabs.ResumeLayout(true);
    }

    private Button CreateConversationTab(ConversationMeta meta, int displayNumber)
    {
        var isActive = meta.Id == _conversationManager?.ActiveConversationId;
        var title = FormatConversationTitle(meta, displayNumber);
        var palette = AppAppearance.Palette(_config.Theme);
        var tab = new Button
        {
            Text = title,
            Tag = meta.Id,
            Width = GetConversationTabWidth(title),
            Height = ScaleValue(30),
            Margin = new Padding(0, 0, ScaleValue(5), 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = isActive ? palette.ActiveTabBack : palette.InputBack,
            ForeColor = isActive ? palette.ActiveTabText : palette.MutedText,
            Font = UiFont(8.5F, isActive ? FontStyle.Bold : FontStyle.Regular),
            Image = LoadIconImage("app-image-24.png", ScaleValue(14)),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
            Padding = new Padding(ScaleValue(8), 0, ScaleValue(8), 0),
            ContextMenuStrip = CreateConversationTabMenu(meta.Id),
        };
        tab.FlatAppearance.BorderSize = 0;
        tab.Click += async (_, _) => await SwitchConversationAsync(meta.Id);
        return tab;
    }

    private ContextMenuStrip CreateConversationTabMenu(string conversationId)
    {
        var menu = new ContextMenuStrip();
        var renameItem = menu.Items.Add(T("Rename"));
        renameItem.Click += async (_, _) => await RenameConversationAsync(conversationId);
        var deleteItem = menu.Items.Add(T("Delete"));
        deleteItem.Click += async (_, _) => await DeleteConversationAsync(conversationId);
        return menu;
    }

    private Task SwitchConversationAsync(string conversationId) =>
        RunConversationActionAsync(async () =>
        {
            if (_conversationManager == null)
                return;

            await _conversationManager.SwitchToConversationAsync(conversationId);
            _configManager.Save(_config);
            RebuildConversationTabs();
            LoadActiveConversationMessages();
            UpdateTitleBarText();
        }, "切换会话失败");

    private Task DeleteConversationAsync(string conversationId) =>
        RunConversationActionAsync(async () =>
        {
            if (_conversationManager == null)
                return;

            if (_conversationManager.ConversationList.Count <= 1)
            {
                MessageBox.Show(this, "至少保留一个会话。", "会话",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(this, "确定要删除这个会话吗？", "会话",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
                return;

            var deletingActive = conversationId == _conversationManager.ActiveConversationId;
            if (_pendingResponses.TryGetValue(conversationId, out var pendingState))
            {
                pendingState.Cancellation?.Cancel();
                if (deletingActive && _pendingResponseRow != null)
                    RemoveVisiblePendingResponseBubble();
            }

            await _conversationManager.DeleteConversationAsync(conversationId);
            if (deletingActive)
            {
                var next = _conversationManager.ConversationList.FirstOrDefault();
                if (next != null)
                    await _conversationManager.SwitchToConversationAsync(next.Id);
            }

            _configManager.Save(_config);
            RebuildConversationTabs();
            LoadActiveConversationMessages();
            UpdateTitleBarText();
        }, "删除会话失败");

    private Task RenameConversationAsync(string conversationId) =>
        RunConversationActionAsync(async () =>
        {
            if (_conversationManager == null)
                return;

            var meta = _conversationManager.ConversationList.FirstOrDefault(item => item.Id == conversationId);
            var displayNumbers = BuildConversationDisplayNumbers();
            var displayNumber = displayNumbers.GetValueOrDefault(conversationId, 1);
            var title = PromptForConversationTitle(meta?.Title ?? "", displayNumber);

            if (string.IsNullOrWhiteSpace(title))
                return;

            await _conversationManager.RenameConversationAsync(conversationId, title);
            _configManager.Save(_config);
            RebuildConversationTabs();
            UpdateTitleBarText();
        }, "重命名会话失败");

    private string? PromptForConversationTitle(string currentTitle, int displayNumber)
    {
        var titleForEditing = IsDefaultConversationTitle(currentTitle)
            ? FormatDefaultConversationTitle(displayNumber)
            : currentTitle;

        using var dialog = new Form
        {
            Text = T("Rename"),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(ScaleValue(360), ScaleValue(128)),
            Font = UiFont(10F),
            Padding = new Padding(ScaleValue(14)),
        };

        var label = new Label
        {
            Text = T("ConversationTitle"),
            AutoSize = true,
            Location = new Point(dialog.Padding.Left, dialog.Padding.Top),
        };
        var titleBox = new TextBox
        {
            Text = titleForEditing,
            Location = new Point(dialog.Padding.Left, label.Bottom + ScaleValue(8)),
            Width = dialog.ClientSize.Width - dialog.Padding.Horizontal,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right,
        };
        var okButton = new Button
        {
            Text = T("OK"),
            DialogResult = DialogResult.OK,
            Size = new Size(ScaleValue(82), ScaleValue(30)),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
        };
        var cancelButton = new Button
        {
            Text = T("Cancel"),
            DialogResult = DialogResult.Cancel,
            Size = okButton.Size,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
        };
        okButton.Location = new Point(
            dialog.ClientSize.Width - dialog.Padding.Right - okButton.Width,
            dialog.ClientSize.Height - dialog.Padding.Bottom - okButton.Height);
        cancelButton.Location = new Point(okButton.Left - ScaleValue(10) - cancelButton.Width, okButton.Top);

        dialog.Controls.Add(label);
        dialog.Controls.Add(titleBox);
        dialog.Controls.Add(okButton);
        dialog.Controls.Add(cancelButton);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        titleBox.SelectAll();
        return dialog.ShowDialog(this) == DialogResult.OK
            ? titleBox.Text.Trim()
            : null;
    }

    private void HandleUiException(Exception ex, string title)
    {
        System.Diagnostics.Debug.WriteLine($"[MainForm] {title}: {ex}");
        if (_isClosing || IsDisposed)
            return;

        MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private Dictionary<string, int> BuildConversationDisplayNumbers()
    {
        if (_conversationManager == null)
            return [];

        return _conversationManager.ConversationList
            .OrderBy(meta => meta.CreatedAt)
            .Select((meta, index) => new { meta.Id, Number = index + 1 })
            .ToDictionary(item => item.Id, item => item.Number);
    }

    private int GetConversationTabWidth(string title)
    {
        var textWidth = TextRenderer.MeasureText(title, UiFont(8.5F, FontStyle.Bold)).Width + ScaleValue(42);
        return Clamp(textWidth, ScaleValue(84), ScaleValue(188));
    }

    private static bool IsDefaultConversationTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return true;

        var normalized = title.Trim();
        return normalized.Equals("新会话", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("窗体", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("Chat ", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("New Chat", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("New Conversation", StringComparison.OrdinalIgnoreCase);
    }

    private string FormatConversationTitle(ConversationMeta meta, int displayNumber)
    {
        var title = IsDefaultConversationTitle(meta.Title)
            ? FormatDefaultConversationTitle(displayNumber)
            : meta.Title.Trim();
        return title.Length > 18 ? title[..17] + "..." : title;
    }

    private void UpdateTitleBarText()
    {
        titleLabel.Text = "GPT Image Playground";
    }

    private void ClearChatPanel()
    {
        while (_chatPanel.Controls.Count > 0)
        {
            var control = _chatPanel.Controls[0];
            _chatPanel.Controls.RemoveAt(0);
            control.Dispose();
        }

        _pendingResponseRow = null;
        _pendingResponseBubble = null;
        _pendingResponseLabel = null;
        ResizeChatPanelHeight();
        RestoreChatScroll(0);
    }

    private static string BuildCompletionMessage(GenerateResult result)
    {
        var successCount = result.SuccessCount > 0 ? result.SuccessCount : result.SavedPaths.Count;
        var totalCount = result.TotalCount > 0 ? result.TotalCount : successCount;
        var serverTimeLabel = totalCount > 1 ? "服务器最长耗时" : "服务器耗时";
        var message = totalCount > 1
            ? $"✅ 完成！生成 {successCount}/{totalCount} 张图片，{serverTimeLabel} {result.ServerTimeSeconds:F0} 秒，总耗时 {result.TotalTimeSeconds:F0} 秒。"
            : $"✅ 完成！生成 {successCount} 张图片，{serverTimeLabel} {result.ServerTimeSeconds:F0} 秒，总耗时 {result.TotalTimeSeconds:F0} 秒。";

        if (result.FailedMessages.Count == 0)
            return message;

        return message +
            Environment.NewLine +
            $"{result.FailedMessages.Count} 个子请求失败：" +
            Environment.NewLine +
            string.Join(Environment.NewLine, result.FailedMessages.Select(failure => $"  - {failure}"));
    }
}
