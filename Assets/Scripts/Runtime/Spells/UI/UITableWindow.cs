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
        /// ЧАСЫ КОТЛА: запас ХВОСТА в ТАКТАХ МИРА, то есть в действиях игрока
        /// (docs/10 §0.2, §1). Главная ручка ядра — крутится в инспекторе
        /// на объекте Table Window, без перекомпиляции и даже в Play Mode.
        /// Ноль выключает остывание целиком — удобно, чтобы снять чистый замер темпа.
        /// </summary>
        public int CoolingTacts => _coolingTacts;

        private const float _animDuration = 0.5f;

        /// <summary>Скорость сглаживания кольца: модель тикает раз в ход, вид догоняет плавно.</summary>
        private const float RING_LERP_SPEED = 10f;

        /// <summary>Последние 25% срока — кольцо слота пульсирует (канал К3, движение, docs/12 §4.1).</summary>
        private const float ALARM_PART = 0.25f;

        /// <summary>
        /// Насколько приглушается кольцо элемента, который ЖДЁТ своей очереди тикать.
        /// Яркость здесь — не единственный и не главный канал: «ждёт» читается прежде
        /// всего полной дугой (у хвоста она короче и на глазах убывает) и неподвижностью
        /// (пульсирует только хвост). Приглушение — третий, избыточный канал, ради него
        /// одного цветом ничего не кодируется (docs/12 §9).
        /// </summary>
        private const float WAITING_ALPHA = 0.35f;

        [SerializeField] private UIFullFlask[] _flasks;
        [SerializeField] private Button _checkCombinationButton;
        [SerializeField] private TextMeshProUGUI _resultMergeText;

        [Header("Часы котла")]
        [Tooltip("Запас ХВОСТА в ТАКТАХ МИРА — в действиях игрока (перелив или варка), не в секундах. " +
                 "Тикает только хвостовой элемент; когда он уходит, следующий получает эти же N заново. " +
                 "КАЛИБРОВАТЬ ПО ЗАМЕРУ «переливов на схлопывание» (TactMeter), а не по секундам: " +
                 "давление не накопительное, поэтому при N заметно больше замера котёл не стынет никогда. " +
                 "0 выключает остывание целиком.")]
        [SerializeField] private int _coolingTacts = 8;
        [Tooltip("Кольцо остывания на КАЖДОМ слоте, по индексу слота, параллельно _flasks (docs/12 §5.2). Живое кольцо всегда одно — на последнем занятом слоте. Image: Type = Filled, Fill Method = Radial 360, Origin = Top, Clockwise.")]
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
        private bool[] _ringWaiting;
        private Vector3[] _ringBaseScales;
        private Color[] _ringBaseColors;
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
        /// Кольца котла по ПРАВИЛУ ХВОСТА (решение владельца 02.08.2026).
        ///
        /// Живое кольцо в котле ровно одно — на ПОСЛЕДНЕМ занятом слоте. Только у него
        /// идёт счётчик, только его дуга убывает и только оно пульсирует на последних
        /// тактах. Остальные занятые слоты стоят с ПОЛНЫМ приглушённым кольцом, и это
        /// не заглушка, а правда: их запас не тронут, и, став хвостом, элемент начнёт
        /// свои N с полного.
        ///
        /// Что этим чинится. И1 раздавала сроки по порядку гибели — ближайший на хвостовом
        /// слоте, дальний на головном. Формально честно, на глаз — ложь: свежий элемент
        /// получал самый старый срок, а старый выглядел перезапущенным. Теперь тикающий
        /// слот и улетающий слот — один и тот же, и объяснять раскладку больше нечего.
        ///
        /// Отсюда и подпись: одно число вместо списка сроков. Кто хвост, вид знает сам.
        /// </summary>
        /// <param name="tailRemainingTacts">Сколько тактов осталось хвосту. 0 — никто не тикает.</param>
        /// <param name="totalTacts">Полный запас, из него берётся доля кольца. 0 — остывание выключено.</param>
        public void SetCooling(int tailRemainingTacts, int totalTacts)
        {
            if (_slotRings == null)
                return;
            // Show() зовут из UIService раньше, чем отработает Awake этого окна, —
            // состояние колец обязано пережить такой порядок
            EnsureRingState();

            // остывание выключено ручкой: колец не должно быть вовсе. Приглушённое
            // «полное» кольцо утверждало бы, что срок есть, — а его нет
            bool isCoolingOn = totalTacts > 0;
            int tailSlot = _curretnEmptyPositions - 1;

            // Проход по СЛОТАМ, а не по занятым: освободившийся слот обязан быть погашен
            // тем же кадром, иначе кольцо ушедшего хвоста застынет на последнем значении.
            for (int slot = 0; slot < _slotRings.Length; slot++)
            {
                Image ring = _slotRings[slot];
                bool isOccupied = slot < _curretnEmptyPositions;

                if (!isOccupied || !isCoolingOn)
                {
                    _ringTargetFills[slot] = 0f;
                    SetAlarm(slot, false);
                    SetWaiting(slot, false);
                    continue;
                }

                if (slot != tailSlot)
                {
                    // ЖДЁТ: запас цел и не расходуется — кольцо полное и приглушённое
                    _ringTargetFills[slot] = 1f;
                    SetAlarm(slot, false);
                    SetWaiting(slot, true);
                    continue;
                }

                // ТИКАЕТ: деление целых тактов — обязательно во float, иначе кольцо
                // схлопнется в ступеньку «1 или 0» и перестанет что-либо показывать
                float normalized = Mathf.Clamp01((float)tailRemainingTacts / totalTacts);
                _ringTargetFills[slot] = normalized;

                // Свежий слот: кольцо обязано родиться полным, а не наползать снизу.
                // Иначе полсекунды после вставки самым пустым выглядит новичок —
                // то есть картинка врёт ровно про то, ради чего её и рисуют.
                if (ring != null && ring.fillAmount <= 0.001f)
                    ring.fillAmount = normalized;

                SetWaiting(slot, false);
                SetAlarm(slot, normalized <= ALARM_PART);
            }
        }

        /// <summary>Котёл пуст — часам нечего показывать, кольца гасим сразу и жёстко.</summary>
        public void ClearCooling()
        {
            SetCooling(0, _coolingTacts);
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
        /// «ЖДЁТ» против «ТИКАЕТ»: приглушение кольца у элемента, чей счётчик стоит.
        /// Нужно потому, что по правилу хвоста полная дуга сама по себе двусмысленна —
        /// у хвоста она тоже полная в первый такт. Приглушение снимает эту двусмысленность
        /// мгновенно: яркое кольцо в котле ровно одно, и оно же единственное в опасности.
        ///
        /// Меняется только альфа авторского цвета, а не оттенок: перекрашивать кольцо
        /// значило бы завести цветовой код там, где сигнал уже несут дуга и движение.
        /// </summary>
        private void SetWaiting(int slot, bool isWaiting)
        {
            if (_slotRings == null || slot < 0 || slot >= _slotRings.Length)
                return;

            Image ring = _slotRings[slot];
            if (ring == null || _ringWaiting[slot] == isWaiting)
                return;
            _ringWaiting[slot] = isWaiting;

            Color baseColor = _ringBaseColors[slot];
            ring.color = isWaiting
                ? new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * WAITING_ALPHA)
                : baseColor;
        }

        /// <summary>
        /// Последние 25% срока — кольцо ЭТОГО слота пульсирует (К3, движение, docs/12 §4.1).
        /// По правилу хвоста пульсировать может только хвостовое кольцо: остальные не тикают,
        /// и тревога на них была бы прямой ложью. Цвет при этом не меняется: цвет — не канал
        /// (docs/12 §9), сигнал несут длина дуги и движение.
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

        /// <summary>Окно уходит: гасим и пульсацию, и приглушение, чтобы кольца вернулись авторскими.</summary>
        private void StopAllAlarms()
        {
            if (_slotRings == null)
                return;
            EnsureRingState();

            for (int i = 0; i < _slotRings.Length; i++)
            {
                SetAlarm(i, false);
                SetWaiting(i, false);
            }
        }

        /// <summary>
        /// Ленивая инициализация состояния колец — по одной ячейке на слот.
        /// Базовый масштаб и базовый цвет запоминаются до первой тревоги и первого
        /// приглушения: и пульсация, и «ждёт» возвращают кольцо именно в них,
        /// а не в <c>Vector3.one</c> и не в белый.
        /// </summary>
        private void EnsureRingState()
        {
            int ringCount = _slotRings != null ? _slotRings.Length : 0;
            if (_ringTargetFills != null && _ringTargetFills.Length == ringCount)
                return;

            _ringTargetFills = new float[ringCount];
            _ringAlarms = new bool[ringCount];
            _ringWaiting = new bool[ringCount];
            _ringBaseScales = new Vector3[ringCount];
            _ringBaseColors = new Color[ringCount];
            for (int i = 0; i < ringCount; i++)
            {
                _ringBaseScales[i] = _slotRings[i] != null ? _slotRings[i].rectTransform.localScale : Vector3.one;
                _ringBaseColors[i] = _slotRings[i] != null ? _slotRings[i].color : Color.white;
            }
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
