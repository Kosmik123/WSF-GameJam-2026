# GDD: Uroboros (WSF GameJam 2026)

> Wersja robocza, piszemy razem. Rzeczy oznaczone **[DO USTALENIA]** są otwarte.
> Lista zadań: [TASKS.md](TASKS.md).

## 1. Wizja w jednym zdaniu

Uciekasz po ciele węża zwiniętego w pierścień przed jego własną paszczą, która go pożera. Musisz okrążyć cały świat i stanąć za paszczą, na czubku głowy.

## 2. Temat i odniesienie do tematu (Uroboros)

- Świat to wąż pożerający własny ogon: torus, na którego powierzchni toczy się gra.
- Cykl jest też mechaniką: po zwycięstwie zaczyna się kolejna runda, w której paszcza jest szybsza. Koniec jest początkiem.
- Cel ucieczki (za paszczą) jest tam, gdzie zaczęliśmy: pętla zamyka się w miejscu startu.

## 3. Filary projektu

1. **Prezentacja przede wszystkim.** Jam jest oceniany głównie na podstawie prezentacji, więc gra ma wyglądać i brzmieć mocno nawet kosztem głębi systemów.
2. **Presja ucieczki.** Paszcza jest zawsze za plecami. Każda decyzja (walczyć czy biec) to wymiana czasu na bezpieczeństwo.
3. **Zręcznościowa walka.** Umiejętność gracza (unik, timing) ma znaczenie, a nie tylko statystyki.
4. **Jedna czytelna pętla.** Uciekaj, walcz, przetrwaj, okrąż świat, powtórz szybciej.

## 4. Świat

- **Kształt:** torus 3D. Pozycje w świecie opisują dwa kąty: `θ` (wokół dużego koła, kierunek biegu) i `φ` (wokół przekroju rury). Gracz może wejść na spód i do środka dziury.
- **Segmenty:** kilkanaście (domyślnie 14) proceduralnych segmentów ciała węża. Każdy ma własną rozdzielczość siatki i własny profil terenu, a granice między segmentami płynnie się łączą.
- **Zróżnicowanie:** każdy segment może mieć inny charakter terenu (płaski, pagórki, kratery, grzbiety) i inny materiał.
- **Wygląd:** stylizowany low poly, ciało węża (łuski, wzór). **[DO USTALENIA]** paleta kolorów i referencje.

## 5. Zagrożenie: paszcza

- Strefa śmierci w kształcie szczęki węża, która krąży po torusie.
- Porusza się **jednostajnie**, ze stałą prędkością kątową. Nie przyspiesza w ramach rundy.
- Dotknięcie paszczy zabija natychmiast, niezależnie od HP.
- Gracz musi być szybszy od paszczy, żeby ją okrążyć, więc walka z wrogami kosztuje czas.
- Wygląd: animowana szczęka (otwiera się i zamyka), po bokach strefy zagrożenia efekty (poświata, dźwięk, drgania kamery).

## 6. Pętla rozgrywki

1. Start: gracz stoi z przodu paszczy.
2. Gracz biegnie wzdłuż torusa, przed nim pojawiają się wrogowie.
3. Gracz walczy lub ich omija. Straty HP i czas spędzony w walce zmniejszają przewagę nad paszczą.
4. Gracz okrąża cały torus i dociera na czubek głowy węża (za paszczą).
5. **Zwycięstwo rundy.** Zaczyna się kolejna runda: paszcza jest szybsza, wrogów jest więcej.
6. Śmierć (paszcza albo HP = 0) kończy przejście. **[DO USTALENIA]** czy gracz zaczyna od nowa od rundy 1.

## 7. Gracz

- Kamera: z góry pod skosem, pomiędzy top-down a widokiem zza pleców. „Góra” kamery to normalna powierzchni, więc świat obraca się wokół gracza.
- Ruch: po powierzchni torusa (współrzędne `θ`, `φ`), z kontrolerem kinematycznym, bez fizyki na zakrzywionej powierzchni.
- Zdrowie: pasek HP, wrogowie zadają obrażenia. **[DO USTALENIA]** czy HP się regeneruje albo leczy.
- Walka: **melee + unik** (hack and slash). Atak bronią, unik (przewrót lub dash z krótką nietykalnością), ewentualnie parowanie. **[DO USTALENIA]** czy gracz ma combo i czy jest jedna broń.

## 8. Wrogowie

- Zakres: **4 lub więcej typów** (ambitnie, patrz ryzyka).
- Wrogowie pojawiają się w współrzędnych `(θ, φ)` na torusie, przed graczem.
- **[DO USTALENIA]** konkretne typy (propozycja do rozważenia): biegacz (szybki, słaby), strzelec (dystans), pancerny (wolny, wytrzymały), roj (wiele małych), ewentualny mini-boss na czubku głowy.
- Trudność narasta z kolejnymi rundami: więcej wrogów i szybsza paszcza.

## 9. Progresja

- Brak rozbudowanych systemów. Jedyna progresja to **narastająca trudność** w kolejnych rundach.
- Ulepszenia (perki, waluta, meta) są poza zakresem, jeśli zabraknie czasu. **[DO USTALENIA]** czy dodamy je, jeśli zostanie czas.

## 10. Czytelność i interfejs

- Wskaźniki na ekranie: **kierunek i odległość do paszczy** oraz **pasek postępu okrążenia** (gdzie jest cel).
- Efekty w świecie: poświata, dźwięk i drgania kamery przy zbliżaniu się paszczy.
- UI: HP gracza, numer rundy, ekran startowy, ekran zwycięstwa rundy, ekran śmierci.

## 11. Styl wizualny i dźwięk

- **Grafika:** stylizowany low poly, spójna paleta, shadery i efekty cząsteczkowe podbijające prezentację (łuski, mgła, poświata paszczy). **[DO USTALENIA]** paleta i referencje.
- **Dźwięk:** muzyka rosnąca w napięciu przy zbliżaniu się paszczy, efekty walki, dźwięk paszczy. **[DO USTALENIA]** źródło assetów audio.

## 12. Zakres i ryzyka

| Ryzyko | Jak ograniczamy |
|--------|-----------------|
| Za dużo typów wrogów (4+) na kilkadziesiąt godzin | Najpierw 2 typy z działającą walką, kolejne dokładamy według priorytetów w [TASKS.md](TASKS.md). |
| Kamera i sterowanie na torusie wymagają dopracowania | Prototyp ruchu i kamery jako pierwsze zadanie po świecie. |
| Walka zręcznościowa wymaga czasu na „feel” | Zrobić wcześnie, grywalny prototyp walki z jednym wrogiem. |
| Jam jest oceniany po prezentacji | Zarezerwować ostatnie godziny na polish, efekty, dźwięk i nagranie prezentacji. |

## 13. Zespół

- Programista (gameplay i systemy).
- Grafik (świat, postać, wrogowie, paszcza, UI, efekty).
- Claude (kod: świat, ruch, kamera, walka; według podziału w [TASKS.md](TASKS.md)).

## 14. Otwarte pytania

- Dokładny termin jamu.
- Konkretne typy wrogów i ich zachowania.
- Co dzieje się po śmierci (restart od rundy 1 czy od bieżącej).
- Czy gra ma dodatkowe ulepszenia, jeśli zostanie czas.
- Paleta kolorów i referencje wizualne.
- Źródło muzyki i dźwięków.
