# Formatowanie instrukcji

- **Instrukcja po `if` / `for` / `foreach` / `while` zawsze w osobnej linijce** — nigdy w tej samej linii co warunek.
- **Jednolinijkowe ciało `if` / `for` / `foreach` / `while` piszemy bez klamer.** Klamry tylko dla ciał wieloliniowych.
- **Zasady dla `if` / `for` / `foreach` / `while` są wspólne.** Wszystkie te konstrukcje (warunki i pętle) traktujemy tak samo.
- **Zagnieżdżone konstrukcje (`if` / `for` / `foreach` / `while` w innym `if` / `for` / `foreach` / `while`) traktujemy jako jedną całość — klamry są albo wszędzie, albo nigdzie:**
  - Jeśli **którakolwiek** konstrukcja w łańcuchu musi mieć klamry (bo ma ciało wieloliniowe), **wszystkie** konstrukcje w tym łańcuchu, zewnętrzne i wewnętrzne, mają klamry, nawet te jednolinijkowe.
  - Jeśli **żadna** nie musi mieć klamer (każda ma jednolinijkowe ciało), **żadna ich nie ma**.
  - **Wyjątek: warunek, którego ciałem jest wyłącznie `return`, `break` lub `continue`, nigdy nie dostaje klamer** (zawsze w osobnej linijce), także wewnątrz konstrukcji z klamrami.
- **Łańcuch `if` / `else if` / `else` to jedna całość:** jeśli którakolwiek gałąź musi mieć klamry, wszystkie gałęzie je mają.
- **Unikamy `else` i `else if`.** Zazwyczaj się da:
  - przez operator ternarny przy prostym przypisaniu lub zwróceniu jednej z dwóch wartości (ma pierwszeństwo przed wieloma `return`),
  - przez `switch expression` przy trzech lub więcej wartościach (bez zagnieżdżonych ternary),
  - przez wydzielenie logiki do osobnej metody, która w pierwszej gałęzi kończy się `return` po `if`, gdy gałęzie wykonują akcje, a nie wybierają wartości.
- **Sprawdzenie boola na fałsz zapisujemy tylko przez `== false`.** Operator `!` jest dozwolony wyłącznie przylegający do `=`: w `!=` (`if (a != b)`) oraz w przypisaniu odwróconej wartości (`b = !a`).
- **Obiekty Unity (`UnityEngine.Object`: komponenty, `GameObject`, assety, w tym `ScriptableObject`):**
  - dotyczy każdego sprawdzenia, czy obiekt istnieje (czy referencja nie jest pusta ani zniszczona), także przy porównywaniu z `null`,
  - czy **istnieje** → `if (target)`,
  - czy **nie istnieje** → `if (target == null)` (nie `if (!target)` ani `if (target == false)`).
- **Zmienne lokalne typu klasy lub struktury deklarujemy przez `var`.** Typy wbudowane (`string`, `int`, `bool`, `float` itd.) deklarujemy z jawnym typem.

```csharp
if (isActive == false)
    return;

for (int i = 0; i < actors.Count; i++)
    actors[i].Reset();

if (target == null)
    return;

if (a != b)
    Sync();

isClosed = !isOpen;

// zmienne lokalne: var dla klas i struktur, jawny typ dla typów wbudowanych
var door = GetComponent<Door>();
var offset = new Vector3(0f, 1f, 0f);
string label = door.Label;
int count = actors.Count;

if (target && target.IsReady == false)
{
    target.Prepare();
    target.Notify();
}

// zagnieżdżone, wszystkie jednolinijkowe: bez klamer nigdzie
for (int i = 0; i < actors.Count; i++)
    if (actors[i].IsDead)
        actors[i].Remove();

// zagnieżdżone, wewnętrzna wieloliniowa: klamry wszędzie
for (int i = 0; i < actors.Count; i++)
{
    if (actors[i].IsDead)
    {
        actors[i].Remove();
        count++;
    }
}

// wyjątek: strażnik z continue / break / return bez klamer, mimo klamer na zewnątrz
for (int i = 0; i < actors.Count; i++)
{
    if (actors[i] == null)
        continue;

    if (actors[i].IsDead)
    {
        actors[i].Remove();
        count++;
    }
}

// zamiast else przy zwracaniu jednej z dwóch wartości: operator ternarny
private string GetLabel()
{
    return isLocked ? "Locked" : "Open";
}

// trzy lub więcej wartości: switch expression
private string GetLabel()
{
    return state switch
    {
        DoorState.Locked => "Locked",
        DoorState.Closed => "Closed",
        _ => "Open",
    };
}

// zamiast else przy akcjach: osobna metoda z return po if
private void Interact()
{
    if (isLocked)
    {
        ShowLock();
        PlaySound();
        return;
    }

    Open();
}

// zamiast if / else przy przypisaniu: operator ternarny
string label = isLocked ? "Locked" : "Open";

// else nieunikniony, jedna gałąź ma klamry: wszystkie gałęzie mają klamry
if (isLocked)
{
    ShowLock();
    PlaySound();
}
else
{
    Open();
}
```

Źle:

```csharp
if (!isActive) return;                        // instrukcja w tej samej linii, negacja przez !
for (int i = 0; i < n; i++) { Do(i); }        // zbędne klamry i instrukcja w tej samej linii
if (target != null)                           // istnienie obiektu Unity sprawdzamy przez if (target)
    target.Prepare();
if (!target)                                  // brak obiektu Unity sprawdzamy przez target == null
    return;

for (int i = 0; i < actors.Count; i++)
{
    if (actors[i].IsDead)                     // wewnętrzna jednolinijkowa bez klamer, gdy zewnętrzna je ma
        actors[i].Remove();
    Tick(actors[i]);
}

for (int i = 0; i < actors.Count; i++)
{
    if (actors[i] == null)
    {                                         // klamry przy samym continue / break / return
        continue;
    }
}

if (isLocked)
{
    ShowLock();
    PlaySound();
}
else
    Open();                                   // jedna gałąź ma klamry, druga nie

if (isLocked)
    label = "Locked";
else                                          // zbędny else: ternary
    label = "Open";

Door door = GetComponent<Door>();             // klasa: var
Vector3 offset = new Vector3(0f, 1f, 0f);     // struktura: var
var count = actors.Count;                     // typ wbudowany: jawny typ (int)
```
