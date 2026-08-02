using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Battle
{
    public class UIUnit : MonoBehaviour, IAction
    {
        public event Action OnSelectClickButton;

        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Image _fillHealthImage;
        [SerializeField] private TextMeshProUGUI _healthValueText;
        [SerializeField] private GameObject _timer;
        [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private Image _fillTimerImage;
        [SerializeField] private Image _selectedImage;
        [SerializeField] protected Button _selectedButton;

        /// <summary>
        /// ПОКАДРОВАЯ АНИМАЦИЯ. Живёт здесь, а не в отдельном компоненте, намеренно:
        /// бой целиком в uGUI, юнит — это <see cref="Image"/>, а не <c>SpriteRenderer</c>,
        /// поэтому вся анимация сводится к смене <see cref="Image.sprite"/> по кадрам,
        /// и заводить ради этого второй MonoBehaviour на том же объекте нечего.
        ///
        /// ⏱ ЭТО ЕДИНСТВЕННОЕ МЕСТО В БОЮ, ГДЕ ЕСТЬ НАСТЕННЫЕ ЧАСЫ, и это законно:
        /// анимация — картинка, а не игровое время (так же живут DOTween-твины
        /// в <c>UITableWindow</c>). Логика от неё не зависит ни в одной точке:
        /// урон наносит ТАКТ (<see cref="TargetController.Tick"/>), а не кадр;
        /// <see cref="PlayAttack"/> вызывается ПОСЛЕ того, как удар уже случился.
        /// Выключить анимацию — значит потерять картинку, а не сломать бой.
        /// </summary>
        [Header("Покадровая анимация (кадры — Sprite'ы, не Animator)")]
        [SerializeField] private Image _visualImage;
        [SerializeField] private Sprite[] _idleFrames;
        [SerializeField] private Sprite[] _attackFrames;
        [SerializeField] private Sprite[] _deathFrames;

        [Tooltip("Кадров в секунду для покоя / удара / смерти.")]
        [SerializeField] private float _idleFps = 12f;
        [SerializeField] private float _attackFps = 16f;
        [SerializeField] private float _deathFps = 14f;

        private Sprite[] _currentFrames;
        private float _secondsPerFrame;
        private float _timeInFrame;
        private int _frameIndex;
        private bool _isLooping;
        private bool _destroyWhenFinished;

        /// <summary>Есть ли чем проиграть смерть — иначе владелец убивает объект сразу.</summary>
        public bool HasDeathFrames => _visualImage != null && _deathFrames != null && _deathFrames.Length > 0;

        public void Init()
        {
            Selected(false);
            _selectedButton.onClick.AddListener(() => OnSelectClickButton?.Invoke());
        }

        /// <summary>
        /// Таймер удара в ТАКТАХ мира, а не в секундах (docs/10 §0.2). Игроку показывается
        /// целое «ходов до удара»: дробные секунды в пошаговой игре не означают ничего —
        /// между его действиями таймер вообще не двигается.
        /// </summary>
        public void SetTimer(int currentTacts, int maxTacts)
        {
            _timerText.text = currentTacts.ToString();
            _fillTimerImage.fillAmount = maxTacts <= 0 ? 0f : (float)(maxTacts - currentTacts) / maxTacts;
        }

        public void SetActiveTimer(bool value) => _timer.SetActive(value);

        public void SetHealth(int currentHealth, int maxHealth)
        {
            _fillHealthImage.fillAmount = (float)currentHealth / maxHealth;
            _healthValueText.text = currentHealth.ToString();
        }

        public void SetName(string name)
        {
            if (name == null || name == "")
            {
                _nameText.enabled = false;
                return;
            }
            _nameText.text = name;
            _nameText.enabled = true;
        }

        public void Selected(bool value) => _selectedImage.enabled = value;

        /// <summary>
        /// Покой. Фаза берётся случайной, чтобы шеренга одинаковых зомби не дышала
        /// в такт: одинаковая фаза читается как один объект, размноженный копипастой.
        /// </summary>
        public void PlayIdle()
        {
            if (!StartClip(_idleFrames, _idleFps, loop: true, destroyWhenFinished: false))
                return;
            // явный UnityEngine: в файле есть using System, и голое Random было бы CS0104
            _frameIndex = UnityEngine.Random.Range(0, _idleFrames.Length);
            _visualImage.sprite = _idleFrames[_frameIndex];
        }

        /// <summary>
        /// Замах и удар. ЧИСТО КАРТИНКА: урон уже нанесён тактом, до этого вызова.
        /// Смерть не перебивается — она последняя.
        /// </summary>
        public void PlayAttack()
        {
            if (_destroyWhenFinished)
                return;
            StartClip(_attackFrames, _attackFps, loop: false, destroyWhenFinished: false);
        }

        /// <summary>
        /// Смерть и самоуничтожение по последнему кадру. Зовётся вместо немедленного
        /// <c>Destroy</c> из <c>UnitRuntime.Dispose(true)</c>.
        ///
        /// Слот к этому моменту уже свободен (<c>UIUnitPosition.SetFree</c>), и в него
        /// может немедленно встать боец следующей волны — поэтому труп уходит
        /// первым ребёнком, то есть ПОД новичка, и перестаёт ловить клики.
        /// </summary>
        public void PlayDeath()
        {
            if (_nameText != null)
                _nameText.enabled = false;
            if (_selectedImage != null)
                _selectedImage.enabled = false;
            if (_timer != null)
                _timer.SetActive(false);
            if (_selectedButton != null)
                _selectedButton.interactable = false;
            if (_visualImage != null)
                _visualImage.raycastTarget = false;

            transform.SetAsFirstSibling();

            if (!StartClip(_deathFrames, _deathFps, loop: false, destroyWhenFinished: true))
                Destroy(gameObject);
        }

        private bool StartClip(Sprite[] frames, float fps, bool loop, bool destroyWhenFinished)
        {
            if (_visualImage == null || frames == null || frames.Length == 0)
                return false;

            _currentFrames = frames;
            _secondsPerFrame = fps <= 0f ? 0.1f : 1f / fps;
            _timeInFrame = 0f;
            _frameIndex = 0;
            _isLooping = loop;
            _destroyWhenFinished = destroyWhenFinished;
            _visualImage.sprite = frames[0];
            return true;
        }

        private void Awake()
        {
            // покой стартует сам: у героя на сцене компонента UnitRuntime нет вовсе,
            // а анимация ему нужна ровно так же, как врагам
            PlayIdle();
        }

        /// <summary>
        /// Смена кадра. <see cref="Time.deltaTime"/>, а не unscaled: на паузе
        /// (<c>Time.timeScale = 0</c>) картинка обязана замереть вместе с игрой.
        /// </summary>
        private void Update()
        {
            if (_currentFrames == null || _visualImage == null)
                return;

            _timeInFrame += Time.deltaTime;
            if (_timeInFrame < _secondsPerFrame)
                return;

            // при просадке кадров перескакиваем, а не догоняем по одному кадру за Update:
            // иначе анимация растянулась бы ровно там, где и так тяжело
            int advance = Mathf.Max(1, (int)(_timeInFrame / _secondsPerFrame));
            _timeInFrame -= advance * _secondsPerFrame;
            _frameIndex += advance;

            if (_frameIndex < _currentFrames.Length)
            {
                _visualImage.sprite = _currentFrames[_frameIndex];
                return;
            }

            if (_isLooping)
            {
                _frameIndex %= _currentFrames.Length;
                _visualImage.sprite = _currentFrames[_frameIndex];
                return;
            }

            _visualImage.sprite = _currentFrames[_currentFrames.Length - 1];
            _currentFrames = null;

            if (_destroyWhenFinished)
            {
                Destroy(gameObject);
                return;
            }

            PlayIdle();
        }

        public void ClearAction()
        {
            _selectedButton.onClick.RemoveAllListeners();
            OnSelectClickButton = null;
        }
    }
}
