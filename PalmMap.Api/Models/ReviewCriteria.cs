namespace PalmMap.Api.Models;

public static class ReviewCriteria
{
    public static readonly Dictionary<string, string> Pharmacy = new()
    {
        { "assortment", "Ассортимент" },
        { "prices", "Цены" },
        { "service", "Обслуживание" },
        { "accessibility", "Доступность" }
    };

    public static readonly Dictionary<string, string> HealthCenter = new()
    {
        { "quality", "Качество услуг" },
        { "equipment", "Оборудование" },
        { "staff", "Персонал" },
        { "convenience", "Удобство" }
    };

    public static readonly Dictionary<string, string> Hospital = new()
    {
        { "treatment", "Качество лечения" },
        { "equipment", "Оборудование" },
        { "staff", "Персонал" },
        { "conditions", "Условия пребывания" }
    };

    public static readonly Dictionary<string, string> Dentist = new()
    {
        { "treatment", "Качество лечения" },
        { "equipment", "Оборудование" },
        { "staff", "Персонал" },
        { "painless", "Безболезненность" }
    };

    public static readonly Dictionary<string, string> Lab = new()
    {
        { "accuracy", "Точность анализов" },
        { "speed", "Скорость" },
        { "equipment", "Оборудование" },
        { "staff", "Персонал" }
    };

    public static readonly Dictionary<string, string> Clinic = new()
    {
        { "quality", "Качество услуг" },
        { "queues", "Очереди" },
        { "staff", "Персонал" },
        { "convenience", "Удобство" }
    };

    public static readonly Dictionary<string, string> OtherMed = new()
    {
        { "quality", "Качество услуг" },
        { "equipment", "Оборудование" },
        { "staff", "Персонал" },
        { "convenience", "Удобство" }
    };

    public static readonly Dictionary<string, string> HealthyFood = new()
    {
        { "food_quality", "Качество еды" },
        { "prices", "Цены" },
        { "assortment", "Ассортимент" },
        { "service", "Обслуживание" }
    };

    public static readonly Dictionary<string, string> Alcohol = new()
    {
        { "assortment", "Ассортимент" },
        { "prices", "Цены" },
        { "service", "Обслуживание" },
        { "accessibility", "Доступность" }
    };

    public static readonly Dictionary<string, string> Gym = new()
    {
        { "equipment", "Оборудование" },
        { "staff", "Персонал" },
        { "prices", "Цены" },
        { "convenience", "Удобство" }
    };

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

