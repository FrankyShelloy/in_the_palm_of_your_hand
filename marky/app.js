// app.js — Карта здоровья Тульской области

let myMap;
let placemarks = [];
let userAddedPlaces = [];
let basePlaces = [];
let addMode = false;
let pendingCoords = null;

// Хранилище всех объектов для быстрого поиска
let allPlacesMap = new Map();

// Типы и стили меток
const placeTypes = {
    pharmacy: { color: '#2196F3', icon: '💊' },
    health_center: { color: '#4CAF50', icon: '🩺' },
    hospital: { color: '#E91E63', icon: '🏥' },
    dentist: { color: '#9C27B0', icon: '🦷' },
    lab: { color: '#FF9800', icon: '🔬' },
    clinic: { color: '#00BCD4', icon: '🏨' },
    other_med: { color: '#607D8B', icon: '⚕️' },
    healthy_food: { color: '#8BC34A', icon: '🍏' },
    alcohol: { color: '#F44336', icon: '🍷' },
    gym: { color: '#FF5722', icon: '🏋️' }
};

// Инициализация карты
ymaps.ready(init);

async function init() {
    const center = [54.1934, 37.6179]; // Тула
    const zoom = 11;
    const bounds = [[53.2, 35.2], [54.8, 39.8]]; // Тульская область

    myMap = new ymaps.Map('map', {
        center: center,
        zoom: zoom,
        controls: ['zoomControl']
    }, {
        restrictMapArea: bounds
    });

    myMap.events.add('click', onMapClick);
    await loadPlacesFromJson();
    setupFilters();
    setupAddButton();
    setupReviewModal();
    
    // Делаем функцию глобально доступной
    window.openReviewForm = openReviewForm;
}

// Загрузка данных из JSON
async function loadPlacesFromJson() {
    try {
        const res = await fetch('data/tula-objects.json');
        const dbObjects = await res.json();

        basePlaces = dbObjects.map((obj, index) => {
            // ОСНОВНОЕ ИСПРАВЛЕНИЕ: Преобразуем id в строку для единообразия
            const id = String(obj.id); // Преобразуем число в строку
            
            const place = {
                id: id, // Теперь это строка
                name: obj.name || 'Мед. объект',
                type: obj.type || 'other_med',
                lat: parseFloat(obj.lat),
                lng: parseFloat(obj.lng),
                address: obj.address || 'Адрес не указан',
                avgRating: 0,
                count: 0
            };
            
            // Сохраняем в карту для быстрого поиска
            allPlacesMap.set(id, place);
            
            return place;
        });

        // Также добавляем userAddedPlaces в карту
        userAddedPlaces.forEach(place => {
            allPlacesMap.set(place.id, place);
        });

        renderPlaces([...basePlaces, ...userAddedPlaces]);
        console.log('Загружено объектов:', basePlaces.length);
        console.log('Первые 5 ID:', basePlaces.slice(0, 5).map(p => p.id));
    } catch (e) {
        console.error('Ошибка загрузки tula-objects.json:', e);
        alert('Не удалось загрузить объекты. Убедитесь, что data/tula-objects.json существует и валиден.');
        basePlaces = [];
        renderPlaces([...basePlaces, ...userAddedPlaces]);
    }
}

// Отображение объектов на карте
function renderPlaces(places) {
    placemarks.forEach(pm => myMap.geoObjects.remove(pm));
    placemarks = [];

    places.forEach(place => {
        if (!place.lat || !place.lng) return;
        const pm = createPlacemark(place);
        placemarks.push(pm);
        myMap.geoObjects.add(pm);
    });

    applyFilters();
}

// Создание метки
// Создание метки
function createPlacemark(place) {
    const reviewsInfo = calculateRating(place.id);
    const rating = reviewsInfo.count > 0
        ? `${reviewsInfo.avgRating.toFixed(1)} ⭐ (${reviewsInfo.count} оценок)`
        : 'Оценок пока нет';

    const typeConfig = placeTypes[place.type] || { color: '#999', icon: '📍' };

    // ДЕБАГ: логируем создаваемую метку
    console.log('Создаем метку:', place.id, place.name, place.type);

    const placemark = new ymaps.Placemark(
        [place.lat, place.lng],
        {
            balloonContentHeader: `<b>${place.name || 'Объект'}</b>`,
            balloonContentBody: `
                <p><b>Тип:</b> ${getFriendlyTypeName(place.type)}</p>
                <p><b>Адрес:</b> ${place.address || 'Не указан'}</p>
                <p><b>Рейтинг:</b> ${rating}</p>
                <button onclick="openReviewForm('${place.id}')" style="margin-top:8px;padding:4px 8px;background:#007aff;color:white;border:none;border-radius:4px;">
                    Оставить отзыв
                </button>
            `,
            iconContent: typeConfig.icon
        },
        {
            preset: 'islands#blueStretchyIcon',
            iconColor: typeConfig.color
        }
    );

    placemark.metaData = { 
        type: place.type, 
        id: place.id,
        name: place.name,
        address: place.address
    };
    return placemark;
}

// Применить фильтры
function applyFilters() {
    const activeTypes = Array.from(document.querySelectorAll('#filters input:checked'))
        .map(cb => cb.dataset.type);

    placemarks.forEach(pm => {
        myMap.geoObjects.remove(pm);
    });

    placemarks.forEach(pm => {
        if (activeTypes.includes(pm.metaData.type)) {
            myMap.geoObjects.add(pm);
        }
    });
}

// Настройка фильтров
function setupFilters() {
    document.querySelectorAll('#filters input').forEach(cb => {
        cb.addEventListener('change', applyFilters);
    });
}

// Режим добавления объекта
function setupAddButton() {
    document.getElementById('add-place-btn').addEventListener('click', () => {
        addMode = true;
        alert('Кликните на карте, чтобы указать местоположение');
    });

    document.getElementById('cancel-place').addEventListener('click', () => {
        document.getElementById('add-place-modal').style.display = 'none';
        addMode = false;
    });

    document.getElementById('submit-place').addEventListener('click', submitNewPlace);
}

function onMapClick(e) {
    if (!addMode) return;
    addMode = false;
    pendingCoords = e.get('coords');
    document.getElementById('add-place-modal').style.display = 'flex';
}

function submitNewPlace() {
    const name = document.getElementById('place-name').value.trim();
    const type = document.getElementById('place-type').value;

    if (!name) {
        alert('Введите название');
        return;
    }

    const [lat, lng] = pendingCoords;
    const newPlace = {
        id: `user_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
        name,
        type,
        lat,
        lng,
        address: `Добавлено пользователем (${lat.toFixed(4)}, ${lng.toFixed(4)})`,
        avgRating: 0,
        count: 0
    };

    userAddedPlaces.push(newPlace);
    allPlacesMap.set(newPlace.id, newPlace);
    renderPlaces([...basePlaces, ...userAddedPlaces]);

    document.getElementById('place-name').value = '';
    document.getElementById('add-place-modal').style.display = 'none';
}

// === СИСТЕМА ОТЗЫВОВ ===

function getReviews(placeId) {
    const stored = localStorage.getItem(`reviews_${placeId}`);
    return stored ? JSON.parse(stored) : [];
}

function saveReviews(placeId, reviews) {
    localStorage.setItem(`reviews_${placeId}`, JSON.stringify(reviews));
}

function calculateRating(placeId) {
    const reviews = getReviews(placeId);
    if (reviews.length === 0) {
        return { avgRating: 0, count: 0 };
    }
    const sum = reviews.reduce((acc, r) => acc + r.rating, 0);
    return {
        avgRating: sum / reviews.length,
        count: reviews.length
    };
}

// Исправленная функция открытия формы отзыва
function openReviewForm(placeId) {
    console.log('=== ОТКРЫТИЕ ФОРМЫ ОТЗЫВА ===');
    console.log('Получен ID:', placeId, 'Тип:', typeof placeId);
    
    // Преобразуем в строку на всякий случай
    const idStr = String(placeId);
    
    // Быстрый поиск по Map
    const place = allPlacesMap.get(idStr);
    
    if (place) {
        console.log('Найден объект:', place.name);
        window.currentReviewPlaceId = idStr;
        document.getElementById('review-place-name').textContent = place.name;
        document.getElementById('review-modal').style.display = 'flex';
        return;
    }
    
    // Если не найдено в Map, ищем в метках
    console.log('Поиск в метках...');
    const placemark = placemarks.find(pm => {
        console.log('Метка ID:', pm.metaData.id, 'Тип:', typeof pm.metaData.id);
        return String(pm.metaData.id) === idStr;
    });
    
    if (placemark) {
        console.log('Найден в метках:', placemark.metaData.name);
        window.currentReviewPlaceId = idStr;
        document.getElementById('review-place-name').textContent = placemark.metaData.name;
        document.getElementById('review-modal').style.display = 'flex';
        return;
    }
    
    // Последняя попытка - поиск по всем массивам
    console.log('Поиск по всем массивам...');
    const allPlaces = [...basePlaces, ...userAddedPlaces];
    const foundPlace = allPlaces.find(p => String(p.id) === idStr);
    
    if (foundPlace) {
        console.log('Найден в массивах:', foundPlace.name);
        window.currentReviewPlaceId = idStr;
        document.getElementById('review-place-name').textContent = foundPlace.name;
        document.getElementById('review-modal').style.display = 'flex';
        return;
    }
    
    // Если ничего не найдено
    console.error('Объект не найден нигде!');
    console.log('Все ID в allPlacesMap:', Array.from(allPlacesMap.keys()));
    console.log('Все ID в метках:', placemarks.map(pm => pm.metaData.id));
    
    alert(`Объект не найден. ID: ${placeId}\nПроверьте консоль браузера (F12) для подробностей.`);
}

function setupReviewModal() {
    // Звезды рейтинга
    document.querySelectorAll('#star-rating span').forEach(star => {
        star.addEventListener('click', function () {
            const value = parseInt(this.dataset.value);
            window.selectedRating = value;

            document.querySelectorAll('#star-rating span').forEach((s, i) => {
                const isActive = i + 1 <= value;
                s.textContent = isActive ? '★' : '☆';
                s.classList.toggle('star-active', isActive);
            });
        });
    });

    // Отмена
    document.getElementById('cancel-review').addEventListener('click', () => {
        document.getElementById('review-modal').style.display = 'none';
    });

    // Отправка отзыва
    document.getElementById('submit-review').addEventListener('click', () => {
        const rating = window.selectedRating;
        const comment = document.getElementById('review-comment').value.trim();
        const placeId = window.currentReviewPlaceId;

        if (!rating) {
            alert('Пожалуйста, поставьте оценку');
            return;
        }

        const reviews = getReviews(placeId);
        reviews.push({
            rating: rating,
            comment: comment,
            timestamp: Date.now()
        });
        saveReviews(placeId, reviews);

        document.getElementById('review-modal').style.display = 'none';
        renderPlaces([...basePlaces, ...userAddedPlaces]); // Обновляем отображение
        
        alert('Спасибо за ваш отзыв!');
    });
}

// Вспомогательные функции
function getFriendlyTypeName(type) {
    const map = {
        pharmacy: 'Аптека',
        health_center: 'Центр здоровья',
        hospital: 'Больница',
        dentist: 'Стоматология',
        lab: 'Лаборатория',
        clinic: 'Поликлиника',
        other_med: 'Мед. учреждение',
        healthy_food: 'Здоровое питание',
        alcohol: 'Алкоголь / табак',
        gym: 'Спорт / активность'
    };
    return map[type] || type;
}