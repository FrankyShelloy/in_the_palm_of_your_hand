(function() {
    function getTheme() {
        const storedTheme = localStorage.getItem('theme');
        if (storedTheme) {
            return storedTheme;
        }
        return 'system';
    }

    function setTheme(theme) {
        if (theme === 'system') {
            localStorage.removeItem('theme');
            if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
                document.documentElement.setAttribute('data-theme', 'dark');
            } else {
                document.documentElement.setAttribute('data-theme', 'light');
            }
        } else {
            localStorage.setItem('theme', theme);
            document.documentElement.setAttribute('data-theme', theme);
        }
        updateActiveButton(theme);
    }

    function updateActiveButton(theme) {
        const buttons = document.querySelectorAll('.theme-btn');
        buttons.forEach(btn => {
            if (btn.dataset.theme === theme) {
                btn.classList.add('active');
            } else {
                btn.classList.remove('active');
            }
        });
    }

    const currentTheme = getTheme();
    setTheme(currentTheme);

    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', e => {
        if (!localStorage.getItem('theme')) {
            setTheme('system');
        }
    });

    window.setAppTheme = setTheme;

    document.addEventListener('DOMContentLoaded', () => {
        updateActiveButton(getTheme());
        
        document.querySelectorAll('.theme-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                setAppTheme(btn.dataset.theme);
            });
        });
    });
})();
