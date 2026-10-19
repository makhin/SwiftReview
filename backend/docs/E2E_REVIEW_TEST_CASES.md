# Ручные E2E: London / CS, TFO, DC

## Подготовка и пользователи

Набор предназначен для Development: UI http://localhost:5173, API http://localhost:5080.
Перед первым прогоном выполнить `backend/scripts/seed-test-data.sql` после миграций.
Скрипт удаляет прежние прикладные данные и аудит; повторный запуск возвращает начальный
набор, сохраняя схему и миграции. На время сброса остановить API и другие процессы записи.
Из корня локального Docker checkout:

```bash
bash -c 'source scripts/configure-database.sh; orp_start_sql; orp_sql -d SwiftReviewDev < backend/scripts/seed-test-data.sql'
```

После сброса запустить `./run-dev.docker.sh`. SQL-интеграционные тесты используют отдельные
временные базы и не требуют сброса SwiftReviewDev.

### Перезаполнение через SSMS

1. Остановить API (Stop в Visual Studio/Rider или Ctrl+C в launcher), импортер и другие
   процессы, которые записывают данные. Сам SQL Server оставить запущенным.
2. Подключиться в SSMS к SQL Server, который использует тестовый API.
   Для локального Docker это **Server name: `localhost,14333`**, **SQL Server Authentication**,
   **Login: `sa`**. Пароль — текущее значение `MSSQL_SA_PASSWORD` из окружения или локального
   `backend/.env`. Если SSMS запущен на другом компьютере, вместо localhost указать адрес
   машины с SQL Server. Для внешнего SQL Server использовать сервер и способ аутентификации
   из своей конфигурации API; Docker для него не нужен.
3. Открыть **File → Open → File…** и выбрать
   [backend/scripts/seed-test-data.sql](../scripts/seed-test-data.sql) из checkout проекта.
4. В списке баз на панели SQL Editor выбрать **SwiftReviewDev** (для внешней среды —
   соответствующую тестовую базу API). Проверить выбранную базу отдельным запросом:

   ```sql
   SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName;
   ```

5. Убедиться, что в выбранной базе уже применены миграции ORP. Скрипт не создаёт базу и
   не применяет миграции. Если он сообщает `Apply ... migration`, сначала применить
   миграции по [backend/README.md](../README.md), затем повторить заполнение.
6. **Выполнить весь файл** кнопкой Execute / F5 без выделения отдельных строк.
   Не оборачивать его в собственный `BEGIN TRANSACTION`: скрипт управляет транзакцией сам.
   **Будут удалены все текущие прикладные данные, включая пользователей, сообщения,
   назначения, ревью и аудит, в выбранной базе.** Схема и история миграций сохранятся.
7. Во вкладке Messages дождаться сообщения:

   ```text
   Loaded London, 7 users, 3 common workflows and 100 unassigned demo messages.
   ```

   При ошибке очистка и загрузка откатываются вместе. Устранить указанную причину и
   повторить весь файл; не продолжать с отдельной SQL-команды в середине скрипта.
8. Проверить результат отдельным запросом в той же базе:

   ```sql
   SELECT Name FROM orp.Branches;                         -- Только London
   SELECT UserName, IsGlobalAdministrator
   FROM orp.Users ORDER BY UserName;                      -- 7 пользователей
   SELECT State, COUNT(*) AS MessageCount
   FROM orp.Messages GROUP BY State;                      -- New: 100
   SELECT COUNT(*) AS ReviewCount FROM orp.Reviews;       -- 0
   SELECT COUNT(*) AS AssignmentCount FROM orp.Assignments; -- 0
   ```

9. Запустить API и frontend с подключением к этой же базе и обновить вкладки браузера.
   В локальном Docker checkout использовать `./run-dev.docker.sh`; в Windows с внешним
   SQL Server — обычный запуск через Visual Studio или `./run-dev.ps1`.

Повторный запуск файла полностью возвращает исходный набор для E2E. Числовые ID могут
измениться, поэтому для входа использовать логины ниже, а сообщения искать по `TEST-xxxxx`.
SQL-логин `sa` нужен для подключения SSMS; это не пользователь приложения.

### Каких пользователей использовать

Тестовые аккаунты приложения работают в **Development** через параметр `?user=<логин>`;
отдельный пароль для них в этом режиме не требуется. Frontend передаёт логин API в
`X-Debug-User`. В production этот способ входа не применяется.

| Доступ | Пользователь 1 | Пользователь 2 | Сообщений |
| --- | --- | --- | --- |
| London / CS | [cs.reviewer1](http://localhost:5173/messages?user=cs.reviewer1) | [cs.reviewer2](http://localhost:5173/messages?user=cs.reviewer2) | 25 |
| London / TFO | [tfo.reviewer1](http://localhost:5173/messages?user=tfo.reviewer1) | [tfo.reviewer2](http://localhost:5173/messages?user=tfo.reviewer2) | 50 |
| London / DC | [dc.reviewer1](http://localhost:5173/messages?user=dc.reviewer1) | [dc.reviewer2](http://localhost:5173/messages?user=dc.reviewer2) | 25 |
| Все три департамента | [admin](http://localhost:5173/messages?user=admin) | — | 100 |

Для первого полного прогона использовать такие пары:

| Процесс | Кто назначает | Кто подтверждает первым | Кто завершает | Сообщение |
| --- | --- | --- | --- | --- |
| CS Incoming | cs.reviewer1 → cs.reviewer2 | cs.reviewer2 | cs.reviewer1, уровень 2 | TEST-00001 |
| TFO Incoming | tfo.reviewer1 → tfo.reviewer2 | tfo.reviewer2 | tfo.reviewer1, уровень 2 | TEST-00002 |
| DC Incoming | dc.reviewer1 → dc.reviewer2 | dc.reviewer2 | Он же: достаточно уровня 1 | TEST-00003 |
| TFO Outgoing | tfo.reviewer1 → tfo.reviewer2 | tfo.reviewer2 | Он же: достаточно уровня 1 | TEST-00076 |

Назначать можно в **Messages**. Для подтверждения перейти в **Message Review**:
[CS reviewer2](http://localhost:5173/messages/assigned?user=cs.reviewer2),
[TFO reviewer2](http://localhost:5173/messages/assigned?user=tfo.reviewer2),
[DC reviewer2](http://localhost:5173/messages/assigned?user=dc.reviewer2).
Для второго подтверждения открыть тот же раздел под
[CS reviewer1](http://localhost:5173/messages/assigned?user=cs.reviewer1) или
[TFO reviewer1](http://localhost:5173/messages/assigned?user=tfo.reviewer1).
В сценариях Undo его выполняет reviewer1 соответствующего департамента через
**Message Review → View → History → Undo approval**.
`reviewer1` и `reviewer2` имеют одинаковые права; номера обозначают участников сценария,
а не ограничения на уровень подтверждения.

Обычные пользователи имеют Assign, подтверждения уровней 1–3, разрешённый workflow Undo
и просмотр аудита. Они не администраторы и не имеют workflow.manage. Для основных
сценариев не использовать admin: его исключения обходят ограничения обычных пользователей.
Для двух участников удобно открыть отдельные профили браузера с соответствующим `?user=`.

Единственный бранч — London. Все 100 сообщений изначально `New`, без назначений и ревью.
Времена сообщений охватывают последние 100 часов: убрать фильтры даты и состояния.
Искать по `TEST-xxxxx`, не по числовому ID: ID меняются после сброса.

## Общий порядок и ожидаемые правила

Для каждого сценария использовать выделенное ниже сообщение. Пользователь 1 своего
департамента нажимает Assign и выбирает пользователя 2; состояние становится `Assigned`.
Пользователь 2 переходит в **Message Review** (`/messages/assigned?user=<username>`),
находит сообщение и нажимает Confirm, проверяет уровень, вводит комментарий с ID
сценария, нажимает Confirm в диалоге. Для второго уровня заново открыть Confirm после
обновления данных. Подтверждать может любой авторизованный пользователь департамента,
а не только назначенный; повторное участие одного пользователя разрешено.

Для двух уровней после первого ожидается `WaitingForSecondReview`, назначение остаётся
на пользователе 2. Последний уровень переводит в `Completed` и снимает назначение.
Одношаговые процессы сразу переходят из `Assigned` в `Completed`.
UI может показывать бизнес-название стадии; точные состояния указаны как значения API.

После каждого успешного сценария обновить страницу, проверить состояние, исполнителя
и **View → History** в разделе Message Review. Должны сохраняться регистрация, назначение, ReviewStarted/ReviewApproved
для каждого подтверждения, комментарии и правильные авторы; при завершении —
MessageCompleted и окончание назначения. Не должно быть дублированных подтверждений
или оставшегося InProgress. Reject в диалоге Confirm отсутствует.

## Сценарии

| ID | Сообщение | Участники и шаги после назначения | Ожидаемый результат |
| --- | --- | --- | --- |
| E2E-01 | TEST-00001, CS Incoming MT199 | cs.reviewer2 подтверждает уровень 1; cs.reviewer1 — уровень 2 | Два разных автора, промежуточное WaitingForSecondReview, затем Completed |
| E2E-02 | TEST-00002, TFO Incoming MT299 | tfo.reviewer2 подтверждает уровень 1; tfo.reviewer1 — уровень 2 | То же для TFO |
| E2E-03 | TEST-00003, DC Incoming MT671 | dc.reviewer2 подтверждает уровень 1 | Completed, второго уровня нет, обычный Undo недоступен |
| E2E-04 | TEST-00076, TFO Outgoing MT299 | tfo.reviewer2 подтверждает уровень 1 | Completed, второго уровня нет, обычный Undo недоступен |
| E2E-05 | TEST-00004, CS Incoming | cs.reviewer2 подтверждает оба уровня | Два подтверждения одного автора, Completed |
| E2E-06 | TEST-00005, TFO Incoming | tfo.reviewer2 подтверждает оба уровня | Два подтверждения одного автора, Completed |
| E2E-07 | TEST-00007, CS Incoming | cs.reviewer2 подтверждает уровень 1; cs.reviewer1 выполняет Undo; cs.reviewer2 повторяет уровень 1; cs.reviewer1 завершает уровень 2 | После Undo — Assigned, назначение сохраняется, прежнее ревью Undone, есть ConfirmationUndone; затем Completed |
| E2E-08 | TEST-00008, TFO Incoming | Те же шаги с tfo.reviewer2 и tfo.reviewer1 | То же для TFO |
| E2E-09 | TEST-00010, CS Incoming | cs.reviewer2 подтверждает оба уровня, затем проверяет возможность Undo | Completed; обычный Undo недоступен |
| E2E-10 | TEST-00011, TFO Incoming | tfo.reviewer2 подтверждает оба уровня, затем проверяет возможность Undo | Completed; обычный Undo недоступен |
| E2E-11 | TEST-00013 (CS), TEST-00014 (TFO), TEST-00015 (DC) | По инструкции ниже проверить изоляцию под каждым департаментом | Свои сообщения доступны, чужие недоступны; назначения только внутри департамента |
| E2E-12 | TEST-00016, CS Incoming | По инструкции ниже отправить повтор с устаревшей версией | HTTP 409, только одно подтверждение уровня 1, затем нормальное завершение с новой версией |

### E2E-11: изоляция

1. Под admin получить ID трёх выделенных сообщений из `GET /api/messages/grid?skip=0&take=100`.
2. Для каждого `<dept>.reviewer1` проверить список без фильтров: CS — 25, TFO — 50,
   DC — 25; чужих сообщений нет. Справочник бранчей содержит только London.
3. Через `GET /api/messages/{id}` с `X-Debug-User` текущего пользователя запросить своё
   и оба чужих сообщения: своё доступно, чужие не раскрывают содержимое (403/404).
4. На своём сообщении открыть Assign: доступны только допустимые пользователи своего
   департамента и глобальный admin; обычных пользователей чужих департаментов нет.
   Самоназначение обычному пользователю недоступно. Закрыть диалог без сохранения.
5. С актуальной версией чужого сообщения, полученной admin, отправить
   `POST /api/messages/{id}/reviews/confirm` от чужого ревьюера:
   `{"level":1,"expectedVersion":"<version>"}`. Ожидается 403; сообщение остаётся New,
   нет назначений и ревью. Повторить для каждого департамента.

### E2E-12: устаревшая версия

1. Назначить TEST-00016 пользователю cs.reviewer2 обычным порядком.
2. Получить `id` через grid и `version` через `GET /api/messages/{id}` с
   `X-Debug-User: cs.reviewer2`; сохранить значение как V1.
3. Отправить `POST /api/messages/{id}/reviews/confirm` с JSON
   `{"level":1,"expectedVersion":"<V1>","comment":"E2E-12"}` и тем же заголовком.
   Ожидается 204, WaitingForSecondReview, новая версия V2.
4. Повторить идентичный POST с V1. Ожидается 409; состояние не меняется, второй записи
   ReviewApproved нет. Это последовательная проверка устаревшего запроса, не тест гонки.
5. Обновить UI и подтвердить уровень 2 с актуальной версией. Ожидается Completed.

Для API-запросов использовать `Content-Type: application/json`; поля `id` и `version`
читать из текущих ответов, не копировать из предыдущего наполнения.

## Протокол выполнения

19 октября 2026 года выполнен браузерный прогон всех 12 сценариев: **Passed**.
Использовался Playwright с реальным Chromium и текущими API/SQL Server, без моков.
История повторно проверена после загрузки диалогов; первоначальные задержки переключения
History и условия повторной проверки описаны в отчёте.

[Фактические результаты, снимки и замечания](e2e-results/2026-10-19/README.md).

После прогона: 11 сообщений Completed, 89 New; 20 Approved и 2 Undone;
активных назначений и InProgress нет. База оставлена в этом состоянии для просмотра.

Автоматическое покрытие: SeedTestDataTests проверяет состав и scopes; ConfirmationSqlTests —
четыре маршрута, чужой доступ, Undo, версии, синхронизированные конкурентные запросы и
повторное наполнение после завершённого ревью. Эти тесты работают с временными SQL-базами.
