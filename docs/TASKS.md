# Lista zadań

Priorytety: **MUST** (bez tego nie ma gry), **SHOULD** (mocno poprawia grę lub prezentację), **COULD** (jeśli zostanie czas).
Właściciel: `P` programista, `G` grafik, `C` Claude, `?` do ustalenia.
Zasada pracy: każdy feature na własnej gałęzi od `develop`, merge do `develop` robi człowiek.
Termin jamu: **do ustalenia**. Gdy go poznamy, dopiszemy daty.

## Zrobione

- [x] Świat: proceduralny torus z segmentami o różnej rozdzielczości i terenie (`TorusWorld`, `TorusSurface`, `TorusSegmentMeshBuilder`).
- [x] Szum terenu z Unity.Mathematics.
- [x] Przebudowa świata po zmianie w Inspectorze (OnValidate).

## W trakcie

- [ ] Profil segmentu jako klasa odkształcająca mesh (`Deform`), `NoiseTorusSegmentProfile`, wygładzanie normalnych na granicach (gałąź `feature/torus-segment-deformers`, czeka na merge). Właściciel: C.

## Etap 1: prototyp grywalny (MUST)

- [ ] **Gracz: ruch po torusie** w współrzędnych `(θ, φ)`, orientacja według normalnej powierzchni, sterowanie (Input System). `C`
- [ ] **Kamera** z góry pod skosem, podążająca za graczem, „góra” = normalna powierzchni. `C`
- [ ] **Paszcza (logika):** jednostajny ruch po `θ`, śmierć gracza przy wejściu w strefę. `C`
- [ ] **Cel i zwycięstwo rundy:** wykrycie okrążenia i dotarcia na czubek głowy. `C`
- [ ] **Pętla rund:** po zwycięstwie nowa runda z szybszą paszczą i większą trudnością, ekran śmierci i restart. `P`
- [ ] **Model paszczy** (siatka, animacja otwierania i zamykania). `G`
- [ ] **Model gracza** (low poly, animacje: bieg, atak, unik, obrażenia, śmierć). `G`
- [ ] **Materiał i wygląd ciała węża** (łuski, kolory, różne warianty segmentów). `G`

## Etap 2: walka i wrogowie (MUST)

- [ ] **Zdrowie** (gracz, wrogowie), obrażenia, śmierć. `P`
- [ ] **Walka gracza:** atak melee, unik z nietykalnością, hit stop i odrzut. `C`
- [ ] **Wróg 1 (biegacz)** z AI poruszającym się po torusie i atakiem. `C`
- [ ] **Wróg 2 (strzelec lub pancerny)**. `P`
- [ ] **Spawner wrogów** w współrzędnych `(θ, φ)` przed graczem, skalowanie ze trudnością rundy. `P`
- [ ] **Modele i animacje wrogów 1 i 2.** `G`

## Etap 3: czytelność i interfejs (SHOULD)

- [ ] **HUD:** pasek HP, numer rundy. `P`
- [ ] **Wskaźnik paszczy:** kierunek i odległość, pasek postępu okrążenia. `P`
- [ ] **Ekrany:** start, zwycięstwo rundy, śmierć, pauza. `G` (grafika), `P` (logika)
- [ ] **Efekty paszczy w świecie:** poświata, drgania kamery, zmiana kolorów przy zbliżaniu się. `C`

## Etap 4: więcej zawartości (SHOULD / COULD)

- [ ] **Wróg 3 i wróg 4** (kolejne typy). `P`, modele: `G`
- [ ] **Mini-boss na czubku głowy** (COULD).
- [ ] **Różne profile terenu** dla segmentów (pagórki, kratery, grzbiety) jako nowe podklasy `TorusSegmentProfile`. `C`
- [ ] **Ulepszenia / perki** (COULD, jeśli zostanie czas).

## Etap 5: prezentacja i polish (SHOULD, rezerwujemy ostatnie godziny)

- [ ] **Shadery i efekty:** łuski, mgła, post-processing (URP Volume), efekty cząsteczkowe walki. `G`
- [ ] **Muzyka i dźwięki:** napięcie przy zbliżaniu się paszczy, walka, UI. `?`
- [ ] **Balans:** prędkość paszczy, siła wrogów, tempo rund.
- [ ] **Build i test** na docelowej platformie jamu.
- [ ] **Prezentacja:** nagranie / trailer, opis gry, zrzuty ekranu. `?`

## Decyzje do podjęcia

- [ ] Termin jamu.
- [ ] Typy wrogów 1–4 i ich zachowania.
- [ ] Co po śmierci: restart od rundy 1 czy od bieżącej.
- [ ] Paleta kolorów i referencje wizualne.
- [ ] Źródło muzyki i dźwięków.
