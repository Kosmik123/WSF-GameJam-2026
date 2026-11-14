# AGENTS.md

Instrukcje dla agentów (Claude i inni) pracujących w tym repozytorium. W razie konfliktu z ogólnymi zasadami studia pierwszeństwo mają zasady z tego pliku.

## Projekt

- **Gra:** Uroboros, projekt na WSF GameJam 2026 (Unity, URP, Input System). Termin: 30 godzin od startu.
- **Dokumenty:** [docs/GDD.md](docs/GDD.md) (projekt gry) i [docs/TASKS.md](docs/TASKS.md) (lista zadań). Przed większą zmianą sprawdź, czy mieści się w GDD, i aktualizuj oba pliki, gdy coś ustalimy.
- **Namespace bazowy:** `WSFGameJam2026`, odpowiada folderowi pod `Assets/_Project/Scripts/`.

## Zasady kodu

- Stosuj skill `bipolar-rules` (styl kodu C#, hermetyzacja, struktura folderów). Nasze pliki trafiają wyłącznie do `Assets/_Project/`.
- Pytanie to nie polecenie: gdy użytkownik pyta, odpowiedz i niczego nie zmieniaj.

## Git

- **Gałęzie, które istnieją na origin, to tylko `develop` i `master`.** Gałęzi `feature/*` **nigdy nie pushujemy**. Zostają lokalnie.
- **Push i merge robi wyłącznie użytkownik.** Agent nie wypycha żadnych gałęzi (także `develop` i `master`) i nie merguje do `develop` ani `master`, dopóki użytkownik wyraźnie o to nie poprosi w danej sytuacji.
- **Każdy feature to osobna gałąź** `feature/<nazwa>`, zakładana **od `develop`**, najlepiej w osobnym worktree.
- Agent commituje na swojej gałęzi feature, gdy skończy zadanie, i informuje użytkownika, że gałąź jest gotowa do merge'u.
- Do commitów dodaj stopkę `Co-Authored-By` zgodnie z ustawieniami sesji.
- Nie dodawaj do repozytorium katalogu `.claude/worktrees/` ani innych lokalnych plików narzędzi.

## Unity

- Nie modyfikuj zewnętrznych bibliotek ani modułów w `Assets/Bipolar/`. Rozszerzaj je w `Assets/_Project/`.
- Pliki `.meta` commitujemy razem z assetami. Nie usuwaj ich ani nie zmieniaj GUID-ów istniejących assetów.
- Kod nie jest kompilowany poza edytorem Unity. Zaznaczaj w raporcie, że zmiana nie była uruchomiona w Unity, jeśli tak jest.
