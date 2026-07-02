# GPT Image 上下文智能化与设置页帮助说明开发方案

## 1. 背景与目标

当前项目已经具备本地会话、历史消息、提示词增强、自动附加上一张生成图等基础能力，但上下文判断主要集中在 `Services/ReferenceDetector.cs` 中，通过硬编码关键词判断用户是否在引用上一张图。这种方式有三个明显问题：

1. 识别能力脆弱：只要用户换一种说法，可能就无法触发上下文。
2. 维护成本高：关键词越补越多，仍然覆盖不了真实表达。
3. 用户体验不透明：用户不知道系统为什么会自动带图，失败时也难以理解。

本次方案目标：

1. 保留当前 Image API 请求形态，让真正发起图片生成/编辑的请求仍然使用配置中的 `gpt-image-2`。
2. 将 `ReferenceDetector` 的关键词判断升级为“上下文决策器”，让系统根据会话结构、用户输入完整度、最近图片、手动附件、历史提示词等信息自动编排上下文。
3. 在设置界面“上下文”Tab 中加入帮助说明图标，鼠标悬停即可解释每个参数的作用。
4. 先形成文档确认，确认后再进入代码实现。

## 2. 官方能力边界

根据 OpenAI 官方图像生成文档：

1. Image API 支持直接选择 GPT Image 模型，例如 `gpt-image-2`，并提供 `/images/generations` 与 `/images/edits` 两类能力。
2. Responses API 更适合对话式、多轮、可编辑的图片体验，它能把图像输入/输出放进上下文，也支持 `previous_response_id` 等状态管理能力。
3. 关键差异是：Image API 由调用方直接选择 GPT Image 模型；Responses API 由主模型调用图像生成工具，图像工具内部处理 GPT Image 模型选择。

因此，本项目如果坚持“本次生图 API 请求的模型就是 `gpt-image-2`”，建议继续走 Image API，并在本地做上下文编排。Responses API 可作为未来高级模式，但不放入本次默认实现。

参考：

- OpenAI Image generation guide: https://developers.openai.com/api/docs/guides/image-generation
- OpenAI Conversation state guide: https://developers.openai.com/api/docs/guides/conversation-state

## 3. 当前代码现状

已阅读的关键文件：

- `Services/ReferenceDetector.cs`
  - 当前通过 `StrongPatterns`、`WeakPatterns` 判断是否引用上下文。
  - 文件中中文字符串存在编码乱码，进一步降低可维护性。
- `Services/PromptEnhancer.cs`
  - 当前入口是 `Enhance(prompt, conversation)`。
  - 若 `ReferenceDetector` 判断命中，就自动附加最近一张生成图。
  - 若开启提示词上下文，就拼接历史摘要和最近提示词。
- `Forms/MainForm.cs`
  - `TriggerSend()` 中先调用 `PromptEnhancer.Enhance()`，再调用 `_apiService.GenerateAsync(enhancedPrompt, attachedCopy, ...)`。
  - 因此，上下文智能化的最佳切入点是 `PromptEnhancer` 及其依赖服务。
- `Models/Conversation.cs`
  - 已有 `GetLastGeneratedImagePath()`、`GetRecentUserPrompts()`、`GetRecentGeneratedImagePaths()`。
  - 可以直接作为上下文候选来源。
- `Models/ContextWindowConfig.cs`
  - 已有 `MaxContextPrompts`、`MaxContextImages`，但当前没有完整映射到 `AppConfig` 和设置页。
- `Forms/SettingsForm.cs`
  - “上下文”Tab 是运行时动态构建的，适合直接添加帮助图标和 ToolTip。

## 4. 总体设计

新增一个“上下文决策”层，替代关键词检测。

推荐结构：

```text
MainForm.TriggerSend
  -> PromptEnhancer.Enhance(prompt, conversation, manuallyAttachedImages)
      -> ContextDecisionService.BuildDecision(...)
          -> ContextCandidateBuilder
          -> ContextScorer
          -> ContextPackageBuilder
      -> 返回 enhancedPrompt、autoAttachedImagePaths、decisionReason
  -> ImageApiService.GenerateAsync(enhancedPrompt, finalImagePaths)
```

核心思想不是“猜一句话里有没有某几个词”，而是像 Codex 使用上下文一样：

1. 收集候选上下文：最近提示词、压缩摘要、最近生成图、手动上传图、当前会话标题/时间。
2. 对当前请求做结构化分析：是否完整、是否依赖历史、是否需要编辑已有图、是否已有手动附件。
3. 根据预算和配置选择最小必要上下文包。
4. 把决策结果变成对 `gpt-image-2` 更明确的 prompt 和参考图输入。
5. 在 UI 或调试日志中保留“为什么自动带了上下文”的原因，方便后续排错。

## 5. 上下文决策器设计

### 5.1 新增模型

建议新增 `Models/ContextDecision.cs`：

```csharp
internal enum ContextIntent
{
    StandaloneGeneration,
    ContinueFromConversation,
    EditRecentImage,
    EditSelectedImages,
    Ambiguous
}

internal sealed class ContextDecision
{
    public ContextIntent Intent { get; init; }
    public bool ShouldAttachRecentImages { get; init; }
    public bool ShouldInjectTextContext { get; init; }
    public List<string> SelectedImagePaths { get; init; } = [];
    public List<string> SelectedTextContext { get; init; } = [];
    public string DecisionReason { get; init; } = "";
    public double Confidence { get; init; }
}
```

### 5.2 新增服务

建议新增 `Services/ContextDecisionService.cs`：

职责：

1. 根据当前 prompt、会话历史、手动附件、上下文配置生成 `ContextDecision`。
2. 不写死自然语言关键词列表。
3. 不调用额外文本模型，避免破坏“生图请求就是 `gpt-image-2`”的前提。
4. 使用可解释的结构特征和上下文候选评分。

可用特征：

1. 手动附件优先
   - 用户已上传图片时，优先认为用户想基于附件生成或编辑。
   - 自动上下文不再重复附加最近图，除非开启“允许手动附件叠加历史图”。
2. Prompt 完整度
   - 如果当前 prompt 有明确主体、风格、场景、构图描述，倾向独立生成。
   - 如果当前 prompt 很短、缺少主体，但会话中刚生成过图，倾向继续上下文。
3. 历史距离
   - 最近一轮生成图权重最高。
   - 时间越久、轮次越远，权重越低。
4. 会话连续性
   - 当前 prompt 与最近用户提示词长度、结构、主题相近，倾向注入文本上下文。
   - 如果是新主题或信息完整，少注入上下文，避免污染。
5. 图像预算
   - `MaxContextImages` 控制最多自动附加几张历史图。
   - 默认只自动附加 1 张，减少 `gpt-image-2` 高保真图像输入带来的 token 成本。

### 5.3 可解释评分，不使用硬编码关键词

示例评分方向：

```text
score = 0

如果用户有手动附件：
  intent = EditSelectedImages
  score += 0.9

如果 prompt 很短且最近有生成图：
  score += 0.35

如果 prompt 缺少明确主体但包含明显属性调整结构：
  score += 0.25

如果距离上一张图小于 N 轮：
  score += 0.20

如果 prompt 与上一轮主题向量/词项相似：
  score += 0.20

如果 prompt 包含完整新主体、新场景、新风格：
  score -= 0.35
```

这里的重点是：不要维护“上一张、这张、改成”等词表。可以通过更通用的文本结构特征判断，例如：

1. prompt 长度。
2. 名词性片段数量。
3. 逗号/顿号/换行分隔的描述密度。
4. 是否出现尺寸、风格、镜头、主体、场景等完整生成要素。
5. 与历史 prompt 的相似度。

第一版可以使用轻量本地算法：

1. 中文/英文统一按字符 bigram、词项、标点切分构造向量。
2. 使用 cosine similarity 计算当前 prompt 与最近 prompt 的相似度。
3. 这不是 LLM，但比关键词更稳定，也不会增加 API 成本。

未来可选增强：

1. 引入本地 ONNX embedding 模型，做真正语义检索。
2. 增加可选的文本模型“上下文路由器”，但这会引入第二个 API 模型，不建议作为本次默认方案。

## 6. Prompt 编排设计

当前 `PromptEnhancer` 只在判断命中时拼接上下文。建议改成“上下文包”方式。

### 6.1 Prompt 模板

当需要注入文本上下文时：

```text
请基于以下本地会话上下文理解用户意图；如果当前用户提示词已经足够完整，请优先遵循当前提示词。

【会话摘要】
{CompressedSummary}

【最近提示词】
1. ...
2. ...

【当前用户请求】
{Prompt}
```

当需要编辑最近图时：

```text
请把随请求附带的参考图作为当前编辑基础。
保留用户未要求改变的主体、构图、身份一致性和关键细节。

【当前用户请求】
{Prompt}
```

当独立生成时：

```text
{Prompt}
```

### 6.2 避免上下文污染

需要明确规则：

1. 当前 prompt 完整时，不强行塞历史提示词。
2. 手动上传图优先级高于自动附加图。
3. 自动附加图片时最多附加 `MaxContextImages` 张，默认 1。
4. 历史摘要只作为辅助，不应覆盖用户本轮明确要求。
5. 如果置信度低于阈值，优先不自动附图，只注入少量文本上下文或完全独立生成。

## 7. 配置项设计

建议把 `ContextWindowConfig` 和 `AppConfig` 补齐：

新增或启用：

```csharp
public int MaxContextPrompts { get; set; } = 5;
public int MaxContextImages { get; set; } = 1;
public decimal ContextAutoAttachThreshold { get; set; } = 0.55M;
public bool ShowContextDecisionHint { get; set; } = true;
public bool AllowHistoryImagesWithManualAttachments { get; set; } = false;
```

原配置保留：

```csharp
public int MaxActiveMessages { get; set; } = 20;
public int CompressionTriggerCount { get; set; } = 30;
public int KeepRecentCount { get; set; } = 10;
public bool EnableReferenceDetection { get; set; } = true;
public bool EnablePromptEnhancement { get; set; } = true;
```

建议改名但兼容旧配置：

1. `EnableReferenceDetection` UI 展示为“智能引用历史图”。
2. 内部可继续保留字段名，避免破坏已保存 `appsettings.json`。
3. 后续大版本再重命名为 `EnableSmartContextImages`。

## 8. UI 帮助说明设计

用户要求：设置界面“上下文”Tab 上加帮助说明图标，鼠标覆盖时显示每个参数作用。

### 8.1 实现方式

在 `SettingsForm.BuildContextTab()` 中：

1. 将 `_contextTable` 从 3 列调整为 4 列。
2. 第 4 列放一个小型帮助按钮或 Label，文本为 `?` 或 `i`。
3. 新增一个 `ToolTip _contextToolTip` 字段。
4. 每行调用统一方法添加参数、控件、帮助图标。

建议新增方法：

```csharp
private void AddContextRow(
    TableLayoutPanel table,
    string labelText,
    Control control,
    int row,
    string helpText)
```

帮助图标可用：

```csharp
var help = new Label
{
    Text = "?",
    TextAlign = ContentAlignment.MiddleCenter,
    Cursor = Cursors.Help,
    ForeColor = Color.FromArgb(37, 99, 235),
    Font = UiFont(9F, FontStyle.Bold),
};
_contextToolTip.SetToolTip(help, helpText);
```

不建议使用 MessageBox，因为用户明确要“光标覆盖时显示”。

### 8.2 每个参数说明文案

建议 ToolTip 文案：

1. 会话存储目录
   - “保存本地会话、历史提示词和生成记录的位置。切换目录后，新旧会话不会自动合并。”
2. 活跃消息数
   - “当前会话中直接参与上下文分析的最近消息数量。数值越大，连续性越强，但上下文也更容易变杂。”
3. 压缩触发阈值
   - “当消息数量超过该值时，较早历史会被压缩成摘要，避免会话无限变长。”
4. 保留最近消息
   - “压缩历史时始终保留的最近消息数量。建议小于压缩触发阈值。”
5. 智能引用历史图
   - “开启后，系统会判断本轮请求是否依赖最近生成图，并在需要时自动附加历史图作为参考图。”
6. 提示词上下文
   - “开启后，系统会把会话摘要和最近提示词整理进请求，帮助模型理解连续创作意图。”
7. 最近提示词数量
   - “允许注入到上下文中的最近用户提示词数量。”
8. 最多历史参考图
   - “自动附加历史生成图的上限。gpt-image-2 会高保真处理图像输入，数量越多成本和耗时越高。”
9. 自动附图阈值
   - “上下文决策置信度达到该值才自动附加历史图。越高越保守，越低越积极。”
10. 显示决策提示
    - “生成前或状态栏显示本次是否使用了历史图/文本上下文，便于理解自动行为。”

第一版如果不想增加太多控件，可以先给已有 6 项加 ToolTip；智能上下文相关新增项可以第二步添加。

## 9. 文件级改造清单

### 9.1 新增文件

1. `Models/ContextDecision.cs`
2. `Services/ContextDecisionService.cs`
3. `Services/TextSimilarityService.cs`

### 9.2 修改文件

1. `Services/PromptEnhancer.cs`
   - 构造函数改为接收 `ContextDecisionService`。
   - `Enhance()` 返回 `DecisionReason`、`Confidence`。
   - 移除对 `ReferenceDetector` 的直接依赖。
2. `Services/ReferenceDetector.cs`
   - 第一版可保留但不再主路径使用。
   - 确认稳定后删除或标记 obsolete。
3. `Models/AppConfig.cs`
   - 补齐 `MaxContextPrompts`、`MaxContextImages` 等配置字段。
4. `Models/ContextWindowConfig.cs`
   - 增加阈值、显示决策提示、手动附件叠加策略。
5. `Services/ConversationManager.cs`
   - `CreateContextConfigFromAppConfig()` 和 `EnsureConversationConfig()` 同步新增字段。
6. `Forms/MainForm.cs`
   - 初始化 `_promptEnhancer` 时注入新服务。
   - 根据 `DecisionReason` 可选更新 pending 文案，例如“已自动引用最近生成图”。
7. `Forms/SettingsForm.cs`
   - 上下文 Tab 增加帮助图标与 ToolTip。
   - 可选增加“最近提示词数量、最多历史参考图、自动附图阈值、显示决策提示”等控件。

## 10. 分阶段实现计划

### 阶段一：帮助说明图标

目标：

1. 不改变生成逻辑。
2. 在上下文 Tab 每个参数右侧增加帮助图标。
3. 解决当前上下文 Tab 中文乱码显示问题。

验收：

1. 设置页打开正常。
2. 鼠标悬停帮助图标能看到说明。
3. 窗口缩放后帮助图标不重叠、不挤压输入控件。

### 阶段二：上下文决策器替换关键词

目标：

1. 新增 `ContextDecisionService`。
2. `PromptEnhancer` 改为使用上下文决策器。
3. `ReferenceDetector` 不再作为主路径。
4. 自动附图变成可解释决策，而不是关键词命中。

验收用例：

1. 用户输入完整新主题：“生成一张赛博朋克城市夜景”。
   - 预期：不自动附加上一张图。
2. 上一轮生成头像后，用户输入：“眼神再锐利一点，背景保持不变”。
   - 预期：自动附加最近生成图。
3. 用户手动上传参考图并输入：“按这个风格生成海报”。
   - 预期：使用手动附件，不额外附加历史图。
4. 连续多轮微调同一张图。
   - 预期：持续引用最近生成图，但不无限附加多张历史图。
5. 长会话超过压缩阈值。
   - 预期：仍能使用摘要和最近提示词，不丢失基本连续性。

### 阶段三：上下文参数补齐

目标：

1. 设置页补充 `MaxContextPrompts`、`MaxContextImages`、`ContextAutoAttachThreshold`。
2. 将这些配置完整保存到 `appsettings.json`。
3. 会话加载时同步到 `ContextWindowConfig`。

验收：

1. 保存设置后重启应用，配置仍存在。
2. 调低阈值后自动附图更积极。
3. 调高阈值后自动附图更保守。

## 11. 风险与取舍

### 11.1 不引入额外 LLM 的限制

因为用户明确要求生图请求使用 `gpt-image-2`，第一版不建议再调用一个文本模型做意图识别。这样可以避免：

1. 多一次 API 成本和延迟。
2. 额外模型配置复杂度。
3. “到底哪个模型在理解上下文”的产品解释问题。

代价是：上下文决策器不是完整 LLM 推理，只是更智能的本地上下文编排。

### 11.2 自动附图成本

官方文档说明 `gpt-image-2` 会以高保真方式处理图像输入。自动附加历史图越多，输入 token 成本和耗时越高。因此默认建议：

1. `MaxContextImages = 1`
2. 只有置信度足够高才自动附图
3. 用户手动附件优先

### 11.3 Responses API 路线

Responses API 的上下文能力更接近“模型自己决定生成还是编辑”，但它不符合“直接请求模型就是 `gpt-image-2`”这个约束。因此本次不建议切换。

未来可以作为高级模式：

1. `ImageApiMode = DirectImageApi`
2. `ImageApiMode = ResponsesConversation`

默认仍为 `DirectImageApi`。

## 12. 建议最终方案

我建议按以下顺序执行：

1. 先做设置页帮助图标，低风险，立刻提升可用性。
2. 再新增 `ContextDecisionService`，用上下文包编排替换 `ReferenceDetector` 的关键词判断。
3. 最后补齐上下文参数配置，让用户可以控制“自动上下文”的激进程度。

这条路线能保持当前 `gpt-image-2` Image API 调用不变，又能把“上下文能力”从关键词触发升级为可解释、可配置、可继续演进的智能上下文系统。
