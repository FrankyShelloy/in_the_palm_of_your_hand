const apiBase = "/api";
const tokenKey = "palmmap_token";
let currentUser = null;

// DOM Elements
const els = {

  authActions: document.getElementById("auth-actions"),
  userInfo: document.getElementById("user-info"),
  logout: document.getElementById("btn-logout"),
  loginForm: document.getElementById("login-form"),
  registerForm: document.getElementById("register-form"),
  loginError: document.getElementById("login-error"),
  registerError: document.getElementById("register-error"),
  modal: document.getElementById("modal-auth"),
  btnShowLogin: document.getElementById("btn-show-login"),
  btnShowRegister: document.getElementById("btn-show-register"),
  modalClose: document.getElementById("modal-close"),
  tabs: document.querySelectorAll(".tab"),
  profileEmail: document.getElementById("profile-email"),
  profileName: document.getElementById("profile-name"),
  profileLevel: document.getElementById("profile-level"),
  profileReviews: document.getElementById("profile-reviews"),
  achievements: document.getElementById("achievements-list"),
  reviewForm: document.getElementById("review-form"),
  reviewText: document.getElementById("review-text"),
  reviewStatus: document.getElementById("review-status"),
  
  // New elements
  profileToggle: document.getElementById("profile-toggle"),
  profilePanel: document.getElementById("profile-panel"),
  userReviewsList: document.getElementById("user-reviews-list"),
  objectReviewsPanel: document.getElementById("object-reviews-panel"),
  objectReviewsContent: document.getElementById("object-reviews-content"),
  objectReviewsTitle: document.getElementById("object-reviews-title"),

  // Collapsible sections
  toggleReviews: document.getElementById("toggle-reviews"),
  reviewsContainer: document.getElementById("user-reviews-container"),
  toggleAchievements: document.getElementById("toggle-achievements"),
  achievementsContainer: document.getElementById("user-achievements-container"),
  
  // Profile visibility
  profileClose: document.getElementById("profile-close"),
  topbarProfileToggle: document.getElementById("topbar-profile-toggle"),
};

function saveToken(token) {
  localStorage.setItem(tokenKey, token);
}
function getToken() {
  return localStorage.getItem(tokenKey);
}
function clearToken() {
  localStorage.removeItem(tokenKey);
}

function showModal(mode) {
  els.modal.classList.remove("hidden");
  switchTab(mode);
}
function hideModal() {
  els.modal.classList.add("hidden");
  els.loginError.textContent = "";
  els.registerError.textContent = "";
}

function switchTab(mode) {
  els.tabs.forEach((tab) => {
    const active = tab.dataset.tab === mode;
    tab.classList.toggle("active", active);
  });
  els.loginForm.classList.toggle("hidden", mode !== "login");
  els.registerForm.classList.toggle("hidden", mode !== "register");
}

async function api(path, options = {}) {
  const token = getToken();
  const headers = { "Content-Type": "application/json", ...(options.headers || {}) };
  if (token) headers.Authorization = `Bearer ${token}`;
  const res = await fetch(`${apiBase}${path}`, { ...options, headers });
  
  if (!res.ok) {
    let errorData;
    try {
      errorData = await res.clone().json();
    } catch {
      try {
        errorData = await res.clone().text();
      } catch (e) {
        errorData = res.statusText || `HTTP ${res.status}`;
      }
    }
    throw errorData;
  }

  if (res.status === 204) return null;
  
  const text = await res.text();
  if (!text) return null;
  
  try {
    return JSON.parse(text);
  } catch (e) {
    return text;
  }
}

function formatError(err) {
  if (!err) return "Неизвестная ошибка";
  
  // Если это строка (например, JSON), пробуем распарсить
  if (typeof err === 'string') {
    try {
      const parsed = JSON.parse(err);
      return formatError(parsed);
    } catch (e) {
      return err; // Просто строка
    }
  }

  // Обработка 401 Unauthorized (стандартный ответ ASP.NET Core)
  if (err.status === 401 || err.title === "Unauthorized") {
    return "Неверный email или пароль.";
  }

  // Массив ошибок Identity
  if (Array.isArray(err)) {
    return err.map(e => translateIdentityError(e)).join('<br>');
  }

  // Объект с сообщением (наш кастомный формат { message: "..." })
  if (err.message) {
    // Если message это JSON строка, пробуем распарсить
    if (typeof err.message === 'string' && err.message.trim().startsWith('{')) {
        try {
            const parsed = JSON.parse(err.message);
            return formatError(parsed);
        } catch {}
    }
    return err.message;
  }
  
  // ValidationProblemDetails (errors: { Field: ["Error"] })
  if (err.errors) {
    // Собираем все ошибки валидации в один список
    return Object.values(err.errors).flat().join('<br>');
  }

  // Если есть заголовок ошибки, но нет деталей (например, 400 Bad Request без body)
  if (err.title) {
      return err.title;
  }

  return "Произошла ошибка при выполнении запроса.";
}

function translateIdentityError(error) {
    const code = error.code;
    switch (code) {
        case "DuplicateEmail": return "Этот Email уже зарегистрирован.";
        case "DuplicateUserName": return "Это имя пользователя уже занято.";
        case "InvalidEmail": return "Некорректный Email.";
        case "PasswordTooShort": return "Пароль слишком короткий (минимум 6 символов).";
        case "PasswordRequiresNonAlphanumeric": return "Пароль должен содержать спецсимвол (!?@...).";
        case "PasswordRequiresDigit": return "Пароль должен содержать цифру.";
        case "PasswordRequiresLower": return "Пароль должен содержать строчную букву.";
        case "PasswordRequiresUpper": return "Пароль должен содержать заглавную букву.";
        case "InvalidToken": return "Неверный или устаревший токен.";
        case "PasswordMismatch": return "Неверный пароль.";
        default: return error.description || "Произошла ошибка.";
    }
}

async function loadProfile() {
  try {
    const data = await api("/auth/me");
    currentUser = data;
    els.userInfo.textContent = data.email;
    els.userInfo.classList.remove("hidden");
    els.logout.classList.remove("hidden");
    els.topbarProfileToggle.classList.remove("hidden");
    els.btnShowLogin.classList.add("hidden");
    els.btnShowRegister.classList.add("hidden");

    els.profileEmail.textContent = data.email;
    els.profileName.textContent = data.displayName ?? "—";
    els.profileLevel.textContent = `Уровень ${data.level}`;
    els.profileReviews.textContent = data.reviewCount ?? 0;

    await loadAchievements();
    await loadReviews();
  } catch (err) {
    logout();
  }
}

async function loadAchievements() {
  try {
    const profile = await api("/profile");
    const list = profile.achievements ?? [];
    els.achievements.innerHTML = "";
    if (list.length === 0) {
      els.achievements.innerHTML = '<li class="muted">Достижений пока нет</li>';
      return;
    }
    list.forEach((a) => {
      const li = document.createElement("li");
      li.innerHTML = `<div class="title">${a.title}</div><div class="desc">${a.description}</div><div class="tag">${a.requiredReviews} отзывов</div>`;
      els.achievements.appendChild(li);
    });
  } catch (err) {
    console.error(err);
  }
}

async function loadReviews() {
  try {
    const items = await api("/reviews");
    els.userReviewsList.innerHTML = "";
    if (items.length === 0) {
      els.userReviewsList.innerHTML = '<div class="muted" style="padding:10px">Вы ещё не оставляли отзывов</div>';
      return;
    }
    items.forEach((r) => {
      const div = document.createElement("div");
      div.className = "review-card-small";
      const date = new Date(r.createdAt).toLocaleDateString();
      const stars = '★'.repeat(r.rating) + '☆'.repeat(5 - r.rating);
      
      const likeActive = r.userVote === 1 ? 'active' : '';
      const dislikeActive = r.userVote === -1 ? 'active' : '';
      
      const safePlaceName = (r.placeName || '').replace(/'/g, "\\'");

      div.innerHTML = `
        <div class="place-name">${r.placeName}</div>
        <div class="rating">${stars} <span style="color:var(--muted);font-size:0.8em;margin-left:6px">${date}</span></div>
        ${r.comment ? `<div style="margin-top:4px;font-size:0.85em;color:var(--text)">${r.comment}</div>` : ''}
        
        <div class="review-footer" style="margin-top: 8px; border-top: 1px solid var(--border); padding-top: 6px;">
            <div class="vote-controls" style="display: flex; gap: 10px;">
                <button class="vote-btn ${likeActive}" onclick="voteReview('${r.id}', true, '${r.placeId}', '${safePlaceName}')">
                    👍 <span class="count">${r.likes}</span>
                </button>
                <button class="vote-btn ${dislikeActive}" onclick="voteReview('${r.id}', false, '${r.placeId}', '${safePlaceName}')">
                    👎 <span class="count">${r.dislikes}</span>
                </button>
            </div>
        </div>
      `;
      els.userReviewsList.appendChild(div);
    });
  } catch (err) {
    console.error(err);
  }
}

function logout() {
  clearToken();
  els.userInfo.classList.add("hidden");
  els.logout.classList.add("hidden");
  els.topbarProfileToggle.classList.add("hidden");
  els.btnShowLogin.classList.remove("hidden");
  els.btnShowRegister.classList.remove("hidden");
  els.profileEmail.textContent = "-";
  els.profileName.textContent = "-";
  els.profileLevel.textContent = "Уровень 1";
  els.profileReviews.textContent = "0";
  els.achievements.innerHTML = "";
  els.userReviewsList.innerHTML = "";
}

// Event wiring
function toggleProfileVisibility() {
    const panel = els.profilePanel;
    // Just toggle the class. CSS handles the sliding.
    // No need to resize map or mess with display:none since it's an overlay.
    panel.classList.toggle("panel-hidden");
}

els.profileClose?.addEventListener("click", toggleProfileVisibility);
els.topbarProfileToggle?.addEventListener("click", toggleProfileVisibility);

function setupCollapsible(header, container) {
    if (!header || !container) return;
    header.addEventListener('click', () => {
        container.classList.toggle('open');
        // container.classList.toggle('hidden'); // Removed to prevent conflict with CSS transitions
        header.classList.toggle('active');
    });
}

setupCollapsible(els.toggleReviews, els.reviewsContainer);
setupCollapsible(els.toggleAchievements, els.achievementsContainer);

els.btnShowLogin?.addEventListener("click", () => showModal("login"));
els.btnShowRegister?.addEventListener("click", () => showModal("register"));
els.modalClose?.addEventListener("click", hideModal);
els.tabs.forEach((tab) => tab.addEventListener("click", () => switchTab(tab.dataset.tab)));
els.logout?.addEventListener("click", logout);

els.loginForm?.addEventListener("submit", async (e) => {
  e.preventDefault();
  els.loginError.textContent = "";
  try {
    const email = document.getElementById("login-email").value.trim();
    const password = document.getElementById("login-password").value;
    const res = await api("/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    });
    saveToken(res.token);
    hideModal();
    await loadProfile();
  } catch (err) {
    const msg = formatError(err);
    els.loginError.innerHTML = "<strong>Ошибка входа:</strong><br>" + msg;
    els.loginError.classList.add("error");
  }
});

// Forgot password button handler - must be inside a deferred function
document.addEventListener("DOMContentLoaded", () => {
  const btnForgotPassword = document.getElementById("btn-forgot-password");
  if (btnForgotPassword) {
    btnForgotPassword.addEventListener("click", async (e) => {
      e.preventDefault();
      const email = prompt("Введите ваш email для сброса пароля:");
      if (!email) return;
      try {
        const res = await fetch(`${apiBase}/auth/forgot-password`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ email }),
        });
        if (res.ok) {
          alert("Письмо для сброса пароля отправлено! Проверьте ваш email.");
        } else {
          let errorData;
          try {
            errorData = await res.json();
          } catch {
            errorData = await res.text();
          }
          alert("Ошибка: " + formatError(errorData));
        }
      } catch (error) {
        alert("Ошибка подключения к серверу");
      }
    });
  }
});

els.registerForm?.addEventListener("submit", async (e) => {
  e.preventDefault();
  els.registerError.textContent = "";
  els.registerError.classList.remove("error", "success");
  try {
    const email = document.getElementById("register-email").value.trim();
    const password = document.getElementById("register-password").value;
    const displayName = document.getElementById("register-name").value.trim();
    const res = await fetch(`${apiBase}/auth/register`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password, displayName }),
    });
    if (res.status === 202) {
      els.registerError.textContent = "Регистрация успешна! Проверьте email для подтверждения.";
      els.registerError.classList.add("success");
      document.getElementById("register-email").value = "";
      document.getElementById("register-password").value = "";
      document.getElementById("register-name").value = "";
      return;
    }
    if (!res.ok) {
      let errorData;
      try {
        errorData = await res.json();
      } catch {
        errorData = await res.text();
      }
      throw errorData;
    }
    hideModal();
  } catch (err) {
    const msg = formatError(err);
    els.registerError.innerHTML = "<strong>Ошибка регистрации:</strong><br>" + msg;
    els.registerError.classList.add("error");
  }
});

els.reviewForm?.addEventListener("submit", async (e) => {
  e.preventDefault();
  els.reviewStatus.textContent = "";
  try {
    const content = els.reviewText.value.trim();
    if (!content) return;
    await api("/reviews", {
      method: "POST",
      body: JSON.stringify({ content }),
    });
    els.reviewText.value = "";
    els.reviewStatus.textContent = "Сохранено";
    await loadProfile();
  } catch (err) {
    els.reviewStatus.innerHTML = '<strong>Ошибка:</strong> ' + formatError(err);
    els.reviewStatus.classList.add("error");
  }
});

// Init
if (getToken()) {
  loadProfile();
}

// ==========================================
// INTEGRATION: Map Logic from marky/app.js
// ==========================================

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
if (typeof ymaps !== 'undefined') {
    ymaps.ready(initMap);
} else {
    console.warn('Yandex Maps API not loaded');
}

async function initMap() {
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
    await loadPlaceReviewsForMap(); // Загрузить отзывы с сервера
    renderPlaces([...basePlaces, ...userAddedPlaces]); // Перерисовать с рейтингами
    setupFilters();
    setupAddButton();
    setupReviewModal();
    
    // Делаем функцию глобально доступной
    window.openReviewForm = openReviewForm;
}

// Загрузка данных из JSON и API
async function loadPlacesFromJson() {
    try {
        // 1. Загружаем статические данные
        const res = await fetch('data/tula-objects.json');
        let dbObjects = await res.json();

        // 2. Загружаем данные из БД
        try {
            const resApi = await fetch(`${apiBase}/places`);
            if (resApi.ok) {
                const apiPlaces = await resApi.json();
                // Мапим API объекты в формат приложения, если нужно, или просто добавляем
                // API возвращает: { id, name, type, latitude, longitude, address }
                const mappedApiPlaces = apiPlaces.map(p => ({
                    id: p.id,
                    name: p.name,
                    type: p.type,
                    lat: p.latitude,
                    lng: p.longitude,
                    address: p.address
                }));
                dbObjects = [...dbObjects, ...mappedApiPlaces];
            }
        } catch (e) {
            console.error('Ошибка загрузки мест из API:', e);
        }

        basePlaces = dbObjects.map((obj, index) => {
            // ОСНОВНОЕ ИСПРАВЛЕНИЕ: Преобразуем id в строку для единообразия
            const id = String(obj.id); // Преобразуем число в строку
            
            const place = {
                id: id, // Теперь это строка
                name: obj.name || 'Мед. объект',
                type: obj.type || 'other_med',
                lat: parseFloat(obj.lat), // JSON uses lat/lng
                lng: parseFloat(obj.lng),
                address: obj.address || 'Адрес не указан',
                avgRating: 0,
                count: 0
            };
            
            // Сохраняем в карту для быстрого поиска
            allPlacesMap.set(id, place);
            
            return place;
        });

        // userAddedPlaces теперь не нужен для персистентности, но оставим для совместимости если что-то еще его использует
        // renderPlaces([...basePlaces, ...userAddedPlaces]);
        renderPlaces(basePlaces);
        console.log('Загружено объектов:', basePlaces.length);
    } catch (e) {
        console.error('Ошибка загрузки данных:', e);
        // alert('Не удалось загрузить объекты. Убедитесь, что data/tula-objects.json существует и валиден.');
        basePlaces = [];
        renderPlaces(basePlaces);
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

async function showObjectReviews(placeId, placeName) {
    if (!els.objectReviewsPanel) return;
    
    els.objectReviewsTitle.textContent = `Отзывы: ${placeName}`;
    els.objectReviewsContent.innerHTML = '<div class="muted">Загрузка...</div>';
    
    try {
        const reviews = await getPlaceReviews(placeId);
        els.objectReviewsContent.innerHTML = "";
        
        if (reviews.length === 0) {
            els.objectReviewsContent.innerHTML = '<div class="muted" style="padding:10px">Отзывов пока нет. Будьте первым!</div>';
            return;
        }
        
        reviews.forEach(r => {
            const div = document.createElement("div");
            div.className = "object-review-card";
            const date = new Date(r.createdAt).toLocaleDateString();
            const stars = '★'.repeat(r.rating) + '☆'.repeat(5 - r.rating);
            
            const isAuthor = currentUser && currentUser.id === r.userId;
            
            // Escape strings for onclick
            const safePlaceName = placeName.replace(/'/g, "\\'");
            const safeComment = (r.comment || '').replace(/'/g, "\\'");

            let actionsHtml = '';
            if (isAuthor) {
                actionsHtml = `
                    <div class="review-actions">
                        <button class="icon-btn small" onclick="editReview('${r.id}', ${r.rating}, '${safeComment}', '${placeId}')" title="Редактировать">✏️</button>
                        <button class="icon-btn small" onclick="deleteReview('${r.id}', '${placeId}', '${safePlaceName}')" title="Удалить">🗑️</button>
                    </div>
                `;
            }

            const likeActive = r.userVote === 1 ? 'active' : '';
            const dislikeActive = r.userVote === -1 ? 'active' : '';

            div.innerHTML = `
                <div class="review-header">
                    <span class="review-author">
                        👤 ${r.userName}
                        <span class="level-badge">Lvl ${r.userLevel || 1}</span>
                    </span>
                    <span class="review-date">${date}</span>
                </div>
                <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px;">
                    <div style="color:#fbbf24;">${stars}</div>
                    ${actionsHtml}
                </div>
                ${r.comment ? `<div class="review-text">${r.comment}</div>` : ''}
                
                <div class="review-footer" style="margin-top: 10px; border-top: 1px solid var(--border); padding-top: 8px;">
                    <div class="vote-controls" style="display: flex; gap: 12px;">
                        <button class="vote-btn ${likeActive}" onclick="voteReview('${r.id}', true, '${placeId}', '${safePlaceName}')">
                            👍 <span class="count">${r.likes}</span>
                        </button>
                        <button class="vote-btn ${dislikeActive}" onclick="voteReview('${r.id}', false, '${placeId}', '${safePlaceName}')">
                            👎 <span class="count">${r.dislikes}</span>
                        </button>
                    </div>
                </div>
            `;
            els.objectReviewsContent.appendChild(div);
        });
    } catch (e) {
        console.error(e);
        els.objectReviewsContent.innerHTML = '<div class="error">Не удалось загрузить отзывы</div>';
    }
}

// Global functions for review actions
window.voteReview = async function(reviewId, isLike, placeId, placeName) {
    if (!currentUser) {
        alert('Войдите, чтобы голосовать');
        return;
    }
    try {
        await api(`/reviews/${reviewId}/vote`, {
            method: 'POST',
            body: JSON.stringify({ isLike })
        });
        
        // Очистить кэш чтобы получить свежие голоса
        placeReviewsCache.delete(placeId);
        
        // Refresh reviews panel if open
        if (els.objectReviewsPanel && !els.objectReviewsPanel.classList.contains('hidden') && els.objectReviewsTitle.textContent.includes(placeName)) {
             await showObjectReviews(placeId, placeName);
        }
        
        // Also refresh profile if we are logged in
        if (getToken()) {
            await loadReviews();
        }
    } catch (e) {
        console.error(e);
        alert('Ошибка при голосовании: ' + formatError(e));
    }
};

window.deleteReview = async function(reviewId, placeId, placeName) {
    if (!confirm('Вы уверены, что хотите удалить этот отзыв?')) return;
    try {
        await api(`/reviews/${reviewId}`, { method: 'DELETE' });
        
        // Очистить кэш
        placeReviewsCache.delete(placeId);
        
        await showObjectReviews(placeId, placeName);
        if (getToken()) {
            await loadProfile();
        }
        
        // Обновить карту
        await loadPlacesFromJson();
    } catch (e) {
        console.error(e);
        alert('Ошибка при удалении: ' + formatError(e));
    }
};

window.editReview = function(reviewId, rating, comment, placeId) {
    // Reuse the review form but change its behavior
    openReviewForm(placeId, reviewId, rating, comment);
};


// Создание метки
function createPlacemark(place) {
    const reviewsInfo = calculateRatingSync(place.id);
    const rating = reviewsInfo.count > 0
        ? `${reviewsInfo.avgRating.toFixed(1)} ⭐ (${reviewsInfo.count} оценок)`
        : 'Оценок пока нет';

    const typeConfig = placeTypes[place.type] || { color: '#999', icon: '📍' };

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

    // Load reviews when clicked
    placemark.events.add('click', () => {
        showObjectReviews(place.id, place.name);
    });

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
    const btn = document.getElementById('add-place-btn');
    if(!btn) return; // Button removed from HTML
    
    btn.addEventListener('click', () => {
        addMode = true;
        alert('Кликните на карте, чтобы указать местоположение');
    });

    const cancelBtn = document.getElementById('cancel-place');
    if(cancelBtn) {
        cancelBtn.addEventListener('click', () => {
            document.getElementById('add-place-modal').style.display = 'none';
            addMode = false;
        });
    }

    const submitBtn = document.getElementById('submit-place');
    if(submitBtn) {
        submitBtn.addEventListener('click', submitNewPlace);
    }
}

function onMapClick(e) {
    if (!addMode) return;
    addMode = false;
    pendingCoords = e.get('coords');
    document.getElementById('add-place-modal').style.display = 'flex';
}

async function submitNewPlace() {
    const name = document.getElementById('place-name').value.trim();
    const type = document.getElementById('place-type').value;

    if (!name) {
        alert('Введите название');
        return;
    }

    const [lat, lng] = pendingCoords;

    try {
        // Отправляем на сервер
        const res = await api('/places', {
            method: 'POST',
            body: JSON.stringify({
                name: name,
                type: type,
                latitude: lat,
                longitude: lng,
                address: `Добавлено пользователем (${lat.toFixed(4)}, ${lng.toFixed(4)})`
            })
        });

        const newPlace = {
            id: String(res.id),
            name: res.name,
            type: res.type,
            lat: res.latitude,
            lng: res.longitude,
            address: res.address,
            avgRating: 0,
            count: 0
        };

        // Добавляем в список и на карту
        basePlaces.push(newPlace);
        allPlacesMap.set(newPlace.id, newPlace);
        renderPlaces(basePlaces);

        document.getElementById('place-name').value = '';
        document.getElementById('add-place-modal').style.display = 'none';
        alert('Объект успешно добавлен!');
    } catch (e) {
        console.error(e);
        alert('Ошибка при добавлении объекта: ' + (e.title || e));
    }
}

// === СИСТЕМА ОТЗЫВОВ (через API) ===

// Кэш отзывов для карты
let placeReviewsCache = new Map();

async function getPlaceReviews(placeId) {
    // Проверяем кэш
    if (placeReviewsCache.has(placeId)) {
        return placeReviewsCache.get(placeId);
    }
    
    try {
        const res = await fetch(`${apiBase}/reviews/place/${placeId}`);
        if (res.ok) {
            const reviews = await res.json();
            placeReviewsCache.set(placeId, reviews);
            return reviews;
        }
    } catch (e) {
        console.error('Ошибка загрузки отзывов:', e);
    }
    return [];
}

async function calculateRating(placeId) {
    const reviews = await getPlaceReviews(placeId);
    if (reviews.length === 0) {
        return { avgRating: 0, count: 0 };
    }
    const sum = reviews.reduce((acc, r) => acc + r.rating, 0);
    return {
        avgRating: sum / reviews.length,
        count: reviews.length
    };
}

// Синхронная версия для начальной отрисовки (использует кэш)
function calculateRatingSync(placeId) {
    const reviews = placeReviewsCache.get(placeId) || [];
    if (reviews.length === 0) {
        return { avgRating: 0, count: 0 };
    }
    const sum = reviews.reduce((acc, r) => acc + r.rating, 0);
    return {
        avgRating: sum / reviews.length,
        count: reviews.length
    };
}

async function openReviewForm(placeId, reviewId = null, rating = 0, comment = '') {
    const idStr = String(placeId);
    const place = allPlacesMap.get(idStr) || basePlaces.find(p => String(p.id) === idStr);
    
    if (!place) {
        alert(`Объект не найден. ID: ${placeId}`);
        return;
    }

    // Проверяем авторизацию
    if (!getToken()) {
        alert('Для добавления отзыва необходимо войти в систему');
        showModal('login');
        return;
    }

    // Если это новый отзыв, проверяем, не оставлял ли пользователь уже отзыв
    if (!reviewId) {
        try {
            const res = await api(`/reviews/check/${idStr}`);
            if (res.hasReview) {
                alert('Вы уже оставили отзыв на этот объект');
                return;
            }
        } catch (e) {
            console.error('Ошибка проверки отзыва:', e);
        }
    }

    window.currentReviewPlaceId = idStr;
    window.currentReviewPlaceName = place.name;
    window.currentReviewId = reviewId;

    document.getElementById('review-place-name').textContent = reviewId ? `Редактирование: ${place.name}` : place.name;
    document.getElementById('review-modal').style.display = 'flex';
    
    // Сбросить форму
    window.selectedRating = rating;
    document.querySelectorAll('#star-rating span').forEach((s, i) => {
        const isActive = i + 1 <= rating;
        s.textContent = isActive ? '★' : '☆';
        s.classList.toggle('star-active', isActive);
    });
    document.getElementById('review-comment').value = comment;
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

    const cancelBtn = document.getElementById('cancel-review');
    if(cancelBtn) {
        cancelBtn.addEventListener('click', () => {
            document.getElementById('review-modal').style.display = 'none';
        });
    }

    const submitBtn = document.getElementById('submit-review');
    if(submitBtn) {
        submitBtn.addEventListener('click', async () => {
            const rating = window.selectedRating;
            const comment = document.getElementById('review-comment').value.trim();
            const placeId = window.currentReviewPlaceId;
            const placeName = window.currentReviewPlaceName;
            const reviewId = window.currentReviewId;

            if (!rating) {
                alert('Пожалуйста, поставьте оценку');
                return;
            }

            try {
                if (reviewId) {
                    await api(`/reviews/${reviewId}`, {
                        method: 'PUT',
                        body: JSON.stringify({
                            rating: rating,
                            comment: comment || null
                        })
                    });
                } else {
                    await api('/reviews', {
                        method: 'POST',
                        body: JSON.stringify({
                            placeId: placeId,
                            placeName: placeName,
                            rating: rating,
                            comment: comment || null
                        })
                    });
                }

                // Очистить кэш для этого места
                placeReviewsCache.delete(placeId);
                
                document.getElementById('review-modal').style.display = 'none';
                
                // Обновить карту и профиль
                await loadPlaceReviewsForMap();
                renderPlaces(basePlaces);
                
                // Обновить профиль если авторизован
                if (getToken()) {
                    await loadProfile();
                }
                
                // Refresh reviews panel if open
                if (els.objectReviewsPanel && els.objectReviewsTitle.textContent.includes(placeName)) {
                    showObjectReviews(placeId, placeName);
                }
                
                alert(reviewId ? 'Отзыв обновлен!' : 'Спасибо за ваш отзыв!');
            } catch (err) {
                const msg = formatError(err);
                alert('Ошибка: ' + msg);
            }
        });
    }
}

// Загрузка всех отзывов для объектов на карте
async function loadPlaceReviewsForMap() {
    const placeIds = basePlaces.map(p => p.id);
    for (const placeId of placeIds) {
        await getPlaceReviews(placeId);
    }
}

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
