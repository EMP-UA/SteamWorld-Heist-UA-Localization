# SteamWorld Heist — Ukrainian Localization (by EMP_UA)

**UA:** Цей репозиторій містить технічний вихідний код та скрипти, розроблені для української локалізації SteamWorld Heist.
**EN:** This repository contains the technical source code and scripts developed for the Ukrainian localization of SteamWorld Heist.

---

## 🔡 Font Engineering Toolset / Інструментарій для роботи зі шрифтами (`SWH.FontTool`)

**UA:** `SWH.FontTool` — набір C#-інструментів (Core / Analyzer / CLI) для впровадження повного українського алфавіту у власний бінарний формат шрифтів гри (`.fnt` + `.png`), з повністю новою геометрією для кожної літери замість запозичення пікселів чи форми з оригінальної кирилиці.

**EN:** `SWH.FontTool` — a suite of C# tools (Core / Analyzer / CLI) for injecting the full Ukrainian alphabet into the game's proprietary binary font format (`.fnt` + `.png`), with fully new geometry for every letter instead of borrowing pixels or shapes from the original Cyrillic.

### Можливості / Features

- **UA:** Реверс-інжиніринг бінарного формату `.fnt` (магічний заголовок `bfnt`, автоматичне визначення розміру запису/stride) — без жодної документації від розробника, лише побайтовий аналіз. / **EN:** Reverse-engineered the binary `.fnt` format (magic header `bfnt`, automatic record-stride detection) — with no developer documentation, purely through byte-level analysis.
- **UA:** Повне перепакування атласу: усі 66 українських літер отримують нову геометрію у свіжому просторі PNG (полотно зростає безпечно; `numChars` не змінюється — підтверджено емпірично, що рушій це толерує). / **EN:** Full atlas repack: all 66 Ukrainian letters get freshly generated geometry in newly added PNG space (the canvas grows safely; `numChars` never changes — empirically confirmed the engine tolerates this).
- **UA:** Калібрування "від латиниці": базова лінія та висота тіла літери прив'язані до ОРИГІНАЛЬНОЇ латиниці шрифту (яка гарантовано коректна — англійська версія гри відвантажується й виглядає правильно), а НЕ до оригінальної кирилиці, яка подекуди сама була невідповідного розміру в тих самих файлах. / **EN:** Latin-first calibration: baseline and body-height are anchored to the font's ORIGINAL Latin glyphs (guaranteed correct — the English version ships and looks right), NOT to the original Cyrillic, which in some files was itself an inconsistent size.
- **UA:** Індивідуальний, вимірюваний коефіцієнт трекінгу (XAdvance) для КОЖНОГО шрифту окремо — щільність міжлітерного інтервалу в оригіналі коливається від ~0.64 (сильно конденсовані шрифти) до ~1.0 (звичайний текст); універсальна формула тут не працює. / **EN:** A per-font, measured tracking (XAdvance) ratio — the original's inter-letter spacing density ranges from ~0.64 (heavily condensed fonts) to ~1.0 (body text); a single universal formula does not work here.
- **UA:** Постійний діагностичний інструмент (`LatinReferenceDiagnostic`), що вимірює ПОВНУ латиницю (і `.fnt`-метрики, і фактичне піксельне чорнило з `.png`) кожного шрифту гри — еталонні дані, на основі яких калібрується кирилиця. / **EN:** A permanent diagnostic tool (`LatinReferenceDiagnostic`) that measures the ENTIRE Latin alphabet (both `.fnt` metrics and actual pixel ink from the `.png`) of every game font — the ground-truth data the Cyrillic is calibrated against.
- **UA:** Безпечна система донорів для "неіснуючих" українських гліфів (Ё/Ъ/ё тощо відсутні в оригінальних шрифтах): 3-рівнева ієрархія (мертвий слот → неризикований екзотичний Latin Extended → Latin-1 в останню чергу), яка уникає символів, що активно використовуються іншими локалізаціями (Español/Français/Deutsch/Italiano) у тому ж файлі. / **EN:** A safe donor system for Ukrainian glyphs with no direct equivalent (Ё/Ъ/ё etc. are absent from the original fonts): a 3-tier hierarchy (dead slot → low-risk exotic Latin Extended → Latin-1 as a last resort) that avoids characters actively used by other localizations (Spanish/French/German/Italian) in the same file.

### Чому не Oswald / Comfortaa? / Why not Oswald / Comfortaa?

**UA:** Перші версії інструменту використовували шрифти Oswald-Bold і Comfortaa для рендеру нових літер. Обидва згодом замінені на родину **Fira Sans** (Carrois Type Design для Mozilla/Telefónica, кирилицю розширювали болгарські дизайнери Nikoltchev/Kateliev) — та сама відкрита ліцензія OFL, вага й пропорції підібрані виміряно (векторне порівняння товщини штриха), щоб замінити шрифт непомітно для ока. Детальніше, включно з причиною заміни, — у [`SWH.FontTool/FONT_APPROACH.md`](SWH.FontTool/FONT_APPROACH.md).

<details>
<summary>Технічна причина заміни / Technical reason for the swap (клікни, щоб розгорнути / click to expand)</summary>

**UA:** Перевірка походження показала, що кириличне розширення ОБОХ початкових шрифтів (Google Fonts) виконувала одна й та сама студія — Cyreal (Олексій Ваняшин, росія). Оскільки ціль проєкту — україномовна локалізація без жодних запозичень з російськомовного технічного стеку, обидва шрифти замінено на Fira Sans, чию латиницю й кирилицю створювала одна команда (Carrois Type Design / болгарські дизайнери Nikoltchev-Kateliev) без стосунку до Cyreal.

**EN:** A provenance check found that the Cyrillic extension of both original fonts (Google Fonts) was done by the same studio — Cyreal (Alexei Vanyashin, russia). Since the project's goal is a Ukrainian localization with no borrowed pieces from the Russian-language technical stack, both fonts were swapped for Fira Sans, whose Latin and Cyrillic were built by one team (Carrois Type Design / Bulgarian designers Nikoltchev-Kateliev) with no relation to Cyreal.

</details>

### Вимоги / Requirements

**UA:** .NET 10 SDK. Інструмент не містить жодних файлів гри чи шрифтів — після клонування створіть (або дайте програмі створити) теки original-fonts/ і ttf-fonts/ поруч зі скомпільованим .exe та покладіть туди свої файли (оригінальні .png з гри та TTF-шрифти відповідно). Жодних хардкод-шляхів у коді немає.
**EN:** .NET 10 SDK. The tool ships with no game files or fonts — after cloning, create (or let the app create) the original-fonts/ and ttf-fonts/ folders next to the compiled .exe and drop your own files there (the game's original .png and TTF fonts, respectively). There are no hardcoded paths anywhere in the code.

---

## 📝 Localization Editor GUI / Редактор локалізації з графічним інтерфейсом (`SWH.LocEditor`)

**UA:** `SWH.LocEditor` — WPF-застосунок (розділений на `SWH.LocEditor.Core` — чиста логіка без залежності від GUI, і `SWH.LocEditor.GUI` — WPF-інтерфейс) для роботи з мовним CSV гри напряму: без проміжних кроків через QuickBMS, з живою валідацією перекладу по клітинці й одним екраном для всього процесу вичитки.

**EN:** `SWH.LocEditor` — a WPF application (split into `SWH.LocEditor.Core` — pure logic with no GUI dependency, and `SWH.LocEditor.GUI` — the WPF interface) for working with the game's language CSV directly: no intermediate QuickBMS steps, live per-cell translation validation, and a single screen for the whole proofreading workflow.

### Можливості / Features

- **UA:** Читає `.csv.z` напряму (розпаковує в пам'яті через `SWH.LocEditor.Core`, без зовнішніх утиліт) або звичайний `.csv`; зберігає назад у той самий формат, з якого відкрито. / **EN:** Reads `.csv.z` directly (decompressed in-memory via `SWH.LocEditor.Core`, no external utilities) or a plain `.csv`; saves back in whatever format it was opened from.
- **UA:** Автоматичне визначення "технічних" рядків — оригінал без жодної літери (лише теги/плейсхолдери) АБО коментар розробника прямо каже "do not translate"/"don't translate" — такі рядки виключені з "Без перекладу" й підрахунку дублікатів, і показують м'яку примітку замість тексту для перекладу. / **EN:** Automatic detection of "technical" rows — an original with no actual letters (just tags/placeholders) OR a developer comment explicitly saying "do not translate"/"don't translate" — such rows are excluded from "Untranslated" and duplicate counting, and show a soft note instead of translatable text.
- **UA:** Живі перевірки перекладу під час редагування: кирилиця у службовій змінній (`%д` замість `%d` — падіння рушія), незбалансовані теги, та попередження про підозріло довший/коротший за оригінал переклад (пороги підібрані під природну довшість української мови, а не задають хибних спрацювань на кожному рядку). / **EN:** Live translation checks while editing: Cyrillic inside an engine variable (`%д` instead of `%d` — crashes the engine), unbalanced tags, and warnings for a translation suspiciously longer/shorter than the original (thresholds tuned for Ukrainian's natural length expansion, not false-triggering on every row).
- **UA:** Виявлення дублікатів оригіналу (однаковий текст під різними ключами) з одним кліком розповсюдження перекладу на всю групу, і окремим маркером, якщо дублікати вже розійшлися в перекладі. / **EN:** Duplicate-original detection (identical text under different keys) with a one-click propagate-to-group action, plus a separate marker if duplicates have already drifted out of sync in translation.
- **UA:** Колонка "Вичитка" — редагована: приймає готові позначки (`+`, `-`, `+/-`) або довільний коментар до рядка; автозбереження тихо пише робочий TSV (лише ті колонки, якими володіє програма — див. нижче) у `review/`, якщо є незбережена робота. / **EN:** An editable "Review" column — accepts ready-made markers (`+`, `-`, `+/-`) or a free-form per-row comment; autosave silently writes the working TSV (only the columns the program owns — see below) into `review/` whenever there's unsaved work.
- **UA:** Чітке розмежування джерел даних review-таблиці: ключ/оригінал/коментар розробника завжди беруться з оригінального CSV гри, а не з review; дві останні колонки Google-таблиці (побажання щодо перекладу, версія/прогрес вичитки) призначені виключно для людей-рецензентів і програма їх ніколи не читає, не зберігає і не перезаписує. / **EN:** A clear separation of the review table's data sources: the key/original/developer comment always come from the game's original CSV, never from review; the Google Sheet's last two columns (translation suggestions, review version/progress) are for human reviewers only, and the program never reads, stores, or overwrites them.
- **UA:** Темна/світла тема, масштаб шрифту, фільтри за статусом (Без перекладу / Перекладено / Технічні / Дублікати / Проблемні / Змінено / Пройшли вичитку / Без вичитки) з живими лічильниками. / **EN:** Dark/light theme, font-size scaling, status filters (Untranslated / Translated / Technical / Duplicates / Issues / Modified / Reviewed / Not reviewed) with live counters.
- **UA:** Просте файлове логування (`logs/`) ключових операцій і необроблених винятків — для діагностики проблем без потреби відтворювати їх при мені. / **EN:** Simple file logging (`logs/`) of key operations and unhandled exceptions — for troubleshooting without needing to reproduce a problem live.

### Вимоги / Requirements

**UA:** .NET (WPF, Windows) SDK відповідно до `SWH.LocEditor.GUI.csproj`. Автоматично створює теки `original/`, `review/`, `output/` поруч зі скомпільованим .exe (можна або покласти файли туди заздалегідь, або обрати їх вручну через діалог) — жодних хардкод-шляхів у коді немає.
**EN:** .NET (WPF, Windows) SDK per `SWH.LocEditor.GUI.csproj`. Automatically creates `original/`, `review/`, `output/` folders next to the compiled .exe (files can either be dropped in ahead of time or picked manually via a dialog) — there are no hardcoded paths anywhere in the code.

---

## 🛡️ Technical Transparency (for Nexus Mods) / Технічна прозорість

**UA:** Для забезпечення безпеки та прозорості моїх інсталяторів я надаю скрипт **Inno Setup (`packexe.iss`)**.
- **Призначення:** автоматизує розгортання локалізованих активів у теку гри, а саме: заміну основного текстового архіву (`en.csv.z`), розгортання власних атласів шрифтів (`.png`) та згенерованих визначень шрифтів (`.fnt`), оновлення вмісту DLC заміною перепакованих архівів (`.impak`).
- **Безпека:** інсталятор виконує лише операції копіювання файлів у теці гри.
- **Реєстр:** інсталятор створює стандартний запис у **HKEY_CURRENT_USER** лише для (1) підтримки деінсталятора через Панель керування Windows та (2) відстеження версії (щоб запобігти дублюванню встановлення).
- **Очищення:** усі записи реєстру та встановлені файли повністю видаляються деінсталятором. Жодні глобальні системні налаштування, драйвери чи параметри безпеки не змінюються.

**EN:** To ensure the safety and transparency of my installers, I am providing the **Inno Setup script (`packexe.iss`)**.
- **Purpose:** automates the deployment of localized assets to the game directory: replacing the main text archive (`en.csv.z`), deploying custom font atlases (`.png`) and generated font definitions (`.fnt`), updating DLC content by replacing repacked archives (`.impak`).
- **Security:** the installer only performs file-copy operations within the game's folder.
- **Registry Use:** the installer creates a standard entry in **HKEY_CURRENT_USER** solely for (1) uninstaller support via the Windows Control Panel and (2) version tracking (to prevent duplicate installations).
- **Cleanup:** all registry entries and installed files are completely removed by the uninstaller. No global system settings, drivers, or security configurations are modified.

---

## 🧰 Third-party Tools & Credits / Подяки

- **[QuickBMS](https://github.com/LittleBigBug/QuickBMS):** **UA:** основний рушій для екстракції та повторного імпорту текстових архівів `.z` гри. **EN:** the primary engine used for extracting and re-importing the game's `.z` text archives.
- **Font Metrics Research:** **UA:** особлива подяка **sb8gapi** та спільноті **[Graj po Polsku](https://grajpopolsku.pl/)** — їхній технічний аналіз структури `.fnt` SteamWorld Heist став основою для початкових версій моїх C#-інструментів. **EN:** special thanks to **sb8gapi** and the **[Graj po Polsku](https://grajpopolsku.pl/)** community — their technical analysis of the SteamWorld Heist `.fnt` structure was the foundation for the early versions of my C# tools.
- **Archive Logic:** **UA:** технічні інсайти щодо `.z`/`.csv.z` файлів взято зі **Steam Community** (обговорення моддингу *SteamWorld Quest*). **EN:** technical insights for handling `.z`/`.csv.z` files were sourced from **Steam Community** modding discussions for *SteamWorld Quest*.
- **[Inno Setup](https://jrsoftware.org/isinfo.php):** **UA:** використано для створення професійного пакета встановлення з визначенням версії та деінсталятором. **EN:** used to create the installation package with version detection and uninstaller support.
- **[Fira Sans / Fira Sans Extra Condensed](https://github.com/bBoxType/FiraSans):** **UA:** шрифтова родина (Carrois Type Design для Mozilla/Telefónica, OFL), використана для рендеру всіх нових українських гліфів — з чітким, задокументованим авторством. **EN:** the font family (Carrois Type Design for Mozilla/Telefónica, OFL) used to render all new Ukrainian glyphs — with clean, documented authorship.
- **[SixLabors.ImageSharp / ImageSharp.Drawing](https://github.com/SixLabors/ImageSharp):** **UA:** обробка PNG-атласів та рендер гліфів у `SWH.FontTool`. **EN:** PNG atlas processing and glyph rendering in `SWH.FontTool`.
- **[Ollama](https://ollama.com/) / TranslateGemma 12B:** **UA:** локальний ШІ для початкового проходу перекладу (без хмарних API-ключів). **EN:** local AI used for the initial translation pass (no cloud API keys involved).
- **AI coding assistant (Claude):** **UA:** частину коду в цьому репозиторії написано за допомогою AI-асистента — усі технічні рішення, вимірювання, перевірка в грі та фінальне ухвалення лишаються за автором. **EN:** portions of the code in this repository were drafted with the help of an AI coding assistant — all technical decisions, measurements, in-game verification, and final sign-off remain the author's own.

---

## ⚙️ Development Workflow / Робочий процес

**UA:** Цей проєкт розроблений одноосібно — без команди чи співавторів — і охоплює складний конвеєр розробки:
1. **Екстракція та пакування:** **[QuickBMS](https://github.com/LittleBigBug/QuickBMS)** для розшифрування архівів `.z`, власний **.impak Repacker** для збереження оригінальних рівнів стиснення при перепакуванні DLC.
2. **AI-переклад:** локальний **TranslateGemma 12B** (через Ollama) для початкового проходу перекладу, з подальшим ретельним ручним редагуванням і адаптацією тону під всесвіт *SteamWorld*.
3. **Технічна валідація:** `TextValidator` — злиття перевірених перекладів (TSV з Google Таблиць) назад у формат гри з автоматичною перевіркою тегів, змінних `%`, переносів рядка та випадкової кирилиці у службових змінних (падіння рушія).
4. **Шрифтова інженерія:** `SWH.FontTool` — повне впровадження українського алфавіту в бінарні шрифти гри, детально описано вище й у [`SWH.FontTool/FONT_APPROACH.md`](SWH.FontTool/FONT_APPROACH.md).

**EN:** This project was developed independently — no team or co-authors — and involves a complex development pipeline:
1. **Extraction & Packaging:** **[QuickBMS](https://github.com/LittleBigBug/QuickBMS)** for decrypting `.z` archives, a custom **.impak Repacker** that preserves original per-file compression levels when rebuilding DLC archives.
2. **AI-Enhanced Translation:** a local **TranslateGemma 12B** instance (via Ollama) for the initial translation pass, followed by extensive manual proofreading and tone adaptation to the *SteamWorld* universe.
3. **Technical Validation:** `TextValidator` — merges reviewed translations (TSV from Google Sheets) back into the game's format with automatic checks for tags, `%` placeholders, newlines, and stray Cyrillic in engine variables (which crashes the engine).
4. **Font Engineering:** `SWH.FontTool` — full Ukrainian alphabet injection into the game's binary fonts, detailed above and in [`SWH.FontTool/FONT_APPROACH.md`](SWH.FontTool/FONT_APPROACH.md).

---

## 📂 Repository Structure / Структура репозиторію

- **`/installer`** — **UA:** вихідний скрипт **Inno Setup (`.iss`)** — повна прозорість того, як локалізація розгортається й видаляється. **EN:** the **Inno Setup (`.iss`)** source script — full transparency on how the localization is deployed and uninstalled.
- **`/SWH.FontTool`** — **UA:** повний C#-набір (.NET 10) для шрифтової інженерії, три проєкти:
  - `SWH.FontTool.Core` — **UA:** спільні моделі та конфігурація (парсинг бінарного `.fnt`, `GlyphRecord`, система донорів для відсутніх гліфів). **EN:** shared models and configuration (binary `.fnt` parsing, `GlyphRecord`, the donor system for missing glyphs).
  - `SWH.FontTool.Analyzer` — **UA:** основний рушій: генерація нової геометрії, рендер PNG-атласу, діагностичні інструменти (в т.ч. `LatinReferenceDiagnostic`) та ізольовані експерименти, якими перевірялись припущення рушія гри (толерантність до більшого PNG, поведінка ID-полів тощо). **EN:** the core engine: new-geometry generation, PNG atlas rendering, diagnostic tools (including `LatinReferenceDiagnostic`), and isolated experiments used to validate assumptions about the game engine (tolerance for a larger PNG, ID-field behavior, etc).
  - `SWH.FontTool.CLI` — **UA:** консольне меню — точка входу. **EN:** the console menu — the entry point.
- **`/SWH.LocEditor`** — **UA:** WPF-редактор локалізації з графічним інтерфейсом, два проєкти:
  - `SWH.LocEditor.Core` — **UA:** чиста логіка без залежності від GUI: парсинг `.csv`/`.csv.z`, злиття review-TSV, виявлення технічних/дубльованих рядків, валідація перекладу. **EN:** pure GUI-independent logic: `.csv`/`.csv.z` parsing, review-TSV merging, technical/duplicate row detection, translation validation.
  - `SWH.LocEditor.GUI` — **UA:** WPF-інтерфейс: тема, фільтри, живе редагування по клітинці, автозбереження, логування. **EN:** the WPF interface: theming, filters, live per-cell editing, autosave, logging.
- **`/tools`** — **UA:** окремі одно-файлові C#-утиліти:
  - `TextValidator.cs` — **UA:** злиття перекладів і технічна QA (детально описано в Development Workflow). **EN:** translation merging and technical QA (see Development Workflow above).
  - `ImpakRepacker.cs` — **UA:** перепакування DLC-архівів зі збереженням оригінальних рівнів стиснення. **EN:** DLC archive repacking that preserves original compression levels.
  - `OllamaTranslatorClient.cs` — **UA:** клієнт для автоматизації ШІ-перекладу через локальний Ollama. **EN:** automation client for the AI translation pass via local Ollama.
- **`LICENSE`** — **UA:** ліцензія проєкту (MIT). **EN:** the project's license (MIT).

---

### ⚖️ Copyright Note / Примітка щодо авторських прав

**UA:** Увесь код і скрипти в цьому репозиторії — авторська робота, надана виключно для некомерційного використання фанатами та для технічної прозорості перед майданчиками модів (напр. Nexus Mods). Оригінальні активи, тексти та бінарні формати гри належать розробнику SteamWorld Heist; цей репозиторій не містить жодних видобутих файлів гри.

**EN:** All code and scripts in this repository are original work, provided solely for non-commercial fan use and for technical transparency toward mod platforms (e.g. Nexus Mods). The original assets, text, and binary formats belong to the developer of SteamWorld Heist; this repository contains no extracted game files.
