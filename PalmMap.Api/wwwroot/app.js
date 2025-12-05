const apiBase = "/api";
const tokenKey = "palmmap_token";

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
  reviewsList: document.getElementById("reviews-list"),
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
    const msg = await res.text();
    throw new Error(msg || res.statusText);
  }
  if (res.status === 204) return null;
  return res.json();
}

async function loadProfile() {
  try {
    const data = await api("/auth/me");
    els.userInfo.textContent = data.email;
    els.userInfo.classList.remove("hidden");
    els.logout.classList.remove("hidden");
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
    els.reviewsList.innerHTML = "";
    items.forEach((r) => {
      const div = document.createElement("div");
      div.className = "review-card";
      const date = new Date(r.createdAt).toLocaleString();
      div.innerHTML = `<div class="review-meta">${date}</div><div>${r.content}</div>`;
      els.reviewsList.appendChild(div);
    });
  } catch (err) {
    console.error(err);
  }
}

function logout() {
  clearToken();
  els.userInfo.classList.add("hidden");
  els.logout.classList.add("hidden");
  els.btnShowLogin.classList.remove("hidden");
  els.btnShowRegister.classList.remove("hidden");
  els.profileEmail.textContent = "-";
  els.profileName.textContent = "-";
  els.profileLevel.textContent = "Level 1";
  els.profileReviews.textContent = "0";
  els.achievements.innerHTML = "";
  els.reviewsList.innerHTML = "";
}

// Event wiring
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
    els.loginError.textContent = "Ошибка входа: " + (err.message || "неизвестная ошибка");
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
          const msg = await res.text();
          alert("Ошибка: " + msg);
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
      const msg = await res.text();
      throw new Error(msg);
    }
    hideModal();
  } catch (err) {
    els.registerError.textContent = "Ошибка регистрации: " + (err.message || "неизвестная ошибка");
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
    els.reviewStatus.textContent = "Не удалось";
    els.reviewStatus.classList.add("error");
  }
});

// Init
if (getToken()) {
  loadProfile();
}
