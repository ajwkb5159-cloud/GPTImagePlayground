# GPTImagePlayground

GPTImagePlayground is a lightweight Windows desktop client for AI image generation. It provides a simple chat-style interface for sending prompts, attaching reference images, configuring API parameters, and saving generated images locally.

## Features

- Generate images from text prompts.
- Attach one or more reference images for image editing or guided generation.
- Configure API base URL, API key, model name, output directory, and request timeout from the settings window.
- Preview generated images directly in the app.
- Save generated images automatically to the configured output folder.
- Display basic token usage and request timing information when returned by the API.
- Keep local credentials in `appsettings.json`, which is intentionally ignored by Git.

## Tech Stack

- C#
- .NET 10
- Windows Forms
- System.Text.Json
- HttpClient

## Requirements

- Windows
- .NET SDK compatible with `net10.0-windows`
- An OpenAI-compatible image generation API endpoint
- A valid API key

## Getting Started

Clone the repository:

```powershell
git clone https://github.com/ajwkb5159-cloud/GPTImagePlayground.git
cd GPTImagePlayground
```

Build the project:

```powershell
dotnet build
```

Run the app:

```powershell
dotnet run
```

## Configuration

Open the settings window in the app and fill in:

- `BaseUrl`: API service base URL, for example `https://api.example.com/v1`
- `ApiKey`: your API key
- `Model`: image model name, for example `gpt-image-2`
- `OutputDir`: local folder for generated images
- `TimeoutMinutes`: request timeout in minutes

The app stores these values in `appsettings.json` next to the executable. This file may contain sensitive credentials and should not be committed to GitHub.

## Usage

1. Launch the application.
2. Open settings and configure your API connection.
3. Enter a prompt in the input box.
4. Optionally attach reference images.
5. Send the request and wait for the generated result.
6. View the preview and find the saved image in your output directory.

## Notes

- The application calls OpenAI-compatible `/images/generations` and `/images/edits` endpoints.
- Generated image responses can be handled from either `b64_json` data or image URLs.
- Build output folders such as `bin/`, `obj/`, Visual Studio files, and local configuration files are excluded from version control.

## License

This project currently does not include a license file. Add one before publishing if you want to clearly define how others may use, modify, or distribute the code.
