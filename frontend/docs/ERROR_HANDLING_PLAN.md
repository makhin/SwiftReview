# План обработки ошибок frontend

## Цель

Подготовить обработку ошибок к росту приложения, сохранив понятный пользователю
интерфейс и единый канал диагностики. Глобальная инфраструктура должна регистрировать
ошибки, но не подменять локальные состояния страниц, запросов и DevExtreme-компонентов.

План рассчитан на несколько небольших pull request. После каждого этапа должны проходить:

```bash
npm run lint
npm run typecheck
npm test
npm run build
```

## Текущее состояние

Уже реализовано:

- `RouteErrorBoundary` на корневом route ловит ошибки рендера route-компонентов,
  загрузки lazy chunks и route loaders/actions;
- `PageError` предоставляет безопасное общее представление ошибки;
- страница текущего пользователя локально различает 401, 403, 5xx и сетевые ошибки;
- TanStack Query не повторяет 4xx и ограниченно повторяет остальные ошибки;
- DataGrid сообщений загружает данные через DevExtreme `CustomStore`;
- backend преобразует необработанные исключения в `ProblemDetails`, пишет их в лог и
  возвращает correlation ID.

Не хватает:

- единой точки отправки frontend-ошибок в систему наблюдаемости;
- регистрации ошибок, пойманных React boundary;
- регистрации terminal query/mutation errors;
- явной регистрации ошибок `CustomStore` через DataGrid;
- общей нормализации `ProblemDetails` и correlation ID на API-границе;
- согласованной политики: какие ошибки показываются локально, какие требуют уведомления,
  а какие завершают весь route.

## Архитектурные решения

### Глобальный handler отвечает за диагностику, а не за весь UX

Единая функция `reportError(error, context)` отправляет диагностические данные в выбранный
telemetry provider. Она не показывает toast, не выполняет redirect, не перезагружает страницу
и не скрывает ошибку от вызывающего кода.

Пользовательское представление остаётся на ближайшем осмысленном уровне:

| Источник | Где показывать ошибку | Повтор |
|---|---|---|
| Ошибка route/render/chunk | `RouteErrorBoundary` | Перезагрузка или повтор навигации |
| Ошибка page query | Внутри страницы или секции | `refetch()` |
| Ошибка mutation | Рядом с действием; при необходимости toast | Повтор действия пользователем |
| Ошибка DataGrid load | В области таблицы | Штатный `refresh()` DataGrid |
| Фоновая ошибка при наличии stale data | Не заменять рабочий экран | Ненавязчивое уведомление только при необходимости |

### Не строим иерархию исключений заранее

На первом этапе достаточно расширить существующий `ApiError` необязательными полями:

```ts
type ApiErrorOptions = {
  status: number;
  title?: string;
  detail?: string;
  correlationId?: string;
  cause?: unknown;
};
```

Не создаём отдельные классы для каждой HTTP-ошибки. Авторизация, конфликт и временная
недоступность определяются по `status` и, когда backend введёт стабильные машинные коды,
по такому коду.

### TanStack Query и DevExtreme остаются независимыми

Обычные запросы и мутации используют TanStack Query. Загрузка server-backed grid остаётся
в `CustomStore`, поддерживаемом `DevExtreme.AspNet.Data`; её нельзя переносить в TanStack
Query ради унификации обработки ошибок.

### Не устанавливаем browser handlers без необходимости

`window.error` и `unhandledrejection` добавляются только если выбранный telemetry SDK не
регистрирует их самостоятельно. Иначе одно событие может быть отправлено несколько раз.

## Этап 1. Единая модель API-ошибки

### Изменения

1. Расширить `src/shared/api/errors.ts`, сохранив один класс `ApiError`.
2. Добавить небольшую функцию преобразования неуспешного `Response` в `ApiError`.
3. Если ответ имеет content type `application/problem+json`, безопасно прочитать `title`,
   `detail` и correlation ID; некорректное тело не должно скрывать исходный HTTP status.
4. Использовать функцию и в typed OpenAPI API-функциях, и в `messagesApi.ts`.
5. Не показывать пользователю raw `detail`: backend detail может содержать внутреннюю
   информацию. UI выбирает безопасный текст по статусу и контексту операции.
6. Сохранять `AbortError`/отменённый signal как отмену, а не регистрировать его как сбой.

### Тесты

- корректный `ProblemDetails` преобразуется в `ApiError`;
- пустое и некорректное тело ответа не ломает преобразование;
- сохраняются status и correlation ID;
- отменённый запрос не превращается в пользовательскую ошибку;
- текст внутреннего исключения не попадает в UI.

### Критерии готовности

- обе существующие API-интеграции создают ошибки одинакового формата;
- компонентам не требуется разбирать `Response` или JSON;
- текущие retry-правила продолжают различать 4xx и остальные ошибки.

## Этап 2. Канал регистрации ошибок

### Изменения

1. Создать `src/shared/lib/reportError.ts` с минимальным контрактом контекста:
   `source`, `operation`, `route`, `correlationId` и дополнительными безопасными tags.
2. Не отправлять access tokens, request/response body, персональные данные и raw grid filters.
3. В development оставить стандартное поведение React и удобный console output.
4. В production подключить выбранный telemetry provider через эту функцию. Не создавать
   собственный endpoint логирования без отдельного решения по безопасности и эксплуатации.
5. Ошибка самого reporter не должна приводить к новой пользовательской ошибке.

### Критерии готовности

- вызывающий код не зависит от SDK конкретного поставщика;
- тест может подменить reporter без глобального состояния между тестами;
- одно тестовое исключение содержит route, operation и correlation ID;
- чувствительные данные отсутствуют в payload.

## Этап 3. React и route errors

### Изменения

1. Передать production callbacks `onCaughtError`, `onUncaughtError` и
   `onRecoverableError` в `createRoot`.
2. Передавать в reporter React component stack, но никогда не отображать его пользователю.
3. Сохранить `RouteErrorBoundary` как пользовательский fallback.
4. Регистрировать в `RouteErrorBoundary` route response/loader errors, которые не проходят
   через React root callbacks.
5. Не выполнять reload автоматически. Кнопка остаётся явным действием пользователя.
6. Предотвратить двойную отправку одного и того же исключения между root callbacks и
   route boundary; сначала решить это явным разделением источников, а не сложным dedup cache.

### Тесты

- render error показывает безопасный fallback и отправляется reporter один раз;
- rejected lazy import регистрируется без показа URL chunk пользователю;
- 403 и 5xx сохраняют разные безопасные сообщения;
- recoverable React error регистрируется и не заменяет страницу fallback-экраном.

### Критерии готовности

- необработанная render error не оставляет пустой экран;
- production telemetry получает component stack;
- development overlay и console diagnostics не ухудшены.

## Этап 4. TanStack Query и mutations

### Изменения

1. Создать `QueryCache` и `MutationCache` с глобальными `onError` callbacks в
   `src/app/providers/queryClient.ts`.
2. Регистрировать terminal errors после применения retry-политики.
3. Не регистрировать отменённые запросы и ожидаемые validation/authorization errors как
   неожиданные системные сбои.
4. Разрешить query/mutation metadata уточнять `operation` и отключать повторный report,
   если ошибка уже зарегистрирована специализированной интеграцией.
5. Не включать глобальный `throwOnError` и не показывать глобальный toast для query errors.
6. Сохранить локальные `PageError` и `refetch()` на странице текущего пользователя.

### Тесты

- 400/403 не повторяются;
- network/5xx используют текущую ограниченную retry-политику;
- terminal unexpected error регистрируется один раз;
- abort не регистрируется;
- локальный Retry продолжает работать.

### Критерии готовности

- новая query автоматически получает базовую диагностику;
- глобальная политика не заменяет локальное error-состояние;
- существующие тесты `QueryClient` остаются изолированными.

## Этап 5. DevExtreme DataGrid

### Изменения

1. Добавить `onDataErrorOccurred` в `MessagesPage` и передавать ошибку в reporter с
   `source: 'devextreme-grid'` и безопасным именем операции.
2. Сохранить загрузку данных в `CustomStore` и `DevExtreme.AspNet.Data`.
3. Показывать безопасный текст ошибки в области таблицы и предоставить явный retry через
   `DataGrid.instance().refresh()`.
4. Не создавать общий wrapper над DataGrid до появления второго grid с теми же требованиями.
5. После появления второго grid вынести только доказанно общую часть: reporting, безопасное
   сообщение и retry action. Колонки, store и load options остаются у страницы.

### Тесты

- ошибка `CustomStore.load` регистрируется с правильной operation;
- raw backend detail и сериализованные фильтры не отправляются и не отображаются;
- retry вызывает штатный `refresh()`;
- paging, filtering, sorting и grouping продолжают выполняться сервером.

### Критерии готовности

- grid error не теряется и не приводит к пустой странице;
- таблица не использует TanStack Query для load operations;
- пользователь может повторить загрузку без полного reload приложения.

## Этап 6. Политика авторизации и уведомлений

Выполнять после появления production authentication flow и первых mutations.

1. Определить одно место для реакции на 401: обновление сессии, переход на login или
   сообщение о завершении сессии.
2. Не трактовать 403 как истёкшую сессию и не выполнять автоматический redirect.
3. Ввести toast только для ошибок действий, которые не имеют естественной области
   отображения. Query ошибки страниц остаются inline.
4. Для 409/validation errors показывать предметное сообщение рядом с формой или действием.
5. Зафиксировать, какие классы ошибок считаются ожидаемыми и не создают alert в telemetry.

## Этап 7. Проверка в production-like окружении

1. Добавить browser smoke tests для render error, 403, 500, network failure и DataGrid load
   failure.
2. Проверить source maps и убедиться, что они доступны telemetry backend, но не публикуют
   исходники без принятого решения.
3. Проверить correlation ID от frontend события до backend log.
4. Настроить sampling и grouping после появления реальных данных, а не заранее.
5. Зафиксировать runbook: где искать ошибку, как найти backend request и какой минимум
   контекста должен быть у события.

## Рекомендуемый порядок pull request

1. **PR 1 — API errors:** нормализация `ProblemDetails`, correlation ID и тесты.
2. **PR 2 — Error reporting:** `reportError` и интеграция выбранного telemetry provider.
3. **PR 3 — React reporting:** callbacks `createRoot` и уточнение `RouteErrorBoundary`.
4. **PR 4 — Async reporting:** `QueryCache`, `MutationCache` и тесты retry/reporting.
5. **PR 5 — Grid error UX:** `onDataErrorOccurred`, безопасное сообщение и refresh.
6. **PR 6 — Auth and notifications:** только после появления реального authentication flow
   и mutations.

Каждый PR должен быть независимым, не совмещаться с визуальным редизайном и не добавлять
абстракции, для которых ещё нет реального потребителя.

## Отложенные решения

| Решение | Когда рассматривать |
|---|---|
| `window.error` / `unhandledrejection` | Telemetry SDK не перехватывает эти события |
| Общий DataGrid wrapper | Появился второй grid с одинаковым error UX |
| Машинные error codes | Backend гарантирует стабильный контракт кодов |
| Offline queue ошибок | Появилось подтверждённое требование offline mode |
| Собственный logging endpoint | Есть владелец, security review, retention и rate limiting |
| Глобальный toast service | Появилось несколько действий без локального error container |

## Полезные ссылки

- [React: error boundaries](https://react.dev/reference/react/Component#catching-rendering-errors-with-an-error-boundary)
- [React: production error logging in `createRoot`](https://react.dev/reference/react-dom/client/createRoot#error-logging-in-production)
- [TanStack Query: `QueryCache`](https://tanstack.com/query/latest/docs/reference/QueryCache)
- [TanStack Query: `MutationCache`](https://tanstack.com/query/latest/docs/reference/MutationCache)
- [DevExtreme DataGrid: `onDataErrorOccurred`](https://js.devexpress.com/React/Documentation/25_1/ApiReference/UI_Components/dxDataGrid/Configuration/#onDataErrorOccurred)
