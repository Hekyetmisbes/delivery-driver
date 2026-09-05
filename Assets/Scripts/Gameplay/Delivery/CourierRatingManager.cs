using System;
using System.Collections.Generic;
using DeliveryDriver.Quest;
using DeliveryDriver.UI;
using DeliveryDriver.UI.Smartphone;
using UnityEngine;

/// <summary>
/// Kurye Puanı (1.0 - 5.0 Yıldız) ve Dinamik Müşteri Yorumları Yöneticisi.
/// Teslimat performansına (kusursuzluk, hasarsızlık, gecikme, kaza) göre kurye puanını günceller,
/// positive_reviews.json ve negative_reviews.json veritabanından dinamik yorumlar üretir
/// ve kargo tekliflerinin kazanç katsayısını belirler.
/// </summary>
public class CourierRatingManager : MonoBehaviour
{
    private const string RatingPrefsKey = "DeliveryDriver.CourierRating";
    private const string RatingCountPrefsKey = "DeliveryDriver.CourierRatingCount";
    private const string ReviewHistoryPrefsKey = "DeliveryDriver.CourierReviewHistory";

    public static CourierRatingManager Instance { get; private set; }

    [Serializable]
    public class LocalizedReviewEntry
    {
        public string id;
        public float rating;
        public string customerName_tr;
        public string customerName_en;
        public string comment_tr;
        public string comment_en;

        public string GetCustomerName() =>
            LocalizationTable.CurrentLocale == LocalizationTable.EnglishLocale ? customerName_en : customerName_tr;

        public string GetComment() =>
            LocalizationTable.CurrentLocale == LocalizationTable.EnglishLocale ? comment_en : comment_tr;
    }

    [Serializable]
    public class ReviewDatabaseFile
    {
        public LocalizedReviewEntry[] reviews;
    }

    [Serializable]
    public class ActiveReviewItem
    {
        public string id;
        public string customerName;
        public float rating;
        public string comment;
        public string timeString;
        public bool isPositive;
    }

    [Serializable]
    private class SavedReviewHistory
    {
        public List<ActiveReviewItem> items = new List<ActiveReviewItem>();
    }

    [Header("Courier Stats")]
    [SerializeField] private float currentRating = 5.0f;
    [SerializeField] private int totalRatedDeliveries = 0;

    private readonly List<ActiveReviewItem> recentReviews = new List<ActiveReviewItem>();
    private ReviewDatabaseFile positiveReviewsDb;
    private ReviewDatabaseFile negativeReviewsDb;
    private readonly HashSet<string> usedReviewIds = new HashSet<string>();

    public float CurrentRating => currentRating;
    public int TotalRatedDeliveries => totalRatedDeliveries;
    public IReadOnlyList<ActiveReviewItem> RecentReviews => recentReviews;

    public event Action<float> OnRatingChanged;
    public event Action<ActiveReviewItem> OnNewReviewAdded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static CourierRatingManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        Instance = FindFirstObjectByType<CourierRatingManager>();
        if (Instance != null) return Instance;

        GameObject go = new GameObject("[CourierRatingManager]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<CourierRatingManager>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadDatabases();
        LoadData();
    }

    private void Start()
    {
        HookQuestManagerEvents();
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted.RemoveListener(HandleQuestCompleted);
            QuestManager.Instance.OnQuestFailed.RemoveListener(HandleQuestFailed);
        }
    }

    public void HookQuestManagerEvents()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestCompleted.RemoveListener(HandleQuestCompleted);
            QuestManager.Instance.OnQuestFailed.RemoveListener(HandleQuestFailed);

            QuestManager.Instance.OnQuestCompleted.AddListener(HandleQuestCompleted);
            QuestManager.Instance.OnQuestFailed.AddListener(HandleQuestFailed);
        }
    }

    private void HandleQuestCompleted(QuestData quest)
    {
        RecordDeliveryOutcome(quest, true);
    }

    private void HandleQuestFailed(QuestData quest)
    {
        RecordDeliveryOutcome(quest, false);
    }

    /// <summary>
    /// Kurye puanına göre kargo kazanç çarpanı (0.50x - 1.35x).
    /// 5.0 Yıldız -> 1.35x (VIP & Primli Kargolar)
    /// 4.0 Yıldız -> 1.00x (Standart kargo ücreti)
    /// 3.0 Yıldız -> 0.75x
    /// 2.0 Yıldız -> 0.60x
    /// 1.0 Yıldız -> 0.50x (Düşük bütçeli kargolar)
    /// </summary>
    public float GetRatingRewardMultiplier()
    {
        if (currentRating >= 4.8f) return 1.35f;
        if (currentRating >= 4.4f) return 1.20f;
        if (currentRating >= 3.8f) return 1.00f;
        if (currentRating >= 3.0f) return 0.80f;
        if (currentRating >= 2.0f) return 0.65f;
        return 0.50f;
    }

    public string GetRatingTierTitle()
    {
        if (currentRating >= 4.8f) return "VIP / Mükemmel Kurye";
        if (currentRating >= 4.2f) return "Kıdemli Kurye";
        if (currentRating >= 3.5f) return "Standart Kurye";
        if (currentRating >= 2.5f) return "Gelişmekte Olan Kurye";
        return "Riskli Kurye (Düşük Bütçeli)";
    }

    public void RecordDeliveryOutcome(QuestData quest, bool succeeded)
    {
        if (quest == null) return;

        PerformanceRating rank = quest.Rating;
        float cargoHealth = (quest.Cargo != null && quest.Cargo.IsFragile) ? quest.Cargo.CargoHealth : 100f;

        // Pozitif teslimat kriteri: Görev tamamlandı ve kargo ağır hasar almadı
        bool isPositive = succeeded && (rank == PerformanceRating.S || rank == PerformanceRating.A || rank == PerformanceRating.B);
        if (succeeded && (rank == PerformanceRating.D || cargoHealth < 50f))
        {
            isPositive = false; // Kargo ezildi veya ağır hasar aldı
        }

        ActiveReviewItem review;
        if (isPositive)
        {
            // Puan artışı (+0.05 ile +0.15)
            float delta = rank == PerformanceRating.S ? 0.12f : (rank == PerformanceRating.A ? 0.08f : 0.04f);
            currentRating = Mathf.Clamp(currentRating + delta, 1.0f, 5.0f);
            review = CreatePositiveReview(rank);
        }
        else
        {
            // Puan düşüşü (-0.20 ile -0.35)
            float penalty = (!succeeded || rank == PerformanceRating.F) ? 0.35f : (rank == PerformanceRating.D ? 0.25f : 0.15f);
            currentRating = Mathf.Clamp(currentRating - penalty, 1.0f, 5.0f);
            review = CreateNegativeReview(rank);
        }

        totalRatedDeliveries++;
        recentReviews.Insert(0, review);
        while (recentReviews.Count > 15)
        {
            recentReviews.RemoveAt(recentReviews.Count - 1);
        }

        SaveData();
        OnRatingChanged?.Invoke(currentRating);
        OnNewReviewAdded?.Invoke(review);

        // Akıllı telefon bildirim baloncuğunu tetikle
        if (SmartphoneUI.Instance != null)
        {
            string toastTitle = isPositive ? "Yeni Müşteri Değerlendirmesi" : "Müşteri Şikayeti";
            string toastBody = $"{review.customerName}: [{review.rating:F1}/5.0] {review.comment}";
            SmartphoneUI.Instance.TriggerNotification(toastTitle, toastBody);
        }
    }

    private ActiveReviewItem CreatePositiveReview(PerformanceRating rank)
    {
        LocalizedReviewEntry entry = PickReviewEntry(positiveReviewsDb, true);
        float starRating = rank switch
        {
            PerformanceRating.S => 5.0f,
            PerformanceRating.A => 4.8f,
            _ => 4.4f
        };

        if (entry != null)
        {
            return new ActiveReviewItem
            {
                id = entry.id,
                customerName = entry.GetCustomerName(),
                rating = starRating,
                comment = entry.GetComment(),
                timeString = "Yeni",
                isPositive = true
            };
        }

        return new ActiveReviewItem
        {
            id = "default_pos",
            customerName = "Kızılay Müşterisi",
            rating = starRating,
            comment = "Teslimat tam vaktinde ve hasarsız ulaştı, teşekkürler!",
            timeString = "Yeni",
            isPositive = true
        };
    }

    private ActiveReviewItem CreateNegativeReview(PerformanceRating rank)
    {
        LocalizedReviewEntry entry = PickReviewEntry(negativeReviewsDb, false);
        float starRating = rank switch
        {
            PerformanceRating.F => 1.0f,
            PerformanceRating.D => 1.5f,
            _ => 2.0f
        };

        if (entry != null)
        {
            return new ActiveReviewItem
            {
                id = entry.id,
                customerName = entry.GetCustomerName(),
                rating = starRating,
                comment = entry.GetComment(),
                timeString = "Yeni",
                isPositive = false
            };
        }

        return new ActiveReviewItem
        {
            id = "default_neg",
            customerName = "Şehir Sakini",
            rating = starRating,
            comment = "Paket gecikti veya hasarlı teslim edildi. Memnun kalmadım.",
            timeString = "Yeni",
            isPositive = false
        };
    }

    private LocalizedReviewEntry PickReviewEntry(ReviewDatabaseFile db, bool isPositive)
    {
        if (db == null || db.reviews == null || db.reviews.Length == 0) return null;

        List<LocalizedReviewEntry> unused = new List<LocalizedReviewEntry>();
        for (int i = 0; i < db.reviews.Length; i++)
        {
            if (!usedReviewIds.Contains(db.reviews[i].id))
            {
                unused.Add(db.reviews[i]);
            }
        }

        if (unused.Count == 0)
        {
            usedReviewIds.Clear();
            unused.AddRange(db.reviews);
        }

        int index = UnityEngine.Random.Range(0, unused.Count);
        LocalizedReviewEntry selected = unused[index];
        usedReviewIds.Add(selected.id);
        return selected;
    }

    private void LoadDatabases()
    {
        TextAsset posAsset = Resources.Load<TextAsset>("Localization/Reviews/positive_reviews");
        if (posAsset != null && !string.IsNullOrEmpty(posAsset.text))
        {
            try
            {
                positiveReviewsDb = JsonUtility.FromJson<ReviewDatabaseFile>(posAsset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CourierRatingManager] Error parsing positive_reviews.json: {ex.Message}");
            }
        }

        TextAsset negAsset = Resources.Load<TextAsset>("Localization/Reviews/negative_reviews");
        if (negAsset != null && !string.IsNullOrEmpty(negAsset.text))
        {
            try
            {
                negativeReviewsDb = JsonUtility.FromJson<ReviewDatabaseFile>(negAsset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CourierRatingManager] Error parsing negative_reviews.json: {ex.Message}");
            }
        }
    }

    private void LoadData()
    {
        currentRating = PlayerPrefs.GetFloat(RatingPrefsKey, 5.0f);
        totalRatedDeliveries = PlayerPrefs.GetInt(RatingCountPrefsKey, 0);

        string jsonHistory = PlayerPrefs.GetString(ReviewHistoryPrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(jsonHistory))
        {
            try
            {
                SavedReviewHistory history = JsonUtility.FromJson<SavedReviewHistory>(jsonHistory);
                if (history != null && history.items != null && history.items.Count > 0)
                {
                    recentReviews.Clear();
                    recentReviews.AddRange(history.items);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CourierRatingManager] Failed to load review history: {ex.Message}");
            }
        }

        // Başlangıç varsayılan yorumları
        SeedInitialReviews();
    }

    private void SeedInitialReviews()
    {
        recentReviews.Clear();
        recentReviews.Add(new ActiveReviewItem
        {
            id = "seed_1",
            customerName = "Kızılay Medikal Deposu",
            rating = 5.0f,
            comment = "Acil medikal kargomuz tam vaktinde ve eksiksiz ulaştı. Çok profesyonel kurye!",
            timeString = "15 dk önce",
            isPositive = true
        });

        recentReviews.Add(new ActiveReviewItem
        {
            id = "seed_2",
            customerName = "Tunalı Pastanesi",
            rating = 5.0f,
            comment = "Hassas pasta siparişimiz tek bir çizik bile olmadan geldi. Elinize sağlık!",
            timeString = "42 dk önce",
            isPositive = true
        });

        recentReviews.Add(new ActiveReviewItem
        {
            id = "seed_3",
            customerName = "Batıkent Elektronik",
            rating = 4.8f,
            comment = "Hassas ekran panelleri çok özenli taşınmış, teslimat hızı muhteşemdi.",
            timeString = "Bugün 14:15",
            isPositive = true
        });

        recentReviews.Add(new ActiveReviewItem
        {
            id = "seed_4",
            customerName = "Lojistik Merkezi",
            rating = 5.0f,
            comment = "Hoş geldiniz! 5.0 başlangıç puanınızla VIP kargo ilanlarına erişebilirsiniz.",
            timeString = "Bugün 12:00",
            isPositive = true
        });
    }

    private void SaveData()
    {
        PlayerPrefs.SetFloat(RatingPrefsKey, currentRating);
        PlayerPrefs.SetInt(RatingCountPrefsKey, totalRatedDeliveries);

        SavedReviewHistory history = new SavedReviewHistory();
        history.items.AddRange(recentReviews);
        string json = JsonUtility.ToJson(history);
        PlayerPrefs.SetString(ReviewHistoryPrefsKey, json);
        PlayerPrefs.Save();
    }
}
