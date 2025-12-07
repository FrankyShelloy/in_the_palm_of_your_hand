namespace PalmMap.Api.Models;

public enum AchievementProgressType
{
    FirstPlaceAdded = 1,           // Первые шаги - добавить первый объект
    ReviewsCount = 2,              // Внимательный горожанин - оценить N объектов
    PhotosCount = 3,               // Фотограф здоровья - добавить N фотографий
    DetailedReviewsCount = 4,      // Объективный критик - развёрнутые отзывы (>100 символов)
    BalancedReviews = 5,            // Баланс мнений - оценить по 2 объекта каждого типа
    NewPlacesAdded = 6,            // Детектив инфраструктуры - добавить 3 новых объекта
    HighRatedHealthyPlaces = 7,     // Эксперт здоровья - 10 объектов здорового питания 4.5+
    TopThreeRating = 8,            // Легенда платформы - топ-3 в рейтинге
    PlacesReviewedByOthers = 9,     // Командный игрок - 5 объектов оценены другими
    AllRatingsUsed = 10,           // Эмоциональный аналитик - использовать все оценки 1-5
    PlacesInOneDay = 11,           // Быстрые пальцы - 3 объекта за один день
    TestReviewSubmitted = 99       // ТЕСТ: Проверка - отправить один отзыв на модерацию
}

public class Achievement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty; // unique machine-readable code
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "🏆"; // Emoji иконка
    public AchievementProgressType ProgressType { get; set; }
    public int TargetValue { get; set; } // Целевое значение для достижения
    public int RequiredReviews { get; set; } // Оставлено для обратной совместимости

    public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
