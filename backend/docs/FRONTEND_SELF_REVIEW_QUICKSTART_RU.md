# Короткий workflow: назначить себя и подтвердить сообщение

Для `SINGLE_CONFIRMATION` и `DOUBLE_CONFIRMATION` пользователь может назначить себя и выполнить все подтверждения. Нужны доступ к processing scope сообщения, `message.assign` и право соответствующего уровня (`review.level1`, затем `review.level2`). Одного права просмотра недостаточно. Самоназначение определяется настройкой `AllowSelfAssignment` в базе, а не кодом workflow. Для этих двух профилей она включена миграцией/seed; для другого workflow её можно включить отдельно.

Оба профиля используют `Immediate`: frontend вызывает `confirm`, без `start` и `approve`. Назначение сохраняется между уровнями и снимается при завершении.

```text
SINGLE_CONFIRMATION: выбрать → assign себе → confirm(1) → Completed
DOUBLE_CONFIRMATION: выбрать → assign себе → confirm(1) → confirm(2) → Completed
```

Все запросы выполняются от имени одного пользователя. Локальный API: `http://localhost:5080`; для Development передавать `X-Debug-User` с его username или ID. Для JSON-запросов — `Content-Type: application/json`. ID ниже условные.

После обновления backend нужно применить миграцию `AddWorkflowSelfAssignment`; существующие сообщения пересоздавать не нужно.

## 1. Выбрать сообщение

```http
GET /api/me
```

Сохранить `userId`, например `21`.

```http
POST /api/messages/grid

{ "status": "New", "page": 1, "pageSize": 20 }
```

Из `items` выбрать сообщение, например `id = 1001`, с `currentAssigneeId = null`. По `workflowDefinitionId` и справочнику `GET /api/workflows` проверить код `SINGLE_CONFIRMATION` либо `DOUBLE_CONFIRMATION`.

```http
GET /api/messages/1001
GET /api/messages/1001/assignment-candidates
```

Показать `body` и реквизиты. Собственный `userId` должен присутствовать в кандидатах: это означает, что backend разрешает назначение на текущем этапе.

## 2. Назначить себя

```http
POST /api/messages/1001/assign

{ "assignedTo": 21 }
```

`assignedTo` — свой `userId` из `/me`. Успех: `204 No Content`, статус `New → Assigned`.

## 3. Подтвердить первый уровень

Заново получить `GET /api/messages/1001`. Проверить `canConfirm == true`, взять `confirmationLevel` и свежую `version`.

```http
POST /api/messages/1001/reviews/confirm

{
  "level": 1,
  "expectedVersion": "AAAAAAAAB9E=",
  "comment": null
}
```

`expectedVersion` обязательно заменить на `version` из последнего ответа; строка в примере условная. Успех: `204`.

- `SINGLE_CONFIRMATION`: статус `Completed`, исполнитель очищен — обработка закончена.
- `DOUBLE_CONFIRMATION`: статус `WaitingForSecondReview`, исполнитель остаётся тем же.

## 4. Подтвердить второй уровень — только DOUBLE_CONFIRMATION

Снова получить `GET /api/messages/1001`. Проверить `canConfirm == true`, `confirmationLevel == 2` и взять **новую** `version`.

```http
POST /api/messages/1001/reviews/confirm

{
  "level": 2,
  "expectedVersion": "AAAAAAAAB9I=",
  "comment": null
}
```

Подставить фактическую новую версию. Успех: `204`, статус `Completed`, исполнитель очищен. Повторный assign между уровнями и отдельный `/complete` не нужны.

## 5. Обновление экрана и ошибки

После каждого изменения обновлять грид через DevExtreme CustomStore/DataSource, открытую карточку и `GET /api/messages/state-counts`. `204` не содержит JSON.

Команды выполнять последовательно, блокируя повторное нажатие. При 403/409 показать ошибку и обновить данные; не повторять confirm автоматически. После сетевой ошибки сначала проверить состояние и аудит — подтверждение могло сохраниться.

Если сообщение уже назначено себе, пропустить assign. Если назначено другому, самоназначение через `/assign` не сработает: смена исполнителя выполняется отдельным `/reassign` с тем же телом и проверками прав/кандидатов.

[Полное руководство по API и альтернативным действиям](FRONTEND_MESSAGE_WORKFLOW_RU.md).
