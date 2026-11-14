# Wzór README modułu studia

Każdy moduł w `Assets/Bipolar/<NazwaModułu>/` ma plik `README.md` w swoim głównym folderze, zbudowany według poniższego wzoru. Dzięki temu agent (i człowiek) wie, czego w nim szukać. README pisz krótko: ma pozwolić użyć modułu bez czytania jego kodu.

Gdy tworzysz lub aktualizujesz README modułu, trzymaj się tej struktury.

---

````markdown
# <NazwaModułu>

Jedno zdanie: co moduł robi.

## Kiedy używać

- Sytuacje, w których ten moduł jest właściwym wyborem (np. „gdy obiekt ma reagować na interakcję gracza”).

## Kiedy NIE używać

- Zadania, do których moduł nie służy, i co wtedy zrobić.

## Szybki start

Najczęstszy przypadek użycia, minimalny działający przykład (5–15 linii):

```csharp
// przykład w stylu zasad bipolar-rules
```

Jeśli potrzebna konfiguracja w scenie lub Inspectorze (komponenty, assety, referencje), opisz ją kolejnymi krokami.

## Najważniejsze API

| Typ / metoda | Do czego |
|--------------|----------|
| `NazwaKlasy` | krótki opis |

## Czego nie robić

- Typowe błędy i antywzorce (np. „nie używaj `FindObjectOfType`, zarejestruj obiekt w systemie”).

## Zależności

- Inne moduły lub paczki, których wymaga (jeśli są).
````

---

## Wskazówki

- Przykład użycia jest ważniejszy niż opis. Agent kopiuje wzorzec z przykładu.
- Sekcja „Kiedy NIE używać” zapobiega sięganiu po moduł na siłę.
- Nazwa pliku zawsze `README.md`, żeby agent wiedział, czego szukać.
- Przykłady kodu w README stosują zasady stylu z `SKILL.md` (hermetyzacja, atrybuty nad polem, formatowanie `if` / `for`).
