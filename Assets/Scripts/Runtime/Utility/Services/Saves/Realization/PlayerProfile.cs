using System;
using System.Collections.Generic;

namespace Utility.Services.Saves
{
    /// <summary>
    /// ЧТО ИМЕННО ПЕРЕЖИВАЕТ ПЕРЕЗАГРУЗКУ ВКЛАДКИ. Один класс, плоская структура,
    /// никаких ссылок на ассеты и <c>UnityEngine.Object</c> — только данные.
    ///
    /// Состав v1 задан docs/09 (скоуп фазы 1, пункт 5): «пройденный уровень + герой + валюта».
    /// Состав v2 задан docs/10 §13 (принятая мета): карта перепроходимых узлов, купленные
    /// рецепты, экипированная колода, слоты колоды и открытые герои.
    ///
    /// ПОЧЕМУ ПУБЛИЧНЫЕ ПОЛЯ, а не свойства, как принято в остальном проекте.
    /// <c>JsonUtility</c> сериализует поля и не умеет авто-свойства: у свойства
    /// backing-поле называется <c>&lt;Version&gt;k__BackingField</c>, и ровно это имя
    /// уехало бы в сейв живого игрока. Формат сохранения обязан читаться человеком
    /// в баг-репорте, поэтому имена полей — часть контракта, и они выбраны явно.
    /// Цена известна: инварианты полем не защитить, поэтому их чинит
    /// <see cref="Normalize"/> сразу после чтения.
    ///
    /// ОГРАНИЧЕНИЯ <c>JsonUtility</c>, под которые заложена структура (см. <see cref="ProfileSerializer"/>):
    /// нет словарей, нет полиморфизма, <c>null</c> у строк превращается в <c>""</c>.
    /// Поэтому: никаких <c>Dictionary</c> — только массивы <c>[Serializable]</c>-структур,
    /// никаких базовых классов в полях, и «нет значения» кодируется пустой строкой,
    /// а не <c>null</c>.
    ///
    /// ⚠️ ИНИЦИАЛИЗАТОРЫ ПОЛЕЙ. <c>JsonUtility.FromJson</c> ИХ ПРИМЕНЯЕТ к тому, чего нет
    /// в json. Для <see cref="Version"/> это стоило бы распознавания чужого сейва
    /// (см. комментарий там же), поэтому у полей, добавленных метой, инициализаторов
    /// НЕТ вовсе: значения по умолчанию проставляет <see cref="CreateNew"/>, чинит
    /// <see cref="Normalize"/>, а старым сейвам их выдаёт <see cref="ProfileMigration"/>.
    /// Так «значение по умолчанию» лежит в трёх известных местах, а не размазано
    /// по объявлениям полей.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        /// <summary>
        /// ВЕРСИЯ ФОРМАТА. Не версия игры. Растёт на единицу каждый раз, когда старый сейв
        /// перестаёт читаться «как есть» и ему нужна миграция (<see cref="ProfileMigration"/>).
        ///
        /// v1 — уровень + герой + валюта (docs/09).
        /// v2 — мета docs/10 §13: <see cref="LevelIndex"/> сменился на <see cref="ClearedNodes"/>,
        ///      появились колода, покупки, слоты и открытые герои.
        /// </summary>
        public const int CURRENT_VERSION = 2;

        /// <summary>
        /// Слотов колоды на старте (docs/10 §14.4). Слоты ОБЩИЕ ДЛЯ ВСЕХ ГЕРОЕВ
        /// (§15.1: «это свойство игрока, а не книги» — иначе цена утраивается
        /// и слоты перестают быть желанными), поэтому число лежит в профиле,
        /// а не в записи героя.
        /// </summary>
        public const int BASE_DECK_SLOTS = 4;

        /// <summary>
        /// Потолок слотов (docs/10 §14.4: «4 на старте → 8 покупками»).
        ///
        /// ⚠️ Единственное число меты, которое НЕ выводится из данных: ни в одном ассете
        /// сегодня нет ни лестницы цен слотов (150/250/400/600 из §14.4), ни признака
        /// «сколько их всего». Когда лестница цен станет ассетом, потолок обязан выводиться
        /// из её длины, а не жить здесь.
        /// </summary>
        public const int MAX_DECK_SLOTS = 8;

        /// <summary>
        /// Версия формата ЭТОГО конкретного сейва. 0 — сейв не наш.
        ///
        /// ⚠️ У поля СОЗНАТЕЛЬНО НЕТ инициализатора, и трогать это нельзя.
        /// <c>JsonUtility.FromJson</c> применяет инициализаторы полей к тому, чего нет
        /// в json: с <c>= CURRENT_VERSION</c> любой чужой объект под нашим ключом
        /// (<c>{"foo":1}</c>) читался бы как нормальный профиль текущей версии, и в лог
        /// уходило бы «прогресс загружен» вместо «это не наш сейв». Проверено — именно
        /// так себя и вело. Версию проставляют явно: <see cref="CreateNew"/> при создании
        /// и <see cref="ProfileSerializer.ToJson"/> при записи.
        /// </summary>
        public int Version;

        /// <summary>
        /// ⚠️ ЛЕГАСИ v1, ЧИТАЕТСЯ ТОЛЬКО МИГРАЦИЕЙ. Не трогать из игрового кода.
        ///
        /// В v1 здесь лежал «индекс уровня, на котором игрок стоит сейчас», и он же был
        /// счётчиком пройденного. С метой (docs/10 §13.4) узлы стали перепроходимыми,
        /// и «текущего уровня» больше не существует — есть только «сколько узлов пройдено»
        /// (<see cref="ClearedNodes"/>).
        ///
        /// Поле осталось в классе ИМЕННО ПОТОМУ, что <c>JsonUtility</c> не умеет
        /// переименований: без него значение из сейва v1 некуда прочитать, и прогресс
        /// всех, кто уже играл, пропал бы молча. <see cref="ProfileMigration"/> переливает
        /// его в <see cref="ClearedNodes"/> и обнуляет. Цена — <c>"LevelIndex":0</c>
        /// в каждом новом сейве; удалить поле можно будет тогда, когда сейвов v1
        /// заведомо не останется.
        /// </summary>
        public int LevelIndex;

        /// <summary>
        /// СКОЛЬКО УЗЛОВ КАРТЫ ПРОЙДЕНО (docs/10 §13.4). Оно же «какие узлы открыты»:
        /// карта линейна, пройденное — всегда префикс, поэтому одного числа достаточно
        /// и массив «пройден ли узел N» был бы дороже без единого нового ответа.
        ///
        /// Открыт узел с индексом <c>ClearedNodes</c> (первый непройденный) и все, что
        /// до него. Узлы ПЕРЕПРОХОДИМЫ — это и есть то, чем docs/10 §13.4 закрывает
        /// тупик <c>HomeController._configIndex</c>, который никогда не сбрасывался.
        ///
        /// Индекс, а не имя ассета, — потому что <c>BattleConfig[]</c> в <c>GameManager</c>
        /// это упорядоченный список, и порядок в нём и есть карта. Цена честная и записана
        /// здесь: ПЕРЕСТАНОВКА уровней в инспекторе сдвигает прогресс всем игрокам сразу.
        /// </summary>
        public int ClearedNodes;

        /// <summary>
        /// Выбранный герой — по <c>Book.HeroId</c>, а не по индексу в списке окна.
        /// Индекс сломался бы от любой перестановки героев на сцене, а «герой пропал»
        /// в вебе неотличимо от «сейв слетел». Пустая строка = игрок ещё не выбирал,
        /// берётся герой по умолчанию из <c>GameManager._startBook</c>.
        /// </summary>
        public string HeroId = string.Empty;

        /// <summary>
        /// Мягкая валюта — «клубки» (docs/10 §15). Тратится на рецепты и слоты колоды
        /// (§13.3). Героев за неё не покупают никогда: карта даёт идентичность,
        /// валюта — глубину внутри героя.
        /// </summary>
        public int Coins;

        /// <summary>
        /// СЛОТОВ КОЛОДЫ. Общие для всех героев (docs/10 §15.1). Растут покупками
        /// от <see cref="BASE_DECK_SLOTS"/> до <see cref="MAX_DECK_SLOTS"/>.
        /// </summary>
        public int DeckSlots;

        /// <summary>
        /// ОТКРЫТЫЕ ГЕРОИ — <c>Book.HeroId</c> тех, кого выдал прогресс (docs/10 §13.4:
        /// боссы узлов 5 и 10). Герои со <c>Book.UnlockChapter == 0</c> сюда не пишутся:
        /// они открыты всегда, и дублировать это в сейве значило бы завести второй
        /// источник истины.
        /// </summary>
        public string[] UnlockedHeroes;

        /// <summary>
        /// ПРОГРЕСС ПО ГЕРОЯМ: у кого что куплено и что экипировано.
        ///
        /// Массив, а не словарь, — ограничение <c>JsonUtility</c> (см. <see cref="ProfileSerializer"/>).
        /// Героев трое, поиск линейный и случается на переключении экрана.
        /// </summary>
        public HeroProgress[] Heroes;

        /// <summary>
        /// Язык — ЗЕРКАЛО, а не источник истины. Источник — <c>PlayerChoiceLanguageSource</c>
        /// в <c>PlayerPrefs</c>, и переезжать ему сюда пока НЕЛЬЗЯ, вот почему:
        ///
        /// язык обязан быть известен СИНХРОННО в <c>Awake</c> компонентов сцены — <c>LocalizedText</c>
        /// ставит строку до первого кадра. Сейв читается асинхронно (облако отвечает по сети).
        /// Забрать язык в профиль означало бы либо показать первый кадр не на том языке,
        /// либо задержать весь UI до ответа сети — ради настройки, которую сегодня даже
        /// негде переключить: экрана настроек нет, <c>PlayerChoiceLanguageSource.Save</c>
        /// не зовёт никто.
        ///
        /// Что поле всё-таки делает: едет в облако вместе с остальным профилем, поэтому
        /// когда экран настроек появится, выбор языка переедет между устройствами сам.
        /// Тогда же <c>PlayerChoiceLanguageSource</c> станет читать эту строку, а не префы, —
        /// это одна строка кода, и место для неё уже есть.
        /// </summary>
        public string LanguageCode = string.Empty;

        /// <summary>
        /// Свежий профиль. Он же — результат отката при битом сейве.
        /// ЕДИНСТВЕННЫЙ способ создать профиль в игровом коде: только здесь проставляется
        /// <see cref="Version"/>, у которого нет инициализатора (и почему — см. там же),
        /// и значения по умолчанию для полей меты.
        /// </summary>
        public static PlayerProfile CreateNew()
        {
            PlayerProfile profile = new PlayerProfile { Version = CURRENT_VERSION };
            profile.Normalize();
            return profile;
        }

        /// <summary>
        /// Чинит инварианты после чтения. Сейв — ВНЕШНИЕ данные: он лежит в браузере игрока,
        /// правится руками из консоли за десять секунд и переживает откат версии игры.
        /// Отрицательный уровень или отрицательный кошелёк не должны доходить до игровой логики.
        ///
        /// Здесь же выдаются значения по умолчанию полям меты — у них нет инициализаторов
        /// (см. комментарий к классу), поэтому <c>DeckSlots = 0</c> означает не «ноль слотов»,
        /// а «поля не было». Ноль слотов и так бессмыслен: колода перестала бы собираться.
        /// </summary>
        public void Normalize()
        {
            if (Version <= 0)
                Version = CURRENT_VERSION;
            if (LevelIndex < 0)
                LevelIndex = 0;
            if (ClearedNodes < 0)
                ClearedNodes = 0;
            if (Coins < 0)
                Coins = 0;

            if (DeckSlots < BASE_DECK_SLOTS)
                DeckSlots = BASE_DECK_SLOTS;
            if (DeckSlots > MAX_DECK_SLOTS)
                DeckSlots = MAX_DECK_SLOTS;

            HeroId ??= string.Empty;
            LanguageCode ??= string.Empty;
            UnlockedHeroes = CleanStrings(UnlockedHeroes);

            Heroes = CleanHeroes(Heroes);
            foreach (HeroProgress hero in Heroes)
                hero.Normalize();
        }

        /// <summary>
        /// Запись героя. <c>null</c> — героя ещё не касались; создавать её должен тот,
        /// кто первым что-то в неё пишет (см. <see cref="GetOrCreateHero"/>), иначе
        /// сейв распухал бы записями героев, которых игрок не открывал.
        /// </summary>
        public HeroProgress FindHero(string heroId)
        {
            if (string.IsNullOrEmpty(heroId) || Heroes == null)
                return null;

            foreach (HeroProgress hero in Heroes)
                if (hero != null && hero.HeroId == heroId)
                    return hero;

            return null;
        }

        public HeroProgress GetOrCreateHero(string heroId)
        {
            HeroProgress existing = FindHero(heroId);
            if (existing != null)
                return existing;

            HeroProgress hero = new HeroProgress { HeroId = heroId };
            hero.Normalize();

            List<HeroProgress> heroes = new List<HeroProgress>(Heroes ?? Array.Empty<HeroProgress>()) { hero };
            Heroes = heroes.ToArray();
            return hero;
        }

        public bool IsHeroUnlocked(string heroId)
        {
            if (string.IsNullOrEmpty(heroId) || UnlockedHeroes == null)
                return false;

            foreach (string unlocked in UnlockedHeroes)
                if (unlocked == heroId)
                    return true;

            return false;
        }

        /// <summary><c>true</c> — герой действительно открылся именно сейчас (есть что сохранять).</summary>
        public bool UnlockHero(string heroId)
        {
            if (string.IsNullOrEmpty(heroId) || IsHeroUnlocked(heroId))
                return false;

            List<string> unlocked = new List<string>(UnlockedHeroes ?? Array.Empty<string>()) { heroId };
            UnlockedHeroes = unlocked.ToArray();
            return true;
        }

        public override string ToString()
            => $"v{Version}, узлов пройдено {ClearedNodes}, герой «{HeroId}», клубков {Coins}, " +
               $"слотов {DeckSlots}, героев в записях {(Heroes == null ? 0 : Heroes.Length)}";

        internal static string[] CleanStrings(string[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<string>();

            List<string> clean = new List<string>(source.Length);
            foreach (string value in source)
                if (!string.IsNullOrEmpty(value) && !clean.Contains(value))
                    clean.Add(value);

            return clean.ToArray();
        }

        private static HeroProgress[] CleanHeroes(HeroProgress[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<HeroProgress>();

            List<HeroProgress> clean = new List<HeroProgress>(source.Length);
            foreach (HeroProgress hero in source)
            {
                if (hero == null || string.IsNullOrEmpty(hero.HeroId))
                    continue;
                if (clean.Exists(existing => existing.HeroId == hero.HeroId))
                    continue;
                clean.Add(hero);
            }
            return clean.ToArray();
        }
    }

    /// <summary>
    /// ПРОГРЕСС ОДНОГО ГЕРОЯ. Отдельный <c>[Serializable]</c>-класс, потому что
    /// <c>JsonUtility</c> не умеет словарей: «герой → его покупки» пишется массивом.
    ///
    /// Рецепты хранятся идентификаторами-цепочками (<c>Core.Spells.RecipeId</c>,
    /// формат <c>«3&gt;3&gt;4»</c>) — обоснование там же. Ссылок на ассеты в профиле
    /// не бывает по построению.
    /// </summary>
    [Serializable]
    public class HeroProgress
    {
        /// <summary>Кто это — <c>Book.HeroId</c>, тот же ключ, что и в <see cref="PlayerProfile.HeroId"/>.</summary>
        public string HeroId;

        /// <summary>
        /// КУПЛЕННЫЕ рецепты. Рецепты длины 1 сюда НЕ пишутся: они бесплатны по правилу
        /// (docs/10 §14.4 «открыто с начала — оба рецепта длины 1 своего героя»), и
        /// правило дешевле списка — оно само подхватит рецепт, который геймдизайнер
        /// добавит книге завтра.
        /// </summary>
        public string[] OwnedRecipes;

        /// <summary>
        /// ЭКИПИРОВАННАЯ КОЛОДА — то, из чего собирается <c>Table</c> партии и пул
        /// стихий колб (docs/10 §13.1). Порядок — тот, в котором игрок брал рецепты:
        /// он виден в окне колоды и не должен прыгать от пересохранения.
        ///
        /// Пустой массив имеет ОТДЕЛЬНЫЙ смысл, отличный от «нет записи»: запись есть,
        /// значит стартовую колоду уже выдавали, и выдавать её снова нельзя — иначе
        /// разобранная игроком колода собиралась бы обратно при каждом входе.
        /// </summary>
        public string[] EquippedRecipes;

        public void Normalize()
        {
            HeroId ??= string.Empty;
            OwnedRecipes = PlayerProfile.CleanStrings(OwnedRecipes);
            EquippedRecipes = PlayerProfile.CleanStrings(EquippedRecipes);
        }
    }
}
