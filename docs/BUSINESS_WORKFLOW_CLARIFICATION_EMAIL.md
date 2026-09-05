# Business workflow clarification email

## English

**Subject:** Clarification needed: review sequence and reviewer assignment rules

Hi team,

We are reviewing the workflow rules for assigning and approving SWIFT messages and would like to confirm the expected business behaviour.

Could you please clarify the following points?

1. **Review level sequence**

   Must every workflow start with review level 1 and proceed in ascending order: level 1, then level 2, then level 3?

2. **Optional review levels**

   Can an optional level be skipped? For example, is a workflow with required levels 1 and 3, while level 2 is optional, valid?

3. **Initial assignment model**

   How should an unassigned message receive its first reviewer?

   - Does the system assign it automatically?
   - Does a user select an unassigned message and assign it to themselves?
   - Does an administrator or team leader assign it?
   - Should more than one of these options be supported?

4. **Automatic assignment pool**

   If assignment is automatic, which users should be included in the eligible pool?

   Please confirm whether selection should consider:

   - the user's role and permission for the required review level;
   - access to the message's branch and department;
   - whether the user is active and available;
   - the user's current workload;
   - whether the user has already reviewed or approved the message;
   - any team, location, shift, or substitute-reviewer rules.

   Please also clarify how the system should choose between equally eligible users and what should happen when no eligible reviewer is available.

5. **Assignment after approval**

   When another review level is required, should the system automatically assign the message to the next reviewer, leave it unassigned for self-selection, or wait for an administrator to assign it?

6. **Manual reassignment and the four-eyes rule**

   If a user approved an earlier level, should the system prevent that user from being manually assigned to a later review level? They would otherwise be unable to perform the review because of the four-eyes rule.

   Should this restriction apply only to earlier approvals, or also to users whose earlier review was rejected or undone?

7. **Who may assign and reassign messages?**

   Should these actions be available only to administrators and team leaders, or may reviewers assign messages to themselves or transfer them to another reviewer?

8. **Reassignment during an active review**

   If a message is reassigned while a review is already in progress, should the active review remain with the user who started it, be transferred to the new assignee, or must reassignment be prohibited until the active review is completed?

9. **Workload and availability**

   How should reviewer workload be calculated: all assigned messages, only active reviews, or weighted by review level or message complexity? How should absence, shifts, temporary unavailability, and workload limits affect eligibility?

10. **Fallback assignment**

    If no eligible reviewer is available in the message's branch or department, should the message remain unassigned and be escalated, or may the system use a reviewer from another approved pool?

11. **Self-assignment and prioritisation**

    If reviewers may select work themselves, which unassigned messages should they see, how should those messages be prioritised, and what should happen if two users try to claim the same message at the same time?

12. **Rejected messages**

    Is rejection final, or should a rejected message be corrected and returned to the review process? If it can be reopened, who may do so and from which review level should processing continue?

13. **Undo and administrative overrides**

    Who may undo an approval, within what time period, and under what conditions? Should an administrator be able to override assignment or review restrictions, and how should such an override be approved and audited?

14. **Workflow configuration changes**

    When a workflow is changed or deactivated, should messages already registered under that workflow continue with the original configuration or adopt the new one?

15. **Escalation and service levels**

    Are there deadlines for assignment or review? If a message remains unassigned or overdue, who should be notified and when should it be escalated?

The current documentation and implementation contain some of these behaviours, but we would like to confirm the intended business rules before treating them as mandatory constraints.

Examples of valid workflows and assignment scenarios would also be very helpful.

Thank you.

## Русский перевод

**Тема:** Требуется уточнение: последовательность проверок и правила назначения проверяющих

Здравствуйте, коллеги!

Мы анализируем правила процесса назначения и проверки SWIFT-сообщений и хотели бы подтвердить ожидаемую бизнес-логику.

Просим уточнить следующие вопросы.

1. **Последовательность уровней проверки**

   Должен ли каждый workflow начинаться с уровня 1 и продолжаться по возрастанию: сначала уровень 1, затем уровень 2 и уровень 3?

2. **Необязательные уровни проверки**

   Можно ли пропускать необязательный уровень? Например, допустим ли workflow, в котором уровни 1 и 3 обязательны, а уровень 2 необязателен?

3. **Модель первоначального назначения**

   Как сообщение без ответственного должно получить первого проверяющего?

   - Система назначает его автоматически?
   - Пользователь выбирает неназначенное сообщение и назначает его себе?
   - Сообщение назначает администратор или руководитель группы?
   - Должна ли система поддерживать несколько этих вариантов одновременно?

4. **Пул автоматического назначения**

   Если назначение выполняется автоматически, какие пользователи должны входить в пул подходящих кандидатов?

   Просим подтвердить, должны ли при выборе учитываться:

   - роль пользователя и право на выполнение требуемого уровня проверки;
   - доступ к филиалу и подразделению сообщения;
   - активность и доступность пользователя;
   - текущая загрузка пользователя;
   - участие пользователя в предыдущей проверке или подтверждении этого сообщения;
   - правила, связанные с командой, местоположением, сменой или замещающими проверяющими.

   Также просим уточнить, как система должна выбирать между равноценными кандидатами и что должно происходить, если подходящего проверяющего нет.

5. **Назначение после подтверждения**

   Если требуется следующий уровень проверки, должна ли система автоматически назначить следующего проверяющего, оставить сообщение без ответственного для самостоятельного выбора или ожидать назначения администратором?

6. **Ручное переназначение и принцип четырёх глаз**

   Если пользователь подтвердил предыдущий уровень, должна ли система запрещать вручную назначать его на последующий уровень? Иначе пользователь не сможет выполнить проверку из-за принципа четырёх глаз.

   Должно ли это ограничение относиться только к предыдущим подтверждениям или также к пользователям, чья предыдущая проверка была отклонена или отменена?

7. **Кто может назначать и переназначать сообщения?**

   Должны ли эти действия быть доступны только администраторам и руководителям групп или проверяющие также могут назначать сообщения себе и передавать их другому проверяющему?

8. **Переназначение во время активной проверки**

   Если сообщение переназначается во время уже начатой проверки, должна ли активная проверка остаться у начавшего её пользователя, перейти к новому ответственному или переназначение следует запретить до завершения проверки?

9. **Загрузка и доступность**

   Как следует рассчитывать загрузку проверяющего: по всем назначенным сообщениям, только по активным проверкам или с учётом веса уровня проверки и сложности сообщения? Как отсутствие, смены, временная недоступность и лимиты загрузки должны влиять на выбор кандидата?

10. **Резервное назначение**

    Если в филиале или подразделении сообщения нет подходящего проверяющего, должно ли сообщение остаться без ответственного с последующей эскалацией или система может выбрать пользователя из другого разрешённого пула?

11. **Самостоятельное назначение и приоритизация**

    Если проверяющие могут самостоятельно выбирать работу, какие неназначенные сообщения они должны видеть, как эти сообщения следует приоритизировать и что должно произойти, если два пользователя одновременно пытаются взять одно сообщение?

12. **Отклонённые сообщения**

    Является ли отклонение окончательным или отклонённое сообщение можно исправить и вернуть в процесс проверки? Если его можно открыть повторно, кто имеет на это право и с какого уровня должна продолжиться проверка?

13. **Отмена подтверждения и административные исключения**

    Кто может отменить подтверждение, в течение какого времени и при каких условиях? Может ли администратор обходить ограничения назначения или проверки, и как такое исключение должно согласовываться и фиксироваться в аудите?

14. **Изменение конфигурации workflow**

    Если workflow изменён или деактивирован, должны ли уже зарегистрированные с ним сообщения продолжать обработку по первоначальной конфигурации или перейти на новую?

15. **Эскалация и уровни обслуживания**

    Установлены ли сроки назначения и проверки? Если сообщение остаётся без ответственного или срок обработки нарушен, кого и когда необходимо уведомить и на каком этапе выполнять эскалацию?

Текущая документация и реализация уже содержат некоторые из этих правил, однако мы хотели бы подтвердить ожидаемую бизнес-логику, прежде чем считать их обязательными ограничениями процесса.

Также будут полезны примеры допустимых workflow и сценариев назначения.

Спасибо!
