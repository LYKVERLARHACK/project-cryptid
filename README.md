# Project Cryptid

**A local YouTube fact-checking and AI content analysis tool for Windows**

Project Cryptid detects the currently playing YouTube video and analyzes it using locally running AI models through [Ollama](https://ollama.com/). No URL copying or manual uploads required.

## Features

- **Claim Check** — Extracts key claims made in a video, searches the web for supporting or contradicting information, and evaluates the results using a local LLM (Language Learning Model).
- **AI Content Detection** — Examines sampled video frames and the transcript for visual inconsistencies and potential AI-generated content.
- **Automatic Browser Video Detection** — Detects the active YouTube video in supported browsers.
- **Local AI Processing** — Runs analysis through `qwen3.5:4b` using Ollama.
- **Incident History** — Saves completed analyses and failures locally in SQLite, with support for follow-up questions.
- **Customizable UI** — Choose between Default, Kids Mode, Inferno, Tech, and White themes.

## Getting Started

### Requirements

- Windows
- Visual Studio
- [Ollama](https://ollama.com/) with `qwen3.5:4b` installed
- Python 3.10+
- Node.js
- FFmpeg
- yt-dlp and faster-whisper

### Installation

1. Clone the repository.
2. Install Python dependencies:

   ```powershell
   python -m pip install --user --upgrade -r requirements.txt
   ```

3. Pull the required Ollama model:

   ```powershell
   ollama pull qwen3.5:4b
   ```

4. Ensure FFmpeg and Node.js are available on your `PATH`.
5. Open `RealityCheckDesktop.csproj` in Visual Studio.
6. Set it as the startup project and press **F5**.

### How To Use

1. Open a YouTube video in a supported browser.
2. Click the Project Cryptid system tray icon.
3. Select **Check Claims** or **Check AI Content**.
4. View the analysis and results in the desktop overlay.

Use the gear menu to change themes, view incident history, or hide the overlay. Press **Escape** or use the close button to dismiss it.

## How It Works

**Claim Checking**
- Retrieves English captions or transcribes speech locally.
- Extracts up to three key claims.
- Searches public web sources for relevant evidence.
- Uses Ollama to compare the claims against search results.

**AI Content Detection**
- Downloads the video's audio and samples up to 12 visual sections.
- Retrieves captions or generates a local transcript.
- Examines sampled frames for visual continuity and potential AI-generation cues.
- Produces an estimated AI-content score and explanation.

Analysis typically targets 40–60 seconds, but actual times depend on hardware, downloads, and model setup.

## Privacy

Project Cryptid is designed with local processing in mind.

- AI inference and speech transcription run locally.
- Web searches send extracted claim queries to external search providers.
- Video and temporary analysis files are stored only during processing and cleaned up afterward.
- Completed reports and follow-up conversations are stored locally in SQLite:

  `%LOCALAPPDATA%\RealityCheckDesktop\incidents.sqlite3`

- Full transcripts, videos, audio, and sampled frames are not stored in incident history.

The app reads the foreground supported browser's address bar to identify the active YouTube video. It does not scan background windows.

## Limitations

- AI-content scores are experimental, uncalibrated estimates, **not probabilities or proof of AI generation**.
- Visual analysis is based on sampled frames, not every frame.
- Audio is used for transcription only; synthetic voice detection and audio forensics are not supported.
- Claim assessments rely on search snippets, not complete source investigations.
- Private, age-restricted, region-blocked, or unavailable videos may fail.
- Whisper's `tiny.en` model downloads on first use. Set `REALITYCHECK_WHISPER_MODEL` to change the model size.
- Follow-up questions use saved incident data only and cannot re-analyze the original video.
- Supported Browsers: Brave, Chrome, MS Edge, Firefox, Opera and Vivaldi.
