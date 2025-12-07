-- Скрипт для обновления достижений в базе данных
-- Выполните этот скрипт в вашей SQLite базе данных (app.db)

UPDATE Achievements 
SET Code = 'first-steps',
    Title = 'Первые шаги',
    Description = 'Начало пути картографа здоровья',
    Icon = '👣',
    ProgressType = 1,
    TargetValue = 1,
    RequiredReviews = 0
WHERE Id = '11111111-1111-1111-1111-111111111111';

UPDATE Achievements 
SET Code = 'attentive-citizen',
    Title = 'Внимательный горожанин',
    Description = 'Проявил внимание к городской среде',
    Icon = '👁️',
    ProgressType = 2,
    TargetValue = 10,
    RequiredReviews = 10
WHERE Id = '22222222-2222-2222-2222-222222222222';

UPDATE Achievements 
SET Code = 'health-photographer',
    Title = 'Фотограф здоровья',
    Description = 'Визуально документируешь городскую среду',
    Icon = '📸',
    ProgressType = 3,
    TargetValue = 15,
    RequiredReviews = 0
WHERE Id = '33333333-3333-3333-3333-333333333333';


