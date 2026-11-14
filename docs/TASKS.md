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
- [ ] **Wróg 2 (strzelec)**. `P`
- [ ] **Spawner wrogów** w współrzędnych `(θ, φ)` przed graczem, skalowanie ze trudnością rundy. `P`
- [ ] **Modele i animacje wrogów 1 i 2.** `G`

## Etap 2b: progresja (MUST)

- [ ] **Doświadczenie i poziomy:** XP od wrogów, progi, awans, wzrost statystyk (HP, obrażenia, szybkość ataku). `C`
- [ ] **Skalowanie trudności** wrogów po segmencie i rundzie, tak by samo bieganie nie wystarczało. `P`
- [ ] **Ekwipunek:** sloty, przedmioty zmieniające statystyki, upuszczanie przez wrogów i skrzynie. `C`
- [ ] **UI progresji:** pasek XP, komunikat awansu, ekwipunek i porównanie przedmiotów. `P`, grafiki: `G`
- [ ] **Ikony i modele przedmiotów** (broń, pancerz, dodatki). `G`

## Etap 3: czytelność i interfejs (SHOULD)

- [ ] **HUD:** pasek HP, numer rundy. `P`
- [ ] **Wskaźnik paszczy:** kierunek i odległość, pasek postępu okrążenia. `P`
- [ ] **Ekrany:** start, zwycięstwo rundy, śmierć, pauza. `G` (grafika), `P` (logika)
- [ ] **Efekty paszczy w świecie:** poświata, drgania kamery, zmiana kolorów przy zbliżaniu się. `C`

## Etap 4: więcej zawartości (SHOULD / COULD)

- [ ] **Wróg 3 (pancerny) i wróg 4 (rój)**. `P`, modele: `G`
- [ ] **Mini-boss na czubku głowy** (COULD).
- [ ] **Różne profile terenu** dla segmentów (pagórki, kratery, grzbiety) jako nowe podklasy `TorusSegmentProfile`. `C`

## Etap 5: prezentacja i polish (SHOULD, rezerwujemy ostatnie godziny)

- [ ] **Shadery i efekty:** łuski, mgła, post-processing (URP Volume), efekty cząsteczkowe walki. `G`
- [ ] **Muzyka i dźwięki:** napięcie przy zbliżaniu się paszczy, walka, UI. `?`
- [ ] **Balans:** prędkość paszczy, siła wrogów, tempo rund.
- [ ] **Build i test** na docelowej platformie jamu.
- [ ] **Prezentacja:** nagranie / trailer, opis gry, zrzuty ekranu. `?`

## Decyzje do podjęcia

- [x] Termin: 30 h.
- [x] Co po śmierci: restart bieżącej rundy.
- [x] Typy wrogów: biegacz, strzelec, pancerny, rój.
- [x] Audio: mieszanka CC0, pracy ręcznej i syntezatorów.
- [ ] Dokładne zachowania i statystyki wrogów.
- [ ] Szczegóły progresji: krzywa XP, sloty i typy przedmiotów, źródła, zachowanie poziomu i sprzętu po śmierci i między rundami.
- [ ] Paleta kolorów i referencje wizualne.
