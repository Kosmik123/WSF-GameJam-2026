# Lista zadań

Priorytety: **MUST** (bez tego nie ma gry), **SHOULD** (mocno poprawia grę lub prezentację), **COULD** (jeśli zostanie czas).
Właściciel: `P` programista, `G` grafik, `C` Claude, `?` do ustalenia.
Zasada pracy: każdy feature na własnej gałęzi od `develop`, merge do `develop` robi człowiek.
Termin jamu: **30 godzin** od startu. Tempo oceniamy na bieżąco i przesuwamy priorytety.

## Budżet czasu (orientacyjny, 30 h)

| Godziny | Cel |
|---------|-----|
| 0-6 | Etap 1: ruch, kamera, paszcza, cel, pętla rund (grywalny prototyp) |
| 6-13 | Etap 2: walka, zdrowie, dwa pierwsze typy wrogów, spawner |
| 13-19 | Progresja (XP, poziomy, ekwipunek), wrogowie 3-4, HUD, wskaźniki, ekrany |
| 19-26 | Etap 4 i 5: zawartość, shadery, efekty, dźwięk |
| 26-30 | Zamrożenie funkcji około godziny 26, potem tylko balans, build i prezentacja |

Jeśli po godzinie 13 nie ma grywalnej walki, wycinamy wrogów 3-4 i mini-bossa.

## Zrobione

- [x] Świat: proceduralny torus z segmentami o różnej rozdzielczości i terenie (`TorusWorld`, `TorusSurface`, `TorusSegmentMeshBuilder`).
- [x] Szum terenu z Unity.Mathematics.
- [x] Przebudowa świata po zmianie w Inspectorze (OnValidate).
- [x] `TorusObject`: ogólna klasa dla wszystkiego, co stoi na torusie (gracz, NPC, przedmioty), pozycja jako `θ`, `φ`; `TorusPlayerMovement` tylko steruje nim z wejścia (A/D obrót, W/S przód i tył).
- [x] Kamera z góry pod skosem za graczem.
- [x] Profil segmentu jako klasa odkształcająca mesh (`Deform`), `NoiseTorusSegmentProfile`, wygładzanie normalnych na granicach.
- [x] Szyja głowy wtopiona w teren: `HeadTerrainBlend`, `TorusHeadMeshBlender`, `TorusHeadTerrain`. Nie testowane w Unity.
- [x] Kolory wierzchołków z meshy grafika trafiają do meshy segmentów (`MeshHeightRasterizer`, profile). Brakuje jeszcze shadera czytającego Vertex Color i koloru szyi głowy.
- [x] `comms.md`: kanał wiadomości między agentami (zasada w `AGENTS.md`).

## W trakcie

- [ ] Propsy na torusie (`TorusPropDefinition` jako ScriptableObject: nazwa, promień kolizji, blokowanie ruchu; rysowanie przez instancing później). Projekt uzgodniony, kodu jeszcze nie ma.
- [ ] Shader czytający Vertex Color (kolory biomów) i kolor szyi głowy. `G`

## Etap 1: prototyp grywalny (MUST)

- [x] **Gracz: ruch po torusie** w współrzędnych `(θ, φ)`, orientacja według normalnej powierzchni, sterowanie (Input System). `C`
- [x] **Kamera** z góry pod skosem, podążająca za graczem. Orientację „góry ekranu” i obrót kamery ustawimy przy dostrajaniu walki, tak by była wygodna (odłożone). `C`
- [x] **Paszcza (logika):** jednostajny ruch po `θ` (`HeadMovement`), strefa działa na każde `Health`: gracza stopniowo pozbawia życia, NPC zabija od razu (pole `MawDamageMode` w `Health`, `MawKillZone`, tylko kąty). `C`
- [x] **Cel i zwycięstwo rundy:** wykrycie okrążenia i dotarcia na czubek głowy: `LapTracker` (suma zmian kąta gracz-głowa, tylko `θ`). `C`
- [ ] **Pętla rund:** po zwycięstwie nowa runda z szybszą paszczą i większą trudnością, ekran śmierci z wynikiem (liczba rund), zapis rekordu i restart od rundy 1. `P`
  - [x] Logika: `RoundController` (przerwa po rundzie, szybsza paszcza i mnożnik wrogów, śmierć kończy przejście, rekord w `PlayerPrefs`, restart od rundy 1), menu *WSF GameJam/Setup/Create Round Loop*. Nie testowane w Unity. `C`
  - [x] Ekran zwycięstwa rundy i ekran śmierci z wynikiem i rekordem: `RoundScreens` (zdarzenia `OnRoundCompleted`, `OnRunEnded`), menu *WSF GameJam/Setup/Create Round Screens*, nie testowane w Unity. `C`
  - [ ] Ładna grafika tych ekranów (teraz placeholder: przyciemnienie i tekst). `G`
- [ ] **Model paszczy** (siatka, animacja otwierania i zamykania). `G`
- [ ] **Model gracza** (low poly, animacje: bieg, atak, unik, obrażenia, śmierć). `G`
- [ ] **Materiał i wygląd ciała węża** (łuski, kolory, różne warianty segmentów). `G`

## Etap 2: walka i wrogowie (MUST)

Kod na bazie systemu walki i wrogów z LD57, przeniesiony na torus (pozycje `(θ, φ)`, odległości po powierzchni, bez NavMesha i fizyki). Ustawienie sceny: menu **WSF GameJam/Setup/Create Combat And Enemies**. Nie testowane jeszcze w Unity.

- [x] **Zdrowie** (gracz, wrogowie), obrażenia, śmierć: `Health`, `DamageInfo`, `CombatQueries`, `Enemy` (śmierć, zdarzenia dla XP). `C`
- [x] **Walka gracza:** atak melee w łuku z combo (3 ciosy, ostatni mocniejszy), unik z nietykalnością (Spacja), hit stop, odrzut, błysk, wstrząs kamery: `PlayerCombat`, `HitStop`, `Knockback`, `BodyFlash`, `HitFeedback`. `C`
- [x] **Wróg 1 (biegacz)** (slime z LD57): otaczanie gracza w sektorach, zapowiedź, wyskok, odskok: `RunnerEnemy`. `C`
- [x] **Wróg 2 (strzelec)** (z LD57): trzyma dystans, strafe, ucieka, strzał z zapowiedzią i linią celowania: `ShooterEnemy`, `EnemyProjectile`. `C`
- [x] **Spawner wrogów** przed graczem, poziom z odległości od paszczy (krzywa) × `RoundMultiplier`, usuwanie wrogów daleko za graczem: `EnemySpawner`. `C`
- [ ] **Test i balans walki w Unity** (feel ataku i uniku, statystyki wrogów). `?`
- [x] **Podpięcie pod pętlę rund:** `RoundController` ustawia `EnemySpawner.RoundMultiplier`, woła `Clear()` i `PlayerCombat.Revive()`, a ekran śmierci pokazuje `RoundScreens`. Nie testowane w Unity. `C`
- [ ] **Modele i animacje wrogów 1 i 2** (teraz są placeholdery z prymitywów). Zdarzenia do animacji: `PlayerCombat.OnAttackStarted/OnDodgeStarted`. `G`

## Etap 2b: progresja (MUST)

- [ ] **Doświadczenie i poziomy:** XP od wrogów, progi, awans, wzrost statystyk (HP, obrażenia, szybkość ataku); reset poziomu i sprzętu przy śmierci. `C`
- [x] **Trudność zależna od paszczy:** `EnemySpawner.GetDifficulty` liczy trudność z aktualnej odległości miejsca od paszczy (krzywa, blisko = łatwo, czubek głowy = najtrudniej) razem z mnożnikiem rundy. Poziom wroga ustalany raz, przy pojawieniu się. Nie testowane w Unity. `C`
- [ ] **Ekwipunek:** sloty, przedmioty zmieniające statystyki, upuszczanie przez wrogów i skrzynie. `C`
- [ ] **UI progresji:** pasek XP, komunikat awansu, ekwipunek i porównanie przedmiotów. `P`, grafiki: `G`
- [ ] **Ikony i modele przedmiotów** (broń, pancerz, dodatki). `G`

## Etap 3: czytelność i interfejs (SHOULD)

- [ ] **HUD:** pasek HP i numer rundy (reszta HUD-u: wskaźnik paszczy, ekrany poniżej). `P`
  - [x] Numer rundy: `RoundState` (numer rundy, `StartNextRound`, `ResetRounds`) i `RoundLabel`, ten sam menu *Create HUD*. `RoundState` czeka na podpięcie pod pętlę rund. `C`
  - [x] Pasek HP gracza z opóźnionym „śladem” obrażeń: `HealthBar`, menu *WSF GameJam/Setup/Create HUD* (nie testowane w Unity). `C`
- [ ] **Wskaźnik paszczy:** kierunek i odległość, pasek postępu okrążenia. `P`
- [ ] **Ekrany:** start, zwycięstwo rundy, śmierć, pauza. `G` (grafika), `P` (logika)
- [ ] **Efekty paszczy w świecie:** poświata, drgania kamery, zmiana kolorów przy zbliżaniu się. `C`

## Etap 4: więcej zawartości (SHOULD / COULD)

- [ ] **Wróg 3 (pancerny) i wróg 4 (rój)**. `P`, modele: `G`
- [ ] **Mini-boss na czubku głowy** (COULD).
- [ ] **Różne profile terenu** dla segmentów (pagórki, kratery, grzbiety) jako nowe podklasy `TorusSegmentProfile`. `C`

## Etap 5: prezentacja i polish (SHOULD, rezerwujemy ostatnie godziny)

- [x] **PSG (addon Blendera):** scatter assetów na płaszczyźnie (klastry drzew, trawa i krzaki wokół, warstwy) i wzdłuż linii adnotacji, na instancjach (linked duplicates) + malowanie podłoża vertex colorem, eksport FBX do Unity. Opis: [PSG.md](PSG.md). `C`
- [ ] **Shadery i efekty:** łuski, mgła, post-processing (URP Volume), efekty cząsteczkowe walki. `G`
- [ ] **Muzyka i dźwięki:** napięcie przy zbliżaniu się paszczy, walka, UI. `?`
  - [x] Pętla dźwięku głowy węża (`Assets/SFX/SnakeHead_Loop.wav`: rumble + ogień, mono, -1 dBFS) jako 3D `AudioSource` na prefabie `Snake Head`. Licencje źródeł (Pixabay/Freesound) do potwierdzenia.
- [ ] **Balans:** prędkość paszczy, siła wrogów, tempo rund.
- [ ] **Build i test** na docelowej platformie jamu.
- [ ] **Prezentacja:** nagranie / trailer, opis gry, zrzuty ekranu. `?`

## Decyzje do podjęcia

- [x] Termin: 30 h.
- [x] Co po śmierci: koniec przejścia, restart od rundy 1. Rekord = liczba ukończonych rund.
- [x] Typy wrogów: biegacz, strzelec, pancerny, rój.
- [x] Audio: mieszanka CC0, pracy ręcznej i syntezatorów.
- [ ] Dokładne zachowania i statystyki wrogów.
- [x] Poziom i sprzęt zostają między rundami, ale giną ze śmiercią (nowa gra od zera).
- [x] Wróg dostaje siłę raz, przy pojawieniu się, i jej nie zmienia.
- [ ] Szczegóły progresji: krzywa XP, sloty i typy przedmiotów, źródła.
- [ ] Paleta kolorów i referencje wizualne.
