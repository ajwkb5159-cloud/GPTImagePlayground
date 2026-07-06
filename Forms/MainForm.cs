using System.Collections.Concurrent;
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
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int ReferenceWidth = 960;
    private const int ReferenceHeight = 680;
    private const float MinUiScale = 0.72F;
    private const float MaxUiScale = 1.08F;
    private const float DesignDpi = 96F;
    private const int ResizeDebounceMs = 50;
    private const int MaxThumbnailCacheEntries = 80;

    private readonly ConfigManager _configManager;
    private AppConfig _config;
    private ImageApiService? _apiService;
    private ConversationManager? _conversationManager;
    private PromptEnhancer? _promptEnhancer;

    // State
    private readonly List<string> _attachedImages = [];
    private bool _isGenerating;
    private readonly ConcurrentDictionary<string, PendingResponseState> _pendingResponses = [];
    private Panel? _pendingResponseRow;
    private Panel? _pendingResponseBubble;
    private Label? _pendingResponseLabel;
    private float _uiScale = 1F;
    private bool _isApplyingResponsiveLayout;
    private bool _wasMinimized;
    private bool _restoreLayoutQueued;
    private bool _isClosing;
    private int _lastChatScrollY;
    private bool _lastChatWasAtBottom = true;
    private readonly Dictionary<(int SizeHundredths, FontStyle Style), Font> _fontCache = [];
    private readonly Dictionary<Button, IconTextButtonState> _iconTextButtonStates = [];
    private readonly System.Windows.Forms.Timer _resizeDebounceTimer = new() { Interval = ResizeDebounceMs };
    private bool _resizeRestoreFromMinimized;
    private int _resizeRestoreScrollY;
    private bool _resizeRestoreToBottom;
    private readonly Dictionary<string, ThumbnailCacheEntry> _thumbnailCache = [];
    private readonly LinkedList<string> _thumbnailLru = [];

    private sealed class PendingResponseState
    {
        public required string ConversationId { get; init; }
        public string Text { get; set; } = "";
        public bool IsError { get; set; }
        public CancellationTokenSource? Cancellation { get; set; }
    }

    private sealed class ThumbnailCacheEntry
    {
        public required Image Image { get; init; }
        public required LinkedListNode<string> Node { get; init; }
    }

    private sealed class IconTextButtonState
    {
        public string Text { get; set; } = "";
        public string ImageKey { get; set; } = "";
        public Image? Image { get; set; }
        public int Gap { get; set; }
    }

    public MainForm()
    {
        InitializeComponent();

        _configManager = new ConfigManager();
        _config = _configManager.Load();
        _apiService = new ImageApiService(_config);
        ApplyLocalization();
        ApplyTheme();

        _chatPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        // ── Event wiring ──
        settingsBtn.Click += SettingsBtn_Click;
        newConversationBtn.Click += NewConversationBtn_Click;
        _attachBtn.Click += AttachBtn_Click;
        _sendBtn.Click += SendBtn_Click;
        _promptBox.KeyDown += PromptBox_KeyDown;

        _resizeDebounceTimer.Tick += (_, _) => FlushResponsiveResize();
        Resize += (_, _) => HandleResponsiveResize();

        chatContainer.SizeChanged += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized || _isApplyingResponsiveLayout)
                return;

            UpdateChatPanelBounds();
            ReflowChatRows();
        };
        chatContainer.MouseWheel += ChatContainer_MouseWheel;
        _chatPanel.MouseWheel += ChatContainer_MouseWheel;

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
        Load += async (_, _) =>
        {
            FitInitialWindowToScreen();
            ApplyResponsiveLayout();

            // Fix placeholder text positioning — set 12px left margin on the edit control
            if (_promptBox.IsHandleCreated)
                SendMessage(_promptBox.Handle, EM_SETMARGINS, (IntPtr)EC_LEFTMARGIN, (IntPtr)ScaleValue(12));

            UpdateChatPanelBounds();

            await InitializeConversationsAsync();
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
            _resizeDebounceTimer.Stop();
            return;
        }

        var restoringFromMinimized = _wasMinimized;
        if (!restoringFromMinimized)
            RememberChatScrollState();

        _resizeRestoreFromMinimized |= restoringFromMinimized;
        _resizeRestoreScrollY = _lastChatScrollY;
        _resizeRestoreToBottom = _lastChatWasAtBottom;
        _resizeDebounceTimer.Stop();
        _resizeDebounceTimer.Start();
    }

    private void FlushResponsiveResize()
    {
        _resizeDebounceTimer.Stop();
        if (IsDisposed || WindowState == FormWindowState.Minimized)
            return;

        var restoringFromMinimized = _resizeRestoreFromMinimized;
        var restoreScrollY = _resizeRestoreScrollY;
        var restoreToBottom = _resizeRestoreToBottom;
        _resizeRestoreFromMinimized = false;

        ApplyResponsiveLayout();

        if (restoringFromMinimized)
        {
            _wasMinimized = false;
            QueueRestoreLayoutRefresh(restoreToBottom, restoreScrollY);
        }
    }

    private void QueueRestoreLayoutRefresh(bool? restoreToBottom = null, int? restoreScrollY = null)
    {
        if (_restoreLayoutQueued || !IsHandleCreated || IsDisposed)
            return;

        var targetScrollY = restoreScrollY ?? _lastChatScrollY;
        var targetToBottom = restoreToBottom ?? _lastChatWasAtBottom;
        _restoreLayoutQueued = true;
        BeginInvoke(() =>
        {
            _restoreLayoutQueued = false;
            if (IsDisposed || WindowState == FormWindowState.Minimized)
                return;

            ApplyResponsiveLayout();
            ForceTextLayoutRefresh(this);
            FinalizeRestoreChatScroll(targetToBottom, targetScrollY);
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
            conversationBar.SuspendLayout();
            inputPanel.SuspendLayout();
            inputCard.SuspendLayout();
            actionBar.SuspendLayout();
            chatContainer.SuspendLayout();

            topBar.Height = ScaleValue(compact ? 42 : 48);
            topBar.Padding = new Padding(ScaleValue(compact ? 10 : 16), 0, ScaleValue(8), 0);
            conversationBar.Height = ScaleValue(compact ? 38 : 42);
            conversationBar.Padding = new Padding(ScaleValue(compact ? 8 : 12), ScaleValue(5), ScaleValue(compact ? 8 : 12), ScaleValue(5));
            newConversationBtn.Text = "";
            newConversationBtn.Font = UiFont(compact ? 8.5F : 9F, FontStyle.Bold);
            newConversationBtn.Size = new Size(ScaleValue(compact ? 32 : 34), ScaleValue(compact ? 28 : 30));
            ApplyIconOnlyButtonLayout(newConversationBtn);
            SetScaledButtonImage(newConversationBtn, "add-24.png", compact ? 18 : 20);
            conversationTabs.Padding = new Padding(ScaleValue(8), 0, 0, 0);

            settingsBtn.Text = compact ? "" : T("Settings");
            if (compact)
            {
                settingsBtn.Size = new Size(ScaleValue(36), ScaleValue(30));
                settingsBtn.Font = UiFont(9F, FontStyle.Bold);
                ApplyIconOnlyButtonLayout(settingsBtn);
                SetScaledButtonImage(settingsBtn, "settings-24.png", 18);
            }
            else
            {
                ApplyAdaptiveIconTextButton(
                    settingsBtn,
                    settingsBtn.Text,
                    "settings-24.png",
                    108,
                    156,
                    34,
                    10F,
                    FontStyle.Bold,
                    20,
                    8);
            }

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

            inputPanel.Padding = compact
                ? new Padding(ScaleValue(10), ScaleValue(6), ScaleValue(10), ScaleValue(10))
                : new Padding(ScaleValue(18), ScaleValue(8), ScaleValue(18), ScaleValue(16));

            inputCard.Padding = new Padding(ScaleValue(compact ? 9 : 12));
            _thumbnailStrip.Height = ScaleValue(compact ? 42 : 50);
            UpdateInputPanelHeight();
            promptHost.Padding = compact
                ? new Padding(ScaleValue(9), ScaleValue(6), ScaleValue(9), ScaleValue(6))
                : new Padding(ScaleValue(12), ScaleValue(8), ScaleValue(12), ScaleValue(8));
            actionBar.Height = ScaleValue(compact ? 38 : 44);
            actionBar.Padding = new Padding(0, ScaleValue(compact ? 6 : 8), 0, 0);

            _attachBtn.Text = tight ? "" : compact ? T("Upload") : T("UploadImage");
            if (tight)
            {
                _attachBtn.Size = new Size(ScaleValue(34), ScaleValue(compact ? 28 : 32));
                _attachBtn.Font = UiFont(compact ? 9F : 10F, FontStyle.Bold);
                ApplyIconOnlyButtonLayout(_attachBtn);
                SetScaledButtonImage(_attachBtn, "upload-image-24.png", compact ? 18 : 20);
            }
            else
            {
                ApplyAdaptiveIconTextButton(
                    _attachBtn,
                    _attachBtn.Text,
                    "upload-image-24.png",
                    compact ? 88 : 112,
                    compact ? 108 : 132,
                    compact ? 28 : 32,
                    compact ? 9F : 10F,
                    FontStyle.Bold,
                    compact ? 18 : 20,
                    compact ? 8 : 10);
            }

            _sendBtn.Text = compact ? T("Generate") : T("GenerateImage");
            ApplyAdaptiveIconTextButton(
                _sendBtn,
                _sendBtn.Text,
                "app-image-24.png",
                tight ? 88 : compact ? 104 : 120,
                compact ? 124 : 148,
                compact ? 28 : 32,
                compact ? 8.5F : 9F,
                FontStyle.Bold,
                compact ? 18 : 20,
                compact ? 8 : 10);

            _promptBox.Font = UiFont(compact ? 9F : 10F);
            if (_promptBox.IsHandleCreated)
                SendMessage(_promptBox.Handle, EM_SETMARGINS, (IntPtr)EC_LEFTMARGIN, (IntPtr)ScaleValue(compact ? 8 : 12));

            UpdateChatPanelBounds();
            ReflowChatRows();
            CenterLoadingLabel();
        }
        finally
        {
            chatContainer.ResumeLayout(false);
            actionBar.ResumeLayout(true);
            inputCard.ResumeLayout(true);
            inputPanel.ResumeLayout(true);
            conversationBar.ResumeLayout(true);
            topBar.ResumeLayout(true);
            ResumeLayout(true);
            _isApplyingResponsiveLayout = false;
        }
    }

    private void UpdateInputPanelHeight()
    {
        var logicalClientSize = GetLogicalClientSize();
        var compact = logicalClientSize.Width < 560 || logicalClientSize.Height < 520;
        var inputHeight = (int)Math.Round(ClientSize.Height * (compact ? 0.28F : 0.26F));
        var baseHeight = Clamp(inputHeight, ScaleValue(compact ? 124 : 142), ScaleValue(176));

        if (_thumbnailStrip.Visible)
            baseHeight += _thumbnailStrip.Height;

        inputPanel.Height = baseHeight;
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

    private Font UiFont(float size, FontStyle style = FontStyle.Regular)
    {
        var fontSize = Math.Max(7.5F, size * _uiScale);
        var sizeKey = (int)Math.Round(fontSize * 100F);
        var key = (sizeKey, style);
        if (_fontCache.TryGetValue(key, out var font))
            return font;

        font = new Font("Microsoft YaHei UI", sizeKey / 100F, style);
        _fontCache[key] = font;
        return font;
    }

    private void DisposeCachedResources()
    {
        _resizeDebounceTimer.Dispose();
        DisposePendingResponses();
        ClearThumbnailCache();

        foreach (var font in _fontCache.Values)
            font.Dispose();

        _fontCache.Clear();

        foreach (var state in _iconTextButtonStates.Values)
            state.Image?.Dispose();

        _iconTextButtonStates.Clear();
    }

    private static int Clamp(int value, int min, int max) =>
        Math.Min(max, Math.Max(min, value));

    private static float Clamp(float value, float min, float max) =>
        Math.Min(max, Math.Max(min, value));

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEWHEEL && TryScrollChatFromMouseWheel(m.WParam, Cursor.Position))
            return;

        base.WndProc(ref m);
    }

    private void ChatContainer_MouseWheel(object? sender, MouseEventArgs e)
    {
        ScrollChatByWheelDelta(e.Delta);
    }

    private bool TryScrollChatFromMouseWheel(IntPtr wParam, Point screenPoint)
    {
        if (chatContainer.IsDisposed || !chatContainer.ClientRectangle.Contains(chatContainer.PointToClient(screenPoint)))
            return false;

        var delta = unchecked((short)((wParam.ToInt64() >> 16) & 0xffff));
        ScrollChatByWheelDelta(delta);
        return true;
    }

    private void ScrollChatByWheelDelta(int delta)
    {
        if (delta == 0 || GetChatMaxScrollY() <= 0)
            return;

        var wheelStep = Math.Max(ScaleValue(48), SystemInformation.MouseWheelScrollLines * ScaleValue(16));
        var targetScrollY = GetChatScrollY() - Math.Sign(delta) * wheelStep;
        RestoreChatScroll(Clamp(targetScrollY, 0, GetChatMaxScrollY()));
    }

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
            ApplyLocalization();
            ApplyTheme();
            ApplyResponsiveLayout();
            _ = InitializeConversationsAsync();
            _promptBox.Focus();
        }
    }

    private void PromptBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            TriggerSend();
            return;
        }

        if (e.KeyCode == Keys.Back
            && string.IsNullOrEmpty(_promptBox.Text)
            && _thumbnailStrip.Controls.Count > 0)
        {
            e.SuppressKeyPress = true;
            RemoveLastThumbnail();
            _promptBox.Focus();
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

    private void SendBtn_Click(object? sender, EventArgs e)
    {
        TriggerSend();
    }

    private async void NewConversationBtn_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_conversationManager == null)
                return;

            await _conversationManager.SaveActiveConversationAsync();
            await _conversationManager.CreateConversationAsync();
            _configManager.Save(_config);
            RebuildConversationTabs();
            LoadActiveConversationMessages();
            UpdateTitleBarText();
            _promptBox.Focus();
        }
        catch (Exception ex)
        {
            HandleUiException(ex, "创建新会话失败");
        }
    }

    // ═══════════════════════════════════════════════════
    //  Core Logic
    // ═══════════════════════════════════════════════════

    private async void TriggerSend()
    {
        if (_conversationManager?.ActiveConversation == null)
            return;

        var conversation = _conversationManager.ActiveConversation;
        var conversationId = conversation.Id;
        if (IsConversationGenerating(conversationId))
            return;

        var prompt = _promptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt) && _attachedImages.Count == 0) return;

        _isGenerating = true;

        var attachedCopy = new List<string>(_attachedImages);
        _promptBox.Clear();
        ClearAttachedThumbnails();
        var enhancedPrompt = prompt;
        ContextDecision? contextDecision = null;

        try
        {
            if (_promptEnhancer != null)
            {
                var enhanceResult = _promptEnhancer.Enhance(
                    prompt,
                    conversation,
                    attachedCopy);
                enhancedPrompt = enhanceResult.EnhancedPrompt;
                contextDecision = enhanceResult.Decision;
                foreach (var imagePath in enhanceResult.AutoAttachedImagePaths)
                {
                    if (!attachedCopy.Contains(imagePath, StringComparer.OrdinalIgnoreCase))
                        attachedCopy.Add(imagePath);
                }
            }

            var userMsg = ChatMessage.UserMessage(prompt, [.. attachedCopy]);
            if (_conversationManager != null)
            {
                await _conversationManager.AddMessageToConversationAsync(conversationId, userMsg);
                if (IsActiveConversation(conversationId))
                {
                    RebuildConversationTabs();
                    UpdateTitleBarText();
                }
            }

            if (IsActiveConversation(conversationId))
                AddChatBubble(userMsg);
            var initialStatus = BuildInitialGenerationStatus(
                contextDecision,
                conversation);

            var apiService = _apiService ?? new ImageApiService(_config);
            using var cts = new CancellationTokenSource(
                TimeSpan.FromMinutes(Math.Max(1, _config.TimeoutMinutes)));
            RegisterPendingResponse(conversationId, initialStatus, cts);
            RefreshActiveConversationControls();

            var progress = new Progress<string>(msg =>
            {
                if (!IsDisposed && !_isClosing)
                    BeginInvoke(() =>
                    {
                        if (IsDisposed || _isClosing)
                            return;

                        UpdatePendingResponseBubble(conversationId, msg);
                        RefreshActiveConversationControls();
                    });
            });

            var result = await Task.Run(() =>
                apiService.GenerateAsync(enhancedPrompt, attachedCopy, progress, cts.Token));

            if (_isClosing || IsDisposed)
                return;

            for (int i = 0; i < result.SavedPaths.Count; i++)
            {
                var imgPath = result.SavedPaths[i];
                var usage = i == 0 ? result.Usage : null;
                var assistantMsg = ChatMessage.AssistantMessage(prompt, imgPath, null, usage);
                if (_conversationManager != null)
                    await _conversationManager.AddMessageToConversationAsync(conversationId, assistantMsg);

                if (IsActiveConversation(conversationId))
                {
                    if (i == 0)
                        CompletePendingResponseWithMessage(conversationId, assistantMsg);
                    else
                        AddChatBubble(assistantMsg);
                }
            }

            var completionMsg = ChatMessage.SystemMessage(BuildCompletionMessage(result));
            if (_conversationManager != null)
                await _conversationManager.AddMessageToConversationAsync(conversationId, completionMsg);
            if (IsActiveConversation(conversationId))
                AddChatBubble(completionMsg);
            if (IsActiveConversation(conversationId))
                RebuildConversationTabs();
        }
        catch (Exception ex)
        {
            if (_isClosing || IsDisposed)
                return;

            var errorMsg = ex switch
            {
                TaskCanceledException => "请求超时，请检查超时设置或降低图片尺寸后重试。",
                HttpRequestException httpEx => $"API 请求失败：{httpEx.Message}",
                DirectoryNotFoundException dirEx => $"输出目录不存在或无法访问：{dirEx.Message}",
                UnauthorizedAccessException accessEx => $"没有文件访问权限，请检查输出目录或图片文件权限：{accessEx.Message}",
                IOException ioEx => $"文件读写失败，请检查磁盘空间、输出目录或图片文件是否可用：{ioEx.Message}",
                InvalidOperationException opEx => opEx.Message,
                _ => $"未知错误：{ex.Message}",
            };
            if (_conversationManager != null)
                await _conversationManager.AddMessageToConversationAsync(
                    conversationId,
                    ChatMessage.SystemMessage(errorMsg));

            if (IsActiveConversation(conversationId))
                CompletePendingResponseWithError(conversationId, errorMsg);
        }
        finally
        {
            ClearPendingResponse(conversationId);
            _isGenerating = _pendingResponses.Count > 0;
            if (!_isClosing && !IsDisposed)
            {
                RefreshActiveConversationControls();
                _promptBox.Focus();
            }
        }
    }

    //  UI Builders
    // ═══════════════════════════════════════════════════

    private void ShowLoading(bool show, string? text = null)
    {
        if (!string.IsNullOrWhiteSpace(text))
            _loadingLabel.Text = text;

        if (show)
        {
            CenterLoadingLabel();
            _loadingOverlay.Visible = true;
            _loadingOverlay.BringToFront();
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

    private bool IsActiveConversation(string conversationId) =>
        string.Equals(
            _conversationManager?.ActiveConversationId,
            conversationId,
            StringComparison.OrdinalIgnoreCase);

    private bool IsConversationGenerating(string conversationId) =>
        _pendingResponses.ContainsKey(conversationId);

    private void RefreshActiveConversationControls()
    {
        var activeConversationId = _conversationManager?.ActiveConversationId;
        var activeConversationGenerating = activeConversationId != null
            && IsConversationGenerating(activeConversationId);

        _isGenerating = _pendingResponses.Count > 0;
        _sendBtn.Enabled = !activeConversationGenerating;
        _attachBtn.Enabled = !activeConversationGenerating;
    }

    private void AddChatBubble(ChatMessage msg)
    {
        var bubble = CreateBubble(msg);
        _chatPanel.Controls.Add(bubble);
        StackChatRows();
        ResizeChatPanelHeight();

        ScrollChatToBottom();
    }

    private void ScrollChatToBottom()
    {
        if (IsHandleCreated)
        {
            BeginInvoke(() =>
            {
                StackChatRows();
                ResizeChatPanelHeight();
                RestoreChatScroll(GetChatMaxScrollY());
            });
        }
    }

    private void AddSystemMessage(string text)
    {
        AddChatBubble(ChatMessage.SystemMessage(text));
    }

}
