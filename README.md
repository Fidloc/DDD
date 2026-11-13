# CoworkingLab — предметне ядро коворкінгу підприємства (ЛР 3)

C# / .NET 8, тести — xUnit. Відкривати у Visual Studio 2022 (версія 17.8 або новіша, бо потрібен .NET 8): `CoworkingLab.sln`.

## Структура та відповідальність

| Обмежений контекст | Програмний модуль | Корінь агрегату | Автор |
|---|---|---|---|
| Погодження резервування | `src/Reservations.Domain` | `ZoneSchedule` (розклад зони) | Мороз Іван |
| Облік інструктажів організаторів | `src/Briefings.Domain` | `BriefingRecord` (облікова картка інструктажів) | Тур Маргарита |
| Фіксація використання зони | `src/ZoneUsage.Domain` | `ZoneUsageRecord` (використання зони) | Лєбідинський Дмитро |
| Ідентифікація та доступ (generic) | — готове корпоративне рішення | — | — |

```
CoworkingLab/
├── CoworkingLab.sln
├── src/
│   ├── Reservations.Domain/
│   │   └── Model/
│   │       ├── DomainEvents.cs, DomainException.cs
│   │       ├── Entities/      Reservation.cs, ZoneSchedule.cs
│   │       └── ValueObjects/  Identifiers.cs, TimeSlot.cs, BookingPolicy.cs, ZoneProfile.cs,
│   │                          Actor.cs, BriefingValidity.cs, Rejection.cs
│   ├── Briefings.Domain/
│   │   └── Model/
│   │       ├── DomainEvents.cs, DomainException.cs
│   │       ├── Entities/      BriefingRecord.cs
│   │       └── ValueObjects/  ValueObjects.cs
│   └── ZoneUsage.Domain/
│       └── Model/
│           ├── DomainEvents.cs, DomainException.cs
│           ├── Entities/      ZoneUsageRecord.cs
│           └── ValueObjects/  ValueObjects.cs
└── tests/
    └── Coworking.Tests/
        ├── Reservations/Domain/  ReservationFixture.cs, ValueObjectTests.cs, ZoneScheduleTests.cs
        ├── Briefings/Domain/     BriefingRecordTests.cs
        └── ZoneUsage/Domain/     ZoneUsageRecordTests.cs
```

Папки `Services` не створено: предметних служб у реалізованих сценаріях немає.

## Залежності

- Три предметні модулі не залежать один від одного і від тестів.
- `Coworking.Tests` посилається на всі три модулі; пакети: xUnit 2.9.2, xunit.runner.visualstudio 2.8.2, Microsoft.NET.Test.Sdk 17.11.1.
- Факти між контекстами передаються як значення (`BriefingValidity`, `ConfirmedSlot`, виклики `Record…`), а не через спільні класи.

## Запуск тестів

У Visual Studio: меню **Test → Run All Tests** (або **Test → Test Explorer** → кнопка «Run All»). Під час першої збірки Visual Studio сама завантажить пакети xUnit з NuGet. Або в терміналі з кореня рішення:

```
dotnet test
```

Очікуваний результат: 24 тести (20 методів, з них два параметризовані), усі пройдено.
