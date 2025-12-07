const apiBase = "/api";
const tokenKey = "palmmap_token";
let currentUser = null;

function escapeHtml(text) {
  if (text == null) return '';
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

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
  profilePoints: document.getElementById("profile-points"),
  achievements: document.getElementById("achievements-list"),
  reviewForm: document.getElementById("review-form"),
  reviewText: document.getElementById("review-text"),
  reviewStatus: document.getElementById("review-status"),
  
  profileToggle: document.getElementById("profile-toggle"),
  profilePanel: document.getElementById("profile-panel"),
  userReviewsList: document.getElementById("user-reviews-list"),
  objectReviewsPanel: document.getElementById("object-reviews-panel"),
  objectReviewsContent: document.getElementById("object-reviews-content"),
  objectReviewsTitle: document.getElementById("object-reviews-title"),
  objectReviewsClose: document.getElementById("object-reviews-close"), // New close button

  toggleReviews: document.getElementById("toggle-reviews"),
  reviewsContainer: document.getElementById("user-reviews-container"),
  toggleAchievements: document.getElementById("toggle-achievements"),
  achievementsContainer: document.getElementById("user-achievements-container"),
  
  profileClose: document.getElementById("profile-close"),
  topbarProfileToggle: document.getElementById("topbar-profile-toggle"),

  ratingsBtn: document.getElementById("btn-show-ratings"),
  ratingsModal: document.getElementById("ratings-modal"),
  ratingsClose: document.getElementById("close-ratings"),
  ratingsList: document.getElementById("ratings-list"),
  currentUserRating: document.getElementById("current-user-rating"),
  userRank: document.getElementById("user-rank"),
  userName: document.getElementById("user-name"),
  userPoints: document.getElementById("user-points"),
  
  adminPanelBtn: document.getElementById("btn-admin-panel"),
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

document.addEventListener('DOMContentLoaded', () => {
  const btn = document.getElementById('btn-vk-login');
  if (btn) {
    btn.addEventListener('click', (e) => {
      e.preventDefault();
      window.location.href = '/api/auth/vk/login';
    });
  }

  const urlParams = new URLSearchParams(window.location.search);
  const vkToken = urlParams.get('vk_token');
  if (vkToken) {
    saveToken(vkToken);
    window.history.replaceState({}, document.title, window.location.pathname);
    updateUI();
  }
});

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
  
  if (typeof err === 'string') {
    try {
      const parsed = JSON.parse(err);
      return formatError(parsed);
    } catch (e) {
      return err; // Просто строка
    }
  }

  if (err.status === 401 || err.title === "Unauthorized") {
    return "Неверный email или пароль.";
  }

  if (Array.isArray(err)) {
    return err.map(e => translateIdentityError(e)).join('<br>');
  }

  if (err.message) {
    if (typeof err.message === 'string' && err.message.trim().startsWith('{')) {
        try {
            const parsed = JSON.parse(err.message);
            return formatError(parsed);
        } catch {}
    }
    return err.message;
  }
  
  if (err.errors) {
    return Object.values(err.errors).flat().join('<br>');
  }

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
    els.ratingsBtn.classList.remove("hidden");
    els.btnShowLogin.classList.add("hidden");
    els.btnShowRegister.classList.add("hidden");

    els.profileEmail.textContent = data.email;
    els.profileName.textContent = data.displayName ?? "—";
    els.profileLevel.textContent = `Уровень ${data.level}`;
    els.profileReviews.textContent = data.reviewCount ?? 0;
    
    const profile = await api("/profile");
    if (els.profilePoints) {
      els.profilePoints.textContent = profile.points ?? 0;
    }
    
    if (els.adminPanelBtn) {
      if (profile.isAdmin) {
        els.adminPanelBtn.classList.remove("hidden");
      } else {
        els.adminPanelBtn.classList.add("hidden");
      }
    }

    await loadAchievements();
    await loadReviews();
  } catch (err) {
    logout();
  }
}

function getAchievementCondition(progressType, targetValue) {

  switch (progressType) {
    case 1: // FirstPlaceAdded
      return "Добавить 1 объект на карту";
    case 2: // ReviewsCount
      return `Оценить ${targetValue} уникальных объектов`;
    case 3: // PhotosCount
      return `Добавить ${targetValue} фотографий в отзывы`;
    case 4: // DetailedReviewsCount
      return `Написать ${targetValue} развёрнутых отзывов (более 100 символов)`;
    case 5: // BalancedReviews
      return "Оценить по 2 объекта каждого типа (здоровое питание, спорт, аптеки/алкоголь)";
    case 6: // NewPlacesAdded
      return `Добавить ${targetValue} новых объектов на карту`;
    case 7: // HighRatedHealthyPlaces
      return `Оценить ${targetValue} объектов здорового питания со средним рейтингом 4.5+`;
    case 8: // TopThreeRating
      return "Занять место в топ-3 рейтинга пользователей";
    case 9: // PlacesReviewedByOthers
      return `Добавить ${targetValue} объектов, которые оценят другие пользователи`;
    case 10: // AllRatingsUsed
      return "Использовать все оценки от 1 до 5 в отзывах";
    case 11: // PlacesInOneDay
      return `Добавить ${targetValue} объекта за один день`;
    default:
      return `Выполнить условие (${targetValue})`;
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
    
    if (profile.newlyEarnedAchievements && profile.newlyEarnedAchievements.length > 0) {
      profile.newlyEarnedAchievements.forEach(achievement => {
        showAchievementNotification(achievement);
      });
    }
    
    list.forEach((a) => {
      const li = document.createElement("li");
      li.className = `achievement-item ${a.earned ? 'achievement-earned' : 'achievement-locked'}`;
      
      const progressHtml = a.earned 
        ? '' 
        : `<div class="achievement-progress">
             <div class="progress-bar">
               <div class="progress-fill" style="width: ${a.progress}%"></div>
             </div>
             <span class="progress-text">${a.progress}%</span>
           </div>`;
      
      const condition = getAchievementCondition(a.progressType, a.targetValue);
      
      li.innerHTML = `
        <div class="achievement-icon">${escapeHtml(a.icon)}</div>
        <div class="achievement-content">
          <div class="achievement-title">${escapeHtml(a.title)}</div>
          <div class="achievement-desc">${escapeHtml(condition)}</div>
          ${progressHtml}
        </div>
      `;
      els.achievements.appendChild(li);
    });
  } catch (err) {
    console.error(err);
  }
}

function showAchievementNotification(achievement) {
  const modal = document.createElement('div');
  modal.className = 'achievement-notification-modal';
  modal.innerHTML = `
    <div class="achievement-notification-content">
      <div class="achievement-notification-icon">${escapeHtml(achievement.icon)}</div>
      <h3>🎉 Поздравляем!</h3>
      <p class="achievement-notification-subtitle">Достижение выполнено!</p>
      <p class="achievement-notification-title">${escapeHtml(achievement.title)}</p>
      <p class="achievement-notification-desc">${escapeHtml(achievement.description)}</p>
      <button class="achievement-notification-close primary">Отлично!</button>
    </div>
  `;
  
  document.body.appendChild(modal);
  
  setTimeout(() => modal.classList.add('show'), 10);
  
  const closeBtn = modal.querySelector('.achievement-notification-close');
  closeBtn.addEventListener('click', () => {
    modal.classList.remove('show');
    setTimeout(() => modal.remove(), 300);
  });
  
  modal.addEventListener('click', (e) => {
    if (e.target === modal) {
      modal.classList.remove('show');
      setTimeout(() => modal.remove(), 300);
    }
  });
}

async function checkAchievementsAfterAction() {
  try {
    const profile = await api("/profile");
    if (profile.newlyEarnedAchievements && profile.newlyEarnedAchievements.length > 0) {
      profile.newlyEarnedAchievements.forEach(achievement => {
        showAchievementNotification(achievement);
      });
    }
  } catch (err) {
    console.error('Ошибка проверки достижений:', err);
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
      const stars = '★'.repeat(Math.min(5, Math.max(0, r.rating))) + '☆'.repeat(5 - Math.min(5, Math.max(0, r.rating)));
      
      const likeActive = r.userVote === 1 ? 'active' : '';
      const dislikeActive = r.userVote === -1 ? 'active' : '';
      
      const safePlaceName = escapeHtml(r.placeName || '').replace(/'/g, "\\'");
      const safeComment = escapeHtml(r.comment);
      const safeRejectionReason = escapeHtml(r.rejectionReason);

      let statusBadge = '';
      if (r.moderationStatus === 'pending') {
        statusBadge = '<span class="status-badge status-pending">⏳ На модерации</span>';
      } else if (r.moderationStatus === 'rejected') {
        statusBadge = `<span class="status-badge status-rejected">❌ Отклонён${safeRejectionReason ? `: ${safeRejectionReason}` : ''}</span>`;
      }

      div.innerHTML = `
        <div class="place-name">${escapeHtml(r.placeName)} ${statusBadge}</div>
        <div class="rating">${stars} <span style="color:var(--muted);font-size:0.8em;margin-left:6px">${escapeHtml(date)}</span></div>
        ${r.comment ? `<div style="margin-top:4px;font-size:0.85em;color:var(--text)">${safeComment}</div>` : ''}
        
        <div class="review-footer" style="margin-top: 8px; border-top: 1px solid var(--border); padding-top: 6px;">
            <div class="vote-controls" style="display: flex; gap: 10px;">
                <button class="vote-btn ${likeActive}" onclick="voteReview('${escapeHtml(r.id)}', true, '${escapeHtml(r.placeId)}', '${safePlaceName}')">
                    👍 <span class="count">${parseInt(r.likes) || 0}</span>
                </button>
                <button class="vote-btn ${dislikeActive}" onclick="voteReview('${escapeHtml(r.id)}', false, '${escapeHtml(r.placeId)}', '${safePlaceName}')">
                    👎 <span class="count">${parseInt(r.dislikes) || 0}</span>
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
  els.ratingsBtn.classList.add("hidden");
  if (els.adminPanelBtn) els.adminPanelBtn.classList.add("hidden");
  els.btnShowLogin.classList.remove("hidden");
  els.btnShowRegister.classList.remove("hidden");
  els.profileEmail.textContent = "-";
  els.profileName.textContent = "-";
  els.profileLevel.textContent = "Уровень 1";
  els.profileReviews.textContent = "0";
  els.achievements.innerHTML = "";
  els.userReviewsList.innerHTML = "";
}

async function loadRatings() {
    try {
        const data = await api('/profile/ratings');
        console.log('Ratings data:', data);

        els.ratingsList.innerHTML = '';
        data.top10.forEach(user => {
            const isCurrentUser = currentUser && user.id === currentUser.id;
            const div = document.createElement('div');
            div.style.padding = '10px 8px';
            div.style.borderBottom = '1px solid var(--border)';
            div.style.borderRadius = '4px';
            div.style.marginBottom = '4px';
            
            const rawName = user.displayName || user.DisplayName || 'Аноним';
            const name = isCurrentUser ? 'Вы' : escapeHtml(rawName);
            const points = parseInt(user.points || user.Points) || 0;
            const level = parseInt(user.level || user.Level) || 1;
            
            let medal = '';
            let medalColor = '';
            let bgColor = '';
            let textColor = '';
            
            if (user.position === 1) {
                medal = '🥇';
                medalColor = '#D4A000'; // Тёмное золото для светлой темы
                bgColor = 'rgba(255, 215, 0, 0.15)';
                textColor = 'color:var(--text);'; // Используем цвет текста темы
            } else if (user.position === 2) {
                medal = '🥈';
                medalColor = '#808080'; // Тёмное серебро
                bgColor = 'rgba(192, 192, 192, 0.15)';
                textColor = 'color:var(--text);';
            } else if (user.position === 3) {
                medal = '🥉';
                medalColor = '#8B4513'; // Тёмная бронза
                bgColor = 'rgba(205, 127, 50, 0.15)';
                textColor = 'color:var(--text);';
            } else {
                textColor = 'color:var(--text);';
            }
            
            const currentUserBg = isCurrentUser ? 'border-left:3px solid #2196F3; padding-left:5px;' : '';
            const combinedBg = bgColor ? `background:${bgColor}; ${currentUserBg}` : currentUserBg;
            
            const medalHtml = medal ? `<span style="margin-right:6px; font-size:16px;">${medal}</span>` : '';
            const medalStyle = medalColor ? `style="color:${medalColor}; font-weight:bold; ${textColor}"` : `style="${textColor}"`;
            
            div.innerHTML = `<div ${medalStyle}><span style="font-weight:bold; margin-right:4px;">${medalHtml}${parseInt(user.position) || 0}.</span> <span style="font-size:16px; font-weight:600;">${name}</span> — <span style="font-weight:600;">${points}</span> <span style="font-weight:600;">очков</span> (Lvl <span style="font-weight:600;">${level}</span>)</div>`;
            div.style.cssText += combinedBg;
            els.ratingsList.appendChild(div);
        });

        if (data.currentUserPosition > 10) {
            els.currentUserRating.style.display = 'block';
            els.userRank.textContent = data.currentUserPosition;
            els.userName.textContent = escapeHtml(data.currentUser.displayName || data.currentUser.DisplayName || 'Аноним');
            els.userPoints.textContent = parseInt(data.currentUser.points || data.currentUser.Points) || 0;
        } else {
            els.currentUserRating.style.display = 'none';
        }
    } catch (err) {
        console.error('Ошибка загрузки рейтинга:', err);
        els.ratingsList.innerHTML = '<div style="color:var(--text-secondary);">Ошибка загрузки рейтинга</div>';
    }
}

function toggleProfileVisibility() {
    const panel = els.profilePanel;
    panel.classList.toggle("panel-hidden");
}

els.profileClose?.addEventListener("click", toggleProfileVisibility);
els.topbarProfileToggle?.addEventListener("click", toggleProfileVisibility);

els.ratingsBtn?.addEventListener("click", async () => {
    els.ratingsModal.style.display = 'flex';
    els.ratingsModal.style.justifyContent = 'center';
    await loadRatings();
});

els.ratingsClose?.addEventListener("click", () => {
    els.ratingsModal.style.display = 'none';
});

els.ratingsModal?.addEventListener("click", (e) => {
    if (e.target === els.ratingsModal) {
        els.ratingsModal.style.display = 'none';
    }
});

function setupCollapsible(header, container) {
    if (!header || !container) return;
    header.addEventListener('click', () => {
        container.classList.toggle('open');
        header.classList.toggle('active');
    });
}

setupCollapsible(els.toggleReviews, els.reviewsContainer);
setupCollapsible(els.toggleAchievements, els.achievementsContainer);

els.objectReviewsClose?.addEventListener("click", () => {
    els.objectReviewsPanel.classList.remove("open");
});

function setupMobileTouchHandlers() {
    if (els.objectReviewsPanel) {
        let touchStartY = 0;
        let touchEndY = 0;
        let isScrolling = false;

        els.objectReviewsPanel.addEventListener('touchstart', (e) => {
            touchStartY = e.touches[0].clientY;
            isScrolling = false;
        }, { passive: true });

        els.objectReviewsPanel.addEventListener('touchmove', (e) => {
            const touchY = e.touches[0].clientY;
            const scrollTop = els.objectReviewsContent?.scrollTop || 0;
            
            if (scrollTop > 0) {
                isScrolling = true;
                return;
            }
            
            if (touchY > touchStartY && touchY - touchStartY > 10) {
                isScrolling = false;
            }
        }, { passive: true });

        els.objectReviewsPanel.addEventListener('touchend', (e) => {
            if (isScrolling) return;
            
            touchEndY = e.changedTouches[0].clientY;
            const swipeDistance = touchEndY - touchStartY;
            
            if (swipeDistance > 50 && !els.objectReviewsContent?.scrollTop) {
                els.objectReviewsPanel.classList.remove("open");
            }
        }, { passive: true });
    }

    if (els.profilePanel) {
        let touchStartX = 0;
        let touchEndX = 0;

        els.profilePanel.addEventListener('touchstart', (e) => {
            touchStartX = e.touches[0].clientX;
        }, { passive: true });

        els.profilePanel.addEventListener('touchend', (e) => {
            touchEndX = e.changedTouches[0].clientX;
            const swipeDistance = touchEndX - touchStartX;
            
            if (swipeDistance > 100) {
                els.profilePanel.classList.add("panel-hidden");
            }
        }, { passive: true });
    }
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', setupMobileTouchHandlers);
} else {
    setupMobileTouchHandlers();
}

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


if (getToken()) {
  loadProfile();
}


let myMap;
let placemarks = [];
let userAddedPlaces = [];
let basePlaces = [];
let addMode = false;
let pendingCoords = null;
let clusterer;

let allPlacesMap = new Map();

let allPlacemarks = [];

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

if (typeof ymaps !== 'undefined') {
    ymaps.ready(initMap);
} else {
    console.warn('Yandex Maps API not loaded');
}

async function initMap() {
    const MyBalloonItemLayout = ymaps.templateLayoutFactory.createClass(
        '<div class="cluster-balloon-item">' +
            '<h4>$[properties.balloonContentHeader]</h4>' +
            '<div>$[properties.balloonContentBody]</div>' +
        '</div>'
    );
    
    ymaps.layout.storage.add('my#balloonItemLayout', MyBalloonItemLayout);

    const center = [54.1934, 37.6179]; // Тула
    const zoom = 10;
    const bounds = [[53.2, 35.2], [54.8, 39.8]]; // Тульская область

    myMap = new ymaps.Map('map', {
        center: center,
        zoom: zoom,
        controls: ['zoomControl', 'typeSelector', 'fullscreenControl']
    }, {
        restrictMapArea: bounds
    });

    myMap.events.add('click', onMapClick);
    
    initClusterer();
    
    await loadPlacesFromJson();
    await loadPlaceReviewsForMap(); // Загрузить отзывы с сервера
    createAllPlacemarks(); // Создаем все метки
    applyFilters(); // Применяем фильтры с кластеризацией
    setupFilters();
    setupAddButton();
    setupReviewModal();
    
    setupZoomBehavior();
    
    window.openReviewForm = openReviewForm;
}

function initClusterer() {
    clusterer = new ymaps.Clusterer({
        preset: 'islands#invertedBlueClusterIcons',
        clusterDisableClickZoom: false,
        clusterOpenBalloonOnClick: true,
        
        clusterBalloonContentLayout: 'cluster#balloonCarousel',
        clusterBalloonItemContentLayout: 'my#balloonItemLayout',
        clusterBalloonPanelMaxMapArea: 0,
        clusterBalloonContentLayoutWidth: 300,
        clusterBalloonContentLayoutHeight: 200,
        clusterBalloonPagerSize: 5,
        
        gridSize: 80,
        groupByCoordinates: false,
        minClusterSize: 2,
        
        clusterIconLayout: 'default#pieChart',
        clusterIconPieChartRadius: 25,
        clusterIconPieChartCoreRadius: 15,
        clusterIconPieChartStrokeWidth: 3,
        
        hasBalloon: true,
        hasHint: false,
        zoomMargin: 50,
        
        clusterHideIconsOnSingleObject: true
    });
    
    myMap.geoObjects.add(clusterer);
}

function setupZoomBehavior() {
    let lastZoom = myMap.getZoom();
    
    myMap.events.add('boundschange', function (e) {
        const newZoom = e.get('newZoom');
        
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

async function loadPlacesFromJson() {
    try {
        const res = await fetch('/data/tula-objects.json');
        
        if (!res.ok) {
            throw new Error(`HTTP error! status: ${res.status}`);
        }
        
        let dbObjects = await res.json();

        try {
            const resApi = await fetch(`${apiBase}/places`);
            if (resApi.ok) {
                const apiPlaces = await resApi.json();
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
            
            allPlacesMap.set(id, place);
            
            return place;
        });

        console.log('Загружено объектов из JSON:', basePlaces.length);
    } catch (e) {
        console.error('Ошибка загрузки данных:', e);
        console.error('Детали ошибки:', {
            message: e.message,
            stack: e.stack,
            url: '/data/tula-objects.json'
        });
        
        const errorMsg = e.message || 'Неизвестная ошибка';
        showNotification(`Ошибка загрузки данных: ${errorMsg}. Проверьте консоль браузера.`, 'error');
        
        basePlaces = [];
        createAllPlacemarks();
        applyFilters();
    }
}

function createAllPlacemarks() {
    allPlacemarks = [];
    
    basePlaces.forEach(place => {
        if (!place.lat || !place.lng) return;
        const pm = createPlacemark(place);
        allPlacemarks.push(pm);
    });
    
    userAddedPlaces.forEach(place => {
        if (!place.lat || !place.lng) return;
        const pm = createPlacemark(place);
        allPlacemarks.push(pm);
    });
}

function renderPlaces(places) {
    createAllPlacemarks();
    applyFilters();
}

async function showObjectReviews(placeId, placeName) {
    if (!els.objectReviewsPanel) return;
    
    els.objectReviewsPanel.classList.add("open");

    els.objectReviewsTitle.textContent = `Отзывы: ${placeName}`;
    els.objectReviewsContent.innerHTML = '<div class="muted">Загрузка...</div>';
    
    try {
        const reviews = await getPlaceReviews(placeId);
        els.objectReviewsContent.innerHTML = "";
        
        if (reviews.length === 0) {
            els.objectReviewsContent.innerHTML = '<div class="muted" style="padding:10px">Отзывов пока нет. Будьте первым!</div>';
            return;
        }
        
        const place = allPlacesMap.get(String(placeId)) || basePlaces.find(p => String(p.id) === String(placeId));
        let placeCriteria = {};
        if (place && place.type) {
            try {
                placeCriteria = await api(`/reviews/criteria/${place.type}`);
            } catch (e) {
                console.error('Ошибка загрузки критериев:', e);
            }
        }
        
        reviews.forEach(r => {
            const div = document.createElement("div");
            div.className = "object-review-card";
            const date = new Date(r.createdAt).toLocaleDateString();
            const rating = Math.min(5, Math.max(0, parseInt(r.rating) || 0));
            const stars = '★'.repeat(rating) + '☆'.repeat(5 - rating);
            
            const isAuthor = currentUser && currentUser.id === r.userId;
            
            const safePlaceName = escapeHtml(placeName).replace(/'/g, "\\'");
            const safeComment = escapeHtml(r.comment || '').replace(/'/g, "\\'");
            const safePhotoUrl = escapeHtml(r.photoUrl || '').replace(/'/g, "\\'");
            const safeReviewId = escapeHtml(r.id);
            const safePlaceId = escapeHtml(placeId);

            let actionsHtml = '';
            if (isAuthor) {
                const criteriaRatingsJson = r.criteriaRatings ? JSON.stringify(r.criteriaRatings).replace(/'/g, "\\'") : 'null';
                actionsHtml = `
                    <div class="review-actions">
                        <button class="icon-btn small" onclick="editReview('${safeReviewId}', ${rating}, '${safeComment}', '${safePlaceId}', '${safePhotoUrl}', ${criteriaRatingsJson}, ${r.isDirectRating !== false})" title="Редактировать">✏️</button>
                        <button class="icon-btn small" onclick="deleteReview('${safeReviewId}', '${safePlaceId}', '${safePlaceName}')" title="Удалить">🗑️</button>
                    </div>
                `;
            }

            const likeActive = r.userVote === 1 ? 'active' : '';
            const dislikeActive = r.userVote === -1 ? 'active' : '';

            let photoHtml = '';
            if (r.photoUrl) {
                const photoUrl = r.photoUrl.startsWith('/uploads/') ? escapeHtml(r.photoUrl) : '';
                if (photoUrl) {
                    photoHtml = `<div class="review-photo"><img src="${photoUrl}" alt="Фото отзыва" style="max-width:100%; max-height:200px; border-radius:8px; margin-top:8px; cursor:pointer;" onclick="window.open('${photoUrl}', '_blank')"></div>`;
                }
            }
            
            const displayName = (currentUser && r.userId === currentUser.id) ? 'Вы' : escapeHtml(r.userName);
            
            let criteriaHtml = '';
            if (r.criteriaRatings && !r.isDirectRating && Object.keys(r.criteriaRatings).length > 0) {
                const criteriaKeys = Object.keys(r.criteriaRatings);
                const criteriaList = criteriaKeys.map(key => {
                    const value = r.criteriaRatings[key];
                    const criterionStars = '★'.repeat(value) + '☆'.repeat(5 - value);
                    const criterionName = placeCriteria[key] || key; // Используем название из критериев или ключ
                    return `<div style="display:flex;justify-content:space-between;align-items:center;padding:4px 0;border-bottom:1px solid var(--border);">
                        <span style="font-size:0.9em;">${escapeHtml(criterionName)}</span>
                        <span style="color:#fbbf24;font-size:0.9em;">${criterionStars}</span>
                    </div>`;
                }).join('');
                
                criteriaHtml = `
                    <div style="margin-top:8px;padding-top:8px;border-top:1px solid var(--border);">
                        <div style="display:flex;justify-content:space-between;align-items:center;cursor:pointer;" onclick="const details = this.nextElementSibling; const arrow = this.querySelector('span:last-child'); details.style.display = details.style.display === 'none' ? 'block' : 'none'; arrow.textContent = details.style.display === 'none' ? '▼' : '▲';">
                            <span style="font-weight:500;font-size:0.9em;">Критерии оценки</span>
                            <span>▼</span>
                        </div>
                        <div style="display:none;margin-top:8px;">
                            ${criteriaList}
                        </div>
                    </div>
                `;
            }
            
            div.innerHTML = `
                <div class="review-header">
                    <span class="review-author">
                        👤 ${displayName}
                        <span class="level-badge">Lvl ${parseInt(r.userLevel) || 1}</span>
                    </span>
                    <span class="review-date">${escapeHtml(date)}</span>
                </div>
                <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px;">
                    <div style="color:#fbbf24;">${stars}</div>
                    ${actionsHtml}
                </div>
                ${criteriaHtml}
                ${r.comment ? `<div class="review-text">${escapeHtml(r.comment)}</div>` : ''}
                ${photoHtml}
                
                <div class="review-footer" style="margin-top: 10px; border-top: 1px solid var(--border); padding-top: 8px;">
                    <div class="vote-controls" style="display: flex; gap: 12px;">
                        <button class="vote-btn ${likeActive}" onclick="voteReview('${safeReviewId}', true, '${safePlaceId}', '${safePlaceName}')">
                            👍 <span class="count">${parseInt(r.likes) || 0}</span>
                        </button>
                        <button class="vote-btn ${dislikeActive}" onclick="voteReview('${safeReviewId}', false, '${safePlaceId}', '${safePlaceName}')">
                            👎 <span class="count">${parseInt(r.dislikes) || 0}</span>
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

window.voteReview = async function(reviewId, isLike, placeId, placeName) {
    if (!currentUser) {
        showNotification('Войдите, чтобы голосовать', 'error');
        return;
    }
    try {
        await api(`/reviews/${reviewId}/vote`, {
            method: 'POST',
            body: JSON.stringify({ isLike })
        });
        
        placeReviewsCache.delete(placeId);
        
        if (els.objectReviewsPanel && !els.objectReviewsPanel.classList.contains('hidden') && els.objectReviewsTitle.textContent.includes(placeName)) {
             await showObjectReviews(placeId, placeName);
        }
        
        if (getToken()) {
            await loadReviews();
        }
    } catch (e) {
        console.error(e);
        showNotification('Ошибка при голосовании: ' + formatError(e), 'error');
    }
};

window.deleteReview = async function(reviewId, placeId, placeName) {
    if (!confirm('Вы уверены, что хотите удалить этот отзыв?')) return;
    try {
        await api(`/reviews/${reviewId}`, { method: 'DELETE' });
        
        placeReviewsCache.delete(placeId);
        
        await showObjectReviews(placeId, placeName);
        if (getToken()) {
            await loadProfile();
        }
        
        await loadPlacesFromJson();
    } catch (e) {
        console.error(e);
        showNotification('Ошибка при удалении: ' + formatError(e), 'error');
    }
};

window.editReview = function(reviewId, rating, comment, placeId, photoUrl, criteriaRatings = null, isDirectRating = true) {
    openReviewForm(placeId, reviewId, rating, comment, photoUrl, criteriaRatings, isDirectRating);
};


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

    placemark.properties.set({
        placeType: place.type,
        placeId: place.id,
        placeName: place.name
    });

    placemark.events.add('click', () => {
        showObjectReviews(place.id, place.name);
    });

    return placemark;
}

function applyFilters() {
    if (!clusterer) return;
    
    const activeTypes = Array.from(document.querySelectorAll('#filters input:checked'))
        .map(cb => cb.dataset.type);

    const visiblePlacemarks = allPlacemarks.filter(pm => {
        const type = pm.properties.get('placeType');
        return activeTypes.includes(type);
    });
    
    clusterer.removeAll();
    
    if (visiblePlacemarks.length > 0) {
        clusterer.add(visiblePlacemarks);
    }
    
    allPlacemarks.forEach(pm => {
        const type = pm.properties.get('placeType');
        const shouldBeVisible = activeTypes.includes(type);
        
        pm.options.set('visible', shouldBeVisible);
    });
}

function setupFilters() {
    const checkboxes = document.querySelectorAll('#filters input');
    
    checkboxes.forEach(cb => {
        const clickType = cb.dataset.type;
        let originalState = null; // Сохраняем исходное состояние перед двойным кликом
        let changeTimeout = null; // Таймаут для обработки одиночного клика
        
        cb.addEventListener('mousedown', function() {
            originalState = cb.checked;
        });
        
        cb.addEventListener('change', function() {
            if (changeTimeout) {
                clearTimeout(changeTimeout);
            }
            
            changeTimeout = setTimeout(() => {
                applyFilters();
                changeTimeout = null;
            }, 300); // Небольшая задержка для проверки двойного клика
        });
        
        cb.addEventListener('dblclick', function(e) {
            e.preventDefault();
            e.stopPropagation();
            
            if (changeTimeout) {
                clearTimeout(changeTimeout);
                changeTimeout = null;
            }
            
            if (originalState !== null) {
                cb.checked = originalState;
            }
            
            handleDoubleClick(clickType);
            
            originalState = null;
        });
        
        const label = cb.closest('label');
        if (label) {
            label.addEventListener('dblclick', function(e) {
                e.preventDefault();
                e.stopPropagation();
                
                if (changeTimeout) {
                    clearTimeout(changeTimeout);
                    changeTimeout = null;
                }
                
                const currentState = cb.checked;
                
                if (originalState !== null) {
                    cb.checked = originalState;
                } else {
                    cb.checked = !currentState;
                }
                
                handleDoubleClick(clickType);
                
                originalState = null;
            });
        }
    });

    const filtersToggle = document.getElementById('filters-toggle');
    const filtersPanel = document.getElementById('filters');
    
    if (filtersToggle && filtersPanel) {
        filtersToggle.addEventListener('click', () => {
            filtersPanel.classList.toggle('collapsed');
        });
    }
}

function handleDoubleClick(typeToShow) {
    const allCheckboxes = document.querySelectorAll('#filters input');
    const allTypes = Array.from(allCheckboxes).map(cb => cb.dataset.type);
    
    const checkedCheckboxes = Array.from(allCheckboxes).filter(cb => cb.checked);
    const checkedTypes = checkedCheckboxes.map(cb => cb.dataset.type);
    
    if (checkedTypes.length === 1 && checkedTypes[0] === typeToShow) {
        allCheckboxes.forEach(cb => {
            cb.checked = true;
        });
        showNotification('Показаны все типы объектов');
    } else {
        allCheckboxes.forEach(cb => {
            cb.checked = (cb.dataset.type === typeToShow);
        });
        const friendlyName = getFriendlyTypeName(typeToShow);
        showNotification(`Показаны только: ${friendlyName}`);
    }
    
    applyFilters();
}

function setupAddButton() {
    const btn = document.getElementById('add-place-btn');
    if(!btn) return; // Button removed from HTML
    
    btn.addEventListener('click', () => {
        addMode = true;
        showNotification('Кликните на карте, чтобы указать местоположение нового объекта');
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
        showNotification('Введите название объекта', 'error');
        return;
    }

    const [lat, lng] = pendingCoords;

    try {
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

        basePlaces.push(newPlace);
        allPlacesMap.set(newPlace.id, newPlace);
        
        const newPlacemark = createPlacemark(newPlace);
        allPlacemarks.push(newPlacemark);
        
        applyFilters();

        document.getElementById('place-name').value = '';
        document.getElementById('add-place-modal').style.display = 'none';
        showNotification(`Объект "${name}" успешно добавлен!`, 'success');
        
        if (getToken()) {
            await checkAchievementsAfterAction();
        }
    } catch (e) {
        console.error(e);
        showNotification('Ошибка при добавлении объекта: ' + (e.title || formatError(e)), 'error');
    }
}


let placeReviewsCache = new Map();

async function getPlaceReviews(placeId) {
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

async function openReviewForm(placeId, reviewId = null, rating = 0, comment = '', photoUrl = null, criteriaRatings = null, isDirectRating = true) {
    const idStr = String(placeId);
    const place = allPlacesMap.get(idStr) || basePlaces.find(p => String(p.id) === idStr);
    
    if (!place) {
        showNotification(`Объект не найден. ID: ${placeId}`, 'error');
        return;
    }

    if (!getToken()) {
        showNotification('Для добавления отзыва необходимо войти в систему', 'error');
        showModal('login');
        return;
    }

    if (!reviewId) {
        try {
            const res = await api(`/reviews/check/${idStr}`);
            if (res.hasReview) {
                showNotification('Вы уже оставили отзыв на этот объект', 'error');
                return;
            }
        } catch (e) {
            console.error('Ошибка проверки отзыва:', e);
        }
    }

    window.currentReviewPlaceId = idStr;
    window.currentReviewPlaceName = place.name;
    window.currentReviewId = reviewId;
    window.currentReviewPlaceType = place.type;

    window.currentReviewHasPhoto = !!photoUrl;
    window.currentReviewDeletePhoto = false;

    document.getElementById('review-place-name').textContent = reviewId ? `Редактирование: ${place.name}` : place.name;
    document.getElementById('review-modal').style.display = 'flex';
    
    let criteria = {};
    try {
        const criteriaRes = await api(`/reviews/criteria/${place.type}`);
        criteria = criteriaRes || {};
    } catch (e) {
        console.error('Ошибка загрузки критериев:', e);
    }

    window.currentReviewCriteria = criteria;

    const useCriteria = !isDirectRating && criteriaRatings && Object.keys(criteriaRatings).length > 0;
    document.getElementById('rating-type-general').checked = !useCriteria;
    document.getElementById('rating-type-criteria').checked = useCriteria;
    
    document.getElementById('star-rating').style.display = useCriteria ? 'none' : 'flex';
    document.getElementById('criteria-rating').style.display = useCriteria ? 'block' : 'none';

    window.selectedRating = rating;
    document.querySelectorAll('#star-rating span').forEach((s, i) => {
        const isActive = i + 1 <= rating;
        s.textContent = isActive ? '★' : '☆';
        s.classList.toggle('star-active', isActive);
    });

    const criteriaContainer = document.getElementById('criteria-rating');
    criteriaContainer.innerHTML = '';
    
    if (Object.keys(criteria).length > 0) {
        Object.entries(criteria).forEach(([key, name]) => {
            const div = document.createElement('div');
            div.style.marginBottom = '12px';
            div.innerHTML = `
                <label style="display:block; margin-bottom:4px; font-weight:500;">${escapeHtml(name)}</label>
                <div class="criteria-star-rating" data-criterion="${escapeHtml(key)}" style="display:flex; gap:4px;">
                    <span data-value="1" style="font-size:24px; cursor:pointer; color:#ddd;">☆</span>
                    <span data-value="2" style="font-size:24px; cursor:pointer; color:#ddd;">☆</span>
                    <span data-value="3" style="font-size:24px; cursor:pointer; color:#ddd;">☆</span>
                    <span data-value="4" style="font-size:24px; cursor:pointer; color:#ddd;">☆</span>
                    <span data-value="5" style="font-size:24px; cursor:pointer; color:#ddd;">☆</span>
                </div>
            `;
            criteriaContainer.appendChild(div);

            if (criteriaRatings && criteriaRatings[key]) {
                const value = criteriaRatings[key];
                div.querySelectorAll('.criteria-star-rating span').forEach((s, i) => {
                    const isActive = i + 1 <= value;
                    s.textContent = isActive ? '★' : '☆';
                    s.style.color = isActive ? '#fbbf24' : '#ddd';
                });
            }

            div.querySelectorAll('.criteria-star-rating span').forEach(star => {
                star.addEventListener('click', function() {
                    const criterionKey = this.closest('.criteria-star-rating').dataset.criterion;
                    const value = parseInt(this.dataset.value);
                    
                    if (!window.selectedCriteriaRatings) {
                        window.selectedCriteriaRatings = {};
                    }
                    window.selectedCriteriaRatings[criterionKey] = value;

                    this.closest('.criteria-star-rating').querySelectorAll('span').forEach((s, i) => {
                        const isActive = i + 1 <= value;
                        s.textContent = isActive ? '★' : '☆';
                        s.style.color = isActive ? '#fbbf24' : '#ddd';
                    });
                });
            });
        });
    }

    if (criteriaRatings) {
        window.selectedCriteriaRatings = { ...criteriaRatings };
    } else {
        window.selectedCriteriaRatings = {};
    }

    document.getElementById('review-comment').value = comment;

    const photoPreview = document.getElementById('photo-preview');
    const photoPreviewImg = document.getElementById('photo-preview-img');
    const photoInput = document.getElementById('review-photo');
    const removePhotoBtn = document.getElementById('remove-photo');

    if (photoUrl) {
        if (photoPreview && photoPreviewImg) {
            photoPreviewImg.src = photoUrl;
            photoPreview.style.display = 'flex';
            photoPreview.style.alignItems = 'center';
        }
    } else {
        if (photoPreview) photoPreview.style.display = 'none';
        if (photoPreviewImg) photoPreviewImg.src = '';
    }

    if (photoInput) photoInput.value = '';
    if (removePhotoBtn) {
        removePhotoBtn.style.display = photoUrl ? 'inline-block' : 'none';
    }
}

function setupReviewModal() {
    document.getElementById('rating-type-general')?.addEventListener('change', function() {
        if (this.checked) {
            document.getElementById('star-rating').style.display = 'flex';
            document.getElementById('criteria-rating').style.display = 'none';
            window.selectedCriteriaRatings = {};
        }
    });

    document.getElementById('rating-type-criteria')?.addEventListener('change', function() {
        if (this.checked) {
            document.getElementById('star-rating').style.display = 'none';
            document.getElementById('criteria-rating').style.display = 'block';
            window.selectedRating = 0;
        }
    });

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

    const photoInput = document.getElementById('review-photo');
    const photoPreview = document.getElementById('photo-preview');
    const photoPreviewImg = document.getElementById('photo-preview-img');
    const removePhotoBtn = document.getElementById('remove-photo');

    if (photoInput) {
        photoInput.addEventListener('change', (e) => {
            const file = e.target.files[0];
            window.currentReviewDeletePhoto = false;

            if (file) {
                if (file.size > 5 * 1024 * 1024) {
                    alert('Размер файла не должен превышать 5MB');
                    photoInput.value = '';
                    return;
                }
                
                const reader = new FileReader();
                reader.onload = (ev) => {
                    photoPreviewImg.src = ev.target.result;
                    photoPreview.style.display = 'flex';
                    photoPreview.style.alignItems = 'center';
                };
                reader.readAsDataURL(file);
            }
        });
    }

    if (removePhotoBtn) {
        removePhotoBtn.addEventListener('click', () => {
            if (window.currentReviewHasPhoto) {
                window.currentReviewDeletePhoto = true;
            }

            photoInput.value = '';
            photoPreview.style.display = 'none';
            photoPreviewImg.src = '';
            removePhotoBtn.style.display = 'none';
        });
    }

    const cancelBtn = document.getElementById('cancel-review');
    if(cancelBtn) {
        cancelBtn.addEventListener('click', () => {
            document.getElementById('review-modal').style.display = 'none';
            photoInput.value = '';
            photoPreview.style.display = 'none';
        });
    }

    const submitBtn = document.getElementById('submit-review');
    if(submitBtn) {
        submitBtn.addEventListener('click', async () => {
            const useCriteria = document.getElementById('rating-type-criteria')?.checked;
            const rating = window.selectedRating;
            const criteriaRatings = window.selectedCriteriaRatings || {};
            const comment = document.getElementById('review-comment').value.trim();
            const placeId = window.currentReviewPlaceId;
            const placeName = window.currentReviewPlaceName;
            const reviewId = window.currentReviewId;
            const photoFile = photoInput?.files[0];

            if (useCriteria) {
                const criteriaKeys = Object.keys(window.currentReviewCriteria || {});
                if (criteriaKeys.length === 0) {
                    showNotification('Критерии для этого типа объекта не найдены', 'error');
                    return;
                }
                const allCriteriaFilled = criteriaKeys.every(key => criteriaRatings[key] && criteriaRatings[key] >= 1 && criteriaRatings[key] <= 5);
                if (!allCriteriaFilled) {
                    showNotification('Пожалуйста, оцените все критерии', 'error');
                    return;
                }
            } else {
                if (!rating || rating < 1 || rating > 5) {
                    showNotification('Пожалуйста, поставьте оценку', 'error');
                    return;
                }
            }

            try {
                let reviewResponse;
                if (reviewId) {
                    reviewResponse = await api(`/reviews/${reviewId}`, {
                        method: 'PUT',
                        body: JSON.stringify({
                            rating: useCriteria ? null : rating,
                            criteriaRatings: useCriteria ? criteriaRatings : null,
                            comment: comment || null,
                            deletePhoto: !!window.currentReviewDeletePhoto
                        })
                    });
                } else {
                    reviewResponse = await api('/reviews', {
                        method: 'POST',
                        body: JSON.stringify({
                            placeId: placeId,
                            placeName: placeName,
                            rating: useCriteria ? null : rating,
                            criteriaRatings: useCriteria ? criteriaRatings : null,
                            comment: comment || null
                        })
                    });
                }

                if (photoFile && reviewResponse?.id) {
                    const formData = new FormData();
                    formData.append('photo', photoFile);
                    
                    const token = getToken();
                    await fetch(`/api/reviews/${reviewResponse.id}/photo`, {
                        method: 'POST',
                        headers: token ? { 'Authorization': `Bearer ${token}` } : {},
                        body: formData
                    });
                }

                placeReviewsCache.delete(placeId);
                
                document.getElementById('review-modal').style.display = 'none';
                
                if (photoInput) {
                    photoInput.value = '';
                    photoPreview.style.display = 'none';
                }
                
                await loadPlaceReviewsForMap();
                createAllPlacemarks();
                applyFilters();
                
                if (getToken()) {
                    await loadProfile();
                    await checkAchievementsAfterAction();
                }
                
                if (els.objectReviewsPanel && els.objectReviewsTitle.textContent.includes(placeName)) {
                    showObjectReviews(placeId, placeName);
                }
                
                showNotification(reviewId ? 'Отзыв обновлен!' : 'Спасибо за ваш отзыв! Он будет опубликован после модерации.', 'success');
            } catch (err) {
                const msg = formatError(err);
                showNotification('Ошибка: ' + msg, 'error');
            }
        });
    }
}

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

function showNotification(message, type = 'info') {
    const notification = document.createElement('div');
    notification.className = 'notification';
    
    if (type === 'error') {
        notification.style.background = 'rgba(220, 53, 69, 0.9)';
    } else if (type === 'success') {
        notification.style.background = 'rgba(40, 167, 69, 0.9)';
    } else {
        notification.style.background = 'rgba(0, 0, 0, 0.85)';
    }
    
    notification.textContent = message;
    notification.style.cssText += `
        position: fixed;
        top: 20px;
        left: 50%;
        transform: translateX(-50%);
        color: white;
        padding: 12px 24px;
        border-radius: 8px;
        z-index: 3000;
        font-size: 14px;
        opacity: 0;
        transition: opacity 0.3s;
        pointer-events: none;
        box-shadow: 0 4px 12px rgba(0,0,0,0.3);
    `;
    
    document.body.appendChild(notification);
    
    setTimeout(() => {
        notification.style.opacity = '1';
    }, 10);
    
    setTimeout(() => {
        notification.style.opacity = '0';
        setTimeout(() => {
            if (notification.parentNode) {
                notification.parentNode.removeChild(notification);
            }
        }, 300);
    }, 3000);
}
