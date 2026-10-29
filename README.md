# Stena

Общая пиксельная стена в реальном времени, аналог Pixel Battle от ВК и r/place. Игроки ставят пиксели с cooldown'ом, изменения сразу видят все, администратор откатывает испорченные области.

План: [docs/PLAN.md](docs/PLAN.md).

Архитектура: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Быстрый старт

Требования: Docker.

Запуск: `docker compose up --build`

API: http://localhost:8080/scalar

## Тесты и CI

[![CI](https://github.com/chanan-duy/stena/actions/workflows/ci.yml/badge.svg)](https://github.com/chanan-duy/stena/actions/workflows/ci.yml)

Запуск тестов: `dotnet test backend/Stena.slnx`, нужны .NET 10 SDK и Docker.
