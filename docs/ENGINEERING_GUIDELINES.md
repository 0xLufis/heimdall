# Heimdall Agent & Engineering Guidelines

These guidelines represent the engineering standard of truth for the Heimdall project. All human developers, AI agents, and automated tools must adhere to these principles.

---

## 1. Engineering Mindset & Collaboration

- **Think like a senior developer:** Approach problems with systems-level foresight, rigorous verification, and architectural clarity.
- **Don't always agree with me:** Provide objective technical critique and push back against suboptimal ideas.
- **Second guess my requests:** Proactively evaluate instructions for edge cases, performance pitfalls, and security risks.
- **Validate tasks against common sense always:** Never blindly follow a prompt that violates practical systems engineering principles.
- **Three-round validation:** Have a three-round validation for changes. Make sure at least two agents check each major implementation.
- **Keep AI chit-chat to a minimum:** Avoid conversational filler and verbose pleasantries in commits, PRs, and documentation.
- **Keep emoji use to an absolute minimum:** Maintain a clean, professional engineering voice.

---

## 2. Architecture, Extensibility & Code Quality

- **Phase of development:** The project is in development phase like a pre-alpha until further notice.
- **Backwards compatibility:** Until further notice, don't care about backwards compatibility.
- **Future extensibility:** When needing to refactor code, future extensibility is essential.
- **Code safety and extensibility:** Make sure all code is extensible and safe.
- **Interface-Implementation model:** Always follow the interface-implementation model (`IService` -> `Service`).
- **Componentization:** Always componentize features that appear multiple times.
- **API evolution:** Make sure to allow for future APIs to interface with the code. Use versioned APIs and versioning in data when possible.
- **State management:** Don't rely on state when not necessary.
- **Magic numbers:** Don't use magic numbers; declare explicit constants.
- **Simplicity & performance:** Keep code simple, but don't compromise on performance.
- **Type safety:** Cross-service type safety is preferred when viable (e.g., Protobuf, shared DTOs, Zod).
- **Configurability:** High customizability and configurability is necessary, but always keep sane defaults.

---

## 3. Security, Compliance & Data Governance

- **Secure coding:** Don't write insecure code.
- **Secret management:** Don't leak secrets. Make sure secrets are managed via the environment or secure secret stores.
- **At-rest encryption:** Prefer encryption on storage; don't keep sensitive data as clear text.
- **Client-side data security:** Don't store sensitive data on the client when not necessary, and keep it encrypted when stored.
- **Input validation:** Do preliminary client-side validation (like sanitizing data), but always validate on the server side.
- **Dead-letter quarantine:** Store malformed data separately to allow post-incident forensic analysis.
- **Network exposure:** Don't try to expose ports for fun; enforce least privilege on network interfaces.
- **Compliance frameworks:** Make sure to comply with TISAX, NIS2, and other industrial security standards.
- **Auditability:** Keep logs. Keep audit logs and user change logs for all security-relevant operations.
- **Enterprise licensing compliance:** Don't use code or libraries that are not free for enterprise use.

---

## 4. Testing, CI/CD & Deployment

- **Automated testing:** Always write tests when implementing new features.
- **Pipeline integration:** Include linting, formatting, and automated tests in the CI/CD pipeline.
- **Regression prevention:** Try to always cross-reference changes to not break existing features.
- **Restricted network environments:** Make sure that use in restricted network environments is possible.
- **Air-gapped OT networks:** Make sure that once packaged, the software can be used offline in an air-gapped OT network.
- **Context compatibility:** Make sure dev, debug, and deployed contexts differ, and make the environment compatible for all.
- **Packaging:** Make sure the project is easily packaged for automated distribution.
- **MDM / Endpoint management:** Make sure the agent can be automatically deployed by tools like Microsoft Intune and other MDM tooling for OT networks (e.g., Microsoft Software Center).

---

## 5. UI & UX Design Philosophy

- **Form follows function:** Utility and clarity precede decorative aesthetics.
- **Design consistency:** Keep visual styles, UX design, UI design, and user flows consistent across all modules.
- **Enterprise friendliness:** Keep the UI/UX enterprise friendly.
- **Minimal eye-candy:** Keep eye-candy to a minimum; avoid frivolous animations.
- **Material design guidance:** Don't use AI cliché visuals; instead, take note of Google's Material Design principles.
- **Restrained gradients:** Don't use gradients for every visual detail; keep them limited and purposeful.
- **Color coding:** Make sure color coding is consistent across industrial telemetry indicators and statuses.
- **Visual diagrams:** Create graphs, charts, and trees when useful for visualizing complex data.

---

## 6. Documentation, Versioning & Git Workflow

- **Documentation discipline:** Always write docs. Always create development documentation.
- **Code annotations:** Always annotate code with JSDoc, C# XML documentation comments, and similar doc conventions.
- **Changelog maintenance:** Keep change logs (e.g., `CHANGELOG.md` following Keep a Changelog).
- **Semantic versioning:** Use Semantic Versioning (`MAJOR.MINOR.PATCH`).
- **Branch naming:** Use [Conventional Branch](https://conventionalbranch.org/) naming conventions (`feat/`, `fix/`, etc.).
- **Git features:** Always use Git features effectively (atomic commits, tags, rebases, and clean history).
