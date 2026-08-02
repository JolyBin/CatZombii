# Лицензии сторонних арт-паков

Реестр прав на весь сторонний арт, распакованный в `RawArt/` и `Assets/Art/`.
Требование Яндекс.Игр — «авторские права на все материалы принадлежат создателю»;
на практике площадка требует доказательство **права использования**.
См. [docs/11 §3.1](../../docs/11-art-direction-and-assets.md).

**Дата регистрации в репозитории: 02.08.2026.**

> ⚠️ **Про дату скачивания.** 02.08.2026 — это дата, когда паки были разобраны
> и занесены в этот реестр. Фактическая дата скачивания архивов владельцем
> **не зафиксирована** и по диску не восстанавливается: mtime файлов — это даты
> из самих архивов (2016–2026), а не даты загрузки. Чеков/писем о скачивании нет.
> Все паки — **бесплатные** (freebies и CC0), покупок не было, поэтому чека
> не существует в принципе.

---

## Сводка

| Пак | Источник | Лицензия | Файл лицензии был в архиве? | Коммерция на ЯИ |
|---|---|---|---|---|
| Free Cartoon Cat Defense Game Asset Kit | CraftPix (freebie) | CraftPix Freebies License | ❌ нет | ✅ да |
| Free Water and Fire Magic Sprite Vector Pack | CraftPix (freebie) | CraftPix Freebies License | ❌ нет | ✅ да |
| Free Alchemy Herbs Vector Icons | CraftPix (freebie) | CraftPix Freebies License | ✅ да (`TXT/license.txt`, только ссылка) | ✅ да |
| 2D Game Zombie Character – Free Sprite Pack 1 | CraftPix (freebie) | CraftPix Freebies License | ❌ нет | ✅ да |
| Kenney Particle Pack 1.1 | Kenney.nl | **CC0** | ✅ да (полный текст) | ✅ да |

**Вывод: юридических блокеров на коммерческий WebGL-билд для Яндекс.Игр нет.**
Ни один пак не требует атрибуции. Но: **3 из 5 архивов не содержали файла лицензии
вообще** — текст ниже восстановлен с сайта первоисточника вручную 02.08.2026.

---

## 1. Free Cartoon Cat Defense Game Asset Kit

- **Источник:** https://craftpix.net/freebies/free-cartoon-cat-defense-game-asset-kit/
- **Лицензия:** https://craftpix.net/file-licenses/ (раздел Freebies)
- **Цена:** $0
- **В архиве:** `Free Assets Craftpix!.url` (ссылка на раздел freebies), `Font Link.txt`.
  Файла лицензии **не было**.
- **Лежит в:** `RawArt/CatDefenseKit/`, `Assets/Art/**/CatDefenseKit/`

Дословно со страницы лицензии (проверено 02.08.2026):

> «You are permitted to use the resources in any number of personal and commercial projects»
>
> «You can sell and distribute games with our assets»
>
> «No attribution or link back to this site is required, however any credit will be highly appreciated.»
>
> «You can NOT resell the source files (PNG, JPG, EPS, Adobe Illustrator, etc)»

> ⚠️ **Ограничение, важное именно нам:** «The Licensed Assets … may not be used,
> in whole or in part, for the purposes of training, fine-tuning, developing,
> testing, validating, or improving any artificial intelligence (AI), machine
> learning (ML) … systems». Эти ассеты нельзя скармливать генеративным моделям.

> ⚠️ **Не-юридический риск (docs/11 §4.1, риск 3):** это готовый кит под игру
> «коты обороняются от зомби» — тема, подозрительно близкая к нашей. Решение
> проекта: берём зомби, VFX и служебные экраны; **котов-юнитов и титульный экран
> не берём.**

## 2. Free Water and Fire Magic Sprite Vector Pack

- **Источник:** https://craftpix.net/freebies/free-water-and-fire-magic-sprite-vector-pack/
- **Лицензия:** https://craftpix.net/file-licenses/ (раздел Freebies)
- **Цена:** $0
- **В архиве:** файла лицензии **не было** вообще.
- **Лежит в:** `RawArt/WaterFireMagic/`, `Assets/Art/VFX/WaterFireMagic/`, `Assets/Art/Icons/WaterFireMagic/`

Формулировки — те же, что в §1 (общая лицензия CraftPix на все freebies).

## 3. Free Alchemy Herbs Vector Icons

- **Источник:** https://craftpix.net/freebies/free-alchemy-herbs-vector-icons/
- **Лицензия:** https://craftpix.net/file-licenses/ (раздел Freebies)
- **Цена:** $0
- **В архиве:** `TXT/license.txt` — содержит **только URL** `https://craftpix.net/file-licenses/`,
  без текста. `TXT/readme.txt` — ссылка на шрифт превью (Moon Get!, см. §6).
- **Лежит в:** `RawArt/AlchemyHerbsIcons/`, `Assets/Art/Icons/AlchemyHerbs/`, `Assets/Art/Backgrounds/AlchemyHerbs/`

Формулировки — те же, что в §1.

## 4. 2D Game Zombie Character – Free Sprite Pack 1

- **Источник:** https://craftpix.net/freebies/2d-game-zombie-character-free-sprite-pack-1/
- **Лицензия:** https://craftpix.net/file-licenses/ (раздел Freebies)
- **Цена:** $0
- **В архиве:** `README.txt` — ссылка на шрифт превью (foo, см. §6). Файла лицензии **не было**.
- **Лежит в:** `RawArt/ZombieSpritePack1/`, `Assets/Art/Characters/Zombies/ZombieSpritePack1/`, `Assets/Art/Backgrounds/ZombieSpritePack1/`

Формулировки — те же, что в §1.

## 5. Kenney Particle Pack 1.1

- **Источник:** https://kenney.nl/assets/particle-pack
- **Лицензия:** **CC0 1.0** — http://creativecommons.org/publicdomain/zero/1.0/
- **Цена:** $0 (донат по желанию)
- **В архиве:** `License.txt` с полным текстом — **единственный пак, приложивший
  настоящую лицензию.** Копия: `KenneyParticlePack-license.txt`.
- **Лежит в:** `RawArt/KenneyParticlePack/`, `Assets/Art/VFX/KenneyParticlePack/`

Дословно из `License.txt`:

> «Particle Pack (1.1) by Kenney Vleugels (Kenney.nl)»
>
> «License (Creative Commons Zero, CC0) http://creativecommons.org/publicdomain/zero/1.0/»
>
> «You may use these assets in personal and commercial projects.
> Credit (Kenney or www.kenney.nl) would be nice but is not mandatory.»

Самая безопасная лицензия из существующих: атрибуция не нужна, отозвать нельзя.

---

## 6. Шрифты — проверено отдельно

**Ни один пак не содержит файлов шрифтов** (`.ttf`/`.otf`/`.woff` — 0 штук на диске).
Три текстовых файла — это лишь ссылки на шрифты, которыми набраны **превью паков**.
Использовать их мы не обязаны; в проекте они сейчас не используются.

| Файл | Шрифт | Источник | Лицензия | Коммерция |
|---|---|---|---|---|
| `RawArt/CatDefenseKit/Font Link.txt` | **Passion One** | Google Fonts | SIL OFL 1.1 | ✅ да |
| `RawArt/AlchemyHerbsIcons/TXT/readme.txt` | **Moon Get!** (MaxiGamer) | dafont.com | категория **«100% Free»** | ✅ да |
| `RawArt/ZombieSpritePack1/README.txt` | **foo** (Raymond Larabie / Typodermic) | dafont.com | **Public domain / GPL / OFL**, автор: *«released under a "no rights reserved" Creative Commons Zero license»* | ✅ да |

> Проверено вручную 02.08.2026 на страницах первоисточников.
> Оговорка по Moon Get!: dafont-категория «100% Free» — это **самодекларация автора**
> в карточке шрифта, а не формальный текст лицензии. Для коммерции этого достаточно,
> но если шрифт реально пойдёт в билд — сохранить скриншот страницы.

---

## Что в этом реестре отсутствует

1. **Чеков нет** — все паки бесплатные, покупок не совершалось.
2. **Даты скачивания нет** — восстановить с диска невозможно (см. оговорку вверху).
3. **Скриншотов страниц лицензий нет.** docs/11 §3.1 (риск 4) требует хранить
   PDF/скриншот на случай, если лицензия изменится задним числом. Здесь только
   текстовые цитаты. **Рекомендуется догнать: сохранить PDF `craftpix.net/file-licenses`
   и карточек 4 паков.**
4. **Членство CraftPix ($48) не покупалось** — все 4 пака взяты из бесплатного
   раздела. Платные паки из docs/11 §3.2 (Match 3 GUI, Magic Gems, Match 3 Tile Set)
   не скачаны.
