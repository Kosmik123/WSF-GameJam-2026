---
name: bipolar-rules
description: Wspólne zasady studia Bipolar Games dla projektów Unity - styl kodu C# (hermetyzacja, SOLID, eventy, kolejność składowych klasy, formatowanie if/for, NaughtyAttributes, Input System) oraz struktura folderów Assets/_Project i używanie modułów studia z Assets/Bipolar (serializacja interfejsów, interakcje, sceny) zamiast pisania własnych odpowiedników. Używaj przy pisaniu, zmienianiu i refaktoryzacji skryptów C# (MonoBehaviour, ScriptableObject), dodawaniu nowych plików do projektu Unity oraz zawsze, gdy użytkownik zadaje pytanie zamiast wydawać polecenie.
---

# BIPOLAR_RULES — wspólne zasady studia Bipolar Games

Uniwersalne zasady komunikacji, struktury projektu i stylu kodu, wspólne dla wszystkich projektów Unity studia Bipolar.
Instrukcje specyficzne dla projektu są w `AGENTS.md` (lub odpowiedniku) danego repozytorium.

> **Nie edytuj tego pliku w trakcie pracy w repozytorium.** To kopia wspólnego szablonu dodawanego
> do każdego projektu. Zmiany wprowadzamy w szablonie i kopiujemy do projektów. Jeśli zasada dotyczy
> tylko jednego projektu, wpisz ją do `AGENTS.md` tego projektu.
> W razie konfliktu pierwszeństwo mają zasady z `AGENTS.md` projektu.

## Pytanie ≠ polecenie

- **Gdy użytkownik zadaje pytanie, odpowiedz na nie i nic nie zmieniaj.** Nie edytuj plików, nie commituj, nie wprowadzaj zmian „przy okazji”. Czytać kod wolno, żeby odpowiedzieć.
- **Działaj dopiero na wyraźne polecenie** („zrób”, „dodaj”, „popraw”, „zacommituj” itp.).
- Jeśli w odpowiedzi proponujesz zmianę, opisz ją i poczekaj na decyzję.

## Struktura folderów

```
Assets/
├── _Project/            ← WSZYSTKIE pliki projektu
│   ├── Scenes/          ← każda scena we własnym folderze o tej samej nazwie (Level01/Level01.unity)
│   ├── Scripts/         ← podfoldery wg systemów (Camera/, Actors/, ...)
│   └── Settings/        ← assety URP, profile Volume
├── Bipolar/             ← moduły studia (każdy z własnym README.md)
└── <ZewnętrznaBiblioteka>/  ← paczki z Asset Store / zewnętrzne biblioteki
```

Zasady:

- **Nasze pliki trafiają wyłącznie do `Assets/_Project/`.** Nowe skrypty, sceny, prefaby, materiały, tekstury, audio — wszystko tam, w odpowiednim podfolderze.
- **Bezpośrednio w `Assets/` leżą tylko zewnętrzne biblioteki** (Asset Store, pluginy, importowane paczki) **oraz moduły studia w `Assets/Bipolar/`**. Nie twórz tam plików projektu.
- **Każda scena ma własny folder o tej samej nazwie co scena**, w `Assets/_Project/Scenes/` (np. `Scenes/Level01/Level01.unity`). Pozostałe pliki należące do sceny trzymamy w jej folderze.
- **Nie modyfikuj zewnętrznych bibliotek.** Jeśli trzeba zmienić ich zachowanie — rozszerz je (wrapper, dziedziczenie, adapter) w `Assets/_Project/`.
- Namespace odpowiada folderowi pod `Scripts/` (patrz „Styl kodu C#”).

## Moduły studia (`Assets/Bipolar/`)

Studio ma własne, gotowe moduły (np. serializacja interfejsów, system interakcji, zarządzanie scenami). Zestaw modułów różni się między projektami, więc ich opisy **nie** są w tym skillu. Każdy moduł ma własny `README.md`.

- **Zanim napiszesz nowy system** (serializacja, interakcje, sceny, zarządzanie zasobami itp.), **sprawdź, czy w `Assets/Bipolar/` jest moduł do tego zadania.** Przejrzyj foldery modułów i przeczytaj `README.md` każdego pasującego.
- **Jeśli moduł pasuje, użyj go. Nie pisz własnego odpowiednika.**
- Jeśli żaden moduł nie pasuje albo nie masz pewności, zapytaj, zanim zaczniesz pisać własne rozwiązanie.
- Nie modyfikuj modułów w repozytorium projektu (zasada jak dla zewnętrznych bibliotek). Brakującą funkcję zaproponuj lub rozszerz w `Assets/_Project/`.
- Unity nie serializuje interfejsów. Referencje do interfejsów w Inspectorze podpinamy przez moduł z `Assets/Bipolar/` (sprawdź jego `README.md`), nie własnym rozwiązaniem.
- Wzór README modułu: `module-readme-template.md`.

## Styl kodu C#

- **Namespace bazowy** to nazwa projektu (np. `MagicJam`); namespace odpowiada folderowi pod `Scripts/`.

### Hermetyzacja (najważniejsze)

Stosujemy zasady **SOLID**, a przede wszystkim **hermetyzację**:

- **Pola są zawsze prywatne.** Żadnych publicznych pól.
- **Na zewnątrz wystawiamy tylko właściwości** (najlepiej tylko do odczytu) i metody.
- Pola widoczne w Inspectorze to `private` + `[SerializeField]`, nigdy `public`.
- Setter publiczny tylko wtedy, gdy naprawdę jest potrzebny; preferuj `private set` lub metodę opisującą intencję (`Enable()`, `ApplyDamage(...)`).
- Nie wystawiaj mutowalnych kolekcji — zwracaj `IReadOnlyList<T>` / `IReadOnlyCollection<T>`.

### Atrybuty — w linijce POWYŻEJ pola

`[SerializeField]` (i pozostałe atrybuty: `[Header]`, `[Tooltip]`, `[Range]` itd.) piszemy **w osobnej linijce nad polem**, nie w tej samej linii.

**Wyjątek: atrybut na argumencie funkcji piszemy w tej samej linii** co argument (`void Log([CallerMemberName] string caller = null)`).

```csharp
public class OperatorCamera : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = 3f;
    public float MoveSpeed => moveSpeed;

    [Tooltip("Time to reach target velocity. 0 = instant.")]
    [SerializeField]
    private float moveSmoothTime = 0.12f;

    private Camera cam;
    public Camera Camera => cam;

    public bool InputEnabled { get; private set; } = true;
}
```

Źle:

```csharp
public float moveSpeed = 3f;                    // publiczne pole
[SerializeField] private float moveSpeed = 3f;  // atrybut w tej samej linii
```

Poprawnie (wyjątek dla argumentów):

```csharp
private void Log(string message, [CallerMemberName] string caller = null) { ... }
```

### Wymagane komponenty — `RequireComponent`

- **Jeśli klasa wymaga komponentu na tym samym obiekcie, ma nad deklaracją klasy `[RequireComponent(typeof(T))]`**, w osobnej linijce.
- **Pola wymaganego komponentu nie serializujemy.** Komponent na pewno jest na obiekcie, więc sposób jego pobrania jest oczywisty i nie podpinamy go w Inspectorze ani w `Reset()`. Pobieramy go w `Awake()` albo przez właściwość, która przy pierwszym odczycie zapisuje referencję w polu.
- **Komponent opcjonalny albo taki, który może być na innym obiekcie, nie ma `RequireComponent`.** W `Reset()` zbieramy go tylko przez `GetComponentInChildren`, `GetComponentInParent` lub `Find...` (patrz `reset-references.md`).
- **W runtime komponent, którego brak jest dopuszczalny** (na tym samym lub innym obiekcie), pobieramy przez `TryGetComponent` i obsługujemy jego brak.

```csharp
[RequireComponent(typeof(Door))]
public class DoorSoundBridge : MonoBehaviour
{
    private Door door;

    private void Awake()
    {
        door = GetComponent<Door>();
    }
}
```

```csharp
[RequireComponent(typeof(Door))]
public class DoorSoundBridge : MonoBehaviour
{
    private Door door;

    private Door Door
    {
        get
        {
            if (door == null)
                door = GetComponent<Door>();

            return door;
        }
    }
}
```

Źle:

```csharp
public class DoorSoundBridge : MonoBehaviour      // brak [RequireComponent(typeof(Door))]
{
    private Door door;

    private void Awake()
    {
        door = GetComponent<Door>();
    }
}

[RequireComponent(typeof(Door))]
public class DoorSoundBridge : MonoBehaviour
{
    [SerializeField]                               // wymagany komponent nie jest serializowany ani podpinany w Reset()
    private Door door;

    private void Reset()
    {
        door = GetComponent<Door>();
    }
}
```

### Pozostałe zasady SOLID w praktyce

- **Single Responsibility:** jeden `MonoBehaviour` = jedna odpowiedzialność. Zamiast jednego wielkiego managera — kilka małych komponentów.
- **Open/Closed, Dependency Inversion:** zależności przez interfejsy lub referencje ustawiane w Inspectorze (`[SerializeField]`), nie przez `FindObjectOfType` / `GameObject.Find` w runtime (w `Reset()` wolno, patrz `reset-references.md`).
- **Interface Segregation:** małe, wąskie interfejsy (`IInteractive`, a nie `IEverything`).
- Komunikacja między systemami przez zdarzenia (`event Action<...>`) zamiast bezpośrednich wywołań w obie strony i cyklicznych zależności.

### Referencje podpinane w `Reset()`

Referencje do komponentów i obiektów sceny podpinamy automatycznie w `Reset()` (`GetComponent`, `...InChildren`, `...InParent`, a menedżery ze sceny przez `FindFirstObjectByType`). Szczegóły i przykłady: `reset-references.md`.

### Eventy

Domyślnie C#-owe eventy (`Action`, własny delegat od dwóch argumentów, nazwy z prefiksem `On`), `UnityEvent` tylko dla efektów niewpływających na grę. Szczegóły i przykłady: `events.md`.

### Enumy i typy rozszerzalne

- **`enum` tylko dla zamkniętych, niezmiennych zbiorów** (kierunki, proste stany). Typy NPC, postaci, przedmiotów i inne rodzaje, które mogą się rozrastać, to `ScriptableObject`, nie enum.
- Szczegóły, wyjątki i przykłady: `enums-and-extensible-types.md` — **czytaj przy każdej propozycji dodania enuma lub typu/rodzaju czegokolwiek.**

### Konwencje nazewnicze

- Klasy, metody, właściwości, eventy: `PascalCase`.
- Pola prywatne i zmienne lokalne: `camelCase` (bez prefiksu `_` / `m_`).
- Stałe: `PascalCase` (`private const float Skin = 0.01f;`).
- Interfejsy z prefiksem `I`.
- Korutyny: patrz „Korutyny” poniżej.
- Eventy z prefiksem `On` (`OnOpened`, `OnCreating`), handlery używane wyłącznie jako handler `Obiekt_NazwaEventu` (`Door_OnOpened`), a metody używane też poza eventem mają zwykłą nazwę mówiącą, co robią. Publiczne metody nie zaczynają się od `On` (szczegóły w `events.md`).
- Typy publiczne (zwykłe klasy, struktury, enumy, delegaty) mogą dzielić plik, gdy są ze sobą ściśle powiązane.
- Komentarze i nazwy w kodzie po angielsku. `<summary>` dla publicznych klas i nieoczywistych członków jest opcjonalne: nie trzeba go pisać.

### Korutyny

Korutyna to specjalny rodzaj metody, więc ma własne zasady nazewnictwa:

- **Nazwa korutyny nie jest czasownikiem.** Wywołanie korutyny w Unity tworzy obiekt, który ją wykonuje, więc nazwa opisuje tę czynność jak rzeczownik, najczęściej odczasownikowy.
- **Nazwa kończy się przyrostkiem `Co`**, żeby od razu było widać, czym jest, i że nie wywołujemy jej jak zwykłej metody (tylko przez `StartCoroutine`).
- **Część przed `Co` opisuje czynność**, którą korutyna wykonuje.

```csharp
private IEnumerator ScreenFadeCo() { ... }
private IEnumerator ElementsCountingCo() { ... }

StartCoroutine(ScreenFadeCo());
```

Źle:

```csharp
private IEnumerator FadeScreen() { ... }   // wygląda jak zwykła metoda, brak przyrostka Co
FadeScreen();                              // nie uruchomi korutyny
```

### Input

Pakiet `com.unity.inputsystem` nazywamy „Input System”, nie „nowy Input System”. Jeśli go używamy, komponenty przyjmują akcje przez `[SerializeField] InputActionReference` (zgodnie z regułą atrybutu powyżej pola).

Właściciel akcji jest oddzielony od jej konsumentów:

- **Akcje (mapy akcji) włącza i wyłącza jeden właściciel**, np. `PlayerInput` albo własny komponent wejścia, zależnie od stanu gry (rozgrywka, UI itd.).
- **Konsumenci tylko subskrybują** `performed` / `canceled` w `OnEnable` i odsubskrybowują w `OnDisable`. Odsubskrybowanie wyłącza akcję dla tego komponentu i nie wpływa na pozostałych.
- **Konsument nie wywołuje `Enable()` ani `Disable()` na akcji współdzielonej**, bo stan włączenia nie jest zliczany: `Disable()` w jednym komponencie wyłącza akcję dla wszystkich.

```csharp
public class Jumper : MonoBehaviour
{
    [SerializeField]
    private InputActionReference jumpAction;

    private void OnEnable()
    {
        jumpAction.action.performed += JumpAction_Performed;
    }

    private void OnDisable()
    {
        jumpAction.action.performed -= JumpAction_Performed;
    }

    private void JumpAction_Performed(InputAction.CallbackContext context)
    {
        Jump();
    }
}
```

Źle:

```csharp
private void OnEnable()
{
    jumpAction.action.Enable();                 // konsument włącza współdzieloną akcję
    jumpAction.action.performed += JumpAction_Performed;
}

private void OnDisable()
{
    jumpAction.action.Disable();                // wyłączy akcję także innym komponentom
    jumpAction.action.performed -= JumpAction_Performed;
}
```

## Co jest w którym pliku

Pliki leżą obok `SKILL.md`. Czytaj je wtedy, gdy dotyczą zadania:

| Plik | Kiedy czytać |
|---|---|
| `formatting.md` | przy każdym pisaniu lub zmianie kodu C# (klamry, `else`, negacja, obiekty Unity) |
| `class-layout.md` | przy każdym pisaniu lub zmianie kodu C# (kolejność składowych, stany podglądane w Inspectorze) |
| `events.md` | przy eventach, delegatach i `UnityEvent` |
| `reset-references.md` | przy podpinaniu referencji w `Reset()` |
| `naughty-attributes.md` | przy atrybutach Inspectora z NaughtyAttributes |
| `enums-and-extensible-types.md` | przy każdym enumie lub typie/rodzaju czegokolwiek |
| `module-readme-template.md` | przy tworzeniu README modułu w `Assets/Bipolar/` |
