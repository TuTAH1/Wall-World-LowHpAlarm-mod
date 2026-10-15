using System;
using UnityEngine;

namespace LowHpAlarm
{
    /// <summary>
    /// Manages the alarm audio effect that plays when the player's HP is low.
    /// </summary>
    public class AudioEffect
    {
        private AudioSource _audioSource;
        private AudioClip _alarmClip;
        private bool _isPlaying;

        /// <summary>
        /// Gets whether the alarm is currently playing.
        /// </summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>
        /// Creates and initializes the audio source and loads the alarm clip.
        /// Call once when the effect is needed (e.g., on session start).
        /// </summary>
        public void Initialize()
        {
            if (_audioSource != null && _alarmClip != null) return;

            if (_audioSource == null)
                LowHpAlarmPlugin.Log("AudioEffect: audioSource is null, creating");
            if (_alarmClip == null)
                LowHpAlarmPlugin.Log("AudioEffect: alarmClip is null, creating");

            GameObject go = new GameObject("LowHpAlarmAudio");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _audioSource = go.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.volume = 1f;
            _audioSource.loop = true;

            LoadAlarmClip();
            LowHpAlarmPlugin.Log("Audio created");
        }

        /// <summary>
        /// Updates the audio playback state based on the player's current HP percentage.
        /// Should be called each frame (or on HP change) while in gameplay.
        /// </summary>
        /// <param name="hpPercent">Current HP as a percentage (0-100).</param>
        public void Update(float hpPercent)
        {
            // Determine if the alarm should be playing based on HP threshold
            bool shouldPlay = LowHpAlarmPlugin.AudioAlarmHp.Value > 0f && hpPercent > 0f && hpPercent <= LowHpAlarmPlugin.AudioAlarmHp.Value;

            if (shouldPlay && !_isPlaying)
            {
                _audioSource.clip = _alarmClip;
                _audioSource.loop = true;
                _audioSource.Play();
                _isPlaying = true;
                LowHpAlarmPlugin.Log($"UpdateAlarm: Alarm started playing (HP={hpPercent:F1}%)");
            }
            else if (!shouldPlay && _isPlaying)
            {
                _audioSource.Stop();
                _isPlaying = false;
                LowHpAlarmPlugin.Log($"UpdateAlarm: Alarm stopped (HP={hpPercent:F1}%)");
            }
        }

        /// <summary>
        /// Cleans up the audio source. Call when the effect is no longer needed (e.g., on session end).
        /// </summary>
        public void Cleanup()
        {
            if (_isPlaying)
            {
                _audioSource.Stop();
                _isPlaying = false;
            }

            // Note: We do not destroy the AudioSource GameObject here because it may be reused
            // if a new session starts quickly. Instead we rely on DontDestroyOnLoad and
            // re-initialize if needed. However, if you want to completely remove it, uncomment:
            // if (_audioSource != null) UnityEngine.Object.Destroy(_audioSource.gameObject);
            // _audioSource = null;
            // _alarmClip = null;
        }

        /// <summary>
        /// Loads the alarm WAV file from the plugin's resources and converts it to an AudioClip.
        /// </summary>
        private void LoadAlarmClip()
        {
            try
            {
                string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string wavPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(assemblyPath), "LowHpAlarmResources", "alarm.wav");
                LowHpAlarmPlugin.Log($"Looking for alarm.wav at: {wavPath}");
                LowHpAlarmPlugin.Log($"File exists: {System.IO.File.Exists(wavPath)}");

                if (System.IO.File.Exists(wavPath))
                {
                    byte[] wavData = System.IO.File.ReadAllBytes(wavPath);
                    _alarmClip = WavUtility.ToAudioClip(wavData, "alarm.wav");
                    if (_alarmClip != null)
                    {
                        LowHpAlarmPlugin.Log("Loaded alarm.wav from Resources");
                    }
                    else
                    {
                        LowHpAlarmPlugin.pluginLogger.LogError("Failed to convert alarm.wav to AudioClip");
                    }
                }
                else
                {
                    LowHpAlarmPlugin.pluginLogger.LogError($"Failed to load alarm.wav from {wavPath}");
                }
            }
            catch (System.Exception ex)
            {
                LowHpAlarmPlugin.pluginLogger.LogError($"Failed to load alarm.wav: {ex.Message}");
            }
        }
    }
}