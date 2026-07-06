namespace ImageGenerator.Forms;

internal partial class MainForm
{
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_isGenerating && e.CloseReason == CloseReason.UserClosing)
        {
            var result = MessageBox.Show(
                this,
                "当前还有图片生成请求正在进行，确定要关闭吗？",
                "确认关闭",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        _isClosing = true;
        foreach (var pendingState in _pendingResponses.Values)
            pendingState.Cancellation?.Cancel();
        if (_conversationManager != null)
        {
            try
            {
                _conversationManager.SaveActiveConversationAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainForm] Failed to save active conversation while closing: {ex.Message}");
            }
        }
        _configManager.Save(_config);
        base.OnFormClosing(e);
    }
}
