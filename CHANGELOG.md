# Changelog

Формат базується на [Keep a Changelog](https://keepachangelog.com/uk/1.1.0/).
Based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.0.2] — 2026-09-28

### UA: Додано
- Масове проставлення позначки вичитки (готові `+`/`-`/`+/-` або довільний
  текст через окреме вікно-запит) одразу для кількох виділених рядків —
  таблиця перекладу тепер підтримує множинне виділення (Ctrl/Shift+клік),
  а в контекстному меню з'явився пункт «Позначити вичитку для вибраних
  рядків» з готовими позначками й пунктом «Очистити»
- Пошук тепер охоплює й коментар розробника, а не лише ключ, оригінал і
  переклад — дозволяє знайти рядки за позначками на кшталт «do not
  translate» чи «Latin: ...»

### UA: Виправлено
- Класифікація «технічних» рядків (`LocEntry.IsTechnical`) — загальне
  правило «коментар розробника містить фразу "do not translate"/"don't
  translate"» хибно позначало технічним увесь рядок і тоді, коли фраза
  стосувалась лише ОДНОГО слова в коментарі, а не всього рядка:
  `encounter_desc_scrapper_04` (стосувалось лише слова «Gallons») та
  `banner_boss_3_killed_header` (лише імені «Vectron») тепер завжди
  перекладні. І навпаки — `weapon_smg_02`, `weapon_smg_03` (абревіатура
  «SMG» лишається як є) та `bark_dora_recruited_no`/`bark_dora_recruited_yes`
  (латинські фрази, коментар розробника — лише переклад-підказка) раніше
  не розпізнавались технічними; додано до списку примусових винятків

---

### EN: Added
- Bulk-applying a review marker (the ready-made `+`/`-`/`+/-`, or free-form
  text via a small prompt window) to several selected rows at once — the
  translation table now supports multi-selection (Ctrl/Shift+click), and
  the context menu gained a "Mark review for selected rows" item with the
  ready-made markers plus a "Clear" option
- Search now also covers the developer comment, not just the key, original
  and translation — finds rows by markers like "do not translate" or
  "Latin: ..."

### EN: Fixed
- "Technical" row classification (`LocEntry.IsTechnical`) — the general
  rule "the developer's comment contains the phrase 'do not translate'/
  'don't translate'" falsely flagged the WHOLE row as technical even when
  the phrase referred to just ONE word in the comment, not the whole row:
  `encounter_desc_scrapper_04` (was about the word "Gallons" only) and
  `banner_boss_3_killed_header` (only the name "Vectron") are now always
  translatable. Conversely, `weapon_smg_02`, `weapon_smg_03` (the "SMG"
  abbreviation stays as-is) and `bark_dora_recruited_no`/
  `bark_dora_recruited_yes` (Latin phrases; the developer's comment is just
  a translation hint) weren't previously recognized as technical; added to
  the forced-exception lists

## [1.0.1] — 2026-07-29

### UA: Змінено
- Збереження з міткою часу — ім'я за замовчуванням тепер
  `en 250726 1920.csv.z` (основа оригіналу + дата й час) замість
  однакового імені, що мовчки затирало попередній переклад
- Поруч із файлом для гри автоматично пишеться парний робочий TSV у теку
  `review/` (ID, оригінал, переклад, коментар розробника, вичитка в
  окремих колонках) — замикає цикл роботи над перекладом між сесіями
- Кнопка «Злити переклад» приймає як робочий TSV (переклад + вичитка),
  так і раніше збережений перекладений `.csv`/`.csv.z` (лише переклад);
  формат визначається автоматично

### UA: Виправлено
- Захист від помилково обраного файлу при злитті перекладу — попередження
  з пропозицією обрати інший файл замість тихого «зіставлено 0», якщо
  жодного ключа поточного оригіналу не знайдено

---

### EN: Changed
- Timestamped saves — the default filename is now `en 250726 1920.csv.z`
  (original's base + date and time) instead of the same name every time,
  which silently clobbered the previous translation
- A paired working TSV is now written automatically alongside the game
  file, into the `review/` folder (ID, original, translation, developer
  comment, review status in separate columns) — closes the translation
  workflow loop between sessions
- "Merge Translation" accepts both a working TSV (translation + review)
  and a previously saved translated `.csv`/`.csv.z` (translation only);
  the format is detected automatically

### EN: Fixed
- Guard against the wrong file being merged — a warning offering to pick
  another file instead of silently reporting "matched 0" when not a
  single key from the current original is found

## [1.0.0] — 2026-07-29

### UA: Додано
- Пряме редагування мовного CSV гри (`.csv`/`.csv.z`) без QuickBMS
- Живі перевірки перекладу під час редагування клітинки (довжина
  відносно оригіналу, збереження технічних маркерів тощо)
- Автоматичне виявлення технічних рядків (немає реального тексту для
  перекладу) і дублікатів оригіналу, з підсвіткою розбіжного перекладу
  серед дублів
- Редагована колонка «Вичитка» — готові позначки (`+`, `-`, `+/-`) або
  довільний коментар до рядка
- Кольорове кодування всього рядка за пріоритетом статусу (проблема >
  розбіжний дублікат > технічний > змінено-незбережено > перекладено >
  без перекладу)
- Автозбереження робочого файлу
- Темна/світла тема

---

### EN: Added
- Direct editing of the game's language CSV (`.csv`/`.csv.z`), no
  QuickBMS
- Live translation checks while editing a cell (length relative to the
  original, preserved technical markers, etc.)
- Automatic detection of technical rows (no actual text to translate)
  and duplicate originals, with highlighting for inconsistent
  translations among duplicates
- An editable "Review" column — ready-made markers (`+`, `-`, `+/-`) or
  a free-form per-row comment
- Whole-row color coding by status priority (issue > inconsistent
  duplicate > technical > modified-unsaved > translated > untranslated)
- Working-file autosave
- Dark/light theme
