# Enumy i typy rozszerzalne

Kiedy używać `enum`, a kiedy `ScriptableObject` jako typu. Dotyczy to każdego kodu C# w projektach Unity studia.

## Zasada

- **`enum` tylko dla zamkniętych, niezmiennych zbiorów wartości.** Takich, które nie będą rosły w trakcie życia projektu.
- **Rodzaje, kategorie i typy, które mogą się rozrastać, to `ScriptableObject`.** Dotyczy to w szczególności:
  - typów NPC i postaci,
  - typów przedmiotów,
  - typów obrażeń, zasobów, umiejętności, wrogów, budynków itp.
- **Nie dodawaj nowej wartości do enuma rodzaju tylko dlatego, że to najszybsze rozwiązanie.** Jeśli w grze pojawi się kolejny typ, ma to być nowy asset, a nie zmiana w kodzie.

## Dlaczego

- Enum jest zamknięty: każdy nowy typ to zmiana w enumie i we wszystkich `switch`ach, które na nim stoją (Open/Closed).
- Unity serializuje enum jako liczbę. Wstawienie wartości w środku lub zmiana kolejności po cichu psuje prefaby, sceny i assety.
- Asset typu może nieść dodatkowe dane (nazwa, ikona, statystyki, prefab) i być edytowany przez projektantów bez dotykania kodu.

## Kiedy enum jest w porządku

- Kierunki (`Left`, `Right`, `Up`, `Down`).
- Stany prostej maszyny stanów, gdy zbiór stanów jest stały i zamknięty.
- Tryby przełączane na sztywno (`Enabled` / `Disabled`, `Day` / `Night`).
- Wszystko, co jest naturalnie skończone i nie zależy od treści gry.

Test: **czy projektant lub kolejna wersja gry mogą chcieć dodać nową wartość?** Jeśli tak, to nie enum.

## Jak to robić

Typ jako asset, referencja ustawiana w Inspectorze:

```csharp
[CreateAssetMenu(menuName = "MagicJam/Item Type")]
public class ItemType : ScriptableObject
{
    [SerializeField]
    private string displayName;

    [SerializeField]
    private Sprite icon;

    public string DisplayName => displayName;
    public Sprite Icon => icon;
}

public class Item : MonoBehaviour
{
    [SerializeField]
    private ItemType type;

    public ItemType Type => type;
}
```

Porównywanie typów to porównanie referencji do assetów:

```csharp
if (item.Type == swordType)
    Equip(item);
```

Gdy typ ma mieć **zachowanie**, a nie tylko dane, użyj interfejsu z osobnymi implementacjami albo ScriptableObject z metodą (strategia), zamiast `switch` po enumie.

## Źle

```csharp
public enum ItemType { Sword, Shield, Potion }   // każdy nowy przedmiot = zmiana kodu

switch (item.Type)
{
    case ItemType.Sword: ... break;
    case ItemType.Shield: ... break;
}
```

## Uwagi

- Nazwa klasy typu bez sufiksu „SO” (`ItemType`, nie `ItemTypeSO`).
- Assety typów trzymamy w `Assets/_Project/` w osobnym podfolderze (np. `Data/ItemTypes/`).
- Jeśli enum rodzaju już istnieje w projekcie, nie przepisuj go „przy okazji”. Zaproponuj migrację i poczekaj na decyzję (zasada „Pytanie ≠ polecenie”).
