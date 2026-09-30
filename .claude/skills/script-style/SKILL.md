---
name: script-style
description: Apply this project's C# conventions when declaring MonoBehaviour fields or adding notifications between gameplay scripts. Covers explicit serialized fields, Inspector grouping and event lifecycles; does not require unrelated scripts to be reformatted.
---

# Field and event style

Three habits, taken from the one project in the user's GitHub history that is their
own work rather than a course prototype (`WheelOfFortune`). They don't conflict with
anything else in this repo's conventions. Apply them to field declarations and
notification wiring you touch; use `AGENTS.md` for the shared project rules.

## Every serialized field is explicit

```csharp
[SerializeField] private AudioSource audioSource;
[SerializeField] private bool isSpinning;
```

Not a bare `AudioSource audioSource;` relying on implicit private. The attribute and
the access modifier are both always written out, even though C# would default to
private without either.

## Group fields under `[Header]`

Inspector fields are grouped by what they belong to, not just listed in declaration
order:

```csharp
[Header("UI and References")]
[SerializeField] private Button leaveButton;

[Header("Wheel Data")]
[SerializeField] private WheelSlice[] slices;
```

A script with more than a handful of fields gets at least two or three of these
groups. Pick the group names from what the fields are *for* (a panel, a data set, a
subsystem), as the reference project's game manager does with "UI and References",
"Wheel Data", "Bomb Panel" and "Inventory System".

## Loose coupling through `event Action<T>`

When one script needs to notify another without owning a direct reference to it,
reach for a plain event rather than wiring the two together directly:

```csharp
public event System.Action<int> OnSpinCompleted;
```

The publisher fires it, the subscriber(s) hook `+=` in `OnEnable` and `-=` in
`OnDisable`. This is the same shape as a `UnityEvent` exposed in the Inspector — use
whichever the AGENTS.md table already points at for the job at hand; reach for
`event Action<T>` when the coupling is between two scripts rather than a script and
a Button/Animator wired in the Inspector.

This is notification guidance, not a requirement to turn every method call into an
event. Keep an appropriate direct reference when one component owns or explicitly
commands another.
