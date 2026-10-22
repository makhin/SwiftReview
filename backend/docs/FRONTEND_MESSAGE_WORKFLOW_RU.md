# Workflow сообщений: вызовы backend из frontend

Актуальность: 21 октября 2026 года. Примеры сверены с текущими endpoint, DTO и обработчиками backend. Это руководство по обработке существующего сообщения: выбор → назначение → проверка → решение → следующий уровень или завершение. Создание сообщений выполняется через импорт; публичного `POST /api/messages` для frontend нет.

## 1. Общие правила

- Базовый URL локального API: `http://localhost:5080/api`.
- JSON отправляется с `Content-Type: application/json`; имена свойств — camelCase, enum — строки (`New`, `ReviewSession` и т. д.).
- В текущей реализации используется Development-аутентификация: заголовок `X-Debug-User` принимает существующий username или числовой user ID. Вне Development этот обработчик отключён; это не production-механизм входа.
- Во всех HTTP-примерах ниже подразумеваются эти заголовки. При смене участника сценария запрос выполняется от его имени. Frontend не передаёт `actorId` или `reviewerId` в тело команды: backend берёт пользователя из аутентификации.
- ID `1001` (message), `21`, `22`, `23` (пользователи), `501`–`503` (review), `7` (workflow) иллюстративны. Подставляйте ID из ответов API; наличие этих записей в seed не предполагается.
- Все команды ниже возвращают `204 No Content`, кроме `reviews/start`, который возвращает `200` и `reviewId`. После `204` нельзя вызывать `response.json()`.
- Комментарий в approve/reject/confirm/undo необязателен, допускается `null`, максимум 2000 символов. Cancel комментарий не принимает.

Пример вызова чтения:

```bash
curl 'http://localhost:5080/api/me' \
  -H 'X-Debug-User: admin'
```

## 2. Инициализация экрана

| Вызов | Для чего нужен |
|---|---|
| `GET /api/me` | Пользователь, permissions, branches, departments, scopes, isGlobalAdministrator |
| `GET /api/message-states` | Коды и подписи статусов, reviewLevel, phase, assignedDescription |
| `GET /api/workflows` | Доступные workflow, шаги и политики подтверждения/undo |
| `GET /api/branches` | Фильтр филиала |
| `GET /api/departments` | Справочник подразделений |
| `GET /api/message-types` | Фильтр типа сообщения |
| `GET /api/messages/state-counts` | Счётчики всех доступных сообщений по статусам |

Независимые справочники можно загружать параллельно. `GET /api/users` возвращает доступных пользователей, но для назначения конкретного message нужно использовать `assignment-candidates`.

Счётчики возвращают объект `{ "items": [...], "total": { "count": 0, "breakdown": [] } }`, а не массив. Для раздела «Моя работа»: `GET /api/messages/state-counts?assignmentScope=mine`. Счётчики не учитывают фильтры грида.

Права нужно проверять в scope сообщения. Плоский список `me.permissions` сам по себе не гарантирует право действовать в конкретном филиале/подразделении. Пользователь может видеть сообщение, но не иметь права обрабатывать его.

## 3. Выбрать сообщение

### 3.1. Загрузить грид

```http
POST /api/messages/grid
Content-Type: application/json
X-Debug-User: admin

{
  "status": "New",
  "page": 1,
  "pageSize": 20,
  "sort": [{ "field": "receivedAt", "direction": "desc" }]
}
```

Ответ: `200 OK`, объект `{ "items": [...], "totalCount": 42 }`. Число 42 приведено для примера.

Важные поля каждой строки:

| Поле | Использование |
|---|---|
| `id`, `externalId` | Числовой ID для URL; внешний reference для отображения |
| `state`, `stateName` | Машинный статус и подпись |
| `currentAssigneeId`, `currentAssigneeName` | Текущий исполнитель; null означает отсутствие назначения |
| `workflowDefinitionId`, `workflowDefinitionName` | Связь со справочником workflow |
| `requiredReviewLevels` | Обязательные уровни; например `[1, 3]`, а не обязательно `[1, 2, 3]` |
| `confirmationMode` | `ReviewSession`, `Immediate` или `None` |
| `canReview` | Текущий пользователь может начать/возобновить review-сессию |
| `canConfirm`, `confirmationLevel` | Доступность и уровень атомарного confirm |
| `version` | Техническая версия строки; передавать её в assign/confirm не нужно |
| `activeReviewId`, `activeReviewLevel`, `activeReviewerId` | Текущая попытка review, если есть |
| `undoReviewId` | Доступное текущему пользователю подтверждение для undo; иначе null |
| `canChangeWorkflow` | Доступность смены workflow |

Для «Моей работы» добавьте `"assignmentScope": "mine"`: это текущий исполнитель **или** активный reviewer. Для общей очереди свойство опустите; строки `all`, `departments`, `assignable` не поддерживаются. Новые неназначенные сообщения обычно не попадают в `mine`.

Дополнительные фильтры: `search`, `messageType`, `branch`, `dateFrom`, `dateTo`. `page` начинается с 1, `pageSize` — от 1 до 500. `search` ищет по externalId или messageType. Не передавайте старые `skip`, `take`, `filter`: этот endpoint отклоняет их с 400.

Грид загружается через DevExtreme `CustomStore`; backend применяет `DevExtreme.AspNet.Data`. Адаптер `load` должен преобразовать paging/sorting в текущий POST-контракт и вернуть `{ data: result.items, totalCount: result.totalCount }`. Загрузку грида не переносить в TanStack Query.

### 3.2. Открыть сообщение

```http
GET /api/messages/1001
```

Ответ `MessageDetailsDto` содержит тело `body`, реквизиты, `state`, `currentAssigneeId`, `version`, `confirmationMode`, `canReview`, `canConfirm`, `confirmationLevel`.

Для карточки с историей:

```http
GET /api/messages/1001/view
```

Этот ответ содержит `approvals`, `auditLog`, `rawText` и требует также доступа к аудиту. Это презентационная модель: `id` — строка, `status` — подпись, а не поле `state`; в approvals нет `reviewId` для команд. Для действий используйте актуальную строку грида и ответ `start`.

Просмотр не запускает review. `GET /messages/{id}` также не возвращает `activeReviewId`, `undoReviewId` или `workflowDefinitionId`: эти поля берутся из грида.

## 4. Основной сценарий: assign → start → approve

Сценарий ниже предполагает активный workflow с обязательными уровнями `[1, 2, 3]` и политиками:

```json
{
  "confirmationMode": "ReviewSession",
  "allowReviewerReuse": false,
  "assignmentRetentionMode": "PerConfirmation",
  "rejectAllowed": true
}
```

Это пример конфигурации, не тело отдельной команды и не универсальные настройки всех workflow. Настоящие значения берите из `GET /api/workflows` по `workflowDefinitionId` строки.

### 4.1. Диспетчер получает кандидатов

```http
GET /api/messages/1001/assignment-candidates
```

Пример ответа:

```json
[
  { "id": 21, "userName": "reviewer1", "displayName": "Reviewer 1" },
  { "id": 22, "userName": "reviewer2", "displayName": "Reviewer 2" }
]
```

Нужны `message.assign` и доступ к обработке сообщения. Список учитывает текущий уровень, права кандидата, scope, текущего исполнителя и запрет повторного reviewer. Пользователь с `message.assign` может назначить себя или другого reviewer с доступом и правом соответствующего уровня в processing scope. Роль Reviewer включает `message.assign`. Не строить список кандидатов самостоятельно из `/users`.

### 4.2. Назначить первого reviewer

От имени диспетчера:

```http
POST /api/messages/1001/assign

{ "assignedTo": 21 }
```

Успех: `204`. Состояние `New → Assigned`, `currentAssigneeId = 21`.

Для назначения и переназначения используется один `assign`. В теле нужен только `assignedTo`. Сервер проверяет актуальные права, кандидата и состояние внутри транзакции. При конфликте обновить сообщение, не повторять команду автоматически. События аудита остаются раздельными: `MessageAssigned` и `MessageReassigned`.

### 4.3. Reviewer 21 начинает проверку

После обновления строки и проверки `canReview`:

```http
POST /api/messages/1001/reviews/start
X-Debug-User: 21

{ "level": 1 }
```

Пример ответа `200 OK`:

```json
{ "reviewId": 501 }
```

Состояние: `Assigned → FirstReviewInProgress`. Сохраните `reviewId` в состоянии открытого диалога. Это ID попытки проверки, не ID сообщения.

Повторный `start` от допустимого reviewer при ещё активной попытке возвращает тот же ID. После cancel/undo новый запуск создаёт новую попытку с новым ID. Нельзя считать `start` безусловно идемпотентным при изменившемся состоянии сообщения.

### 4.4. Reviewer 21 подтверждает проверку

```http
POST /api/messages/1001/reviews/approve
X-Debug-User: 21

{ "level": 1, "reviewId": 501, "comment": "Реквизиты проверены" }
```

Успех: `204`. Попытка 501 становится `Approved`, сообщение — `WaitingForSecondReview`. При `PerConfirmation` назначение заканчивается, `currentAssigneeId = null`.

Backend проверяет активную попытку, её уровень и владельца. Нельзя отправить approve без start или подтвердить старый `reviewId` после перезапуска проверки.

### 4.5. Пройти следующие уровни

При приведённой политике назначения диспетчер заново получает candidates перед каждым уровнем. Затем вызовы идут **последовательно**:

| Кто вызывает | Метод и URL | JSON body | Результат |
|---|---|---|---|
| Диспетчер | `POST /api/messages/1001/assign` | `{"assignedTo":22}` | Исполнитель 22; статус остаётся `WaitingForSecondReview` |
| Reviewer 22 | `POST /api/messages/1001/reviews/start` | `{"level":2}` | `200 {"reviewId":502}`; `SecondReviewInProgress` |
| Reviewer 22 | `POST /api/messages/1001/reviews/approve` | `{"level":2,"reviewId":502,"comment":null}` | `WaitingForThirdReview`; исполнитель очищен |
| Диспетчер | `POST /api/messages/1001/assign` | `{"assignedTo":23}` | Исполнитель 23; статус остаётся `WaitingForThirdReview` |
| Reviewer 23 | `POST /api/messages/1001/reviews/start` | `{"level":3}` | `200 {"reviewId":503}`; `ThirdReviewInProgress` |
| Reviewer 23 | `POST /api/messages/1001/reviews/approve` | `{"level":3,"reviewId":503,"comment":null}` | `Completed`; исполнитель очищен |

ID 502 и 503 заменяются фактическими ответами start. Отдельного `/complete` нет: последний обязательный approve завершает сообщение автоматически.

Для `[1]` первый approve сразу даёт `Completed`. Для `[1, 3]` он даёт `WaitingForThirdReview`: уровень 2 пропускается. Следующий уровень нельзя вычислять просто как `level + 1`.

## 5. Как политики меняют последовательность

| Политика workflow | Поведение frontend |
|---|---|
| Назначение | Начать и подтвердить проверку может только текущий назначенный reviewer с правом уровня, включая администраторов |
| `allowReviewerReuse = false` | Уже подтвердивший другой уровень обычный reviewer не может подтверждать следующий |
| `allowReviewerReuse = true` | Повторное участие reviewer допускается при наличии остальных прав |
| `assignmentRetentionMode = PerConfirmation` | После approve/confirm назначение снимается; перед следующим шагом при необходимости назначить заново |
| `assignmentRetentionMode = UntilWorkflowCompleted` | Между подтверждениями исполнитель сохраняется; при необходимости сменить через assign |
| `rejectAllowed = false` | Reject недоступен даже при активной сессии |

После финального approve/confirm и после reject назначение снимается при любой retention-политике. Сохранение назначения само по себе не даёт права на следующий уровень: оно может требовать переназначения через assign из-за запрета reuse или отсутствия нужного permission.

## 6. Immediate: подтверждение одним вызовом

Для `confirmationMode = Immediate` не вызывать start/approve/reject/cancel. Использовать `confirm`.

1. Выбрать сообщение и назначить его на пользователя, который будет подтверждать. `currentAssigneeId` должен совпадать с его ID.
2. Загрузить свежие детали сообщения.
3. Проверить `canConfirm == true`, взять `confirmationLevel` из актуального ответа.
4. Отправить:

```http
POST /api/messages/1001/reviews/confirm

{
  "level": 1,
  "comment": "Подтверждено"
}
```

Версия в запросе не нужна. SQL RowVersion остаётся внутренней защитой от одновременных изменений в базе.

Успех: `204`. Backend атомарно создаёт и подтверждает попытку, пишет аудит и переводит сообщение на следующий обязательный уровень либо в `Completed`. Промежуточный `*ReviewInProgress` не является отдельным пользовательским шагом.

После каждого confirm перечитать сообщение и грид: проверить доступность действия и следующий уровень. При 409 не повторять подтверждение автоматически.

## 7. Альтернативные действия

### Переназначить

Сначала заново получить `assignment-candidates`, затем от имени пользователя с `message.assign`:

```http
POST /api/messages/1001/assign

{ "assignedTo": 22 }
```

Разрешено при существующем исполнителе в `Assigned`, `WaitingForSecondReview`, `WaitingForThirdReview`. Сохраняет статус и историю подтверждений. При активной проверке сначала её владелец отменяет попытку через cancel. Публичного endpoint для ручного unassign нет.

### Отклонить сообщение

Только `ReviewSession`, активная попытка и `rejectAllowed = true`:

```http
POST /api/messages/1001/reviews/reject
X-Debug-User: 21

{ "level": 1, "reviewId": 501, "comment": "Обнаружено расхождение в реквизитах" }
```

Успех: `204`; message → `Rejected`, review → `Rejected`, исполнитель очищен. Комментарий необязателен. Текущий backend проверяет право соответствующего уровня review и владельца активной попытки; отдельного требования `review.reject` в этом handler нет. Администратор имеет исключения из проверки владельца.

`Rejected` — конечное состояние; публичного reopen/retry отклонённого сообщения нет.

### Отменить текущую попытку проверки

```http
POST /api/messages/1001/reviews/cancel
X-Debug-User: 21

{ "level": 1, "reviewId": 501 }
```

Успех: `204`. Review становится `Cancelled`, сообщение возвращается в ожидание того же уровня, назначение сохраняется:

| Отменённый уровень | Состояние после cancel |
|---|---|
| 1 | `Assigned` |
| 2 | `WaitingForSecondReview` |
| 3 | `WaitingForThirdReview` |

Cancel не означает отрицательное решение. Закрытие окна или навигация сами по себе не отменяют попытку: если UX подразумевает отмену, нужен успешный вызов cancel. Следующий start вернёт новый `reviewId`.

### Отозвать последнее подтверждение: undo

Взять ненулевой `undoReviewId` из свежей строки грида:

```http
POST /api/messages/1001/reviews/undo

{ "reviewId": 502, "comment": "Нужна повторная проверка" }
```

Успех: `204`. Подтверждение становится `Undone`; сообщение возвращается в ожидание отменённого уровня (1 → `Assigned`, 2 → `WaitingForSecondReview`, 3 → `WaitingForThirdReview`). История не удаляется. Для повторной проверки требуется новый start либо confirm согласно режиму.

Условия для обычного пользователя:

- Право `review.undo` в scope сообщения.
- Отозвать можно только последний подтверждённый обязательный уровень.
- `undoApprovalMode`: `Disabled` запрещает; `LatestNonFinal` разрешает последнее нефинальное подтверждение; `Latest` допускает также финальное.
- `undoActorMode`: `OriginalReviewer` требует автора подтверждения; `AnyAuthorizedUser` допускает другого уполномоченного пользователя.
- `undoActiveReviewMode`: `Block` запрещает undo при активной следующей проверке; `Cancel` отменяет её вместе с undo.
- При `PerConfirmation` текущий исполнитель очищается; при `UntilWorkflowCompleted` сохраняется, если он ещё есть. После финального завершения старое назначение автоматически не восстанавливается.

У глобального администратора есть исключения: без активной проверки он может обходить ограничения undoApprovalMode и автора; активная проверка всё ещё учитывается политикой. Не вычисляйте доступный undo только из статуса — используйте `undoReviewId` и обрабатывайте отказ backend. Undo применим и к Immediate, если политика разрешает.

### Изменить workflow до обработки

```http
PUT /api/messages/1001/workflow

{ "workflowDefinitionId": 7 }
```

Нужны `workflow.manage`, доступный scope и допустимое состояние `New`/`Assigned`; попыток review не должно быть либо все они должны быть `Cancelled`/`Undone`. Workflow должен быть активен, направление сообщения должно быть известно.

Backend разрешает только workflow, соответствующий текущим routing rules для типа, направления, подразделений и филиала сообщения. Нельзя выбрать произвольную запись из `/workflows`. `canChangeWorkflow` означает доступность действия, но не гарантирует совместимость любого выбранного workflow. После успеха перечитать состояние и политики.

### ViewOnly

`confirmationMode = None` соответствует workflow без review-шагов; сообщение имеет `state = ViewOnly`. Доступны просмотр, PDF и аудит при наличии прав. Assign, смена workflow и review-команды для такого сообщения запрещены, в том числе для администратора.

## 8. История и PDF

```http
GET /api/messages/1001/audit?skip=0&take=100
GET /api/messages/1001/pdf
```

Audit возвращает `{ items, totalCount }`; в событиях есть actor, oldState/newState, details и correlationId. Для полной истории загружать страницы до totalCount либо использовать `/view`, который возвращает всю историю в порядке от старых событий к новым. `details.reviewId` полезен для диагностики, но не заменяет свежий `undoReviewId` для доступности команды.

PDF возвращает `application/pdf`, читать через `response.blob()`, не JSON. Он формируется из сохранённого body сообщения. Отдельного шага «сохранить PDF» для завершения workflow нет.

## 9. Обновление UI и ошибки

После успешного assign/start/approve/reject/cancel/undo/confirm/workflow:

1. Перезагрузить DevExtreme DataSource/CustomStore, чтобы получить новые state, assignee, capabilities и version.
2. Обновить открытую карточку и историю, если они отображаются.
3. Обновить state-counts с тем же assignmentScope, что у раздела.
4. Обновить candidates, если диалог назначения остаётся открыт.

Сообщение может исчезнуть из текущего фильтра или «Моей работы» — это ожидаемо. Не менять локально только state: вместе с ним меняются версия, доступность действий, назначения и review ID. Если команда вернула успех, а обновление UI упало, сообщить «изменения сохранены, обновите данные» и не повторять команду.

Ошибки возвращаются как ProblemDetails. Пример тела при попытке подтвердить неактивный уровень:

```json
{
  "title": "Domain rule violation",
  "status": 409,
  "detail": "Review level 1 is not currently active."
}
```

Дополнительно могут присутствовать стандартные поля ProblemDetails. Для диагностики сохраняйте заголовок ответа `X-Correlation-ID`; можно отправить собственный ID запроса длиной не более 100 символов.

| HTTP | Действие frontend |
|---|---|
| 400 | Исправить тело/уровень/ID; показать detail |
| 401 | Проверить аутентификацию |
| 403 | Нет прав, неверный владелец или недопустимое для пользователя действие; обновить доступность кнопок |
| 404 | Сообщение/сущность отсутствует или сообщение недоступно; обновить список |
| 409 | Конфликт состояния, бизнес-правил или конкурентного изменения; перечитать данные, закрыть устаревшее действие |
| 500 или обрыв сети | Результат может быть неизвестен; сначала проверить сообщение, грид и аудит |

Одинаковая устаревшая операция может дать 403 или 409 в зависимости от того, на какой проверке она остановилась. Блокируйте повторное нажатие, пока команда выполняется. Не запускайте команды одной цепочки параллельно и не повторяйте approve/confirm автоматически после сетевой ошибки.

При восстановлении review-сессии сверяйте ID попытки: одного совпадения state и level недостаточно, поскольку другой start после cancel мог создать новую попытку на том же уровне.

## 10. Карта endpoint для реализации

| Действие | Метод | URL | Тело | Успех |
|---|---|---|---|---|
| Список | POST | `/api/messages/grid` | page/pageSize и плоские фильтры | 200 items/totalCount |
| Детали | GET | `/api/messages/{id}` | — | 200 MessageDetailsDto |
| Карточка и история | GET | `/api/messages/{id}/view` | — | 200 MessageViewDto |
| Кандидаты | GET | `/api/messages/{id}/assignment-candidates` | — | 200 массив кандидатов |
| Назначение / переназначение | POST | `/api/messages/{id}/assign` | assignedTo | 204 |
| Начало/возобновление | POST | `/api/messages/{id}/reviews/start` | level | 200 reviewId |
| Подтверждение сессии | POST | `/api/messages/{id}/reviews/approve` | level, reviewId, comment | 204 |
| Отклонение | POST | `/api/messages/{id}/reviews/reject` | level, reviewId, comment | 204 |
| Отмена сессии | POST | `/api/messages/{id}/reviews/cancel` | level, reviewId | 204 |
| Атомарное подтверждение | POST | `/api/messages/{id}/reviews/confirm` | level, comment | 204 |
| Отзыв подтверждения | POST | `/api/messages/{id}/reviews/undo` | reviewId, comment | 204 |
| Смена workflow | PUT | `/api/messages/{id}/workflow` | workflowDefinitionId | 204 |
| Аудит | GET | `/api/messages/{id}/audit?skip=0&take=100` | — | 200 items/totalCount |
| PDF | GET | `/api/messages/{id}/pdf` | — | 200 application/pdf |

Для негридового поиска существует `POST /api/messages/search` со своим контрактом skip/take/sort/filter. Не смешивать его с `/grid`: для серверного DevExtreme-грида использовать `/grid`.

## 11. Источники и замечания к текущему frontend

Документ описывает фактический backend. На дату сверки в `frontend/src/pages/messages/api/messagesApi.ts` ещё используется GET `/messages/grid` с loadOptions в query, а state-counts типизирован как массив. Это расходится с текущим POST-контрактом грида и объектом счётчиков. Существующие frontend-вызовы не следует копировать как актуальный HTTP-пример. В рамках этой документации код frontend не менялся.

Основные источники:

- [Message endpoints](../src/ORP.Api/Endpoints/MessageEndpoints.cs), [Review endpoints](../src/ORP.Api/Endpoints/ReviewEndpoints.cs).
- [Контракты запросов и ответов](../src/ORP.Application/Contracts/Contracts.cs), [контракты грида](../src/ORP.Application/Grids/GridContracts.cs).
- [Review handlers](../src/ORP.Application/Reviews/ReviewHandlers.cs), [Confirm handler](../src/ORP.Application/Reviews/ConfirmReviewHandler.cs).
- [Доменные правила сообщения](../src/ORP.Domain/Messages/Message.cs), [правила undo](../src/ORP.Domain/Messages/MessageReviewRules.cs).
- [Grid API](FRONTEND_GRID_API.md), [примеры фильтрации](FRONTEND_GRID_SAMPLE.md), [счётчики](FRONTEND_MESSAGE_STAGE_COUNTS.md), [аудит](FRONTEND_AUDIT_API.md).

Старый `MESSAGE_STATE_MACHINE.md` описывает преимущественно стандартный review-сценарий и содержит обобщения, которые не учитывают все текущие политики. Для вариативного поведения использовать реализацию и правила этого руководства.

## Назначить на себя и подтвердить одним запросом

`POST /api/messages/{id}/reviews/assign-and-confirm`, тело:
`{ "level": 1, "comment": "Looks good" }`, ответ `204`.

Для Immediate workflow: свободное сообщение назначается текущему пользователю,
своё назначение используется повторно, чужое возвращает `409`. Нужны `message.assign`,
доступ к processing scope и право текущего уровня. Проверки обычных assign/confirm
сохраняются, включая reviewer reuse. Операции и аудит выполняются в одной транзакции;
при отказе подтверждения назначение также откатывается. `assignedTo` и версия не нужны.
Для New уровень 1; далее ориентироваться на текущий этап workflow, а не количество кликов.
`canConfirm` относится к обычному confirm и может быть false до назначения.
Не повторять объединённую команду автоматически; после успеха перечитать сообщение.
