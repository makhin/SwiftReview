# Обоснование стандартизации фронтенда / Frontend standardisation rationale

## Русская версия

Решение о переработке фронтенда следует объяснять через согласованный стандарт, стоимость интеграции и дальнейшей поддержки. Оценку качества чужого кода лучше заменить конкретными техническими причинами.

### Основные аргументы

1. **Сначала согласуем поведение системы и API, затем реализуем интерфейс.** Не обязательно ждать готового бэкенда: фронтенд и бэкенд можно разрабатывать параллельно, используя моки по согласованным контрактам. Для этого приложения необходимо заранее определить статусы сообщения, переходы между ними, права пользователей, назначение ревьюверов и правила подтверждения. Если интерфейс самостоятельно определяет эти правила, при интеграции возникают переделки.

2. **DevExtreme — основа согласованного подхода к разработке.** Его ценность для команды — повторное использование готовых компонентов, общей темы и одинаковых способов решения типовых задач. Это сокращает объём собственного кода, который необходимо разрабатывать, тестировать и поддерживать. Выигрыш требует последовательного использования библиотеки; само её подключение ничего не стандартизирует.

3. **Стандартизация предполагает отказ от дублирующих реализаций.** Если каждый проект создаёт собственные таблицы, формы и диалоги, общая тема не решает проблему. Должны совпадать не только цвета, но и поведение компонентов, способы их настройки и подход к поддержке. Собственные компоненты остаются уместны для специфичных бизнес-задач или пробелов библиотеки.

4. **Адаптация существующего кода не всегда дешевле замены.** Нужно сравнивать оставшуюся работу: изменение компонентов, подключение реального API, переработку состояний и прав, тестирование и последующую поддержку. Уже затраченное время заслуживает признания, но само по себе не делает сохранение реализации экономически оправданным. Оценку стоимости адаптации следует подкреплять конкретными примерами из старой и новой реализации.

5. **Бизнес-правила должны обеспечиваться сервером.** Интерфейс показывает доступные действия, но сервер обязан проверять права, допустимость переходов и конфликты одновременных действий. Для приложения с ревью это принципиально: скрытая кнопка не заменяет проверку разрешения операции.

6. **Изменение стандарта — ответственность команды и руководства.** Если DevExtreme выбрали после начала разработки, некорректно обвинять разработчика в несоблюдении будущего решения. Нужно признать изменение условий и объяснить, почему после него потребовалась переработка. На будущее стоит согласовывать стек, контракты и один сквозной сценарий до разработки всего интерфейса.

Само использование TanStack или название `features/swift-message` не доказывает плохого качества кода. Обсуждать стоит конкретные проблемы и соответствие принятой архитектуре.

### Текст для разговора с руководством

Я понимаю недовольство из-за того, что значительная часть первоначальной реализации не вошла в итоговую версию. Работа была сделана до появления бэкенда и до согласования общего стандарта компонентов.

После решения использовать DevExtreme и определения серверных контрактов потребовалось привести интерфейс к согласованной архитектуре. По моей оценке, адаптация первоначального фронтенда требовала больше работы, чем реализация необходимых экранов на выбранной основе. Именно это стало причиной переработки.

Нам важно оценивать решение по стоимости завершения, надёжности и дальнейшей поддержке. Сохранение существующего кода имеет смысл там, где оно помогает этим целям.

Чтобы ситуация не повторялась, предлагаю до масштабной разработки согласовывать бизнес-процесс, API-контракты и библиотеку компонентов, а затем совместно проверять один полностью работающий сценарий. Это позволит разрабатывать фронтенд и бэкенд параллельно с меньшим риском переделок.

## English version

The decision to rework the frontend should be explained in terms of the agreed standard, integration effort and ongoing maintenance. Personal judgements about someone else's code should be replaced with specific technical reasons.

### Key arguments

1. **Agree on system behaviour and API contracts before implementing the interface.** A completed backend is not a prerequisite: frontend and backend development can proceed in parallel, using mocks based on agreed contracts. For this application, message states, state transitions, user permissions, reviewer assignment and confirmation rules need to be defined upfront. If the frontend defines these rules independently, integration leads to rework.

2. **DevExtreme provides a foundation for the agreed development approach.** Its value to the team is the reuse of existing components, a shared theme and consistent solutions to common tasks. This reduces the amount of custom code the team needs to develop, test and maintain. These benefits depend on consistent adoption; adding the library alone does not establish a standard.

3. **Standardisation requires moving away from duplicate implementations.** If every project builds its own grids, forms and dialogs, a shared theme does not solve the problem. Component behaviour, configuration and maintenance practices also need to be consistent. Custom components remain appropriate for specific business needs or gaps in the library.

4. **Adapting existing code is not always cheaper than replacing it.** The comparison should cover the remaining work: replacing components, integrating the real API, revising state and permission handling, testing and ongoing maintenance. The effort already invested deserves recognition, but does not by itself justify retaining an implementation. Estimates of adaptation effort should be supported by concrete examples from the original and replacement implementations.

5. **The server must enforce business rules.** The interface presents available actions, but the server must validate permissions, state transitions and conflicts between concurrent actions. This is essential in a review application: hiding a button is not a substitute for authorising an operation.

6. **A change in standards is a shared responsibility of the team and management.** If DevExtreme was selected after development had started, it would be unfair to criticise the developer for not following a decision that had not yet been made. We should acknowledge the change in direction and explain why it required rework. For future projects, the team should agree on the stack, contracts and one end-to-end scenario before building the full interface.

Using TanStack or naming a feature `features/swift-message` does not, by itself, indicate poor code quality. The discussion should focus on specific problems and alignment with the agreed architecture.

### Suggested wording for management

I understand the frustration that a significant part of the original implementation was not included in the final version. That work was completed before the backend was available and before we agreed on a common component standard.

Once we decided to use DevExtreme and defined the backend contracts, the interface needed to be aligned with the agreed architecture. In my assessment, adapting the original frontend required more work than implementing the necessary screens using the selected approach. That was the reason for the rework.

We should evaluate the decision based on the effort required to complete the application, its reliability and its ongoing maintenance. Retaining existing code makes sense where it supports those goals.

To avoid a repeat, I suggest agreeing on the business workflow, API contracts and component library before substantial development begins, then jointly validating one complete end-to-end scenario. This would allow frontend and backend development to proceed in parallel with a lower risk of rework.
