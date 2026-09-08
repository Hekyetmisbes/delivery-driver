using System;
using System.IO;
using DeliveryDriver.Cinematic;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace DeliveryDriver.Editor.Cinematic
{
    /// <summary>
    /// Unity Recorder motorunu yöneten ve CinematicCaptureHUD ile köprü kurarak
    /// doğrudan 60 FPS MP4 video çıktısı üreten Editor denetleyicisi.
    /// </summary>
    [InitializeOnLoad]
    public static class CinematicRecorderController
    {
        private static RecorderController recorderController;
        private static MovieRecorderSettings movieRecorder;
        private static string currentOutputFile;
        private static bool isRecording;

        public static bool IsRecording => isRecording;
        public static string LastSavedFilePath => currentOutputFile;

        static CinematicRecorderController()
        {
            CinematicCaptureHUD.OnStartRecordingRequested -= HandleStartRecording;
            CinematicCaptureHUD.OnStartRecordingRequested += HandleStartRecording;

            CinematicCaptureHUD.OnStopRecordingRequested -= HandleStopRecording;
            CinematicCaptureHUD.OnStopRecordingRequested += HandleStopRecording;

            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && isRecording)
            {
                HandleStopRecording();
            }
        }

        public static void HandleStartRecording()
        {
            if (isRecording) return;

            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[CinematicRecorder] Video kaydı yalnızca Oyun (Play Mode) çalışırken başlatılabilir.");
                CinematicCaptureHUD.IsRecordingActive = false;
                return;
            }

            CinematicVideoFormat format = CinematicVideoFormat.Trailer_16_9;
            if (CinematicCameraController.Instance != null)
            {
                format = CinematicCameraController.Instance.CurrentFormat;
            }

            int width = format == CinematicVideoFormat.Reels_9_16 ? 1080 : 1920;
            int height = format == CinematicVideoFormat.Reels_9_16 ? 1920 : 1080;
            string subFolder = format == CinematicVideoFormat.Reels_9_16 ? "Reels" : "Trailers";
            string prefix = format == CinematicVideoFormat.Reels_9_16 ? "Reels" : "Trailer";

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outputDirectory = Path.Combine(projectRoot, "Recordings", subFolder);

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileNameWithoutExtension = $"{prefix}_{timestamp}";
            currentOutputFile = Path.Combine(outputDirectory, fileNameWithoutExtension);

            // 1. Controller Settings
            var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRate = 60.0f;
            controllerSettings.CapFrameRate = true;

            // 2. Movie Recorder
            movieRecorder = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieRecorder.name = "Cinematic Movie Recorder";
            movieRecorder.Enabled = true;
            movieRecorder.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
            movieRecorder.OutputFile = currentOutputFile;

            var imageInput = new GameViewInputSettings
            {
                OutputWidth = width,
                OutputHeight = height
            };
            movieRecorder.ImageInputSettings = imageInput;
            movieRecorder.AudioInputSettings.PreserveAudio = true;

            controllerSettings.AddRecorderSettings(movieRecorder);

            // 3. Controller Başlat
            recorderController = new RecorderController(controllerSettings);
            recorderController.PrepareRecording();
            recorderController.StartRecording();

            isRecording = true;
            CinematicCaptureHUD.IsRecordingActive = true;

            Debug.Log($"<color=#33CCFF><b>[CinematicRecorder]</b></color> Kayıt Başlatıldı! Format: <b>{format} ({width}x{height})</b>, 60 FPS.");
        }

        public static void HandleStopRecording()
        {
            if (!isRecording) return;

            try
            {
                if (recorderController != null && recorderController.IsRecording())
                {
                    recorderController.StopRecording();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CinematicRecorder] Kayıt durdurulurken istisna oluştu: {ex.Message}");
            }
            finally
            {
                isRecording = false;
                CinematicCaptureHUD.IsRecordingActive = false;
            }

            string finalMp4Path = currentOutputFile + ".mp4";
            Debug.Log($"<color=#33FF66><b>[CinematicRecorder]</b></color> 🎬 Video başarıyla kaydedildi!\nDosya: <b>{finalMp4Path}</b>");
        }

        public static void OpenRecordingsFolder()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string folder = Path.Combine(projectRoot, "Recordings");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            EditorUtility.RevealInFinder(folder);
        }
    }
}
