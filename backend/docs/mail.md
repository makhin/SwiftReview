
Hi all,

I am currently working on a reusable frontend framework for our project. Recently, I reviewed a similar initiative developed by Alexandr, and we both agreed that **combining our efforts would be much more effective than building two separate solutions.**

Alexandr has already developed a substantial foundation consisting of three projects:

- **[devextreme-theme](https://github.com/makhin/devextreme-theme)**: Corporate branding, design tokens, and DevExtreme styling.
- **[smbc-ui](https://github.com/makhin/smbc-ui)**: Reusable React components, including forms, grids, dialogs, and controls.
- **[smbc-style](https://github.com/makhin/smbc-style)**: An interactive design system demonstrating the components and their usage.

The solution is modular, documented, includes automated tests, and is designed for distribution through our internal Nexus registry.

**The benefits for SMBC are clear:**

- **Less duplicated work and faster delivery** across projects.
- **Consistent UI and simplified maintenance** through shared components.
- **Better utilization of existing DevExtreme licenses**, rather than reinventing functionality we already have.
- **A reusable foundation** that could potentially serve multiple SMBC applications.

Alexandr and I are both interested in jointly developing this solution, combining our experience and the requirements of both teams.

**We would like to request management and architectural support for this collaboration**, including an appropriate allocation of development time.

As a first step, we suggest a short technical review to agree on the direction and next steps.

I've attached a few screenshots illustrating the current implementation.

Best regards,  
[Developer Name]




Hi all,

I am currently working on developing a reusable frontend framework for our project.

During a recent discussion with Alexandr, I discovered that he has already been working on a very similar initiative and has made significant progress. After discussing our approaches, we both felt that **combining our efforts could deliver considerably more value to SMBC than continuing with two independent implementations.**

Alexandr's existing solution consists of three complementary projects:

- **[devextreme-theme](https://github.com/makhin/devextreme-theme)**: A centralized corporate theme with SMBC branding, design tokens, typography, and customized DevExtreme styling.
- **[smbc-ui](https://github.com/makhin/smbc-ui)**: A reusable React/TypeScript component library providing standardized controls, forms, data grids, dialogs, and other common UI elements.
- **[smbc-style](https://github.com/makhin/smbc-style)**: An interactive reference application demonstrating the design system and component usage.

The architecture provides a clear separation between corporate styling, reusable UI components, and application-specific functionality. It also supports independent package versioning and distribution through our internal Nexus registry.

**One particularly important advantage is that SMBC has already invested in DevExtreme licenses.** Building our shared foundation on top of DevExtreme allows us to make better use of that investment rather than developing or adopting another competing component framework. Tailwind CSS is used as a complementary styling and layout tool.

### Benefits for SMBC

- **Avoid duplicated effort:** Both teams are addressing similar challenges. A shared implementation would reduce redundant development.
- **Accelerate delivery:** We can build on an existing foundation instead of starting from scratch.
- **Improve consistency:** Multiple applications could share the same design standards, components, and interaction patterns.
- **Reduce maintenance costs:** Improvements, fixes, and component updates could be shared across projects.
- **Make better use of existing technology:** Maximize the value of our DevExtreme licenses and existing development expertise.
- **Encourage cross-team collaboration:** Combine requirements, experience, and ideas from both projects to create a stronger solution.

The existing implementation already includes a substantial set of reusable components, documentation, an interactive design-system reference, and automated tests. It offers a solid starting point for further development and evaluation.

### My proposal

I would like to propose that **Alexandr and I collaborate on developing a common frontend foundation**, bringing together the existing implementation and the requirements of our respective projects.

Naturally, this would require agreement from both teams' managers and architects, as well as appropriate allocation of development time.

As an initial step, I suggest a joint technical review to evaluate the current implementation, identify any additional requirements, and agree on a shared development roadmap.

I believe this would allow us to deliver a more complete and maintainable solution faster, while avoiding unnecessary duplication of work.

**Instead of two teams independently building similar frameworks, we have an opportunity to create one reusable foundation that could serve multiple SMBC applications.**

I would appreciate your thoughts on this proposal and whether we could move forward with a joint discussion.

Best regards,  
[Developer Name]
