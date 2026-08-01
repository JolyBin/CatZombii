using DG.Tweening;
using System;
using System.Collections.Generic;
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

        /// <summary>Последние 25% срока — кольцо слота пульсирует (канал К3, движение, docs/12 §4.1).</summary>
        private const float ALARM_PART = 0.25f;

        [SerializeField] private UIFullFlask[] _flasks;
        [SerializeField] private Button _checkCombinationButton;
        [SerializeField] private TextMeshProUGUI _resultMergeText;

        [Header("Часы котла")]
        [Tooltip("Срок остывания одного элемента, секунды. Рабочее число — 8 (замеренный такт схлопывания 5 с). Крутится вживую.")]
        [SerializeField] private float _coolingSeconds = 8f;
        [Tooltip("Кольцо остывания на КАЖДОМ слоте, по индексу слота, параллельно _flasks (docs/12 §5.2). Image: Type = Filled, Fill Method = Radial 360, Origin = Top, Clockwise.")]
        [SerializeField] private Image[] _slotRings;

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

        private float[] _ringTargetFills;
        private bool[] _ringAlarms;
        private Vector3[] _ringBaseScales;
        private Sprite _previewDefaultIcon;
        private Vector2 _resultTextBasePosition;

        private void Awake()
        {
            EnsureRingState();

            if (_previewIcon != null)
                _previewDefaultIcon = _previewIcon.sprite;
            if (_resultMergeText != null)
                _resultTextBasePosition = _resultMergeText.rectTransform.anchoredPosition;
        }

        private void Update()
        {
            // Кольца ставятся ИЗ МОДЕЛИ, а не твином (docs/12 §5.5): иначе любое будущее
            // ускорение часов рассинхронит картинку и правду. Здесь только сглаживание.
            if (_slotRings == null)
                return;
            // массив колец — сериализованный, его могут переставить в инспекторе прямо
            // в Play Mode, как и срок остывания рядом
            EnsureRingState();

            for (int i = 0; i < _slotRings.Length; i++)
            {
                Image ring = _slotRings[i];
                if (ring == null)
                    continue;
                ring.fillAmount = Mathf.Lerp(ring.fillAmount, _ringTargetFills[i], Time.deltaTime * RING_LERP_SPEED);
            }
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
            StopAllAlarms();
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
        /// СВОЁ кольцо на каждом элементе котла (решение владельца после живой игры,
        /// 01.08.2026, docs/12 §5.2). Одно общее кольцо на ободе отвечало на вопрос
        /// «когда следующая потеря», но не на вопрос «слетит всё или один» — а спрашивают
        /// игроки именно второе.
        ///
        /// Список приходит В ПОРЯДКЕ ГИБЕЛИ: [0] уйдёт первым. Раскладываем его
        /// ОТ ХВОСТА К ГОЛОВЕ, потому что срабатывание любого срока снимает хвост
        /// (`TableController.DropTail`): k-й по очереди срок = k-е удаление = k-й слот
        /// с конца. Именно эта раздача делает слотовые кольца честными — кольцо гаснет
        /// ровно на том слоте, с которого элемент и улетит.
        /// </summary>
        public void SetCooling(IReadOnlyList<float> remainingSecondsByDeathOrder, float totalSeconds)
        {
            if (_slotRings == null)
                return;
            // Show() зовут из UIService раньше, чем отработает Awake этого окна, —
            // состояние колец обязано пережить такой порядок
            EnsureRingState();

            int count = remainingSecondsByDeathOrder == null
                ? 0
                : Mathf.Min(remainingSecondsByDeathOrder.Count, _curretnEmptyPositions);

            // Проход по СЛОТАМ, а не по срокам: слот без своего срока обязан быть погашен
            // тем же кадром, иначе кольцо ушедшего хвоста застынет на последнем значении.
            for (int slot = 0; slot < _slotRings.Length; slot++)
            {
                Image ring = _slotRings[slot];

                // место слота в порядке гибели: хвост уходит первым, голова последней
                int deathIndex = _curretnEmptyPositions - 1 - slot;
                bool hasDeadline = deathIndex >= 0 && deathIndex < count;

                if (!hasDeadline)
                {
                    _ringTargetFills[slot] = 0f;
                    SetAlarm(slot, false);
                    continue;
                }

                float normalized = totalSeconds <= 0f
                    ? 0f
                    : Mathf.Clamp01(remainingSecondsByDeathOrder[deathIndex] / totalSeconds);
                _ringTargetFills[slot] = normalized;

                // Свежий слот: кольцо обязано родиться полным, а не наползать снизу.
                // Иначе полсекунды после вставки самым пустым выглядит новичок —
                // то есть картинка врёт ровно про то, ради чего её и рисуют.
                if (ring != null && ring.fillAmount <= 0.001f)
                    ring.fillAmount = normalized;

                SetAlarm(slot, normalized <= ALARM_PART);
            }
        }

        /// <summary>Котёл пуст — часам нечего показывать, кольца гасим сразу и жёстко.</summary>
        public void ClearCooling()
        {
            SetCooling(null, _coolingSeconds);
            if (_slotRings == null)
                return;

            for (int i = 0; i < _slotRings.Length; i++)
                if (_slotRings[i] != null)
                    _slotRings[i].fillAmount = 0f;
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

        /// <summary>
        /// Последние 25% срока — кольцо ЭТОГО слота пульсирует (К3, движение, docs/12 §4.1).
        /// Тревога пер-кольцо, а не общая: когда пульсируют три кольца из четырёх, это
        /// и значит «сейчас слетит не один» — тот самый вопрос, на который общее кольцо
        /// не отвечало. Цвет колец при этом не меняется: цвет — не канал (docs/12 §9),
        /// сигнал несут длина дуги и движение.
        /// </summary>
        private void SetAlarm(int slot, bool isAlarm)
        {
            if (_slotRings == null || slot < 0 || slot >= _slotRings.Length)
                return;

            Image ring = _slotRings[slot];
            if (ring == null || _ringAlarms[slot] == isAlarm)
                return;
            _ringAlarms[slot] = isAlarm;

            ring.rectTransform.DOKill();
            ring.rectTransform.localScale = _ringBaseScales[slot];

            if (!isAlarm)
                return;

            ring.rectTransform
                .DOScale(_ringBaseScales[slot] * 1.12f, 0.25f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        private void StopAllAlarms()
        {
            if (_slotRings == null)
                return;
            EnsureRingState();

            for (int i = 0; i < _slotRings.Length; i++)
                SetAlarm(i, false);
        }

        /// <summary>
        /// Ленивая инициализация состояния колец — по одной ячейке на слот.
        /// Базовый масштаб запоминается до первой тревоги: пульсация возвращает
        /// кольцо именно в него, а не в <c>Vector3.one</c>.
        /// </summary>
        private void EnsureRingState()
        {
            int ringCount = _slotRings != null ? _slotRings.Length : 0;
            if (_ringTargetFills != null && _ringTargetFills.Length == ringCount)
                return;

            _ringTargetFills = new float[ringCount];
            _ringAlarms = new bool[ringCount];
            _ringBaseScales = new Vector3[ringCount];
            for (int i = 0; i < ringCount; i++)
                _ringBaseScales[i] = _slotRings[i] != null ? _slotRings[i].rectTransform.localScale : Vector3.one;
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
