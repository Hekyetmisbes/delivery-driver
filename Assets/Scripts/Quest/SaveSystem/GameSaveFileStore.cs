using System;
using System.IO;
using UnityEngine;

namespace DeliveryDriver.Quest
{
    /// <summary>File-level save protection; capture Unity state before writing.</summary>
    public static class GameSaveFileStore
    {
        public static bool TryRead(string path, out GameSaveData data)
        {
            data = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                var candidate = ReadCandidate(path);
                if (!IsValid(candidate))
                    return false;
                data = candidate;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Write(string path, GameSaveData data)
        {
            if (!IsValid(data))
                throw new InvalidDataException("Invalid game save snapshot.");

            string temporaryPath = path + ".tmp";
            string backupPath = path + ".bak";
            RejectFutureVersion(path);
            RejectFutureVersion(backupPath);

            try
            {
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
                if (!TryRead(temporaryPath, out _))
                    throw new InvalidDataException("Temporary save failed validation.");

                if (File.Exists(path))
                {
                    // Keep a valid backup if the primary file is corrupt.
                    string backup = TryRead(path, out _) ? backupPath : null;
                    File.Replace(temporaryPath, path, backup);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static GameSaveData ReadCandidate(string path)
        {
            var candidate = new GameSaveData
            {
                PlayerData = null,
                QuestData = null,
                SaveDate = null,
                SaveVersion = 0
            };
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), candidate);
            return candidate;
        }

        private static bool IsValid(GameSaveData data)
        {
            return data != null &&
                   data.SaveVersion > 0 &&
                   data.SaveVersion <= GameSaveData.CurrentVersion &&
                   data.PlayerData != null &&
                   data.QuestData != null &&
                   !string.IsNullOrWhiteSpace(data.SaveDate);
        }

        private static void RejectFutureVersion(string path)
        {
            if (!File.Exists(path))
                return;

            GameSaveData candidate;
            try
            {
                candidate = ReadCandidate(path);
            }
            catch (ArgumentException)
            {
                return; // Malformed JSON can be replaced without discarding a good backup.
            }

            if (candidate.SaveVersion > GameSaveData.CurrentVersion)
                throw new InvalidDataException("Save belongs to a newer game version.");
        }
    }
}
