// app.js — Карта здоровья Тульской области с кластеризацией

let myMap;
let placemarks = [];
let userAddedPlaces = [];
let basePlaces = [];
let addMode = false;
let pendingCoords = null;
let clusterer;

// Хранилище всех объектов для быстрого поиска
let allPlacesMap = new Map();

// Все метки (видимые и скрытые)
let allPlacemarks = [];

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
    // Создаем кастомный layout внутри init, когда ymaps уже загружен
    const MyBalloonItemLayout = ymaps.templateLayoutFactory.createClass(
        '<div class="cluster-balloon-item">' +
            '<h4>$[properties.balloonContentHeader]</h4>' +
            '<div>$[properties.balloonContentBody]</div>' +
        '</div>'
    );
    
    // Регистрируем кастомный layout
    ymaps.layout.storage.add('my#balloonItemLayout', MyBalloonItemLayout);

    const center = [54.1934, 37.6179]; // Тула
    const zoom = 11;
    const bounds = [[53.2, 35.2], [54.8, 39.8]]; // Тульская область

    myMap = new ymaps.Map('map', {
        center: center,
        zoom: zoom,
        controls: ['zoomControl', 'typeSelector', 'fullscreenControl']
    }, {
        restrictMapArea: bounds
    });

    myMap.events.add('click', onMapClick);
    
    // Инициализируем кластеризатор
    initClusterer();
    
    await loadPlacesFromJson();
    setupFilters();
    setupAddButton();
    setupReviewModal();
    
    // Настройка поведения при зуме
    setupZoomBehavior();
    
    // Делаем функцию глобально доступной
    window.openReviewForm = openReviewForm;
}

// Инициализация кластеризатора
function initClusterer() {
    // Создаем кластеризатор с кастомными стилями
    clusterer = new ymaps.Clusterer({
        // Основные настройки
        preset: 'islands#invertedBlueClusterIcons',
        clusterDisableClickZoom: false,
        clusterOpenBalloonOnClick: true,
        
        // Настройки балуна кластера
        clusterBalloonContentLayout: 'cluster#balloonCarousel',
        clusterBalloonItemContentLayout: 'my#balloonItemLayout',
        clusterBalloonPanelMaxMapArea: 0,
        clusterBalloonContentLayoutWidth: 300,
        clusterBalloonContentLayoutHeight: 200,
        clusterBalloonPagerSize: 5,
        
        // Настройка группировки
        gridSize: 80,
        groupByCoordinates: false,
        minClusterSize: 2,
        
        // Стили кластеров
        clusterIconLayout: 'default#pieChart',
        clusterIconPieChartRadius: 25,
        clusterIconPieChartCoreRadius: 15,
        clusterIconPieChartStrokeWidth: 3,
        
        // Поведение
        hasBalloon: true,
        hasHint: false,
        zoomMargin: 50,
        
        // Оптимизация
        clusterHideIconsOnSingleObject: true
    });
    
    // Добавляем кластеризатор на карту
    myMap.geoObjects.add(clusterer);
}

// Настройка поведения при зуме
function setupZoomBehavior() {
    let lastZoom = myMap.getZoom();
    
    myMap.events.add('boundschange', function (e) {
        const newZoom = e.get('newZoom');
        
        // Меняем параметры кластеризации в зависимости от масштаба
        if (newZoom !== lastZoom) {
            if (newZoom > 16) { // Максимальное приближение - показываем все
                clusterer.options.set({
                    gridSize: 32,
                    minClusterSize: 3
                });
            } else if (newZoom > 14) { // Среднее приближение
                clusterer.options.set({
                    gridSize: 64,
                    minClusterSize: 2
                });
            } else if (newZoom > 12) { // Нормальный вид
                clusterer.options.set({
                    gridSize: 80,
                    minClusterSize: 2
                });
            } else { // Отдаление - агрессивная кластеризация
                clusterer.options.set({
                    gridSize: 120,
                    minClusterSize: 2
                });
            }
            
            lastZoom = newZoom;
        }
    });
}

// Загрузка данных из JSON
async function loadPlacesFromJson() {
    try {
        const res = await fetch('data/tula-objects.json');
        
        if (!res.ok) {
            throw new Error(`HTTP error! status: ${res.status}`);
        }
        
        const dbObjects = await res.json();
        
        // Проверяем, что это массив
        if (!Array.isArray(dbObjects)) {
            throw new Error('JSON file does not contain an array');
        }

        basePlaces = dbObjects.map((obj, index) => {
            const id = String(obj.id || index + 1);
            
            const place = {
                id: id,
                name: obj.name || 'Мед. объект',
                type: obj.type || 'other_med',
                lat: parseFloat(obj.lat) || 54.1934 + (Math.random() - 0.5) * 0.1,
                lng: parseFloat(obj.lng) || 37.6179 + (Math.random() - 0.5) * 0.1,
                address: obj.address || 'Адрес не указан',
                avgRating: 0,
                count: 0
            };
            
            allPlacesMap.set(id, place);
            return place;
        });

        // Загружаем пользовательские места из localStorage
        loadUserPlaces();
        
        // Создаем все метки
        createAllPlacemarks();
        
        // Рендерим с текущими фильтрами
        applyFilters();
        
        console.log('Загружено объектов:', basePlaces.length);
        
    } catch (e) {
        console.error('Ошибка загрузки tula-objects.json:', e);
        console.log('Проверьте:');
        console.log('1. Существует ли файл data/tula-objects.json?');
        console.log('2. Корректный ли JSON формат?');
        console.log('3. Доступен ли файл по указанному пути?');
        
        // Загружаем пользовательские места из localStorage
        loadUserPlaces();
        
        // Показываем демо-данные
        loadDemoData();
        
        // Создаем все метки
        createAllPlacemarks();
        
        // Рендерим с текущими фильтрами
        applyFilters();
    }
}

// Создание всех меток
function createAllPlacemarks() {
    allPlacemarks = [];
    
    // Создаем метки для базовых объектов
    basePlaces.forEach(place => {
        if (!place.lat || !place.lng) return;
        const pm = createPlacemark(place);
        allPlacemarks.push(pm);
    });
    
    // Создаем метки для пользовательских объектов
    userAddedPlaces.forEach(place => {
        if (!place.lat || !place.lng) return;
        const pm = createPlacemark(place);
        allPlacemarks.push(pm);
    });
}

// Загрузка пользовательских мест из localStorage
function loadUserPlaces() {
    try {
        const saved = localStorage.getItem('userAddedPlaces');
        if (saved) {
            userAddedPlaces = JSON.parse(saved);
            userAddedPlaces.forEach(place => {
                allPlacesMap.set(place.id, place);
            });
            console.log('Загружено пользовательских объектов:', userAddedPlaces.length);
        }
    } catch (e) {
        console.error('Ошибка загрузки пользовательских мест:', e);
        userAddedPlaces = [];
    }
}

// Сохранение пользовательских мест в localStorage
function saveUserPlaces() {
    try {
        localStorage.setItem('userAddedPlaces', JSON.stringify(userAddedPlaces));
    } catch (e) {
        console.error('Ошибка сохранения пользовательских мест:', e);
    }
}

// Загрузка демо-данных
function loadDemoData() {
    console.log('Загрузка демо-данных...');
    
    const demoPlaces = [
        {
            id: 'demo_1',
            name: 'Городская больница №1',
            type: 'hospital',
            lat: 54.1934,
            lng: 37.6179,
            address: 'г. Тула, ул. Советская, 15',
            avgRating: 0,
            count: 0
        },
        {
            id: 'demo_2',
            name: 'Аптека "Здоровье"',
            type: 'pharmacy',
            lat: 54.1889,
            lng: 37.6152,
            address: 'г. Тула, пр-т Ленина, 45',
            avgRating: 0,
            count: 0
        },
        {
            id: 'demo_3',
            name: 'Спортзал "Атлет"',
            type: 'gym',
            lat: 54.1967,
            lng: 37.6098,
            address: 'г. Тула, ул. Октябрьская, 32',
            avgRating: 0,
            count: 0
        }
    ];
    
    basePlaces = [...basePlaces, ...demoPlaces];
    demoPlaces.forEach(place => {
        allPlacesMap.set(place.id, place);
    });
}

// Создание метки
function createPlacemark(place) {
    const reviewsInfo = calculateRating(place.id);
    const rating = reviewsInfo.count > 0
        ? `${reviewsInfo.avgRating.toFixed(1)} ⭐ (${reviewsInfo.count} оценок)`
        : 'Оценок пока нет';

    const typeConfig = placeTypes[place.type] || { color: '#999', icon: '📍' };

    const placemark = new ymaps.Placemark(
        [place.lat, place.lng],
        {
            balloonContentHeader: `<b>${place.name || 'Объект'}</b>`,
            balloonContentBody: `
                <div style="max-width: 300px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;">
                    <p><b>Тип:</b> ${getFriendlyTypeName(place.type)} ${typeConfig.icon}</p>
                    <p><b>Адрес:</b> ${place.address || 'Не указан'}</p>
                    <p><b>Рейтинг:</b> ${rating}</p>
                    <button onclick="openReviewForm('${place.id}')" 
                            style="margin-top:12px;padding:8px 16px;background:#007aff;color:white;border:none;border-radius:6px;cursor:pointer;font-size:14px;width:100%;">
                        ✍️ Оставить отзыв
                    </button>
                </div>
            `,
            iconContent: typeConfig.icon,
            // Метаданные для фильтрации и кластеризации
            placeType: place.type,
            placeId: place.id,
            placeName: place.name
        },
        {
            preset: 'islands#blueStretchyIcon',
            iconColor: typeConfig.color,
            iconCaptionMaxWidth: '150',
            hideIconOnBalloonOpen: false,
            openBalloonOnClick: true,
            balloonCloseButton: true
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

// Применить фильтры с учетом кластеризации (ИСПРАВЛЕННАЯ ВЕРСИЯ)
function applyFilters() {
    if (!clusterer) return;
    
    const activeTypes = Array.from(document.querySelectorAll('#filters input:checked'))
        .map(cb => cb.dataset.type);
    
    console.log('Активные типы:', activeTypes);
    console.log('Всего меток:', allPlacemarks.length);
    
    // Получаем только видимые метки по фильтру
    const visiblePlacemarks = allPlacemarks.filter(pm => {
        const type = pm.properties.get('placeType');
        return activeTypes.includes(type);
    });
    
    console.log('Видимых меток:', visiblePlacemarks.length);
    
    // Очищаем кластеризатор
    clusterer.removeAll();
    
    // Добавляем только видимые метки
    if (visiblePlacemarks.length > 0) {
        clusterer.add(visiblePlacemarks);
    }
    
    // Также обновляем видимость меток
    allPlacemarks.forEach(pm => {
        const type = pm.properties.get('placeType');
        const shouldBeVisible = activeTypes.includes(type);
        
        // Устанавливаем видимость метки
        pm.options.set('visible', shouldBeVisible);
    });
}

// Настройка фильтров
function setupFilters() {
    document.querySelectorAll('#filters input').forEach(cb => {
        cb.addEventListener('change', function() {
            console.log('Фильтр изменен:', this.dataset.type, this.checked);
            applyFilters();
        });
    });
}

// Режим добавления объекта
function setupAddButton() {
    document.getElementById('add-place-btn').addEventListener('click', () => {
        addMode = true;
        alert('Кликните на карте, чтобы указать местоположение нового объекта');
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
        alert('Введите название объекта');
        return;
    }

    const [lat, lng] = pendingCoords;
    const newPlace = {
        id: `user_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
        name,
        type,
        lat,
        lng,
        address: `Добавлено пользователем (${lat.toFixed(6)}, ${lng.toFixed(6)})`,
        avgRating: 0,
        count: 0
    };

    userAddedPlaces.push(newPlace);
    allPlacesMap.set(newPlace.id, newPlace);
    
    // Сохраняем в localStorage
    saveUserPlaces();
    
    // Создаем новую метку и добавляем в общий список
    const newPlacemark = createPlacemark(newPlace);
    allPlacemarks.push(newPlacemark);
    
    // Пересчитываем фильтры
    applyFilters();
    
    document.getElementById('place-name').value = '';
    document.getElementById('add-place-modal').style.display = 'none';
    
    alert(`Объект "${name}" успешно добавлен!`);
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

function openReviewForm(placeId) {
    console.log('Открытие формы для ID:', placeId);
    
    const idStr = String(placeId);
    let place = allPlacesMap.get(idStr);
    
    if (!place) {
        // Ищем среди всех мест
        const allPlaces = [...basePlaces, ...userAddedPlaces];
        place = allPlaces.find(p => String(p.id) === idStr);
    }
    
    if (!place) {
        console.error('Объект не найден! ID:', idStr);
        alert('Объект не найден. Возможно, он был удален или скрыт фильтром.');
        return;
    }
    
    window.currentReviewPlaceId = idStr;
    document.getElementById('review-place-name').textContent = place.name;
    document.getElementById('review-modal').style.display = 'flex';
    
    // Сбрасываем рейтинг при открытии
    window.selectedRating = null;
    document.querySelectorAll('#star-rating span').forEach(s => {
        s.textContent = '☆';
        s.classList.remove('star-active');
    });
    document.getElementById('review-comment').value = '';
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
        
        // Эффект при наведении
        star.addEventListener('mouseover', function() {
            const value = parseInt(this.dataset.value);
            document.querySelectorAll('#star-rating span').forEach((s, i) => {
                s.style.opacity = i + 1 <= value ? '1' : '0.5';
            });
        });
        
        star.addEventListener('mouseout', function() {
            document.querySelectorAll('#star-rating span').forEach(s => {
                s.style.opacity = '1';
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
            alert('Пожалуйста, поставьте оценку от 1 до 5 звезд');
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
        
        // Обновляем отображение метки с новым рейтингом
        updatePlacemarkRating(placeId);
        
        alert('Спасибо за ваш отзыв! Рейтинг объекта обновлен.');
    });
}

// Обновление метки после добавления отзыва
function updatePlacemarkRating(placeId) {
    const idStr = String(placeId);
    
    // Ищем метку среди всех созданных
    const placemark = allPlacemarks.find(pm => 
        pm.properties.get('placeId') === idStr || 
        String(pm.metaData?.id) === idStr
    );
    
    if (placemark) {
        const reviewsInfo = calculateRating(placeId);
        const rating = reviewsInfo.count > 0
            ? `${reviewsInfo.avgRating.toFixed(1)} ⭐ (${reviewsInfo.count} оценок)`
            : 'Оценок пока нет';
        
        // Обновляем содержимое балуна
        const place = allPlacesMap.get(idStr) || placemark.metaData;
        const typeConfig = placeTypes[place.type] || { color: '#999', icon: '📍' };
        
        placemark.properties.set({
            balloonContentBody: `
                <div style="max-width: 300px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;">
                    <p><b>Тип:</b> ${getFriendlyTypeName(place.type)} ${typeConfig.icon}</p>
                    <p><b>Адрес:</b> ${place.address || 'Не указан'}</p>
                    <p><b>Рейтинг:</b> ${rating}</p>
                    <button onclick="openReviewForm('${idStr}')" 
                            style="margin-top:12px;padding:8px 16px;background:#007aff;color:white;border:none;border-radius:6px;cursor:pointer;font-size:14px;width:100%;">
                        ✍️ Оставить отзыв
                    </button>
                </div>
            `
        });
    }
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