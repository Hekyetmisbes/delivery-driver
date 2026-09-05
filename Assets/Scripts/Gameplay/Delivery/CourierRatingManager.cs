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
    private const string BatchCounterPrefsKey = "DeliveryDriver.CourierBatchCounter";
    private const string BatchPosPrefsKey = "DeliveryDriver.CourierBatchPos";
    private const string BatchNegPrefsKey = "DeliveryDriver.CourierBatchNeg";
    public const int DeliveriesPerReview = 3;

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
        public string customerName_tr;
        public string customerName_en;
        public float rating;
        public string comment;
        public string comment_tr;
        public string comment_en;
        public string timeString;
        public string timeString_tr;
        public string timeString_en;
        public bool isPositive;

        public string GetCustomerName()
        {
            bool isEn = LocalizationTable.CurrentLocale == LocalizationTable.EnglishLocale;
            if (isEn && !string.IsNullOrEmpty(customerName_en)) return customerName_en;
            if (!string.IsNullOrEmpty(customerName_tr)) return customerName_tr;
            return customerName ?? (isEn ? "Customer" : "Müşteri");
        }

        public string GetComment()
        {
            bool isEn = LocalizationTable.CurrentLocale == LocalizationTable.EnglishLocale;
            if (isEn && !string.IsNullOrEmpty(comment_en)) return comment_en;
            if (!string.IsNullOrEmpty(comment_tr)) return comment_tr;
            return comment ?? string.Empty;
        }

        public string GetTimeString()
        {
            bool isEn = LocalizationTable.CurrentLocale == LocalizationTable.EnglishLocale;
            if (isEn && !string.IsNullOrEmpty(timeString_en)) return timeString_en;
            if (!string.IsNullOrEmpty(timeString_tr)) return timeString_tr;
            return timeString ?? (isEn ? "Just now" : "Yeni");
        }
    }

    [Serializable]
    private class SavedReviewHistory
    {
        public List<ActiveReviewItem> items = new List<ActiveReviewItem>();
    }

    [Header("Courier Stats")]
    [SerializeField] private float currentRating = 5.0f;
    [SerializeField] private int totalRatedDeliveries = 0;
    [SerializeField] private int deliveryBatchCounter = 0;
    [SerializeField] private int batchPositiveCount = 0;
    [SerializeField] private int batchNegativeCount = 0;

    private readonly List<ActiveReviewItem> recentReviews = new List<ActiveReviewItem>();
    private ReviewDatabaseFile positiveReviewsDb;
    private ReviewDatabaseFile negativeReviewsDb;
    private readonly HashSet<string> usedReviewIds = new HashSet<string>();

    public float CurrentRating => currentRating;
    public int TotalRatedDeliveries => totalRatedDeliveries;
    public int DeliveriesCompletedInBatch => deliveryBatchCounter;
    public int DeliveriesUntilNextReview => Mathf.Max(0, DeliveriesPerReview - deliveryBatchCounter);
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
        if (currentRating >= 4.8f) return LocalizationTable.Get("rating_tier_vip");
        if (currentRating >= 4.2f) return LocalizationTable.Get("rating_tier_senior");
        if (currentRating >= 3.5f) return LocalizationTable.Get("rating_tier_standard");
        if (currentRating >= 2.5f) return LocalizationTable.Get("rating_tier_developing");
        return LocalizationTable.Get("rating_tier_risk");
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

        totalRatedDeliveries++;
        deliveryBatchCounter++;
        if (isPositive) batchPositiveCount++;
        else batchNegativeCount++;

        // Her 3 teslimatta bir değerlendirme puanı güncellenir ve yeni müşteri yorumu gelir
        if (deliveryBatchCounter >= DeliveriesPerReview)
        {
            deliveryBatchCounter = 0;
            bool batchIsPositive = batchPositiveCount >= batchNegativeCount;

            ActiveReviewItem review;
            if (batchIsPositive)
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

            batchPositiveCount = 0;
            batchNegativeCount = 0;

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
                string toastTitle = batchIsPositive
                    ? LocalizationTable.Get("toast_new_review")
                    : LocalizationTable.Get("toast_customer_complaint");
                string toastBody = $"{review.GetCustomerName()}: [{review.rating:F1}/5.0] {review.GetComment()}";
                SmartphoneUI.Instance.TriggerNotification(toastTitle, toastBody);
            }
        }
        else
        {
            // Sayacı kaydet ve puan/ilerleme eventini tetikle
            SaveData();
            OnRatingChanged?.Invoke(currentRating);
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
                customerName = entry.customerName_tr,
                customerName_tr = entry.customerName_tr,
                customerName_en = entry.customerName_en,
                rating = starRating,
                comment = entry.comment_tr,
                comment_tr = entry.comment_tr,
                comment_en = entry.comment_en,
                timeString = "Yeni",
                timeString_tr = "Yeni",
                timeString_en = "Just now",
                isPositive = true
            };
        }

        return new ActiveReviewItem
        {
            id = "default_pos",
            customerName = "Kızılay Müşterisi",
            customerName_tr = "Kızılay Müşterisi",
            customerName_en = "City Customer",
            rating = starRating,
            comment = "Teslimat tam vaktinde ve hasarsız ulaştı, teşekkürler!",
            comment_tr = "Teslimat tam vaktinde ve hasarsız ulaştı, teşekkürler!",
            comment_en = "Delivery arrived on time and undamaged, thank you!",
            timeString = "Yeni",
            timeString_tr = "Yeni",
            timeString_en = "Just now",
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
                customerName = entry.customerName_tr,
                customerName_tr = entry.customerName_tr,
                customerName_en = entry.customerName_en,
                rating = starRating,
                comment = entry.comment_tr,
                comment_tr = entry.comment_tr,
                comment_en = entry.comment_en,
                timeString = "Yeni",
                timeString_tr = "Yeni",
                timeString_en = "Just now",
                isPositive = false
            };
        }

        return new ActiveReviewItem
        {
            id = "default_neg",
            customerName = "Şehir Sakini",
            customerName_tr = "Şehir Sakini",
            customerName_en = "Local Resident",
            rating = starRating,
            comment = "Paket gecikti veya hasarlı teslim edildi. Memnun kalmadım.",
            comment_tr = "Paket gecikti veya hasarlı teslim edildi. Memnun kalmadım.",
            comment_en = "Package arrived late or damaged. Not satisfied.",
            timeString = "Yeni",
            timeString_tr = "Yeni",
            timeString_en = "Just now",
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
        deliveryBatchCounter = PlayerPrefs.GetInt(BatchCounterPrefsKey, 0);
        batchPositiveCount = PlayerPrefs.GetInt(BatchPosPrefsKey, 0);
        batchNegativeCount = PlayerPrefs.GetInt(BatchNegPrefsKey, 0);

        string jsonHistory = PlayerPrefs.GetString(ReviewHistoryPrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(jsonHistory))
        {
            try
            {
                SavedReviewHistory history = JsonUtility.FromJson<SavedReviewHistory>(jsonHistory);
                if (history != null && history.items != null)
                {
                    recentReviews.Clear();
                    for (int i = 0; i < history.items.Count; i++)
                    {
                        var item = history.items[i];
                        // Eski yapay fallback/seed yorumları tamamen temizle
                        if (item.id != null && item.id.StartsWith("seed_")) continue;

                        HydrateReviewTranslations(item);
                        recentReviews.Add(item);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CourierRatingManager] Failed to load review history: {ex.Message}");
            }
        }

        recentReviews.Clear();
    }

    private void HydrateReviewTranslations(ActiveReviewItem item)
    {
        if (item == null || string.IsNullOrEmpty(item.id)) return;

        if (string.IsNullOrEmpty(item.customerName_en) || string.IsNullOrEmpty(item.comment_en))
        {
            ReviewDatabaseFile db = item.isPositive ? positiveReviewsDb : negativeReviewsDb;
            if (db != null && db.reviews != null)
            {
                for (int i = 0; i < db.reviews.Length; i++)
                {
                    if (db.reviews[i].id == item.id)
                    {
                        item.customerName_tr = db.reviews[i].customerName_tr;
                        item.customerName_en = db.reviews[i].customerName_en;
                        item.comment_tr = db.reviews[i].comment_tr;
                        item.comment_en = db.reviews[i].comment_en;
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(item.timeString_tr)) item.timeString_tr = "Yeni";
        if (string.IsNullOrEmpty(item.timeString_en)) item.timeString_en = "Just now";
    }

    private void SaveData()
    {
        PlayerPrefs.SetFloat(RatingPrefsKey, currentRating);
        PlayerPrefs.SetInt(RatingCountPrefsKey, totalRatedDeliveries);
        PlayerPrefs.SetInt(BatchCounterPrefsKey, deliveryBatchCounter);
        PlayerPrefs.SetInt(BatchPosPrefsKey, batchPositiveCount);
        PlayerPrefs.SetInt(BatchNegPrefsKey, batchNegativeCount);

        SavedReviewHistory history = new SavedReviewHistory();
        history.items.AddRange(recentReviews);
        string json = JsonUtility.ToJson(history);
        PlayerPrefs.SetString(ReviewHistoryPrefsKey, json);
        PlayerPrefs.Save();
    }
}
