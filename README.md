SteamWorld Heist — Ukrainian Localization (by EMP_UA)

This repository contains the technical source code and scripts I developed for the Ukrainian localization of SteamWorld Heist.


🛡️ Technical Transparency (for Nexus Mods)

To ensure the safety and transparency of my installers, I am providing the Inno Setup script (packexe.iss).
Purpose: Automates the deployment of localized assets (.bank files and DLC folders) to the game directory.
Security: The installer performs only file-copy operations within the game's installation folder and does not modify system settings.


Third-party Tools

For unpacking the main game .z files, I used the QuickBMS script provided by the Steam/Polish localization community.


⚙️ My Development Workflow

This project is a 100% solo effort involving a complex technical pipeline:
Custom C# Tools: I wrote specific tools for unpacking/repacking .impak DLC files and performing font atlas analysis.
QuickBMS: Used for handling encrypted .z archives of the main game.
AI-Assisted Translation: I utilized TranslateGemma 12B for the initial translation pass, followed by my own manual proofreading and contextual editing.
AI Collaboration: I also utilized AI assistance (Google AI Studio/Gemini) for code optimization and technical documentation.


📂 Repository Structure

/installer — Contains my packexe.iss (Inno Setup source).

/tools — (Coming Soon) My C# scripts for asset management and font generation.
