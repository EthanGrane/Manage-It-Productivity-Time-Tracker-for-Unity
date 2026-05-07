# ⏱️ Unity Time Tracker

A lightweight time tracking tool for Unity, designed especially for **self-employed developers** who want to measure real working time without friction.

Tracks your work sessions automatically and stores them in a simple JSON file for easy access, analysis, or integration with other tools.

https://github.com/user-attachments/assets/384dab18-e2bf-43e1-a1cd-de1cf7e999f1

## 🛠️ Installation & Setup

Git
```bash
git clone https://github.com/EthanGrane/Unity-Time-Tracker.git
```

Git URL (Unity Package Manager)
```bash
https://github.com/EthanGrane/Unity-Time-Tracker.git
```

Follow these steps to get started:

### 1. Install
Download or clone this repository into your Unity project.

### 2. Move to Editor folder
Place the `UnityTimerTracker` folder inside: ```Assets/Editor/```

### 3. Open the window
Tools -> Time Tracker

<img width="186" height="110" alt="image" src="https://github.com/user-attachments/assets/e9298924-fc81-4aa7-b6bd-bf5aa72e1f47" />



---

## 🔗 GitHub Setup Guide

You can link your repository to display commits in the timeline.

1. Create a Personal Access Token

Go to GitHub:

Settings → Developer settings → Personal access tokens
Click "Generate new token"

Make sure to enable:

✅ repo (required for private repositories)

---

## 📂 Troubleshooting

You need to put the UnityTimerTracker folder in the path ```Assets/Editor/```

---

## 📂 How It Works

Each work session is stored as:

```json
{
  "start": "2026-03-29 10:15:23",
  "lastCheck": "2026-03-29 11:02:10"
}
```

---

## ⚠️ Disclaimer ⚠️
I'm not a fan of the "vibecoding" approach, this tool was generated with AI (Claude) based on my code and design style. 
