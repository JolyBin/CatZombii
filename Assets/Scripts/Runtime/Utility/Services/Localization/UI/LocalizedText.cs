using TMPro;
using UnityEngine;

namespace Utility.Services.Localization
{
    /// <summary>
    /// Откуда этот TMP берёт текст. Три значения, потому что «не ключ» бывает
    /// двух разных сортов, и путать их дорого: одно — норма, другое — долг.
    /// </summary>
    public enum LocalizedTextSource
    {
        /// <summary>Строка берётся из таблицы по <see cref="LocalizedText.Key"/>. Нормальный случай.</summary>
        Key = 0,

        /// <summary>
        /// Текст ставит КОД (имя героя, номер волны, результат варки). Компонент здесь
        /// нужен не ради текста, а ради объявления намерения: без него проверка сцены
        /// не отличит «осознанно из кода» от «забыли».
        /// </summary>
        Code = 1,

        /// <summary>
        /// ЗАГЛУШКА чужого UI-кита (Layer Lab), которую никто не ставит и которая уйдёт
        /// вместе с китом в фазе 0 (docs/09). Не переводится сознательно — и именно
        /// поэтому помечена, а не пропущена молча: проверка считает такие и не даёт
        /// им раствориться в сцене.
        /// </summary>
        Placeholder = 2,
    }

    /// <summary>
    /// Текст на сцене берётся ПО КЛЮЧУ, а не лежит литералом в TMP.
    ///
    /// ГДЕ ЛОВИТСЯ МОМЕНТ УСТАНОВКИ — главный вопрос этого компонента, и ответов
    /// нужно два, потому что окна в проекте гасятся <c>Canvas.enabled</c>, а не
    /// <c>SetActive</c> (docs/04, решение 4):
    ///
    /// 1. <see cref="OnEnable"/> — срабатывает ОДИН раз при загрузке сцены (все окна
    ///    обязаны быть активны в <c>Awake</c>, иначе <c>UIService</c> их не найдёт)
    ///    и потом только у объектов, которые действительно включают/выключают —
    ///    например у карточек героев и элементов, созданных пулом в рантайме.
    ///    При показе окна он НЕ сработает: <c>Canvas.enabled</c> не трогает активность.
    ///
    /// 2. <c>UIWindow.Show()</c> → <c>ApplyLocalization()</c> — вот он и закрывает
    ///    показ окна. Окно само проходит по своим <see cref="LocalizedText"/>
    ///    и просит каждый обновиться.
    ///
    /// Плюс подписка на смену языка: игрок вправе переключить язык, не перезагружая
    /// вкладку, и открытое в этот момент окно обязано перерисоваться.
    /// </summary>
    [AddComponentMenu("Localization/Localized Text")]
    [DisallowMultipleComponent]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Ключ строки из таблицы Resources/Localization/Localization Table. " +
                 "Соглашение: <экран>.<элемент>, например home.play.")]
        [SerializeField] private string _key;

        [Tooltip("Key — строка из таблицы. Code — текст ставит код (объявление намерения). " +
                 "Placeholder — заглушка чужого UI-кита, переводу не подлежит.")]
        [SerializeField] private LocalizedTextSource _source = LocalizedTextSource.Key;

        [SerializeField] private TMP_Text _text;

        public string Key => _key;
        public LocalizedTextSource Source => _source;
        public TMP_Text Text => _text;

        private void OnEnable()
        {
            Apply();
            Localization.OnLanguageChanged += Apply;
        }

        private void OnDisable() => Localization.OnLanguageChanged -= Apply;

        /// <summary>
        /// Поставить текст сейчас. Зовётся из <see cref="OnEnable"/>, из
        /// <c>UIWindow.Show()</c> и при смене языка.
        ///
        /// Режимы <see cref="LocalizedTextSource.Code"/> и
        /// <see cref="LocalizedTextSource.Placeholder"/> не трогают текст вовсе:
        /// затереть значение, которое только что поставил код, — это тот же дефект
        /// «пустой UI», только с другой стороны.
        /// </summary>
        [ContextMenu("Применить строку")]
        public void Apply()
        {
            if (_source != LocalizedTextSource.Key)
                return;

            if (_text == null)
            {
                Debug.LogError($"[Localization] LocalizedText на «{name}» без ссылки на TMP_Text — строка «{_key}» не покажется.", this);
                return;
            }

            _text.text = Localization.Get(_key);
        }

#if UNITY_EDITOR
        public void EditorSetup(TMP_Text text, string key, LocalizedTextSource source)
        {
            _text = text;
            _key = key;
            _source = source;
        }

        private void Reset() => _text = GetComponent<TMP_Text>();
#endif
    }
}
