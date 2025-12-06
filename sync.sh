#!/bin/bash

# Скрипт синхронизации с GitHub
# Автоматизирует процесс получения и отправки изменений

# Прекратить выполнение при любой ошибке (кроме тех, что в условиях)
set -e

# Цвета для вывода
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}=== Синхронизация с GitHub ===${NC}\n"

# Проверка, что мы в git репозитории
if [ ! -d .git ]; then
    echo -e "${RED}Ошибка: Это не git репозиторий${NC}"
    exit 1
fi

# Получение текущей ветки
CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
echo -e "${BLUE}Текущая ветка:${NC} $CURRENT_BRANCH"

# Проверка наличия несохраненных изменений
if [ -n "$(git status --porcelain)" ]; then
    echo -e "\n${YELLOW}Обнаружены несохраненные изменения:${NC}"
    git status --short
    
    read -p "Хотите сохранить изменения в коммит? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[YyДд]$ ]]; then
        read -p "Введите сообщение коммита: " commit_message
        git add .
        git commit -m "$commit_message"
        echo -e "${GREEN}Изменения сохранены в коммит${NC}"
    else
        echo -e "${YELLOW}Изменения не сохранены. Продолжаем без коммита.${NC}"
    fi
fi

# Получение изменений с удаленного репозитория
echo -e "\n${BLUE}Получение изменений с GitHub...${NC}"
git fetch origin

# Проверка, есть ли изменения на удаленном репозитории
# Принимает опциональный параметр для upstream ветки, по умолчанию использует настроенную upstream ветку
UPSTREAM=${1:-"@{u}"}
LOCAL=$(git rev-parse @)
REMOTE=$(git rev-parse "$UPSTREAM" 2>/dev/null || echo "")
BASE=$(git merge-base @ "$UPSTREAM" 2>/dev/null || echo "")

if [ -z "$REMOTE" ]; then
    echo -e "${YELLOW}Удаленная ветка не найдена. Возможно, это новая ветка.${NC}"
    read -p "Отправить ветку на GitHub? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[YyДд]$ ]]; then
        git push -u origin "$CURRENT_BRANCH"
        echo -e "${GREEN}Ветка отправлена на GitHub${NC}"
    fi
elif [ "$LOCAL" = "$REMOTE" ]; then
    echo -e "${GREEN}Ваша ветка актуальна${NC}"
elif [ "$LOCAL" = "$BASE" ]; then
    echo -e "${YELLOW}Ваша ветка отстает от удаленной${NC}"
    read -p "Получить изменения? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[YyДд]$ ]]; then
        git pull origin "$CURRENT_BRANCH"
        echo -e "${GREEN}Изменения получены${NC}"
    fi
elif [ "$REMOTE" = "$BASE" ]; then
    echo -e "${YELLOW}Ваша ветка опережает удаленную${NC}"
    read -p "Отправить изменения на GitHub? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[YyДд]$ ]]; then
        git push origin "$CURRENT_BRANCH"
        echo -e "${GREEN}Изменения отправлены${NC}"
    fi
else
    echo -e "${YELLOW}Ветки разошлись${NC}"
    echo -e "${YELLOW}Требуется слияние изменений${NC}"
    read -p "Выполнить git pull? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[YyДд]$ ]]; then
        if git pull origin "$CURRENT_BRANCH"; then
            echo -e "${GREEN}Слияние выполнено успешно${NC}"
            
            read -p "Отправить объединенные изменения на GitHub? (y/n): " -n 1 -r
            echo
            if [[ $REPLY =~ ^[YyДд]$ ]]; then
                git push origin "$CURRENT_BRANCH"
                echo -e "${GREEN}Изменения отправлены${NC}"
            fi
        else
            echo -e "${RED}Конфликт при слиянии!${NC}"
            echo -e "${YELLOW}Разрешите конфликты вручную, затем выполните:${NC}"
            echo "  git add <файлы>"
            echo "  git commit"
            echo "  git push origin $CURRENT_BRANCH"
            exit 1
        fi
    fi
fi

# Итоговый статус
echo -e "\n${BLUE}=== Текущий статус ===${NC}"
git status

echo -e "\n${GREEN}Синхронизация завершена!${NC}"
