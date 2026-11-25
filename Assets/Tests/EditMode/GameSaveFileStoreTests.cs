using System;
using System.IO;
using DeliveryDriver.Quest;
using NUnit.Framework;
using UnityEngine;

namespace DeliveryDriver.Tests
{
    public class GameSaveFileStoreTests
    {
        private string directory;
        private string path;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "DeliveryDriverTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "savegame.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        [Test]
        public void FirstSave_RoundTripsWithoutTemporaryFile()
        {
            GameSaveFileStore.Write(path, CreateSave(125));
            Assert.That(GameSaveFileStore.TryRead(path, out var loaded), Is.True);
            Assert.That(loaded.PlayerData.Money, Is.EqualTo(125));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        [Test]
        public void SecondSave_KeepsPreviousVersionInBackup()
        {
            GameSaveFileStore.Write(path, CreateSave(125));
            GameSaveFileStore.Write(path, CreateSave(50));

            Assert.That(GameSaveFileStore.TryRead(path, out var current), Is.True);
            Assert.That(GameSaveFileStore.TryRead(path + ".bak", out var backup), Is.True);
            Assert.That(current.PlayerData.Money, Is.EqualTo(50));
            Assert.That(backup.PlayerData.Money, Is.EqualTo(125));
        }

        [Test]
        public void InvalidPrimary_DoesNotReplaceValidBackup()
        {
            GameSaveFileStore.Write(path, CreateSave(125));
            GameSaveFileStore.Write(path, CreateSave(50));
            File.WriteAllText(path, "{}");

            Assert.That(GameSaveFileStore.TryRead(path, out _), Is.False);
            Assert.That(GameSaveFileStore.TryRead(path + ".bak", out var recovery), Is.True);
            Assert.That(recovery.PlayerData.Money, Is.EqualTo(125));

            GameSaveFileStore.Write(path, CreateSave(75));
            Assert.That(GameSaveFileStore.TryRead(path + ".bak", out var backup), Is.True);
            Assert.That(backup.PlayerData.Money, Is.EqualTo(125));
            Assert.That(GameSaveFileStore.TryRead(path, out var current), Is.True);
            Assert.That(current.PlayerData.Money, Is.EqualTo(75));
        }

        [Test]
        public void InvalidSnapshot_LeavesExistingSaveUntouched()
        {
            GameSaveFileStore.Write(path, CreateSave(125));
            var invalid = CreateSave(50);
            invalid.PlayerData = null;
            Assert.Throws<InvalidDataException>(() => GameSaveFileStore.Write(path, invalid));

            Assert.That(GameSaveFileStore.TryRead(path, out var current), Is.True);
            Assert.That(current.PlayerData.Money, Is.EqualTo(125));
        }

        [Test]
        public void FutureVersion_IsNotOverwritten()
        {
            var future = CreateSave(125);
            future.SaveVersion = GameSaveData.CurrentVersion + 1;
            string original = JsonUtility.ToJson(future);
            File.WriteAllText(path, original);

            Assert.That(GameSaveFileStore.TryRead(path, out _), Is.False);
            Assert.Throws<InvalidDataException>(() => GameSaveFileStore.Write(path, CreateSave(50)));
            Assert.That(File.ReadAllText(path), Is.EqualTo(original));
        }

        [TestCase("{}")]
        [TestCase("")]
        public void InvalidSave_IsRejected(string json)
        {
            File.WriteAllText(path, json);
            Assert.That(GameSaveFileStore.TryRead(path, out _), Is.False);
        }

        private static GameSaveData CreateSave(int money)
        {
            var save = new GameSaveData();
            save.PlayerData.Money = money;
            return save;
        }
    }
}
