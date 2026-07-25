# Методика: впровадження кирилиці в бінарний bitmap-шрифт / Methodology: injecting Cyrillic into a binary bitmap font

**UA:** Цей документ описує ПІДХІД і РІШЕННЯ, знайдені під час розробки `SWH.FontTool` — не покроковий туторіал під конкретну гру, а перелік проблем, з якими стикається практично будь-яка локалізація, що працює з власним (proprietary) бінарним bitmap-шрифтом, та як я їх вирішив. Може бути корисним іншим локалізаторам, що працюють з подібними форматами (bitmap-шрифт = запечені в PNG-атлас гліфи + бінарна таблиця метрик).

**EN:** This document describes the APPROACH and SOLUTIONS found while building `SWH.FontTool` — not a step-by-step tutorial for this specific game, but a list of problems that practically any localization working with a proprietary binary bitmap font will run into, and how I solved them. May be useful to other localizers working with similar formats (bitmap font = glyphs baked into a PNG atlas + a binary metrics table).

**UA:** Жодних видобутих файлів гри в цьому репозиторії немає — лише мій власний код. **EN:** No extracted game files live in this repository — only my own code.

---

## 1. Реверс-інжиніринг бінарного формату / Reverse-engineering the binary format

**UA:** Формат `.fnt` не документований розробником. Побайтовим аналізом встановлено: magic-заголовок `bfnt`, далі таблиця записів фіксованого розміру (stride), кожен запис — `ID(int32)`, `AtlasX/Y/W/H(float×4)`, `XOffset/YOffset(float)`, `XAdvance(int32)`. Розмір запису (32 або 36 байт, залежно від наявності 4-байтового префіксу) визначається автоматично для кожного файлу окремо — не хардкодиться, бо різні шрифти в одній грі можуть мати різний stride.

**EN:** The `.fnt` format has no developer documentation. Byte-level analysis established: a `bfnt` magic header, followed by a fixed-size record table, each record being `ID(int32)`, `AtlasX/Y/W/H(float×4)`, `XOffset/YOffset(float)`, `XAdvance(int32)`. The record size (32 or 36 bytes, depending on a 4-byte prefix) is auto-detected per file — not hardcoded, since different fonts in the same game can use a different stride.

## 2. Архітектура: повне перепакування атласу, а не точкова заміна / Architecture: a full atlas repack, not spot-replacement

**UA:** Перший інстинкт — знайти невикористані/подібні кириличні слоти (напр. українська кирилиця майже повністю збігається з російською) і просто підмінити пікселі "на місці". Це має фундаментальну межу: розмір оригінального боксу під літеру розрахований під ЧУЖУ форму (іншу кириличну літеру), тому нова літера або обрізається, або спотворюється, намагаючись влізти в невідповідну рамку.

Рішення: **повне перепакування**. `numChars` (кількість гліфів у таблиці) змінювати НЕ можна — рушій це не толерує. Але PNG-полотно можна БЕЗПЕЧНО збільшити по висоті (підтверджено емпіричним тестом — див. `PngCanvasExperiment.cs`), і намалювати всі 66 українських літер у свіжому просторі знизу, з нуля розрахованою геометрією. Кожен існуючий бінарний запис перезаписується новими координатами (`AtlasX/Y/W/H`, `XOffset/YOffset`, `XAdvance`), що вказують у НОВЕ місце — стара геометрія лишається на диску, але жоден активний запис на неї більше не посилається.

**EN:** The first instinct is to find unused/similar Cyrillic slots (e.g. Ukrainian Cyrillic mostly overlaps with Russian Cyrillic) and just swap the pixels "in place." This has a fundamental limit: the original box for a letter was sized for SOMEONE ELSE's shape (a different Cyrillic letter), so the new letter either gets clipped or distorted trying to fit an ill-matching frame.

The solution: a **full repack**. `numChars` (the glyph count in the table) cannot change — the engine does not tolerate that. But the PNG canvas CAN be safely grown taller (confirmed by an empirical test — see `PngCanvasExperiment.cs`), and all 66 Ukrainian letters can be drawn in fresh space at the bottom, with geometry computed from scratch. Every existing binary record is rewritten with new coordinates (`AtlasX/Y/W/H`, `XOffset/YOffset`, `XAdvance`) pointing at the NEW location — the old geometry stays on disk, but no active record references it anymore.

## 3. Калібрування "від латиниці", не від оригінальної кирилиці / Latin-first calibration, not the original Cyrillic

**UA:** Найважливіша методологічна знахідка: калібрувати розмір і базову лінію нових літер варто відносно ОРИГІНАЛЬНОЇ ЛАТИНИЦІ шрифту (гарантовано коректна — англійська версія гри відвантажується й виглядає правильно), а НЕ відносно оригінальної кирилиці. У файлах цієї гри виявилось, що оригінальна кирилиця подекуди сама була невідповідного розміру відносно латиниці в ТОМУ Ж файлі (розбіжність 13-17%) — калібрування по ній означало успадкувати чужий дефект розміру як "еталон".

**EN:** The single most important methodological finding: the size and baseline of new letters should be calibrated against the font's ORIGINAL LATIN glyphs (guaranteed correct — the English version ships and looks right), NOT against the original Cyrillic. In this game's files, the original Cyrillic turned out to itself be an inconsistent size relative to the Latin in the SAME file (a 13-17% discrepancy) — calibrating off it would mean inheriting someone else's sizing defect as the "reference."

## 4. Растрове само-калібрування розміру / Raster self-calibration

**UA:** Векторні метрики шрифту (напр. `TextMeasurer.MeasureBounds` у ImageSharp) НЕ завжди збігаються з тим, як шрифт реально рендериться растрово в конкретному рушії. Рішення: обирати кегль (point size) шляхом самоперевірки — рендеримо ВЛАСНИЙ референсний гліф шрифту (напр. латинську 'H'/'a') при пробному розмірі, вимірюємо фактичну піксельну висоту чорнила, і масштабуємо кегль так, щоб відтворити виміряну ЦІЛЬОВУ висоту тіла літери з оригінального шрифту. Це усуває розбіжність вектор-проти-растр.

**EN:** A font's vector metrics (e.g. ImageSharp's `TextMeasurer.MeasureBounds`) don't always match how the font actually renders as raster pixels. The solution: pick the point size via self-calibration — render the font's OWN reference glyph (e.g. Latin 'H'/'a') at a trial size, measure the actual pixel ink height, and scale the point size so it reproduces the original font's measured TARGET body height. This eliminates the vector-vs-raster mismatch.

## 5. Трекінг-коефіцієнт індивідуально на шрифт, а не універсальна формула / A per-font tracking ratio, not a universal formula

**UA:** Щільність міжлітерного інтервалу (`XAdvance`) в оригінальній грі виявилась РАДИКАЛЬНО різною по шрифтах — від ~0.64 (сильно конденсовані заголовки/значки фракцій) до ~1.0 (звичайний текст). Жодна єдина формула (ні фіксована константа, ні природний advance нового TTF ≈1.0) не відтворює це коректно для ВСІХ шрифтів одразу: застосована до сильно конденсованого шрифту, вона або роздуває інтервал на 25-55% (переповнення інтерфейсу), або стискає текст до накладання літер.

Рішення: для кожного шрифту рахуємо МЕДІАНУ відношення `XAdvance / ширина_чорнила` по всіх латинських літерах ОРИГІНАЛУ — це і є його справжня щільність трекінгу. Новий `XAdvance` = ширина чорнила нової літери × цей коефіцієнт.

**Важливий нюанс, знайдений пізніше:** навіть цей підхід не гарантує відсутності накладання, якщо новий шрифт (яким рендериться кирилиця) має інші пропорції літер, ніж оригінал. Оригінальна щільність (напр. 0.64) в оригіналі трималась на РУЧНОМУ, посимвольному кернінгу художників гри під форму САМЕ ЇХНІХ літер — один медіанний коефіцієнт не відтворює це для іншого шрифту. Тому поверх формули додано **підлогу безпеки**: гарантований мінімальний позитивний проміжок між чорнилом і `XAdvance`, незалежно від того, що дає формула. Це рятує читабельність ціною трохи менш "тісного" вигляду за оригінал там, де формула давала накладання.

**EN:** Inter-letter spacing density (`XAdvance`) in the original game turned out to be RADICALLY different per font — from ~0.64 (heavily condensed headers/faction badges) to ~1.0 (regular body text). No single formula (neither a fixed constant nor a new TTF's natural advance ≈1.0) reproduces this correctly for ALL fonts at once: applied to a heavily condensed font, it either inflates the spacing by 25-55% (UI overflow) or compresses the text until letters overlap.

The solution: for each font, compute the MEDIAN `XAdvance / ink-width` ratio over all Latin letters in the ORIGINAL — that is its true tracking density. A new letter's `XAdvance` = its ink width × this ratio.

**An important nuance found later:** even this approach doesn't guarantee no overlap if the new font (used to render the Cyrillic) has different letter proportions than the original. The original density (e.g. 0.64) was held together in the original by the game artists' MANUAL, per-character kerning tuned to the shape of THEIR OWN letters — a single median ratio doesn't reproduce that for a different font. So a **safety floor** was added on top of the formula: a guaranteed minimum positive gap between the ink and `XAdvance`, regardless of what the formula computes. This trades a bit of the original's "tight" look for guaranteed readability wherever the formula alone would have caused overlap.

## 6. Безпечна система донорів для відсутніх гліфів / A safe donor system for missing glyphs

**UA:** Не всі 66 українських літер мають прямий відповідник серед оригінальних кириличних слотів гри (напр. Ё/Ъ/ё в українській абетці не потрібні, тож слотів для їхніх аналогів немає). Для таких випадків потрібен "донор" — вільний слот, куди можна записати нову геометрію. Наївний підхід (перший-ліпший невикористаний слот) ризикує випадково зайняти символ, який АКТИВНО використовується ІНШОЮ мовною локалізацією в тому самому файлі (напр. `º`, `Œ`/`œ`, типографські лапки), що зламало б інтерфейс для гравців інших мов.

Рішення: 3-рівнева ієрархія безпеки — (1) справді мертвий слот (не використовується жодним підтримуваним шрифтом рушія алфавітом), (2) неризиковані екзотичні символи Latin Extended (малоймовірно активно використовуються), (3) Latin-1 в останню чергу, з явним попередженням у консольному звіті про необхідність вручну перевірити цей екран у грі для інших мов.

**EN:** Not all 66 Ukrainian letters have a direct counterpart among the game's original Cyrillic slots (e.g. Ё/Ъ/ё aren't needed in Ukrainian, so there are no slots for their equivalents). Such cases need a "donor" — a free slot to write new geometry into. A naive approach (first unused slot found) risks accidentally claiming a character ACTIVELY used by ANOTHER language's localization in the same file (e.g. `º`, `Œ`/`œ`, typographic quotes), which would break the UI for players of other languages.

The solution: a 3-tier safety hierarchy — (1) a truly dead slot (not used by any alphabet the engine's font actually supports), (2) low-risk exotic Latin Extended characters (unlikely to be actively used), (3) Latin-1 as a last resort, with an explicit console-report warning to manually check that screen in-game for other languages.

<details>
<summary>7. Критерії відбору кириличних шрифтів / Cyrillic Font Selection Criteria (клікни, щоб розгорнути / click to expand)</summary>

**UA:** У межах цього проєкту проводиться ретельний аудит усіх асетів. Під час перевірки з'ясувалося, що кириличні розширення деяких обраних спочатку шрифтів (Oswald, Comfortaa) мають походження, яке не відповідає етичним стандартам проєкту, хоча їхня базова ліцензія є відкритою. Задля забезпечення повної прозорості та відповідності цим стандартам, було здійснено перехід на **Fira Sans** (створений європейськими дизайнерами Carrois Type Design та Nikoltchev/Kateliev).

Практична порада іншим локалізаторам: завжди варто перевіряти походження обох частин шрифту (латиниці й кирилиці) окремо, оскільки інформація про авторів розширень часто відсутня в основному описі.

**EN:** A thorough audit of all assets is conducted for this project. The review revealed that the Cyrillic extensions of certain initially chosen fonts (Oswald, Comfortaa) had a provenance that did not align with the project's ethical standards, despite having open licenses. To ensure full transparency and compliance with these standards, a transition was made to **Fira Sans** (created by European designers Carrois Type Design and Nikoltchev/Kateliev).

Practical advice for other localizers: it is always worth checking the provenance of BOTH parts of a font (Latin and Cyrillic) separately, as information about the authors of extensions is often missing from the main description.

</details>

## 8. Постійна діагностика замість одноразових здогадок / Permanent diagnostics instead of one-off guesses

**UA:** `LatinReferenceDiagnostic` — постійний, повторно застосовний інструмент (не одноразовий скрипт), що вимірює ПОВНУ латиницю (і `.fnt`-метрики, і фактичне піксельне чорнило з `.png`) кожного шрифту гри до початку роботи над кирилицею. Це дає реальні дані (baseline, cap-height, x-height, ascender, descender по кожному шрифту) замість припущень — саме ці дані й "спіймали" описані вище проблеми (розбіжність латиниця/кирилиця, різна щільність трекінгу).

**EN:** `LatinReferenceDiagnostic` is a permanent, reusable tool (not a one-off script) that measures the ENTIRE Latin alphabet (both `.fnt` metrics and actual pixel ink from the `.png`) of every game font before Cyrillic work begins. This provides real data (baseline, cap-height, x-height, ascender, descender per font) instead of assumptions — this exact data is what caught the problems described above (Latin/Cyrillic mismatch, differing tracking density).