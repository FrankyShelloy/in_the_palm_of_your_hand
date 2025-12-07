namespace PalmMap.Api.Models;

// Критерии оценки для каждого типа объекта
public static class ReviewCriteria
{
    // Критерии для аптеки
    public static readonly Dictionary<string, string> Pharmacy = new()
    {
        { "assortment", "Ассортимент" },
        { "prices", "Цены" },
        { "service", "Обслуживание" },
        { "accessibility", "Доступность" }
    };

    // Критерии для центра здоровья
    public static readonly Dictionary<string, string> HealthCenter = new()
    {
        { "quality", "Качество услуг" },
        { "equipment", "Оборудование" },
        { "staff", "Персонал" },
        { "convenience", "Удобство" }
    };

    // Критерии для больницы
    public static readonly Dictionary<string, string> Hospital = new()
    {
        { "treatment", "Качество лечения" },
        { "equipment", "Оборудование" },
        { "staff", "Персонал" },
        { "conditions", "Условия пребывания" }
    };

    // Критерии для стоматологии
    public static readonly Dictionary<string, string> Dentist = new()
    {
        { "treatment", "Качество лечения" },
        { "equipment", "Оборудование" },
        { "staff", "Персонал" },
        { "painless", "Безболезненность" }
    };

    // Критерии для лаборатории
    public static readonly Dictionary<string, string> Lab = new()
    {
        { "accuracy", "Точность анализов" },
        { "speed", "Скорость" },
        { "equipment", "Оборудование" },
        { "staff", "Персонал" }
    };

    // Критерии для поликлиники
    public static readonly Dictionary<string, string> Clinic = new()
    {
        { "quality", "Качество услуг" },
        { "queues", "Очереди" },
        { "staff", "Персонал" },
        { "convenience", "Удобство" }
    };

    // Критерии для мед. учреждения
    public static readonly Dictionary<string, string> OtherMed = new()
    {
        { "quality", "Качество услуг" },
        { "equipment", "Оборудование" },
        { "staff", "Персонал" },
        { "convenience", "Удобство" }
    };

    // Критерии для здорового питания
    public static readonly Dictionary<string, string> HealthyFood = new()
    {
        { "food_quality", "Качество еды" },
        { "prices", "Цены" },
        { "assortment", "Ассортимент" },
        { "service", "Обслуживание" }
    };

    // Критерии для алкоголя/табака
    public static readonly Dictionary<string, string> Alcohol = new()
    {
        { "assortment", "Ассортимент" },
        { "prices", "Цены" },
        { "service", "Обслуживание" },
        { "accessibility", "Доступность" }
    };

    // Критерии для спорт/активность
    public static readonly Dictionary<string, string> Gym = new()
    {
        { "equipment", "Оборудование" },
        { "staff", "Персонал" },
        { "prices", "Цены" },
        { "convenience", "Удобство" }
    };

    // Получить критерии для типа объекта
    public static Dictionary<string, string> GetCriteriaForType(string placeType)
    {
        return placeType switch
        {
            "pharmacy" => Pharmacy,
            "health_center" => HealthCenter,
            "hospital" => Hospital,
            "dentist" => Dentist,
            "lab" => Lab,
            "clinic" => Clinic,
            "other_med" => OtherMed,
            "healthy_food" => HealthyFood,
            "alcohol" => Alcohol,
            "gym" => Gym,
            _ => OtherMed // По умолчанию
        };
    }
}

