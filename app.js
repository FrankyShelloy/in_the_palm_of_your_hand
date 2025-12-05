// app.js — Карта здоровья Тульской области

let myMap;
let placemarks = [];
let userAddedPlaces = [];
let addMode = false;
let pendingCoords = null;

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
}

// Загрузка данных из JSON
async function loadPlacesFromJson() {
    try {
        const res = await fetch('data/tula-objects.json');
        const dbObjects = await res.json();

        // Нормализуем данные
        const normalized = dbObjects.map(obj => ({
            id: obj.id || obj.cfCode + '_' + obj.lat,
            name: obj.name || 'Мед. объект',
            type: obj.type || 'other_med',
            lat: parseFloat(obj.lat || obj.cfLatitude),
            lng: parseFloat(obj.lng || obj.cfLongitude),
            address: obj.address || obj.cfAddress || 'Адрес не указан',
            avgRating: 0,
            count: 0
        }));

        renderPlaces([...normalized, ...userAddedPlaces]);
    } catch (e) {
        console.error('Ошибка загрузки tula-objects.json:', e);
        alert('Не удалось загрузить объекты. Убедитесь, что data/tula-objects.json существует и валиден.');
        renderPlaces(userAddedPlaces); // показать хотя бы пользовательские
    }
}

// Отображение объектов на карте
function renderPlaces(places) {
    // Удалить ВСЁ
    placemarks.forEach(pm => myMap.geoObjects.remove(pm));
    placemarks = [];

    // Создать и добавить всё с нуля
    places.forEach(place => {
        if (!place.lat || !place.lng) return;
        const pm = createPlacemark(place);
        placemarks.push(pm);
    });

    // Применить фильтры
    applyFilters();
}

// Создание метки
function createPlacemark(place) {
    const typeConfig = placeTypes[place.type] || { color: '#999', icon: '📍' };
    const rating = place.count > 0 ? `${place.avgRating.toFixed(1)} ⭐ (${place.count} оценок)` : 'Оценок пока нет';

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
    
    placemark.metaData = { type: place.type, id: place.id };
    return placemark;
}

// Применить фильтры
function applyFilters() {
    const activeTypes = Array.from(document.querySelectorAll('#filters input:checked'))
        .map(cb => cb.dataset.type);

    // Удаляем все метки
    placemarks.forEach(pm => {
        myMap.geoObjects.remove(pm);
    });

    // Добавляем только те, которые соответствуют фильтрам
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
        id: `user_${Date.now()}`,
        name,
        type,
        lat,
        lng,
        address: `Добавлено пользователем (${lat.toFixed(4)}, ${lng.toFixed(4)})`,
        avgRating: 0,
        count: 0
    };

    userAddedPlaces.push(newPlace);
    loadPlacesFromJson();

    document.getElementById('place-name').value = '';
    document.getElementById('add-place-modal').style.display = 'none';
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

function openReviewForm(placeId) {
    alert(`Открыт отзыв для объекта ID: ${placeId}`);
}