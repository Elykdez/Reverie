# C# Conventions

## Naming

- Use `PascalCase` for types, methods, properties, events, constants and `static readonly` fields.
- Use `camelCase` for instance fields, parameters and local variables. Do not prefix fields with `_` or `m_`.
- Omit `private` on class members because it is the default. Keep explicit access modifiers whenever they communicate or change the contract.
- Match the file name to its primary type. Keep additional top-level types in that file only when they are tightly coupled data types.
- Let the namespace provide project identity. Do not add a redundant `Reverie` prefix to new types solely for branding; preserve existing serialized type names unless a rename is intentional and fully migrated.

## Style

- Use four-space indentation and Allman braces. Keep namespace contents indented.
- Put attributes above declarations. Short related attributes may share one attribute list; wrap long attribute arguments vertically.
- Use trailing commas in multiline enum declarations, collection/object initializers and argument lists where the formatter permits them.
- Keep comments rare and explain constraints or intent, not line-by-line mechanics.

## Namespaces

- Use block-scoped namespaces with braces.
- Use `Reverie` for shared, cross-module and project-integration runtime code.
- Use `Reverie.Editor` for editor code and editor-only validation or tests.
- Use `Reverie.UI` for UI code.
- Use `Reverie.<ModuleName>` for feature modules, for example `Reverie.Captura`, `Reverie.Controller`, `Reverie.Dialogue` and `Reverie.Asset`.
- Add only the cross-namespace `using` directives a file requires. Use aliases when two type names collide or an alias materially improves readability.
- Do not apply Reverie namespaces or style rewrites to `Assets/3rd` or package code.

## Serialized State

- On `MonoBehaviour`, `EditorWindow` and similar implementation types, keep inspector-only fields non-public and mark them with `[SerializeField]`. Put the attribute on the line above the field and omit the redundant `private` modifier.
- Keep serialized field names in `camelCase`. Group and constrain inspector data with attributes such as `[Header]`, `[Tooltip]`, `[Min]`, `[Range]` and `[TextArea]`, and provide intentional defaults where appropriate.
- Public `camelCase` fields are acceptable for `ScriptableObject` authoring data and serializable data-transfer types when the fields are the intended data contract. Do not expose component implementation fields merely to make them visible in the Inspector.
- Expose runtime state and behavior through `PascalCase` properties and methods rather than by widening serialized component fields.
- Treat serialized names and type identities as persistent data. Do not rename a serialized field without `[FormerlySerializedAs]` or a complete asset migration.
- When renaming or moving a serialized type, preserve its `.meta` GUID and migrate all namespace-sensitive references, including `m_EditorClassIdentifier`, managed-reference `{class, ns, asm}` entries and reflection strings. Use `[MovedFrom]` when old serialized identities may still exist outside the repository migration.
- After serialization changes, scan scenes, prefabs and assets for stale identities, then compile both runtime and editor assemblies.

## Component Wiring

**Prefab-owned Component should be wired, not discovered.**

- When a component surface should exist in the project, create the actual prefab/scene object and serialize the references through Unity.
- Never assume a GameObject, component, prefab, or serialized reference exists. Inspect and verify the actual prefab/scene wiring before relying on it.
- Do not add fallback behavior, runtime discovery/creation, or optional null-handling unless the user explicitly asks for it.
- Do not add runtime resolver/factory fallbacks such as `ResolveXReferences`, `GetComponentInChildren` searches, or `gameObject.AddComponent` to paper over missing component wiring.
- If a required dependency is missing, wire it in the prefab/scene or report the missing wiring; do not mask it with a fallback.
- If a serialized component reference is explicitly optional, skip the related binding/action with a null check or null-conditional call, and add a log.

## Prefab

- Author reusable Unity GameObject hierarchies, including conversation UI, as prefabs.
- Instantiate and wire existing prefabs. Do not construct reusable hierarchies in runtime code or add procedural creation fallbacks.
- Keep layout, child objects and component configuration in prefab assets. Runtime scripts should drive behavior, content and state.
- Required component references must be serialized and wired in the prefab or scene. Do not hide missing wiring with runtime discovery, component creation or procedural fallback paths.
- Verify the actual prefab or scene wiring before relying on it. Optional references must be explicitly documented and handled as optional.
