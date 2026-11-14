# Kolejność składowych klasy

- **Zagnieżdżone delegaty (`public delegate ...` wewnątrz klasy) deklarujemy na samej górze klasy**, jeszcze przed eventami.
- **Eventy (`public event ...`) deklarujemy zaraz po delegatach**, przed polami, właściwościami i metodami.
- Dalej, w tej kolejności: pola serializowane, pozostałe pola prywatne, właściwości, metody.
- **Wyjątek: właściwość dotycząca tylko jednego pola stoi zaraz za tym polem.** Pozostałe właściwości (bardziej złożone, wyliczane z kilku składowych albo odpowiadające na pytanie, np. `CanActivate`) stoją po wszystkich polach, przed metodami.
- **Metody układamy według kolejności wywołań w silniku Unity:**
  1. `Reset` (wyjątek: przed `Awake`),
  2. `Awake`,
  3. `OnEnable`,
  4. `Start`,
  5. **środek klasy, w dowolnej kolejności:** metody publiczne, prywatne metody pomocnicze, `Update`, `FixedUpdate`, `LateUpdate` i pozostałe komunikaty Unity (`OnTriggerEnter`, `OnCollisionEnter` itd.),
  6. sprzątanie: `OnDisable`, potem `OnDestroy`,
  7. na samym dole metody edytorowe: `OnValidate`, `OnDrawGizmos`, `OnDrawGizmosSelected`.

```csharp
public class Door : MonoBehaviour
{
    private void Reset() { ... }
    private void Awake() { ... }
    private void OnEnable() { ... }
    private void Start() { ... }

    public void Open() { ... }
    private void Update() { ... }
    private void OnTriggerEnter(Collider other) { ... }
    private bool CanOpen() { ... }

    private void OnDisable() { ... }
    private void OnDestroy() { ... }

    private void OnValidate() { ... }
    private void OnDrawGizmos() { ... }
}
```
- **Stany podglądane w Inspectorze** to pola i właściwości, które zmieniają się w trakcie gry i chcemy je widzieć do debugowania (np. `isMoving`, `shotTime`).
  - Pola stanu oznaczamy `[SerializeField]`, choć ich zapis w scenie nie jest potrzebny.
  - Grupujemy je w jednym bloku po pozostałych polach prywatnych, z nagłówkiem `[Header("States")]` nad pierwszym polem. `[Header]` działa tylko na polach, więc gdy blok zaczyna się od właściwości, użyj komentarza `// States`.

```csharp
public class ModeController : MonoBehaviour
{
    /// <summary>Raised after the mode changed; the argument is the new mode.</summary>
    public event Action<FilmMode> OnModeChanged;

    [SerializeField]
    private InputActionReference toggleAction;

    [Header("States")]
    [SerializeField]
    private int lastTickSecond;

    public FilmMode Mode { get; private set; }

    private void Awake() { ... }
}
```

Zagnieżdżony delegat stoi nad eventami:

```csharp
public class Health : MonoBehaviour
{
    public delegate void DamageTakenHandler(float amount, Vector3 hitPoint);

    public event DamageTakenHandler OnDamageTaken;
    public event Action OnDied;

    [SerializeField]
    private float maxHealth = 100f;
}
```

Właściwość jednego pola stoi zaraz za polem, pozostałe po polach:

```csharp
public class Segment : MonoBehaviour
{
    [SerializeField]
    private int length;
    public int Length
    {
        get => length;
        set => length = value;
    }

    private bool isEnabled;

    public bool CanActivate => gameObject.activeSelf && length > 0;
}
```
