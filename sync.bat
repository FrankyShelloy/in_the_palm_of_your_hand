@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo === Синхронизация с GitHub ===
echo.

REM Проверка, что мы в git репозитории
if not exist .git (
    echo [ОШИБКА] Это не git репозиторий
    exit /b 1
)

REM Получение текущей ветки
for /f "tokens=*" %%i in ('git rev-parse --abbrev-ref HEAD') do set CURRENT_BRANCH=%%i
echo Текущая ветка: %CURRENT_BRANCH%
echo.

REM Проверка наличия несохраненных изменений
for /f %%i in ('git status --porcelain ^| find /c /v ""') do set CHANGES=%%i

if %CHANGES% gtr 0 (
    echo [ВНИМАНИЕ] Обнаружены несохраненные изменения:
    git status --short
    echo.
    
    set /p SAVE_CHANGES="Хотите сохранить изменения в коммит? (y/n): "
    if /i "!SAVE_CHANGES!"=="y" (
        set /p COMMIT_MESSAGE="Введите сообщение коммита: "
        git add .
        git commit -m "!COMMIT_MESSAGE!"
        echo [OK] Изменения сохранены в коммит
    ) else (
        echo [ВНИМАНИЕ] Изменения не сохранены. Продолжаем без коммита.
    )
    echo.
)

REM Получение изменений с удаленного репозитория
echo Получение изменений с GitHub...
git fetch origin
echo.

REM Проверка статуса относительно удаленной ветки
for /f "tokens=*" %%i in ('git rev-parse @') do set LOCAL=%%i
for /f "tokens=*" %%i in ('git rev-parse @{u} 2>nul') do set REMOTE=%%i

if not defined REMOTE (
    echo [ВНИМАНИЕ] Удаленная ветка не найдена. Возможно, это новая ветка.
    set /p PUSH_BRANCH="Отправить ветку на GitHub? (y/n): "
    if /i "!PUSH_BRANCH!"=="y" (
        git push -u origin %CURRENT_BRANCH%
        echo [OK] Ветка отправлена на GitHub
    )
    goto :end
)

if "%LOCAL%"=="%REMOTE%" (
    echo [OK] Ваша ветка актуальна
    goto :end
)

REM Проверка, отстаем ли мы от удаленной ветки
git merge-base --is-ancestor HEAD @{u}
if %errorlevel%==0 (
    echo [ВНИМАНИЕ] Ваша ветка отстает от удаленной
    set /p PULL_CHANGES="Получить изменения? (y/n): "
    if /i "!PULL_CHANGES!"=="y" (
        git pull origin %CURRENT_BRANCH%
        echo [OK] Изменения получены
    )
    goto :end
)

REM Проверка, опережаем ли мы удаленную ветку
git merge-base --is-ancestor @{u} HEAD
if %errorlevel%==0 (
    echo [ВНИМАНИЕ] Ваша ветка опережает удаленную
    set /p PUSH_CHANGES="Отправить изменения на GitHub? (y/n): "
    if /i "!PUSH_CHANGES!"=="y" (
        git push origin %CURRENT_BRANCH%
        echo [OK] Изменения отправлены
    )
    goto :end
)

REM Ветки разошлись
echo [ВНИМАНИЕ] Ветки разошлись
echo [ВНИМАНИЕ] Требуется слияние изменений
set /p DO_PULL="Выполнить git pull? (y/n): "
if /i "!DO_PULL!"=="y" (
    git pull origin %CURRENT_BRANCH%
    if %errorlevel%==0 (
        echo [OK] Слияние выполнено успешно
        echo.
        set /p PUSH_MERGED="Отправить объединенные изменения на GitHub? (y/n): "
        if /i "!PUSH_MERGED!"=="y" (
            git push origin %CURRENT_BRANCH%
            echo [OK] Изменения отправлены
        )
    ) else (
        echo [ОШИБКА] Конфликт при слиянии!
        echo [ВНИМАНИЕ] Разрешите конфликты вручную, затем выполните:
        echo   git add ^<файлы^>
        echo   git commit
        echo   git push origin %CURRENT_BRANCH%
        exit /b 1
    )
)

:end
echo.
echo === Текущий статус ===
git status
echo.
echo [OK] Синхронизация завершена!
