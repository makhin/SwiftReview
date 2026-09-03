# План развития frontend

## Цель

Подготовить frontend к росту без преждевременного усложнения архитектуры:

- подключить TanStack Query для серверного состояния;
- отделить HTTP-вызовы от компонентов;
- уменьшить начальный JavaScript bundle;
- унифицировать loading/error/empty состояния;
- сохранить простую и проверяемую структуру проекта.

План рассчитан на последовательные небольшие pull request. Каждый этап должен оставлять
приложение в рабочем состоянии и проходить `npm test`, `npm run typecheck`,
`npm run lint` и `npm run build`.

## Архитектурные решения

### TanStack Query подключаем сейчас

Первым потребителем будет запрос текущего пользователя `/api/me`. Следующие обычные
GET-запросы и мутации также должны использовать TanStack Query.

TanStack Query подключается через `QueryClientProvider`. Для повторно используемых
запросов применяем `queryOptions`, чтобы query key, query function и политика свежести
были определены в одном месте.

### Универсальный Repository Layer пока не создаём

Вместо `BaseRepository<T>` используем небольшие предметные функции:

```ts
getCurrentUser(signal)
getMessage(id, signal)
assignMessage(messageId, assigneeId)
```

Они отвечают за HTTP-запрос, проверку ответа и преобразование транспортной ошибки в
понятную приложению ошибку. TanStack Query отвечает за жизненный цикл запроса и кеш.
React-компонент отвечает только за отображение состояния.

Вернуться к repository-интерфейсам стоит только при появлении второго источника данных,
offline-хранилища или необходимости подменять несколько реализаций одного доменного API.

### Таблицу сообщений не переносим в TanStack Query

`MessagesPage` использует DevExtreme `CustomStore`, серверную пагинацию, сортировку и
фильтрацию. `CustomStore` остаётся владельцем загрузки данных таблицы. Не следует
добавлять поверх него второй кеш с ключами для всех комбинаций `LoadOptions`.

TanStack Query можно применять для отдельных операций над сообщением: details, assign,
approve и справочники. После мутации таблица обновляется через её штатный `refresh()`.

### Глобальный client state store пока не добавляем

- серверное состояние: TanStack Query или DevExtreme `CustomStore`;
- состояние фильтров и навигации: URL;
- локальное состояние интерфейса: `useState`;
- действительно глобальные настройки: небольшой React Context.

Redux или Zustand добавляются только при появлении общего изменяемого клиентского
состояния, которое нельзя естественно разместить в этих слоях.

## Этап 1. Подключить TanStack Query

### Изменения

1. Установить runtime-зависимость `@tanstack/react-query`.
2. Установить `@tanstack/eslint-plugin-query` как dev dependency и подключить его
   рекомендуемые правила к текущему flat ESLint config.
3. Создать `src/app/providers/queryClient.ts` с единственным application-level `QueryClient`.
4. Создать `src/app/providers/AppProviders.tsx` и подключить `QueryClientProvider` вокруг router.
5. Не подключать React Query Devtools в production bundle. При необходимости добавить
   их позже как development-only lazy dependency.

### Начальные настройки

Не задавать одинаковый `staleTime` для всех данных. Для начала:

| Данные | `staleTime` | Обоснование |
|---|---:|---|
| Текущий пользователь | 5 минут | Identity редко меняется в рамках сессии |
| Детали сообщения | 30 секунд | Данные могут изменяться операторами |
| Справочники | 30 минут | Редко меняются |

Оставить `gcTime` по умолчанию, пока измерения не покажут необходимость изменить его.
Retry должен учитывать тип ошибки: не повторять 4xx, разрешить ограниченный повтор для
сетевых ошибок и 5xx. В тестовом `QueryClient` всегда использовать `retry: false`.

### Критерии готовности

- приложение запускается с `QueryClientProvider`;
- provider имеет отдельный unit test;
- никакой запрос ещё не дублируется между TanStack Query и `CustomStore`;
- lint, typecheck, tests и build проходят.

## Этап 2. Перенести текущего пользователя

### Целевая структура

```text
src/
  app/
    providers/
      AppProviders.tsx
      queryClient.ts
  shared/api/
    client.ts
    errors.ts
  pages/current-user/
    currentUserApi.ts
    currentUserQueries.ts
    CurrentUserPage.tsx
```

### Изменения

1. Вынести HTTP-вызов из `CurrentUserPage` в `getCurrentUser(signal)`.
2. Ввести небольшой `ApiError` со статусом ответа; не строить общую иерархию ошибок.
3. Создать `currentUserQueryOptions()` с ключом `['current-user']`.
4. Заменить ручные `useEffect`, `AbortController`, `user` и `error` на `useQuery`.
5. Сохранить текущие loading, success и error состояния интерфейса.
6. Добавить явную кнопку Retry в error state через `refetch()`.

### Тесты

- успешная загрузка профиля;
- пустые permissions/branches/departments;
- 4xx без автоматических повторов;
- сетевая или 5xx ошибка с ожидаемой политикой retry;
- повторный mount использует свежие данные из кеша;
- каждый тест получает новый `QueryClient`, чтобы кеш не протекал между тестами.

### Критерии готовности

- в `CurrentUserPage` нет ручного жизненного цикла HTTP-запроса;
- повторное открытие страницы в пределах `staleTime` не создаёт новый запрос;
- пользователь может повторить неуспешную загрузку;
- существующие показатели покрытия не снижаются.

## Этап 3. Стабилизировать API-границу

1. Все новые endpoint-функции размещать рядом с соответствующей фичей.
2. Использовать типы из сгенерированного `schema.d.ts`; не дублировать DTO вручную.
3. Нормализовать ошибки в API-функциях, а не в React-компонентах.
4. Ввести фабрики query keys по мере появления параметризованных запросов:

```ts
const messageKeys = {
  all: ['messages'] as const,
  detail: (id: number) => ['messages', 'detail', id] as const,
};
```

5. После мутаций инвалидировать только связанные ключи, а не весь кеш.
6. Не делать общий `apiRepository`, `queryService` или wrapper над `useQuery` без
   повторяющейся предметной необходимости.

## Этап 4. Уменьшить initial bundle

Текущая production-сборка предупреждает о крупном JavaScript chunk. Основная мера —
ленивая загрузка route-компонентов.

1. Перевести `CurrentUserPage`, `MessagesPage` и особенно `DesignSystemPage` на dynamic imports.
2. Добавить route-level fallback для загрузки chunk.
3. Проверить, что код design-system и тяжёлые модули DevExtreme не входят в initial
   application chunk.
4. Зафиксировать размеры initial JS до и после изменения в описании PR.
5. Не настраивать ручное разбиение vendor chunks, пока route splitting не измерен.

### Критерии готовности

- каждый крупный route имеет отдельный chunk;
- initial chunk заметно меньше baseline;
- прямое открытие каждого URL и browser refresh работают;
- loading fallback доступен для screen reader.

## Этап 5. Ошибки и состояния страниц

1. Добавить route-level error boundary для ошибок рендера и загрузки chunk.
2. Создать простые общие компоненты `PageLoading`, `PageError` и `EmptyState` только
   после появления второго реального потребителя каждого компонента.
3. Для query-ошибок показывать понятное сообщение и доступное действие Retry.
4. Ошибки авторизации 401/403 обрабатывать отдельно от временных 5xx.
5. Не показывать пользователю raw stack trace или внутренний текст backend exception.

## Этап 6. Интеграционные и browser-тесты

Текущий Vitest suite сохраняется как быстрый unit/component уровень. Дополнительно:

1. Подключить MSW, когда появится второй экран на TanStack Query, и тестировать реальные
   HTTP-вызовы клиента без мокирования внутренних модулей.
2. Оставить unit tests API-функций для сериализации параметров и ошибок.
3. Добавить Playwright после стабилизации основных user flows.
4. Первый минимальный browser smoke suite:
   - открытие `/me`;
   - открытие и фильтрация `/messages`;
   - навигация через global header;
   - отображение 403 и временной серверной ошибки.
5. Не включать `src/pages/design-system/**` и `src/theme/**` в unit coverage согласно текущей
   политике проекта.

## Этап 7. URL как состояние навигации

После появления продуктовых фильтров:

- хранить устойчивые фильтры, сортировку, страницу и выбранную вкладку в query string;
- поддержать Back/Forward и ссылки на конкретное состояние;
- не дублировать одно значение одновременно в URL, Context и query cache;
- определить ограничения для слишком больших DevExtreme filter expressions до их
  сериализации в URL.

## Отложенные решения и условия возврата к ним

| Решение | Когда рассматривать |
|---|---|
| Repository interfaces | Появился второй источник данных или offline mode |
| Persisted query cache | Есть подтверждённый offline/slow-network сценарий |
| Redux/Zustand | Появилось сложное общее клиентское состояние |
| React Query Devtools | Query-граф стал достаточно сложным для регулярной диагностики |
| Общая design-system библиотека | Компоненты используются несколькими приложениями |
| Microfrontend | Независимые команды и релизные циклы действительно этого требуют |

## Рекомендуемый порядок pull request

1. **PR 1 — Query foundation:** зависимости, provider, ESLint plugin, test helper.
2. **PR 2 — Current user migration:** API-функция, query options, новый `CurrentUserPage`.
3. **PR 3 — Route splitting:** lazy routes и измерение bundle.
4. **PR 4 — Error UX:** route boundary и retry состояния.
5. **PR 5 — MSW:** интеграционные тесты запросов после появления второго query flow.

Каждый PR должен быть небольшим и не совмещаться с визуальным редизайном или
несвязанным рефакторингом.

## Полезные ссылки

- [TanStack Query: installation](https://tanstack.com/query/latest/docs/framework/react/installation)
- [TanStack Query: QueryClientProvider](https://tanstack.com/query/latest/docs/framework/react/reference/functions/QueryClientProvider)
- [TanStack Query: queryOptions](https://tanstack.com/query/latest/docs/framework/react/reference/queryOptions)
- [TanStack Query: testing](https://tanstack.com/query/latest/docs/framework/react/guides/testing)
- [TanStack Query: retries](https://tanstack.com/query/latest/docs/framework/react/guides/query-retries)
- [TanStack Query: prefetching and router integration](https://tanstack.com/query/latest/docs/framework/react/guides/prefetching)
