using ImageGenerator.Models;

namespace ImageGenerator.Forms;

internal partial class MainForm
{
    private void RegisterPendingResponse(
        string conversationId,
        string text,
        CancellationTokenSource cancellation)
    {
        var state = new PendingResponseState
        {
            ConversationId = conversationId,
            Text = text,
            Cancellation = cancellation,
        };
        if (_pendingResponses.TryRemove(conversationId, out var oldState))
            oldState.Cancellation?.Dispose();

        _pendingResponses[conversationId] = state;

        _isGenerating = true;
        if (IsActiveConversation(conversationId))
            AddPendingResponseBubble(state);
    }

    private bool TryRestorePendingResponseForActiveConversation()
    {
        var conversationId = _conversationManager?.ActiveConversationId;
        if (conversationId == null || !_pendingResponses.TryGetValue(conversationId, out var state))
            return false;

        AddPendingResponseBubble(state);
        return true;
    }

    private void AddPendingResponseBubble(PendingResponseState state)
    {
        if (DeferChatUiUpdateWhileMinimized())
            return;

        RemoveVisiblePendingResponseBubble();

        var rowWidth = GetChatRowWidth();
        var bubbleWidth = GetBubbleMaxWidth(rowWidth);
        _pendingResponseBubble = CreatePendingBubble(state.Text, bubbleWidth, state.IsError);
        _pendingResponseLabel = _pendingResponseBubble.Controls.OfType<Label>().FirstOrDefault();
        _pendingResponseRow = CreateBubbleRow(_pendingResponseBubble, ChatRole.Assistant, rowWidth);
        _chatPanel.Controls.Add(_pendingResponseRow);
        StackChatRows();
        ResizeChatPanelHeight();
        ScrollChatToBottom();
    }

    private Panel CreatePendingBubble(string text, int maxWidth, bool isError)
    {
        var padding = new Padding(ScaleValue(14), ScaleValue(8), ScaleValue(12), ScaleValue(8));
        var contentMaxWidth = Math.Max(ScaleValue(120), maxWidth - padding.Horizontal);
        var textControl = CreatePendingTextControl(text, isError, contentMaxWidth, UiFont(9F));
        textControl.Location = new Point(padding.Left, padding.Top);

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

        panel.Controls.Add(textControl);
        panel.MinimumSize = new Size(0, textControl.Bottom + padding.Bottom);
        return panel;
    }

    private Control CreatePendingTextControl(string text, bool isError, int contentMaxWidth, Font font)
    {
        if (!isError)
        {
            return new Label
            {
                Text = $"⏳ {text}",
                AutoSize = true,
                MaximumSize = new Size(contentMaxWidth, 0),
                ForeColor = Color.FromArgb(71, 85, 105),
                Font = font,
                Padding = new Padding(0),
            };
        }

        var measured = TextRenderer.MeasureText(
            text,
            font,
            new Size(contentMaxWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        var maxErrorHeight = Clamp(
            (int)Math.Round(chatContainer.ClientSize.Height * 0.45F),
            ScaleValue(120),
            ScaleValue(260));

        return new TextBox
        {
            Text = text,
            Multiline = true,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = measured.Height > maxErrorHeight ? ScrollBars.Vertical : ScrollBars.None,
            Width = contentMaxWidth,
            Height = Math.Min(measured.Height + ScaleValue(8), maxErrorHeight),
            ForeColor = Color.FromArgb(185, 28, 28),
            BackColor = Color.FromArgb(254, 242, 242),
            Font = font,
            TabStop = false,
        };
    }

    private void UpdatePendingResponseBubble(string conversationId, string text)
    {
        if (!_pendingResponses.TryGetValue(conversationId, out var state))
            return;

        state.Text = text;
        if (!IsActiveConversation(conversationId))
            return;

        if (DeferChatUiUpdateWhileMinimized())
            return;

        if (_pendingResponseRow == null)
            AddPendingResponseBubble(state);

        if (_pendingResponseLabel == null || _pendingResponseRow == null) return;

        _pendingResponseLabel.Text = $"⏳ {text}";
        _pendingResponseRow.PerformLayout();
        StackChatRows();
        ResizeChatPanelHeight();
        ScrollChatToBottom();
    }

    private void CompletePendingResponseWithError(string conversationId, string text)
    {
        if (_pendingResponses.TryGetValue(conversationId, out var state))
        {
            state.Text = text;
            state.IsError = true;
        }

        if (!IsActiveConversation(conversationId))
            return;

        if (DeferChatUiUpdateWhileMinimized())
            return;

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
        LayoutBubbleRow(_pendingResponseRow, _pendingResponseBubble, ChatRole.Assistant);
        StackChatRows();
        ResizeChatPanelHeight();
        _pendingResponseRow = null;
        _pendingResponseBubble = null;
        _pendingResponseLabel = null;
        ScrollChatToBottom();
    }

    private void CompletePendingResponseWithMessage(string conversationId, ChatMessage msg)
    {
        if (!IsActiveConversation(conversationId))
            return;

        if (DeferChatUiUpdateWhileMinimized())
            return;

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
        StackChatRows();
        ResizeChatPanelHeight();
        _pendingResponseRow = null;
        ScrollChatToBottom();
    }

    private void ClearPendingResponse(string conversationId)
    {
        if (_pendingResponses.TryRemove(conversationId, out var state))
            state.Cancellation?.Dispose();

        _isGenerating = _pendingResponses.Count > 0;
        if (IsActiveConversation(conversationId) && _pendingResponseRow != null)
            RemoveVisiblePendingResponseBubble();
    }

    private void DisposePendingResponses()
    {
        foreach (var conversationId in _pendingResponses.Keys)
        {
            if (!_pendingResponses.TryRemove(conversationId, out var state))
                continue;

            try
            {
                state.Cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                state.Cancellation?.Dispose();
            }
        }
    }

    private void RemoveVisiblePendingResponseBubble()
    {
        if (DeferChatUiUpdateWhileMinimized())
            return;

        if (_pendingResponseRow != null)
        {
            _chatPanel.Controls.Remove(_pendingResponseRow);
            _pendingResponseRow.Dispose();
            StackChatRows();
            ResizeChatPanelHeight();
        }

        _pendingResponseRow = null;
        _pendingResponseBubble = null;
        _pendingResponseLabel = null;
    }

    // ═══════════════════════════════════════════════════
    //  Thumbnail Helpers
    // ═══════════════════════════════════════════════════
}
