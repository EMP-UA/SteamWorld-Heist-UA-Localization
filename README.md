SteamWorld Heist — Ukrainian Localization (by EMP_UA)

This repository contains the technical source code and scripts I developed for the Ukrainian localization of SteamWorld Heist.

---

## 🛡️ Technical Transparency (for Nexus Mods)

To ensure the safety and transparency of my installers, I am providing the **Inno Setup script (`packexe.iss`)**. 
- **Purpose:** Automates the deployment of localized assets to the game directory, specifically:
  - Replacing the main text archive (`en.csv.z`).
  - Deploying custom font atlases (`.png`) and generated font definitions (`.fnt`).
  - Updating DLC content by replacing repacked archives (`.impak`).
- **Security:** The installer performs file-copy operations within the game's folder. 
- **Registry Use:** The installer creates a standard entry in **HKEY_CURRENT_USER** for two specific purposes:
  1. **Uninstaller support:** To allow users to easily remove the localization via the Windows Control Panel.
  2. **Version tracking:** To detect if version 0.50 is already installed and prevent duplicate installations or guide the user through a re-installation.
- **Cleanup:** All registry entries and installed files are completely removed when the user runs the uninstaller. No global system settings, drivers, or security configurations are modified.

---

## 🧰 Third-party Tools & Credits

This project stands on the shoulders of the modding community. I would like to acknowledge the following tools and researchers:

* **[QuickBMS](https://github.com/LittleBigBug/QuickBMS):** The primary engine used for extracting and re-importing the game's `.z` text archives.
* **Font Metrics Research:** Special thanks to **sb8gapi** and the **[Graj po Polsku](https://grajpopolsku.pl/)** community. Their technical analysis of the SteamWorld Heist `.fnt` structure provided the essential foundation for my custom C# font generation tools.
* **Archive Logic:** Technical insights for handling `.z` and `.csv.z` files were sourced from the **Steam Community** (specifically modding discussions for *SteamWorld Quest*).
* **[Inno Setup](https://jrsoftware.org/isinfo.php):** Used to create the professional installation package with integrated version detection and uninstaller support.

---

### ⚙️ My Development Workflow

This project is a 100% solo technical and linguistic effort involving a complex development pipeline:

* **Custom C# Engineering:** I developed a specialized suite of tools for the localization process, including a **Binary .fnt Master** for font reconstruction, an **.impak Repacker** for DLC assets, and a **Text Validator** for ensuring data integrity (preventing placeholder mismatches and engine crashes).
* **Asset Management:** Utilized **QuickBMS** for decrypting and handling the game's `.z` archives, ensuring the seamless integration of localized `.csv` files.
* **AI-Enhanced Localization:** I utilized a local instance of **TranslateGemma 12B** (via Ollama) for the initial translation pass. This was followed by extensive manual proofreading and contextual editing to match the *SteamWorld* universe's tone.
* **Technical Optimization:** Leveraged **Google AI Studio (Gemini)** for refining C# logic and maintaining high-quality technical documentation.

---

### 📂 Repository Structure

* **/installer** — Contains the **Inno Setup (`.iss`)** source script. This provides full transparency on how the localization is deployed and uninstalled.
* **/tools** — My custom C# development suite:
    * `FontGenMaster.cs` — The core engine for generating font atlases and binary metrics.
    * `ImpakRepacker.cs` — Logic for DLC archive reconstruction and compression analysis.
    * `TextValidator.cs` — Tool for merging translations and performing technical QA.
    * `OllamaTranslatorClient.cs` — Automation client for the AI-assisted translation pass.
