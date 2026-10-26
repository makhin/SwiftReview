# Короткий workflow: назначить себя и подтвердить сообщение

Для `SINGLE_CONFIRMATION` и `DOUBLE_CONFIRMATION` пользователь может назначить себя и выполнить все подтверждения. Нужны доступ к processing scope сообщения, `message.assign` и право соответствующего уровня (`review.level1`, затем `review.level2`). Одного права просмотра недостаточно. Пользователь с `message.assign` может назначить себя или другого подходящего reviewer. Подтверждает только текущий назначенный пользователь, включая администраторов. Роль Reviewer включает `message.assign`.

Оба профиля используют `Immediate`: frontend вызывает `confirm`, без `start` и `approve`. Назначение сохраняется между уровнями и снимается при завершении.

```text
SINGLE_CONFIRMATION: выбрать → assign себе → confirm(1) → Completed
DOUBLE_CONFIRMATION: выбрать → assign себе → confirm(1) → confirm(2) → Completed
```

Все запросы выполняются от имени одного пользователя. Локальный API: `http://localhost:5080`; для Development передавать `X-Debug-User` с его username или ID. Для JSON-запросов — `Content-Type: application/json`. ID ниже условные.

После обновления backend нужно применить миграцию `RequireAssignedReviewer`; существующие сообщения пересоздавать не нужно.

## Вариант для одной кнопки

Кнопка **Назначить на себя и подтвердить** может заменить отдельные assign и первый
confirm одним запросом:

```http
POST /api/messages/1001/reviews/confirm

{ "level": 1, "comment": "Looks good" }
```

Успех: `204`. Свободное сообщение назначается текущему пользователю и подтверждается
в одной транзакции. Если уже назначено себе — только подтверждается; если другому —
возвращается `403`, в том числе для администратора. Нужно право уровня; `message.assign` дополнительно требуется только для свободного сообщения.
Любая ошибка откатывает обе операции и аудит. Поддерживаются только Immediate workflow.
Для DOUBLE_CONFIRMATION перечитать сообщение и отдельно подтвердить уровень 2 через `confirm`.
`canConfirm` учитывает автоматическое назначение; при доступном действии `confirmationLevel` содержит текущий уровень.
Не повторять команду автоматически после конфликта или неопределённого сетевого результата.

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

Нужен только `assignedTo`. Сервер проверяет актуальные права, кандидата и состояние внутри транзакции.

`assignedTo` — свой `userId` из `/me`. Успех: `204 No Content`, статус `New → Assigned`.

## 3. Подтвердить первый уровень

Заново получить `GET /api/messages/1001`. Проверить `canConfirm == true`, взять `confirmationLevel`.

```http
POST /api/messages/1001/reviews/confirm

{
  "level": 1,
  "comment": null
}
```

Успех: `204`. Версию передавать не нужно.

- `SINGLE_CONFIRMATION`: статус `Completed`, исполнитель очищен — обработка закончена.
- `DOUBLE_CONFIRMATION`: статус `WaitingForSecondReview`, исполнитель остаётся тем же.

## 4. Подтвердить второй уровень — только DOUBLE_CONFIRMATION

Снова получить `GET /api/messages/1001`. Проверить `canConfirm == true`, `confirmationLevel == 2`.

```http
POST /api/messages/1001/reviews/confirm

{
  "level": 2,
  "comment": null
}
```

Успех: `204`, статус `Completed`, исполнитель очищен. Повторный assign между уровнями и отдельный `/complete` не нужны.

## 5. Обновление экрана и ошибки

После каждого изменения обновлять грид через DevExtreme CustomStore/DataSource, открытую карточку и `GET /api/messages/state-counts`. `204` не содержит JSON.

Команды выполнять последовательно, блокируя повторное нажатие. При 403/409 показать ошибку и обновить данные; не повторять confirm автоматически. После сетевой ошибки сначала проверить состояние и аудит — подтверждение могло сохраниться.

Если сообщение уже назначено себе, пропустить assign. Если назначено другому, использовать тот же `/assign`; проверки прав и кандидатов сохраняются.

[Полное руководство по API и альтернативным действиям](FRONTEND_MESSAGE_WORKFLOW_RU.md).

[Запустить интерактивную демонстрацию на TypeScript](../../tools/workflow-demo/README.md).
