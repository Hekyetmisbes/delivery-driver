using UnityEngine;
using System.Collections.Generic;

namespace DeliveryDriver.City
{
    /// <summary>
    /// Generates neighborhood names for the city grid.
    /// </summary>
    public static class NeighborhoodNameGenerator
    {
        private static readonly string[] TurkishNeighborhoodNames = new string[]
        {
            // Istanbul neighborhoods
            "Kadıköy", "Beşiktaş", "Beyoğlu", "Şişli", "Üsküdar",
            "Fatih", "Bakırköy", "Kartal", "Maltepe", "Ataşehir",
            "Pendik", "Sarıyer", "Eyüp", "Gaziosmanpaşa", "Esenler",
            "Bahçelievler", "Güngören", "Kağıthane", "Sultangazi", "Arnavutköy",

            // Ankara neighborhoods
            "Çankaya", "Kızılay", "Ulus", "Keçiören", "Mamak",
            "Yenimahalle", "Etimesgut", "Sincan", "Pursaklar", "Altındağ",

            // Izmir neighborhoods
            "Karşıyaka", "Bornova", "Konak", "Alsancak", "Buca",
            "Balçova", "Narlıdere", "Bayraklı", "Çiğli", "Gaziemir",

            // Other cities
            "Kaleiçi", "Lara", "Konyaaltı", "Kepez", "Muratpaşa",
            "Nilüfer", "Osmangazi", "Yıldırım", "Gemlik", "Mudanya",

            // Generic Turkish neighborhood names
            "Yenimahalle", "Cumhuriyet", "İstiklal", "Zafer", "Hürriyet",
            "Yıldız", "Merkez", "Güzelyalı", "Sahil", "Çamlık",
            "Çınarlı", "Bahçe", "Gül", "Yeni", "Eski",
            "Atatürk", "Gazi", "İnönü", "Fevzi Çakmak", "Ticaret",
            "Yeşil", "Mavi", "Altın", "Gümüş", "Beyaz",
            "Köşk", "Saray", "Kale", "Hisar", "Kule"
        };

        private static readonly List<string> usedNames = new List<string>();

        public static string GetRandomName(bool allowDuplicates = false)
        {
            if (!allowDuplicates && usedNames.Count >= TurkishNeighborhoodNames.Length)
            {
                usedNames.Clear();
            }

            List<string> availableNames = new List<string>();
            foreach (var name in TurkishNeighborhoodNames)
            {
                if (allowDuplicates || !usedNames.Contains(name))
                {
                    availableNames.Add(name);
                }
            }

            if (availableNames.Count == 0)
            {
                return "Mahalle " + Random.Range(1, 999);
            }

            string selectedName = availableNames[Random.Range(0, availableNames.Count)];

            if (!allowDuplicates)
            {
                usedNames.Add(selectedName);
            }

            return selectedName;
        }

        public static List<string> GetRandomNames(int count, bool allowDuplicates = false)
        {
            List<string> names = new List<string>();
            for (int i = 0; i < count; i++)
            {
                names.Add(GetRandomName(allowDuplicates));
            }
            return names;
        }

        public static void ResetUsedNames()
        {
            usedNames.Clear();
        }

        public static string[] GetAllNames()
        {
            return (string[])TurkishNeighborhoodNames.Clone();
        }
    }
}
