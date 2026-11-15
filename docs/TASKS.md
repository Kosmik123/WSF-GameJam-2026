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
- [x] Kolory wierzchołków z meshy grafika trafiają do meshy segmentów (`MeshHeightRasterizer`, profile) i są czytane przez shader `WSF/Vertex Color Lit` (sRGB do linear).
- [x] **Biomy na torusie** (6 FBX w `Assets/Bioms`: Pustynia, Las, Bagno, Wybrzeże, Wulkan, Lód): menu *WSF GameJam/Setup/Biomes On Torus* (`BiomeTorusSetupMenu`) importuje FBX, tworzy profile terenu z meshy `Teren_<Biom>`, ustawia 6 segmentów po 314 m, a `TorusBiomeDecorations` rysuje assety przez GPU instancing (bez colliderów). Opis: [PSG.md](PSG.md). Wymaga sprawdzenia w Unity (proporcje, szwy, wydajność).
- [x] `comms.md`: kanał wiadomości między agentami (zasada w `AGENTS.md`).

## W trakcie

- [x] **Propsy na torusie:** `TorusPropDefinition` (ScriptableObject na mesh, z referencją do niego, nazwa propa to nazwa mesha: promień pseudo-collidera `blockRadius`, jeden suwak `visibleRangeDegrees` (θ wokół gracza; 180 = zawsze widoczny, np. drzewa, 0 = klasa wyłączona w całości)), `TorusPropCatalog`, `TorusPropObstacles` (siatka przeszkód, `TorusObject` odpycha się od nich, pole `bodyRadius`), menu *WSF GameJam/Setup/Create Prop Definitions*. Nie testowane w Unity, wartości początkowe zgadnięte z rozmiaru mesha. `C`
- [x] **Menu główne jako wstępny cinematic** (menu i gra w jednej scenie): kamera Cinemachine „Menu VCam” patrzy z góry na cały torus, głowa pożera ogon (w menu szybciej), gracza nie ma (jego komponenty wyłączone, HUD i wrogowie ukryci), na środku przycisk „Play”: po kliknięciu gracz się pojawia, Cinemachine przechodzi na kamerę gracza i startuje pierwsza runda (`MainMenu`, `RoundController.StartRun`, pole `startAutomatically`), menu *WSF GameJam/Setup/Create Main Menu*. Nie testowane w Unity. `C`
- [x] Bariera na głowie: gracz nie może przejść przez theta głowy (+ offset), niezależnie od phi (`HeadBarrier`, `TorusThetaBarrier`, pole `isBlockedByHeadBarrier` w `TorusObject`), menu *WSF GameJam/Setup/Create Head Barrier*. Nie testowane w Unity. `C`
- [x] Przyspieszenie głowy, gdy gracz jest blisko za nią: `HeadMovement` (`maxSpeedDegrees`, `maxSpeedThetaDegrees`, `speedupStartThetaDegrees`, prędkość rośnie liniowo z odległością). Nie testowane w grze. `C`
- [ ] Kolor szyi głowy i wtopienie głowy w teren biomu (głowa stoi na segmencie biomu). `G`
- [ ] **Biomy, poprawki grafiki** (`G`):
  - [ ] Assety są za gęsto, lekko je przerzedzić (PSG: *Density*, *Min Distance*, liczby w warstwach).
  - [ ] Wybrzeże: dodać wodę, której teraz nie ma. Woda ma leżeć niżej, piasek wyżej.
  - [ ] Lodowiec: niebieski lód niżej, biały śnieg wyżej.
- [ ] Wpływ biomów na rozgrywkę (np. rodzaje wrogów), do ustalenia. `?`

## Etap 1: prototyp grywalny (MUST)

- [x] **Gracz: ruch po torusie** w współrzędnych `(θ, φ)`, orientacja według normalnej powierzchni, sterowanie (Input System). `C`
- [x] **Kamera** z góry pod skosem, podążająca za graczem. Orientację „góry ekranu” i obrót kamery ustawimy przy dostrajaniu walki, tak by była wygodna (odłożone). `C`
- [x] **Paszcza (logika):** jednostajny ruch po `θ` (`HeadMovement`), strefa działa na każde `Health`: gracza stopniowo pozbawia życia, NPC zabija od razu (pole `MawDamageMode` w `Health`, `MawKillZone`, tylko kąty). `C`
- [x] **Cel i zwycięstwo rundy:** wykrycie okrążenia i dotarcia do miejsca za głową (`goalBehindHeadDegrees`): `LapTracker` (suma zmian kąta gracz-głowa, tylko `θ`). Decyzja: chodzenie po głowie odpada (pivot głowy nie jest u podstawy, obrót daje schody między głową a segmentem). `C`
- [x] **Wrażliwy punkt węża** za głową i cel rundy: `WeakPoint` z `Health` (drużyna wrogów, odporny na paszczę), maleje wraz z utratą życia do `minScale`, miga na czerwono przy trafieniu (`BodyFlash`), po zniszczeniu zdarzenie `OnDestroyed` kończy rundę (`RoundController`). Menu *WSF GameJam/Setup/Create Weak Point*. `LapTracker` już nie rozstrzyga rundy. Nie testowane w Unity. `C`
  - [ ] Model wrażliwego punktu i animacja ataku gracza na niego (pola `visual`, `animator`, `playerAnimator`). `G`
- [ ] **Pętla rund:** po zwycięstwie nowa runda z szybszą paszczą i większą trudnością, ekran śmierci z wynikiem (liczba rund), zapis rekordu i restart od rundy 1. `P`
  - [x] Logika: `RoundController` (przerwa po rundzie, szybsza paszcza i mnożnik wrogów, śmierć kończy przejście, rekord w `PlayerPrefs`, restart od rundy 1), menu *WSF GameJam/Setup/Create Round Loop*. Nie testowane w Unity. `C`
  - [x] Ekran zwycięstwa rundy i ekran śmierci z wynikiem i rekordem: `RoundScreens` (zdarzenia `OnRoundCompleted`, `OnRunEnded`), menu *WSF GameJam/Setup/Create Round Screens*, nie testowane w Unity. `C`
  - [ ] Ładna grafika tych ekranów (teraz placeholder: przyciemnienie i tekst). `G`
- [ ] **Model paszczy** (siatka, animacja otwierania i zamykania). `G`
- [ ] **Model gracza** (low poly, animacje: bieg, atak, unik, obrażenia, śmierć). Teraz jest postać z paczki RPG Tiny Hero Duo (z LD57), własny model opcjonalny. `G`
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
- [ ] **Modele i animacje wrogów** (teraz bryły w stylu LD57 z animacją w kodzie, własne modele opcjonalne). Zdarzenia do animacji: `PlayerCombat.OnAttackStarted/OnDodgeStarted`. `G`
- [x] **Skille i klasa Mag** (z LD57): sloty Q/E/R/F (akcje `Skill1`–`Skill4`), cooldowny, 4 poziomy skilla (`SkillDefinition` jako ScriptableObject, jedna podklasa na skill, bez enuma), 5 skilli maga (kula ognia, lodowa nova, łańcuch błyskawic, teleport, magiczne kule), strzał maga `RangedAttack`, klasy `PlayerClass` (Mag, Wojownik) i `PlayerClassApplier`, spowolnienie i ogłuszenie wrogów `StatusEffects`, pasek skilli `SkillBar`, reset skilli po śmierci w `RoundController`. Menu *WSF GameJam/Setup/Create Skills And Mage*. Efekty skilli to placeholdery z linii i kul (`SkillVfx`). Skompilowane poza Unity (ze stubami Input System, TMP, UI), nie testowane w Unity. `C`
- [x] **Postać gracza i modele wrogów z LD57:** gracz z paczki *RPG Tiny Hero Duo* (`Assets/RPG Tiny Hero Duo`, tylko potrzebne pliki): Wojownik = postać męska z mieczem i tarczą, Mag = postać kobieca bez broni z niebieskim materiałem. Animacje (bieg do przodu i do tyłu, 3 ciosy combo, rzucanie, unik, trafienie, śmierć) w `Animations/Player/Player.controller`, sterowane przez `PlayerAnimator`; model wstawia `PlayerClassApplier`. Wrogowie: slime (biegacz) i strzelec z LD57 z prymitywów i materiałów LD57, z animacją proceduralną `EnemyVisualAnimator` (port SlimeAnimator, ShooterAnimator i HitReaction bez DOTween). Menu *WSF GameJam/Setup/Apply LD57 Models* (uruchamia się też na końcu *Create Skills And Mage*). Nie testowane w Unity. `C`
- [ ] **Test i balans skilli maga w Unity** (obrażenia z LD57 ×4, cooldowny, zasięgi). `?`
- [x] **Efekty VFX z LD57:** `Vfx/StylizedFx` (port `MageStylizedFx`, `SkillImpactVfx`, `VoxelFxRecipes`, `HitSplash`, `DeathEffects`: wybuchy brył, błyski, pierścienie, błyskawice, lód, dym, spalona ziemia, fala uderzeniowa), efekty skilli maga i wojownika, wygląd pocisków `ProjectileVisual`, `CombatVfx` (iskry trafień w kolorze wroga, wybuch śmierci, trafienie gracza, smuga uniku, łuk cięcia miecza). Miejsce na ziemi liczone z torusa (`VfxGround`), bez raycastów. Menu *WSF GameJam/Setup/Add Combat Vfx*, uruchamia się samo raz (`VfxAutoSetup`). Nie testowane w Unity. `C`
- [x] **Dźwięki z LD57:** synteza dźwięków w kodzie (`Audio/SfxSynthesizer`, port `ProceduralSfxBank`), dźwięki jako assety `SoundEffect` w `Data/Sounds` (z opcjonalnymi nagraniami, np. 3 cięcia miecza z LD57), `SfxPlayer`, `SfxHooks` (cios, unik, strzał maga, skille, trafienie i śmierć gracza, trafienie i śmierć wroga, wygrana runda, rekord), `EnemySfx` (zapowiedź i atak wroga, cięższe dźwięki golema). Menu *WSF GameJam/Setup/Create Audio*, uruchamia się samo raz (`AudioAutoSetup`). Nie testowane w Unity. `C`
- [x] **Skille Wojownika z LD57** (obrażenia ×4): wir, szarża (z nietykalnością), uderzenie w ziemię, rzut toporem (wraca), okrzyk bojowy (odrzut i ogłuszenie). Wojownik zaczyna z 4, okrzyk w puli. `Skills/Warrior/`. Nie testowane w Unity. `C`
- [ ] **Shader cząsteczek w buildzie:** `VfxMaterials` szuka `Universal Render Pipeline/Particles/Unlit` przez `Shader.Find`; przed buildem dodać go do *Always Included Shaders* albo podpiąć materiał w scenie. `?`
- [ ] **Ikony skilli** (`SkillDefinition.Icon`), animacja rzucania czaru dla maga (teraz cios mieczem `Attack04`), ładniejsze efekty według uznania. Zdarzenia: `SkillUser.OnSkillExecuted`, `RangedAttack.OnShot`. `G`
- [x] **Wybór klasy** na ekranie startowym: przyciski Mag i Wojownik w `MainMenu` wołają `PlayerClassApplier.Apply`. Nie testowane w Unity. `C`

## Etap 2b: progresja (MUST)

- [ ] **Doświadczenie i poziomy:** XP od wrogów, progi, awans, wzrost statystyk (HP, obrażenia, szybkość ataku); reset poziomu i sprzętu przy śmierci. Awans może dawać karty skilli: `SkillSlots.GetOfferableSkills` i `SkillSlots.Grant` są gotowe. `C`
- [x] **Trudność zależna od paszczy:** `EnemySpawner.GetDifficulty` liczy trudność z aktualnej odległości miejsca od paszczy (krzywa, blisko = łatwo, czubek głowy = najtrudniej) razem z mnożnikiem rundy. Poziom wroga ustalany raz, przy pojawieniu się. Nie testowane w Unity. `C`
- [ ] **Ekwipunek:** sloty, przedmioty zmieniające statystyki, upuszczanie przez wrogów i skrzynie. `C`
- [ ] **UI progresji:** pasek XP, komunikat awansu, ekwipunek i porównanie przedmiotów. `P`, grafiki: `G`
- [ ] **Ikony i modele przedmiotów** (broń, pancerz, dodatki). `G`

## Etap 3: czytelność i interfejs (SHOULD)

- [ ] **HUD:** pasek HP i numer rundy (reszta HUD-u: wskaźnik paszczy, ekrany poniżej). `P`
  - [x] Numer rundy: `RoundState` (numer rundy, `StartNextRound`, `ResetRounds`) i `RoundLabel`, ten sam menu *Create HUD*. `RoundState` czeka na podpięcie pod pętlę rund. `C`
  - [x] Pasek HP gracza z opóźnionym „śladem” obrażeń: `HealthBar`, menu *WSF GameJam/Setup/Create HUD* (nie testowane w Unity). `C`
- [x] **Wskaźnik paszczy:** pierścień okrążenia z odległością do paszczy (`LapRing`) i czerwona, pulsująca winieta z ostrzeżeniem, gdy paszcza jest blisko (`MawWarning`). Menu *WSF GameJam/Setup/Create Aztec UI*. Nie testowane w Unity. `C`
- [ ] **Ekrany:** start, zwycięstwo rundy, śmierć, pauza. `G` (grafika), `P` (logika)
  - [x] UI w stylu azteckim (projekt: [canvas „Uroboros — propozycja UI”](https://claude.ai/artifact/VR4j8GK12A2xV2qYbzkG7V)): menu startowe z ładowaniem (kręcący się uroboros, `UiSpinner`), wyborem klasy i rekordem, kamera z góry torusa zjeżdża za plecy bohatera (`MainMenu`, `CameraIntro`), pauza pod Escape (`PauseMenu`; `GamePause` jest jedynym właścicielem pauzy), baner „Runda N” (`RoundBanner`), kropki rundy (`RoundDots`), restyling HUD-u i ekranów rund. Sprite’y rysowane w kodzie (`AztecUiSprites`, zapis do `Assets/_Project/Ui/Sprites`). Menu *WSF GameJam/Setup/Create Aztec UI* (po *Create HUD*, *Create Round Loop*, *Create Round Screens*). Skompilowane poza Unity, nie testowane w Unity. `C`
  - [ ] Font nagłówków (teraz domyślny TMP, w projekcie był Cinzel), ikony skilli, pasek XP i ekran awansu po systemie poziomów. `G`, `C`
- [ ] **Efekty paszczy w świecie:** poświata, drgania kamery, zmiana kolorów przy zbliżaniu się. `C`

## Etap 4: więcej zawartości (SHOULD / COULD)

- [x] **Wróg 3 (pancerny) i wróg 4 (rój)**, plus szaman: `GolemEnemy` (uderzenie w ziemię z zapowiedzią), `BatEnemy` (krążenie i przelot przez gracza, grupy 4-6), `ShamanEnemy` (dystans, leczenie wrogów, klątwa pod graczem). Modele z brył w stylu LD57, animacja `EnemyVisualAnimator` (doszło machanie skrzydłami i ramionami). Menu *WSF GameJam/Setup/Create Golem Bats Shaman*, uruchamia się samo raz (`EnemiesAutoSetup`), dodaje wrogów do spawnera. Nie testowane w Unity. `C`, ładniejsze modele: `G`
- [ ] **Mini-boss na czubku głowy** (COULD).
- [ ] **Różne profile terenu** dla segmentów (pagórki, kratery, grzbiety) jako nowe podklasy `TorusSegmentProfile`. `C`

## Etap 5: prezentacja i polish (SHOULD, rezerwujemy ostatnie godziny)

- [x] **PSG (addon Blendera):** scatter assetów na płaszczyźnie (klastry drzew, trawa i krzaki wokół, warstwy) i wzdłuż linii adnotacji, na instancjach (linked duplicates) + malowanie podłoża vertex colorem, eksport FBX do Unity. Opis: [PSG.md](PSG.md). `C`
- [ ] **Shadery i efekty:** łuski, mgła, post-processing (URP Volume), efekty cząsteczkowe walki. `G`
- [ ] **Muzyka i dźwięki:** napięcie przy zbliżaniu się paszczy, walka, UI. `?`
  - [x] Dźwięk głowy węża: pętla `Assets/SFX/SnakeHead_Loop.wav` (rumble + ogień, fade in 2,5 s na starcie runu) i `SnakeHead_Death.wav` (ostatnie 2 s, gra przy śmierci gracza). Steruje `HeadAudio` na prefabie `Snake Head`. Licencje źródeł (Pixabay/Freesound) do potwierdzenia.
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
