# GPTImagePlayground

GPTImagePlayground is a .NET 10 Windows Forms desktop client for AI image generation via OpenAI-compatible image APIs. It offers a chat-style interface with multi-conversation history, configurable image size and output options, concurrent multi-image generation, and local context-aware prompt enhancement.

## Features

### Image Generation
- Generate images from text prompts via `/images/generations`.
- Attach reference images for guided generation or editing via `/images/edits`.
- Concurrent multi-image generation with configurable parallelism (`ImageCount` + `MaxConcurrency`).
- Responses support both `b64_json` and `url` image data.

### Image Configuration
- **Size mode** — `auto`, `preset` (1K/2K/4K tiers with 8 aspect ratios), or `custom` (width × height, normalized to multiples of 16).
- **Output format** — PNG, JPEG, or WebP.
- **Transparent background** option.
- **Moderation** level control.

### Conversations & Context
- Multi-conversation management: create, switch, rename, and delete conversations.
- Conversation history persisted as JSON files with an `index.json` catalog.
- **Context-aware prompt enhancement** — local heuristic analysis decides whether to inject recent conversation text or auto-attach recent generated images.
- **Context compression** — older messages are summarized when the conversation exceeds configured limits, keeping the active window manageable.
- Manual attachments take priority over auto-attached history images (configurable).

### UI & Experience
- **Themes** — light and dark mode.
- **Localization** — simplified Chinese (zh-CN), English (en), and traditional Chinese (zh-TW).
- Inline preview of generated images.
- Token usage and request timing display (when returned by the API).
- Adaptive icon+text toolbar buttons with proper DPI scaling (PerMonitorV2).

### Security & Networking
- API credentials stored in local `appsettings.json` (Git-ignored).
- Configurable request timeout.
- SSL certificate verification toggle for self-signed or non-standard endpoints.

## Tech Stack

- C#
- .NET 10 (net10.0-windows)
- Windows Forms
- System.Text.Json
- HttpClient

## Requirements

- Windows
- .NET SDK compatible with `net10.0-windows`
- An OpenAI-compatible image generation API endpoint
- A valid API key

## Getting Started

```powershell
git clone https://github.com/ajwkb5159-cloud/GPTImagePlayground.git
cd GPTImagePlayground

# Build
dotnet build

# Run
dotnet run

# Publish (optional)
dotnet publish -c Release -o ./publish
```

## Configuration

All settings are managed through the in-app Settings dialog and persisted to `appsettings.json` beside the executable.

### API & Connection

| Setting | Description | Default |
|---------|-------------|---------|
| `BaseUrl` | API service base URL (e.g. `https://api.example.com/v1`) | — |
| `ApiKey` | API key for authentication | — |
| `Model` | Image model name | `gpt-image-2` |
| `TimeoutMinutes` | Request timeout in minutes | `10` |
| `VerifySslCertificate` | Enforce TLS certificate validation | `true` |

### Image Size & Output

| Setting | Description | Default |
|---------|-------------|---------|
| `SizeMode` | `auto`, `preset`, or `custom` | `auto` |
| `SizeTier` | Preset tier: `1K`, `2K`, or `4K` | `1K` |
| `AspectRatio` | Preset ratio: `1:1`, `3:2`, `2:3`, `16:9`, `9:16`, `4:3`, `3:4`, `21:9` | `1:1` |
| `CustomWidth` / `CustomHeight` | Custom dimensions (normalized to multiples of 16, max 3840 px) | `1024` |
| `OutputFormat` | `png`, `jpeg`, or `webp` | `png` |
| `TransparentBackground` | Request transparent background | `false` |
| `Moderation` | Content moderation level | `auto` |

### Generation Strategy

| Setting | Description | Default |
|---------|-------------|---------|
| `ImageCount` | Number of images per request | `1` |
| `UseConcurrentStrategy` | Send parallel sub-requests when `ImageCount > 1` | `true` |
| `MaxConcurrency` | Max parallel sub-requests | `4` |

### Conversation & Context

| Setting | Description | Default |
|---------|-------------|---------|
| `ConversationStoreDir` | Directory for conversation JSON files | `conversations` |
| `MaxActiveMessages` | Max messages kept in active context | `20` |
| `CompressionTriggerCount` | Message count that triggers compression | `30` |
| `KeepRecentCount` | Recent messages preserved during compression | `10` |
| `MaxContextPrompts` | Max recent prompts considered for context | `5` |
| `MaxContextImages` | Max recent images considered for context | `1` |
| `ContextAutoAttachThreshold` | Similarity threshold for auto-attaching history images | `0.55` |
| `EnablePromptEnhancement` | Enable context-aware prompt enhancement | `true` |
| `EnableReferenceDetection` | Detect reference image intent in prompts | `true` |
| `ShowContextDecisionHint` | Show context decision hints in the UI | `true` |
| `AllowHistoryImagesWithManualAttachments` | Combine history images with manual attachments | `false` |

### Appearance

| Setting | Description | Default |
|---------|-------------|---------|
| `Theme` | UI theme: `light` or `dark` | `light` |
| `Language` | UI language: `zh-CN`, `en`, or `zh-TW` | `zh-CN` |

## Usage

1. Launch the application.
2. Open **Settings** and configure your API connection (Base URL, API key, model).
3. Adjust image size, output format, generation strategy, and context options to your preference.
4. Enter a prompt in the chat input box and optionally attach reference images.
5. Send the request. A pending indicator appears while generation is in progress.
6. Generated images are displayed inline as chat bubbles with previews.
7. Images are automatically saved to the configured output directory as `newapi_{yyyyMMdd_HHmmss}.{extension}`.
8. Use the conversation tabs to start new conversations, switch between them, or rename/delete existing ones.
9. Switch theme or language anytime from Settings → Appearance.

## Architecture

```
Program.cs                  App entry point, WinForms bootstrap
Models/                     DTOs for API requests/responses, config, conversations
Services/
  ConfigManager.cs          Read/write appsettings.json
  ImageApiService.cs        HTTP transport, request building, response parsing, file saving
  ImageSizeResolver.cs      Auto/preset/custom size resolution
  ConversationManager.cs    Conversation CRUD, active conversation switching, caching
  ConversationStore.cs      JSON file persistence with atomic writes and index.json
  ContextCache.cs           In-memory cache for recent conversations
  ContextCompressor.cs      Older-message summarization
  ContextDecisionService.cs Heuristic prompt analysis for context injection
  PromptEnhancer.cs         Build final prompt from user input + conversation context
  TextSimilarityService.cs  CJK-aware local similarity scoring
  AppAppearance.cs          Theme palette and localization dictionaries
Forms/
  MainForm.cs               Main chat UI (split into partial files)
  SettingsForm.cs            Settings dialog (split into partial files)
Assets/Icons/               App icon and toolbar icons
```

## API Behavior

- Text-only requests → `POST {BaseUrl}/images/generations` (JSON body).
- Requests with attachments → `POST {BaseUrl}/images/edits` (multipart/form-data).
- Single-request mode sets `n = ImageCount`. Concurrent mode sends `ImageCount` parallel sub-requests with `n = 1`.
- Responses may use `b64_json` or `url`; both are supported.
- `OutputFormat` controls both the request parameter and the saved file extension.
- When `OutputDir` is blank, files save to the system temp directory.

## Notes

- `appsettings.json` may contain API credentials and is excluded from version control via `.gitignore`.
- Build outputs (`bin/`, `obj/`) and Visual Studio files are also Git-ignored.
- Context intelligence is entirely local and heuristic — no external model calls are made for summarization or intent detection.
- The app uses `ApplicationHighDpiMode.PerMonitorV2` for per-monitor DPI awareness.

## License

This project currently does not include a license file. Add one before publishing if you want to clearly define how others may use, modify, or distribute the code.
