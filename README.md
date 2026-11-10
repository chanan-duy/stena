# Stena

Общая пиксельная стена в реальном времени, аналог Pixel Battle от ВК и r/place. Игроки ставят пиксели с cooldown'ом, изменения сразу видят все, администратор откатывает испорченные области.

План: [docs/PLAN.md](docs/PLAN.md).

Архитектура: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Быстрый старт

Требования: Docker.

Запуск: `docker compose up --build`

Веб клиент: http://localhost:8080

API: http://localhost:8080/scalar

Тестовые учётные записи: `admin@stena.test` / `admin123` (администратор), `player@stena.test` / `player123` (игрок).

Боты рисуют логотип на стене 1: `dotnet run --project backend/tools/Stena.Bots -- --wall 1 --bots 20`, нужен .NET 10 SDK.

## Тесты и CI

[![CI](https://github.com/chanan-duy/stena/actions/workflows/ci.yml/badge.svg)](https://github.com/chanan-duy/stena/actions/workflows/ci.yml)

Модульные и интеграционные тесты: `dotnet test backend/Stena.slnx`, нужны .NET 10 SDK и Docker.

Сквозные сценарии в браузере проверяют вход, рассылку изменений и откат с баном. При запущенной системе в папке `frontend`: `npm ci`, `npx playwright install chromium`, `npx playwright test`.

Нагрузочный тест при запущенной системе: `dotnet run --project backend/tools/Stena.Load`. Он создаёт стену и 1000 игроков, 30 секунд ставит 1000 пикселей в секунду, затем 10 секунд по 2000. Результат на одной машине: 50 000 пикселей без отказов, история совпала со стеной, задержка p99 от 2,6 до 13,4 мс в трёх запусках. Если хоть один запрос отклонён или история разошлась со стеной по числу пикселей или по их цветам, тест завершается с ошибкой.
