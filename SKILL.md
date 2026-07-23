---
name: unity-dev
description: General Unity C# coding standards, architecture patterns, and best practices. Applies to all Unity projects.
---

# Unity Development Skill

These are the coding standards and patterns to follow for all Unity C# projects.
For any project-specific architecture, refer to the `PROJECT_GUIDE.md` or `.agents/` folder inside the project repo.

---

## 1. Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Methods / Functions | PascalCase | `UpdateTileColor()`, `FulfillOrder()` |
| Public Properties | PascalCase | `IsBetweenDays`, `CoinsCounter` |
| Private / local fields | camelCase | `timePassed`, `spawnCount`, `cancellationToken` |
| Constants | UPPER_SNAKE_CASE | `MAX_MIX_COUNT`, `DEFAULT_MODEL` |
| Serialized fields | `[SerializeField]` on its own line above the field | (see section 2) |

---

## 2. Field Declaration & Inspector Layout

- Use `[SerializeField] private` for all inspector-visible fields. Avoid `public` fields unless strictly necessary.
- Group related fields under `[Header("GroupName")]`:

```csharp
[Header("Config")]
[SerializeField] private float moveSpeed = 5f;
[SerializeField] private int maxHealth = 3;

[Header("References")]
[SerializeField] private Transform spawnPoint;
```

- Use `{ get; private set; }` for properties that are read-only to other systems but writable internally.
- Use `[RequireComponent(typeof(T))]` when a MonoBehaviour always depends on another component.

---

## 3. Code Style — `var` and Spacing

- **Always prefer `var`** on the left-hand side of a declaration when the type is clear from the right-hand side:

```csharp
// GOOD
var player = GetComponent<PlayerController>();
var enemies = new List<Enemy>();
var distance = Vector3.Distance(a, b);

// BAD
PlayerController player = GetComponent<PlayerController>();
List<Enemy> enemies = new List<Enemy>();
float distance = Vector3.Distance(a, b);
```

- Only use an explicit type when `var` would genuinely obscure what the variable holds (e.g. return types of opaque factory methods).

- **Spacing rules:**
  - One blank line between every method.
  - One blank line between logically distinct blocks inside a method.
  - No trailing whitespace.
  - Opening braces `{` always on their own line (Allman style).
  - Always use braces even for single-line `if` bodies.

```csharp
// GOOD
if (isReady)
{
    StartGame();
}

// BAD
if (isReady) StartGame();
```

---

## 4. MonoBehaviour Lifecycle Rules

- **`Awake()`**: Initialize internal state, cache components, set Singleton references.
- **`Start()`**: Resolve external dependencies, trigger initial game logic.
- **`OnEnable()` / `OnDisable()`**: Subscribe and unsubscribe from events here — never elsewhere.
- **`Update()`**: Keep lean. Delegate all non-trivial logic to descriptively named private helper methods. No heavy allocations, no `GetComponent`, no LINQ in the hot path.
- **Keep MonoBehaviours as thin shells.** All real logic should live in plain C# classes (services, controllers, utils) that are injected in. This makes systems unit-testable without needing a running Unity scene.

```csharp
// BAD — logic directly in MonoBehaviour
private void Update()
{
    if (health <= 0 && !isDead)
    {
        isDead = true;
        // 20 lines of death logic...
    }
}

// GOOD — MonoBehaviour delegates to injected service
private void Update()
{
    _healthService.Tick(Time.deltaTime);
}
```

---

## 5. SOLID & DRY Principles

- **Single Responsibility**: Every class and every method does exactly one thing. If you find yourself writing "and" to describe a method's purpose, split it.
- **Open/Closed**: Prefer extending behaviour via interfaces and composition rather than modifying existing classes.
- **Liskov Substitution**: Subtypes must be usable in place of their base type without breaking behaviour.
- **Interface Segregation**: Prefer small, focused interfaces (`IInteractable`, `IUnlockable`) over large, monolithic ones.
- **Dependency Inversion**: Depend on abstractions (interfaces), not concrete classes. Inject dependencies rather than instantiating them internally.
- **DRY**: If the same logic appears more than once, extract it into a shared helper or service. Never duplicate code across classes — create a utility method or a base class.

---

## 6. Dependency Injection (VContainer)

- Prefer VContainer over Singletons wherever possible. Avoid adding new Singletons unless managing a true root-level system.
- Inject dependencies via `[Inject]` on a `Construct()` method:

```csharp
[Inject]
public void Construct(IMyService service)
{
    this.service = service;
}
```

- Register new services in the project's `LifetimeScope` file. Prefer binding to interfaces over concrete types.
- Use `RegisterEntryPoint<T>()` for classes that need to run logic on scene startup without being a MonoBehaviour.

---

## 7. Async Programming (UniTask)

- Use **UniTask** for all asynchronous operations. Never use raw `System.Threading.Tasks.Task` or Unity Coroutines.
- Always pass a `CancellationToken` to prevent tasks running on destroyed objects:

```csharp
await UniTask.Delay(TimeSpan.FromSeconds(2f), cancellationToken: this.GetCancellationTokenOnDestroy());
```

- For fire-and-forget tasks, call `.Forget()`:

```csharp
RunAnimationAsync().Forget();
```

- When delaying while the game may be paused (`Time.timeScale == 0`), always pass `ignoreTimeScale: true`.

---

## 8. Animations & Tweening (DOTween)

- Use **DOTween** for all visual animations, transitions, shakes, and UI effects. Avoid writing manual lerp loops in `Update()`.
- For any tween that must play while the game is paused, chain `.SetUpdate(true)`:

```csharp
transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
```

---

## 9. Input System

- Use **Unity's new Input System** exclusively. Never use `Input.GetKeyDown()` or any legacy input API.
- Map inputs through generated action classes and a dedicated helper/key-map class.
- Always guard input callbacks against paused state:

```csharp
public void OnInteract(InputAction.CallbackContext ctx)
{
    if (Time.timeScale == 0) return;
    if (ctx.started) { HandleInteract(); }
}
```

---

## 10. Performance & Memory (No GC Spikes)

- Always prefer using serialized fields (`[SerializeField] private`) to assign component references in the Inspector. Retrieving components dynamically at runtime (via `GetComponent<T>()`, `GetComponentInChildren<T>()`, or `FindObjectOfType<T>()`) is a last resort. Caching them in `Awake()` or `Start()` should only be used when serialization is impossible (such as referencing scene-level objects from prefabs, although bridging via singleton/manager serialized fields is preferred). Never call these lookup methods inside `Update()` or loops.
- Use `TryGetComponent<T>()` for safe component lookups in triggers and collisions:

```csharp
if (collision.TryGetComponent(out IInteractable item))
{
    item.OnInteractionTrigger(this);
}
```

- **Never allocate inside hot paths**: avoid `new List<T>()`, `new string`, LINQ (`.Where`, `.Select`, `.ToList()`), or `string +` concatenation inside `Update()`, `FixedUpdate()`, or any per-frame loop.
- Pre-allocate collections and reuse them. Use object pooling for frequently spawned/destroyed GameObjects.
- Use `StringBuilder` for any string building in UI refresh or label update loops.
- Prefer value types (`struct`) for small, frequently used data containers to avoid heap allocation.

---

## 11. Self-Documenting Code

Extract any non-trivial math or multi-step logic out of lifecycle methods into single-purpose, descriptively named private helpers. The method name itself should explain the *why*, not just the *what*:

```csharp
// BAD — magic inline math in Update
moveSpeed = Mathf.Max(1f - mixCount * 0.15f, 0.1f);

// GOOD — extracted, self-documenting
private float CalculateSpeedDebuff(int mixCount)
    => Mathf.Max(1f - mixCount * 0.15f, 0.1f);
```

---

## 12. Documentation Integrity

- Preserve all existing comment blocks, summaries, and `//TODO:` notes that are unrelated to your changes.
- When adding new public methods or complex helpers, add a one-line XML summary comment (`/// <summary>`).
