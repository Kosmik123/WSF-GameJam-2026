# Referencje podpinane w `Reset()`

Gdy komponent potrzebuje referencji do innych komponentów, podpinamy je automatycznie w `Reset()`. Unity wywołuje `Reset()` tylko w edytorze: przy dodaniu komponentu do obiektu oraz po wybraniu *Reset* z menu komponentu. Wynik zapisuje się w polu `[SerializeField]`, więc w grze nie ma kosztu wyszukiwania.

- **W `Reset()` używamy wyłącznie** `GetComponentInChildren<T>()` (przeszukuje też własny obiekt), `GetComponentInParent<T>()` oraz wyszukiwania po scenie (`Find...`, patrz niżej).
- Komponent wymagany (z `[RequireComponent]`) nie jest podpinany w `Reset()`, bo na pewno jest na obiekcie.
- **Komponent wymaga referencji do obiektu na scenie** (np. menedżera) → w `Reset()` wolno użyć wyszukiwania po całej scenie: `FindObjectOfType<T>()` lub `FindFirstObjectByType<T>()` (zależnie od wersji Unity). To wyjątek od zakazu `Find...` w runtime, bo `Reset()` nie działa w grze.
- Pola, które wypełnia `Reset()`, to nadal `private` + `[SerializeField]`. Można je poprawić ręcznie w Inspectorze.
- Wyszukiwanie po scenie w `Reset()` działa na obiektach z otwartej sceny lub prefaba. Referencji do obiektów z innej sceny w ten sposób nie zapiszesz.
- `Reset()` jest pierwszą metodą w kolejności składowych (przed `Awake`).

```csharp
public class DoorSoundBridge : MonoBehaviour
{
    [SerializeField]
    private AudioSource audioSource;

    [SerializeField]
    private Room room;

    [SerializeField]
    private LevelManager levelManager;

    private void Reset()
    {
        audioSource = GetComponentInChildren<AudioSource>();
        room = GetComponentInParent<Room>();
        levelManager = FindFirstObjectByType<LevelManager>();
    }
}
```

Źle:

```csharp
private void Awake()
{
    levelManager = FindFirstObjectByType<LevelManager>();   // wyszukiwanie po scenie w runtime; powinno być w Reset()
}
```
