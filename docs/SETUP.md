# Установка окружения с нуля

Время: примерно 30 минут. Всё бесплатно. Windows 10 или 11.

## Шаг 1. .NET SDK

1. Открыть https://dotnet.microsoft.com/download
2. Скачать **.NET SDK** последней LTS версии (не Runtime, а именно SDK).
3. Установить, перезапустить терминал.
4. Проверка:

```
dotnet --version
```

Должен вывестись номер версии. Если команда не найдена — перезагрузить компьютер.

## Шаг 2. Редактор кода

Подойдёт VS Code или Cursor.

1. Установить редактор.
2. Поставить расширение **C# Dev Kit** от Microsoft.
3. Открыть папку проекта: `Файл -> Открыть папку`.

## Шаг 3. Git

1. Установить https://git-scm.com
2. Настроить один раз:

```
git config --global user.name "ProkStudio"
git config --global user.email "почта_от_github"
```

3. Скачать репозиторий:

```
git clone https://github.com/ProkStudio/WorldBox.git
cd WorldBox
```

## Шаг 4. Шаблоны MonoGame

```
dotnet new install MonoGame.Templates.CSharp
```

Проверка, что шаблоны появились:

```
dotnet new list monogame
```

В списке должен быть `MonoGame Cross-Platform Desktop Application (OpenGL)` с коротким именем `mgdesktopgl`.

## Шаг 5. Первая сборка

```
dotnet build
dotnet run --project src/WorldBox.Desktop
```

Должно открыться окно игры. Если проектов ещё нет — значит срез S0 не сделан, это нормально на старте.

## Шаг 6. Сборка exe

```
dotnet publish src/WorldBox.Desktop -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Готовый `WorldBox.exe` лежит в `src/WorldBox.Desktop/bin/Release/net10.0/win-x64/publish/`.
Это самодостаточный файл: его можно скинуть другому человеку, у которого ничего не установлено.

## Частые проблемы

| Симптом | Причина | Решение |
| --- | --- | --- |
| `dotnet` не найден | не перезапущен терминал | закрыть все окна терминала и открыть заново |
| Ошибка про версию net10.0 | установлена другая версия SDK | поправить `TargetFramework` в csproj под установленную |
| Чёрное окно без картинки | нет драйверов OpenGL | обновить драйвер видеокарты |
| Медленно на ноутбуке | игра запущена на встроенной видеокарте | в параметрах Windows выбрать для exe дискретную |
| Долгая первая сборка | скачиваются пакеты | это разово, дальше быстро |

## Полезные команды

```
dotnet build                      сборка
dotnet test                       тесты
dotnet run --project src/WorldBox.Bench -- --ticks 5000    прогон симуляции без окна
git status                        что изменилось
git add . && git commit -m "s1: генератор карты" && git push    отправить изменения
```
