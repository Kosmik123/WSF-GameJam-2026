# NaughtyAttributes

Systemy mają działać **zarówno z biblioteką NaughtyAttributes, jak i bez niej**. Dlatego:

- **Każdy atrybut z NaughtyAttributes otaczamy `#if NAUGHTY_ATTRIBUTES` … `#endif`.**
- **Namespace podajemy inline przy atrybucie** (`[NaughtyAttributes.Button]`), **nie** przez `using NaughtyAttributes;` nad klasą. `using` poza `#if` nie skompiluje się bez biblioteki.
- Kod nie może zależeć od NaughtyAttributes w działaniu: bez biblioteki znikają tylko udogodnienia w Inspectorze.
- Symbol `NAUGHTY_ATTRIBUTES` musi być zdefiniowany w projekcie, który używa biblioteki (np. *Version Defines* w `.asmdef` albo *Scripting Define Symbols* w Player Settings).
- **Stany podglądane w Inspectorze** (pola i właściwości zmieniające się w trakcie gry): pola stanu to `[SerializeField]`, **nie używamy `[NaughtyAttributes.ShowNonSerializedField]`**, bo źle się wyświetla. Właściwości stanu można oznaczyć `[NaughtyAttributes.ShowNativeProperty]`, w `#if NAUGHTY_ATTRIBUTES`. Bez tego atrybutu stan nadal jest poprawny, tylko niewidoczny w Inspectorze.

```csharp
public class ActorController : MonoBehaviour
{
#if NAUGHTY_ATTRIBUTES
    [NaughtyAttributes.ShowNativeProperty]
#endif
    public ActorState State { get; private set; }

#if NAUGHTY_ATTRIBUTES
    [NaughtyAttributes.Button]
#endif
    private void Retake() { ... }
}
```

Źle:

```csharp
using NaughtyAttributes;          // nie skompiluje się bez biblioteki

public class ActorController : MonoBehaviour
{
    [ShowNativeProperty]          // brak #if NAUGHTY_ATTRIBUTES
    public ActorState State { get; private set; }
}
```
