using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using Iteration1.Services;

namespace Iteration1.Components
{
    /// <summary>
    /// Game controller that manages the target spawning lifecycle.
    /// Spawns the first target on Start and spawns new targets when the current one is destroyed.
    /// Stops spawning when the player has won.
    /// Also handles audio and visual feedback for hits and wins.
    /// </summary>
    public class TargetClicker : MonoBehaviour
    {
        [Inject] private IScoreService _scoreService;
        [Inject] private ITargetSpawnerService _spawnerService;

        [Header("Audio Feedback")]
        [SerializeField] private AudioClip _hitSound;
        [SerializeField] private AudioClip _winSound;

        [Header("Visual Feedback")]
        [SerializeField] private GameObject _hitEffectPrefab;

        private AudioSource _audioSource;

        private void Start()
        {
            if (_spawnerService == null)
            {
                Debug.LogError("[TargetClicker] ITargetSpawnerService not injected.");
                return;
            }

            // Setup audio source
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Subscribe to events
            _spawnerService.OnTargetDestroyed += OnTargetDestroyed;
            if (_scoreService != null)
            {
                _scoreService.OnWin += OnWin;
            }

            _spawnerService.SpawnTarget();
        }

        private void OnTargetDestroyed()
        {
            if (_scoreService == null)
            {
                Debug.LogError("[TargetClicker] IScoreService not injected.");
                return;
            }

            if (!_scoreService.HasWon)
            {
                _spawnerService.SpawnTarget();
            }
        }

        private void OnWin()
        {
            PlaySound(_winSound);
        }

        private void OnDestroy()
        {
            if (_spawnerService != null)
            {
                _spawnerService.OnTargetDestroyed -= OnTargetDestroyed;
            }
            if (_scoreService != null)
            {
                _scoreService.OnWin -= OnWin;
            }
        }

        private void Update()
        {
            // Use new Input System for mouse clicks
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                var mousePos = mouse.position.ReadValue();
                var ray = Camera.main.ScreenPointToRay(mousePos);

                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    // If we hit a target, trigger the click
                    var target = hit.collider.GetComponent<Target>();
                    if (target != null)
                    {
                        // Play hit sound
                        PlaySound(_hitSound);

                        // Spawn particle effect at hit position
                        SpawnHitEffect(hit.point);

                        _scoreService.AddScore(1);
                        _spawnerService.DestroyCurrentTarget();
                    }
                }
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        private void SpawnHitEffect(Vector3 position)
        {
            if (_hitEffectPrefab != null)
            {
                var effect = Instantiate(_hitEffectPrefab, position, Quaternion.identity);
                // Auto-destroy particle effect after 2 seconds
                Destroy(effect, 2f);
            }
        }
    }
}
