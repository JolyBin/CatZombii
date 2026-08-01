namespace Utility.Services.Localization
{
    /// <summary>
    /// Ключи, которые запрашивает КОД. Только они — ключи, живущие в сцене
    /// (в <see cref="LocalizedText"/>) и в ассетах (имена заклинаний, юнитов, героев),
    /// сюда не дублируются: у них уже есть единственное место хранения, а второе
    /// разъедется с первым.
    ///
    /// Зачем константы, а не строки по месту: опечатка в ключе — это не ошибка
    /// компиляции, а маркер <c>#battle.wav#</c> в готовой игре. Здесь опечатка ловится
    /// компилятором, а «где используется этот ключ» — обычным «Find Usages».
    /// </summary>
    public static class LocKeys
    {
        /// <summary>Главный экран: «Уровень {0}».</summary>
        public const string HomeLevel = "home.level";

        /// <summary>Окно боя: «Волна: {0}/{1}».</summary>
        public const string BattleWave = "battle.wave";

        /// <summary>Котёл: варка не дала заклинания. Ветка штатная и частая (docs/10 §6).</summary>
        public const string TableBrewFailed = "table.brew_failed";
    }
}
