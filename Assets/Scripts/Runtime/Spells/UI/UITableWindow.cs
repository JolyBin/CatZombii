using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility.Services.UI;

namespace Core.Spells.UI
{
    public class UITableWindow : UIWindow
    {
        public event Action OnClickCheckCombinationButton;

        /// <summary>
        /// Стол забит: следующий элемент показывать некуда.
        /// </summary>
        public bool IsFull => _curretnEmptyPositions >= _flasks.Length;

        /// <summary>
        /// ЧАСЫ КОТЛА: срок остывания одного элемента, секунды (docs/10 §1).
        /// Главная ручка ядра — крутится в инспекторе на объекте Table Window,
        /// без перекомпиляции и даже в Play Mode. Замеренный такт схлопывания — 5 с,
        /// рабочее значение срока — 8 с.
        /// </summary>
        public float CoolingSeconds => _coolingSeconds;

        private const float _animDuration = 0.5f;

        /// <summary>Скорость сглаживания кольца: модель тикает раз в 100 мс, вид догоняет плавно.</summary>
        private const float RING_LERP_SPEED = 10f;

        /// <summary>Последние 25% срока — кольцо пульсирует (канал К3, движение, docs/12 §4.1).</summary>
        private const float ALARM_PART = 0.25f;

        [SerializeField] private UIFullFlask[] _flasks;
        [SerializeField] private Button _checkCombinationButton;
        [SerializeField] private TextMeshProUGUI _resultMergeText;

        [Header("Часы котла")]
        [Tooltip("Срок остывания одного элемента, секунды. Рабочее число — 8 (замеренный такт схлопывания 5 с). Крутится вживую.")]
        [SerializeField] private float _coolingSeconds = 8f;
        [Tooltip("Одно кольцо на ободе котла (docs/12 §5.2). Image: Type = Filled, Fill Method = Radial 360.")]
        [SerializeField] private Image _coolingRing;
        [Tooltip("Секунды до ближайшей потери, один знак после запятой.")]
        [SerializeField] private TextMeshProUGUI _coolingText;
        [Tooltip("Маркер «уйдёт этот»: переезжает на хвостовой слот при каждой вставке.")]
        [SerializeField] private RectTransform _tailMarker;
        [Tooltip("Замеренный такт схлопывания колбы, секунды. Переводит время кольца в такты игрока.")]
        [SerializeField] private float _tactSeconds = 5f;
        [Tooltip("Держатели насечек: [0] — «один такт», [1] — «два такта». Угол ставит код из такта и срока.")]
        [SerializeField] private RectTransform[] _tactNotches;

        [Header("Предпросмотр варки")]
        [Tooltip("Иконка текущего заклинания. Пустая иконка у конфига — остаётся иконка по умолчанию.")]
        [SerializeField] private Image _previewIcon;
        [Tooltip("Число текущего выигрыша. Никогда не показывает будущий (docs/10 §0).")]
        [SerializeField] private TextMeshProUGUI _previewValueText;
        [SerializeField] private Color _previewReadyColor = new Color(1f, 0.93f, 0.62f, 1f);
        [SerializeField] private Color _previewEmptyColor = new Color(1f, 1f, 1f, 0.35f);

        [Header("Фидбек результата")]
        [SerializeField] private Color _successColor = new Color(0.42f, 0.86f, 0.35f, 1f);
        [SerializeField] private Color _failColor = new Color(0.90f, 0.32f, 0.28f, 1f);

        private Sequence _sequence;
        private int _curretnEmptyPositions;

        private float _ringTargetFill;
        private bool _isAlarm;
        private Vector3 _ringBaseScale = Vector3.one;
        private Sprite _previewDefaultIcon;
        private Vector2 _resultTextBasePosition;
        private float _placedNotchCooling = -1f;
        private float _placedNotchTact = -1f;

        private void Awake()
        {
            if (_coolingRing != null)
                _ringBaseScale = _coolingRing.rectTransform.localScale;
            if (_previewIcon != null)
                _previewDefaultIcon = _previewIcon.sprite;
            if (_resultMergeText != null)
                _resultTextBasePosition = _resultMergeText.rectTransform.anchoredPosition;
        }

        private void Update()
        {
            // Кольцо ставится ИЗ МОДЕЛИ, а не твином (docs/12 §5.5): иначе любое будущее
            // ускорение часов рассинхронит картинку и правду. Здесь только сглаживание.
            if (_coolingRing != null)
                _coolingRing.fillAmount = Mathf.Lerp(_coolingRing.fillAmount, _ringTargetFill, Time.deltaTime * RING_LERP_SPEED);

            UpdateTailMarker();
            PlaceTactNotches();
        }

        public override void Show()
        {
            _checkCombinationButton.onClick.AddListener(()  => OnClickCheckCombinationButton?.Invoke());
            ResetPositions();
            ClearCooling();
            SetPreview(null, 0, false);
            if (_resultMergeText != null)
                _resultMergeText.alpha = 0f;
            base.Show();
        }

        public override void Hide(Action onHide = null)
        {
            _checkCombinationButton.onClick.RemoveAllListeners();
            OnClickCheckCombinationButton = null;
            SetAlarm(false);
            base.Hide(onHide);
        }

        public void ClearFlasks()
        {
            int visibleCount = _curretnEmptyPositions;
            // Состояние обнуляем сразу: анимация не имеет права держать модель.
            // Иначе схлопывание во время полусекундной уборки уедет не в тот слот.
            _curretnEmptyPositions = 0;

            _sequence = DOTween.Sequence();
            for (int i = 0; i < visibleCount; i++)
            {
                int index = i;
                Transform flaskTransform = _flasks[index].transform;
                flaskTransform.DOKill();
                _sequence
                    .Join(flaskTransform.DOScale(0, _animDuration))
                    .Join(flaskTransform.DORotate(new Vector3(0, 0, 360), _animDuration, RotateMode.FastBeyond360));
                _sequence.InsertCallback(_animDuration, () => HideFlask(index));
            }
            _sequence.SetLink(gameObject);
        }

        public void ShowFullFlask(Sprite sprte)
        {
            if (IsFull)
                return;

            UIFullFlask newFlask = _flasks[_curretnEmptyPositions];
            _curretnEmptyPositions++;

            newFlask.transform.DOKill();
            newFlask.gameObject.SetActive(true);
            newFlask.SetSprite(sprte);

            newFlask.transform.localRotation = Quaternion.identity;
            newFlask.transform.localScale = Vector3.zero;

            _sequence = DOTween.Sequence();
            _sequence
                .Join(newFlask.transform.DOScale(1, _animDuration))
                .Join(newFlask.transform.DORotate(new Vector3(0, 0, 360), _animDuration, RotateMode.FastBeyond360));
            _sequence.SetLink(gameObject);
        }

        /// <summary>
        /// Срок истёк: из котла уходит ХВОСТ — последний положенный элемент (docs/10 §1).
        /// Голова цепочки живёт дольше, поэтому тянуть не бессмысленно.
        /// </summary>
        public void RemoveTailFlask()
        {
            if (_curretnEmptyPositions <= 0)
                return;

            _curretnEmptyPositions--;
            int index = _curretnEmptyPositions;
            Transform flaskTransform = _flasks[index].transform;
            flaskTransform.DOKill();

            Sequence dropSequence = DOTween.Sequence();
            dropSequence
                .Join(flaskTransform.DOScale(0, _animDuration * 0.6f))
                .Join(flaskTransform.DORotate(new Vector3(0, 0, -360), _animDuration * 0.6f, RotateMode.FastBeyond360))
                .OnComplete(() => HideFlask(index));
            dropSequence.SetLink(gameObject);
        }

        /// <summary>
        /// Остаток до ближайшей потери. Одно число на весь котёл — потому что каждая
        /// вставка запускает свой срок, а срабатывание любого снимает хвост (docs/12 §5.2).
        /// </summary>
        public void SetCooling(float remainingSeconds, float totalSeconds)
        {
            float normalized = totalSeconds <= 0f ? 0f : Mathf.Clamp01(remainingSeconds / totalSeconds);
            _ringTargetFill = normalized;

            if (_coolingText != null)
                _coolingText.text = Mathf.Max(0f, remainingSeconds).ToString("0.0");

            SetAlarm(normalized <= ALARM_PART);
        }

        /// <summary>Котёл пуст — часам нечего показывать.</summary>
        public void ClearCooling()
        {
            _ringTargetFill = 0f;
            if (_coolingRing != null)
                _coolingRing.fillAmount = 0f;
            if (_coolingText != null)
                _coolingText.text = string.Empty;
            SetAlarm(false);
        }

        /// <summary>
        /// Предпросмотр на кнопке варки: ИКОНКА + ЧИСЛО, не проза (docs/10 §4).
        /// Показывает только текущий выигрыш — никогда «а если доложишь ещё» (docs/10 §0).
        /// </summary>
        public void SetPreview(Sprite icon, int value, bool hasSpell)
        {
            if (_previewIcon != null)
            {
                _previewIcon.sprite = icon != null ? icon : _previewDefaultIcon;
                _previewIcon.color = hasSpell ? _previewReadyColor : _previewEmptyColor;
            }

            if (_previewValueText == null)
                return;

            _previewValueText.text = hasSpell ? value.ToString() : "—";
            _previewValueText.color = hasSpell ? _previewReadyColor : _previewEmptyColor;

            if (!hasSpell)
                return;

            // Число обязано «дёрнуться» в момент вставки, иначе игрок не свяжет
            // своё схлопывание с изменением награды (docs/12 §5.3).
            _previewValueText.rectTransform.DOKill(true);
            _previewValueText.rectTransform.localScale = Vector3.one;
            _previewValueText.rectTransform
                .DOPunchScale(Vector3.one * 0.25f, 0.15f, 6, 0.8f)
                .SetLink(gameObject);
        }

        /// <summary>
        /// Успех и провал обязаны выглядеть по-разному (баг №5). Провал — штатная и частая
        /// ситуация: закон префикса (docs/10 §6) гарантирует валидность префикса РЕЦЕПТА,
        /// но не произвольной цепочки, которую игрок набрал схлопываниями.
        /// </summary>
        public void ShowResult(bool result, string name)
        {
            if (_resultMergeText == null)
                return;

            _resultMergeText.DOKill();
            _resultMergeText.rectTransform.DOKill(true);
            _resultMergeText.rectTransform.anchoredPosition = _resultTextBasePosition;
            _resultMergeText.rectTransform.localScale = Vector3.one;

            _resultMergeText.text = name;
            _resultMergeText.color = result ? _successColor : _failColor;
            _resultMergeText.alpha = 1f;

            Sequence resultSequence = DOTween.Sequence();
            if (result)
                // успех: рывок вверх — награда «подпрыгнула»
                resultSequence.Append(_resultMergeText.rectTransform.DOPunchScale(Vector3.one * 0.4f, 0.35f, 7, 0.7f));
            else
                // провал: тряска вбок. Отличается и цветом, и кинематикой:
                // сигнал «только цветом» запрещён (docs/12 §4.4)
                resultSequence.Append(_resultMergeText.rectTransform.DOShakeAnchorPos(0.35f, new Vector2(30f, 0f), 18, 0f));

            resultSequence
                .AppendInterval(0.9f)
                .Append(_resultMergeText.DOFade(0f, 0.35f))
                .OnComplete(() =>
                {
                    _resultMergeText.rectTransform.anchoredPosition = _resultTextBasePosition;
                    _resultMergeText.rectTransform.localScale = Vector3.one;
                });
            resultSequence.SetLink(gameObject);
        }

        private void SetAlarm(bool isAlarm)
        {
            if (_coolingRing == null || _isAlarm == isAlarm)
                return;
            _isAlarm = isAlarm;

            _coolingRing.rectTransform.DOKill();
            _coolingRing.rectTransform.localScale = _ringBaseScale;

            if (!_isAlarm)
                return;

            _coolingRing.rectTransform
                .DOScale(_ringBaseScale * 1.08f, 0.25f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        /// <summary>
        /// Насечки такта (docs/12 §5.2): игрок не считает секунды, он смотрит, прошло ли
        /// кольцо насечку — «за ней доложить ещё один элемент я уже не успею».
        /// Обе ручки, срок и такт, крутятся вживую, поэтому угол пересчитывается сам.
        /// </summary>
        private void PlaceTactNotches()
        {
            if (_tactNotches == null || _tactNotches.Length == 0)
                return;
            if (Mathf.Approximately(_placedNotchCooling, _coolingSeconds) && Mathf.Approximately(_placedNotchTact, _tactSeconds))
                return;
            _placedNotchCooling = _coolingSeconds;
            _placedNotchTact = _tactSeconds;

            for (int i = 0; i < _tactNotches.Length; i++)
            {
                if (_tactNotches[i] == null)
                    continue;

                float part = _tactSeconds * (i + 1) / Mathf.Max(0.01f, _coolingSeconds);
                // столько тактов в срок не влезает — насечке на кольце места нет
                bool fits = part < 1f;
                if (_tactNotches[i].gameObject.activeSelf != fits)
                    _tactNotches[i].gameObject.SetActive(fits);
                if (!fits)
                    continue;

                // кольцо убывает от верха по часовой, значит по часовой отсчитывается и насечка
                _tactNotches[i].localRotation = Quaternion.Euler(0f, 0f, -part * 360f);
            }
        }

        private void UpdateTailMarker()
        {
            if (_tailMarker == null)
                return;

            bool hasTail = _curretnEmptyPositions > 0;
            if (_tailMarker.gameObject.activeSelf != hasTail)
                _tailMarker.gameObject.SetActive(hasTail);
            if (!hasTail)
                return;

            // Слоты раскладывает GridLayoutGroup, позиция известна только после ребилда,
            // поэтому маркер ведём по живой позиции хвостового слота, а не по индексу.
            Vector3 markerPosition = _tailMarker.position;
            markerPosition.x = _flasks[_curretnEmptyPositions - 1].transform.position.x;
            _tailMarker.position = markerPosition;
        }

        private void HideFlask(int index)
        {
            // слот мог быть уже переиспользован новым элементом, пока шла анимация ухода
            if (index < _curretnEmptyPositions)
                return;

            UIFullFlask flask = _flasks[index];
            flask.gameObject.SetActive(false);
            flask.transform.localScale = Vector3.one;
            flask.transform.localRotation = Quaternion.identity;
        }

        private void ResetPositions()
        {
            foreach (UIFullFlask flask in _flasks)
            {
                flask.transform.DOKill();
                flask.transform.localScale = Vector3.one;
                flask.transform.localRotation = Quaternion.identity;
                flask.gameObject.SetActive(false);
            }
            _curretnEmptyPositions = 0;
        }
    }
}
